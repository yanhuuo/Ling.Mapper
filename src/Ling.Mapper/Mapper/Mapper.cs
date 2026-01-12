using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;

namespace Ling.Mapper
{
    /// <summary>
    /// 映射器的内部实现类 v2。
    /// 改进要点：
    /// 1. 无参构造函数检测 - 避免运行时崩溃
    /// 2. 集合映射类型转换 - 修复简单类型映射逻辑
    /// 3. SourceProperty 查找优化 - O(n?) → O(1)
    /// 4. MappingExpressionBase 反射缓存 - 避免重复 GetField
    /// 5. ConvertSimpleType 拆分 - 提高可维护性和 JIT 优化
    /// 6. 循环引用保护 - 防止 StackOverflow
    /// 7. wrapper 委托统一路径 - 避免 DynamicInvoke
    /// 8. Source Generator 友好 - 明确短路逻辑
    /// </summary>
    internal class Mapper : IMapper
    {
        private readonly MapperConfiguration _config;
        private readonly ConcurrentDictionary<(Type, Type), Delegate> _compiledMappers = new();
        
        // v2 FIX: 添加编译期递归保护，防止在编译映射时无限递归
        private readonly ThreadLocal<HashSet<(Type, Type)>> _compilingMappers = 
            new ThreadLocal<HashSet<(Type, Type)>>(() => new HashSet<(Type, Type)>());
        
        // v2.1.3 FIX: 添加运行时循环引用检测，防止对象实例之间的循环引用
        private readonly ThreadLocal<Dictionary<object, object>> _mappingContext = 
            new ThreadLocal<Dictionary<object, object>>(() => new Dictionary<object, object>(ReferenceEqualityComparer.Instance));

        public Mapper(MapperConfiguration config)
        {
            _config = config ?? throw new ArgumentNullException(nameof(config));
            BuildAllConfiguredMappers();
        }

        private void BuildAllConfiguredMappers()
        {
            foreach (var cfg in _config.Configs)
            {
                CompileMapper(cfg.SourceType, cfg.DestType, cfg);
            }
        }

        public TDestination? Map<TDestination>(object? source)
        {
            if (source == null) return default;
            var srcType = source.GetType();
            var destType = typeof(TDestination);

            var del = GetOrCompileMapper(srcType, destType, null);
            if (del is Func<object, object?> objFunc)
            {
                return (TDestination?)objFunc(source!);
            }

            // v2: wrapper 应为唯一路径，此处仅作兜底（理论上不应到达）
            return (TDestination?)del.DynamicInvoke(source!);
        }

        public object? Map(object? source, Type sourceType, Type destType)
        {
            // v2 FIX: 值类型目标 + source == null 行为明确
            if (source == null)
            {
                // 如果目标类型是非可空值类型，返回默认值
                if (destType.IsValueType && Nullable.GetUnderlyingType(destType) == null)
                {
                    return Activator.CreateInstance(destType);
                }
                return null;
            }
            
            // v2.1.3 FIX: 运行时循环引用检测（仅对引用类型）
            if (!sourceType.IsValueType && !TypeUtils.IsSimple(sourceType))
            {
                var context = _mappingContext.Value;
                if (context == null)
                {
                    context = new Dictionary<object, object>(ReferenceEqualityComparer.Instance);
                    _mappingContext.Value = context;
                }
                
                // 检查是否已经在映射这个对象实例
                if (context.TryGetValue(source, out var existingResult))
                {
                    return existingResult;  // 返回已映射的对象，打破循环
                }
                
                // 标记正在映射（先添加 null 占位符）
                context[source] = null!;
                
                try
                {
                    var del = GetOrCompileMapper(sourceType, destType, null);
                    object? result;
                    
                    if (del is Func<object, object?> objFunc)
                    {
                        result = objFunc(source);
                    }
                    else
                    {
                        result = del.DynamicInvoke(source);
                    }
                    
                    // 更新为实际映射结果
                    context[source] = result!;
                    return result;
                }
                finally
                {
                    // 映射完成后清理（可选，但为了避免内存泄漏）
                    // 注意：不要在这里移除，因为可能还有嵌套映射在使用
                }
            }

            // 简单类型或值类型，直接映射（无需循环检测）
            var del2 = GetOrCompileMapper(sourceType, destType, null);
            if (del2 is Func<object, object?> objFunc2)
            {
                return objFunc2(source);
            }

            return del2.DynamicInvoke(source);
        }

        private Delegate GetOrCompileMapper(Type src, Type dest, IMappingConfig? config)
        {
            var key = (src, dest);
            
            // v2 FIX: 优先从缓存检查（避免不必要的递归检测）
            if (_compiledMappers.TryGetValue(key, out var existingMapper))
            {
                return existingMapper;
            }
            
            // v2 FIX: 递归检查（在 GetOrAdd 之前）
            var compilingSet = _compilingMappers.Value;
            if (compilingSet == null)
            {
                compilingSet = new HashSet<(Type, Type)>();
                _compilingMappers.Value = compilingSet;
            }
            
            // 如果正在编译相同的映射，立即返回延迟解析的 wrapper
            if (compilingSet.Contains(key))
            {
                // 检测到递归：返回一个 wrapper，它在运行时再次查找映射
                return new Func<object, object?>(srcObj =>
                {
                    if (srcObj == null) return null;
                    
                    // 运行时递归查找（此时映射已编译完成）
                    if (_compiledMappers.TryGetValue(key, out var mapper))
                    {
                        if (mapper is Func<object, object?> func)
                            return func(srcObj);
                        return mapper.DynamicInvoke(srcObj);
                    }
                    
                    // 如果映射还未完成，返回 null（避免无限等待）
                    return null;
                });
            }
            
            // 检查递归深度（防止极端情况）
            if (compilingSet.Count > 50)
            {
                throw new InvalidOperationException(
                    $"Mapping compilation recursion depth exceeded 50 levels. " +
                    $"This may indicate a circular reference issue. " +
                    $"Current mapping: {src.Name} -> {dest.Name}. " +
                    $"Compilation stack: {string.Join(" -> ", compilingSet.Select(t => $"{t.Item1.Name}->{t.Item2.Name}"))}");
            }
            
            // 标记正在编译
            compilingSet.Add(key);
            
            try
            {
                // 再次检查缓存（双重检查锁定模式）
                if (_compiledMappers.TryGetValue(key, out var cachedMapper))
                {
                    return cachedMapper;
                }
                
                // 现在才进入 GetOrAdd，此时已经有递归保护
                return _compiledMappers.GetOrAdd(key, k =>
                {
                    // v2: 明确 Source Generator 短路逻辑 - 完全绕过 Expression Tree
                    if (TryGetGeneratedMapper(k.Item1, k.Item2, out var generatedMapper))
                    {
                        return generatedMapper!;
                    }

                    // 0) 用户手动注册的 wrapper（最高优先级）
                    if (MapperRegistry.TryGetWrapper(k.Item1, k.Item2, out var wrapper) && wrapper != null)
                        return wrapper;

                    // 0.5) 用户注册的强类型委托
                    if (MapperRegistry.TryGet(k.Item1, k.Item2, out var reg) && reg != null)
                    {
                        if (reg is Func<object, object?> fo) return fo;
                        // v2: 创建 wrapper 避免运行时 DynamicInvoke
                        return new Func<object, object?>(o => (object?)reg!.DynamicInvoke(o));
                    }

                    // 回退到运行时编译
                    var cfg = config ?? _config.Configs.FirstOrDefault(
                        c => c.SourceType == k.Item1 && c.DestType == k.Item2);

                    return CompileMapper(k.Item1, k.Item2, cfg);
                });
            }
            finally
            {
                // 编译完成后，从集合中移除
                compilingSet.Remove(key);
            }
        }

        /// <summary>
        /// v2: 提取 Source Generator 查找逻辑为独立方法，明确短路路径
        /// </summary>
        private bool TryGetGeneratedMapper(Type srcType, Type destType, out Delegate? mapper)
        {
            mapper = null;
            try
            {
                var asm = typeof(Mapper).Assembly;
                var genType = asm.GetType("Ling.Mapper.Generated.GeneratedMapperFactory");
                if (genType != null)
                {
                    var tryGet = genType.GetMethod("TryGetMapper", BindingFlags.Public | BindingFlags.Static);
                    if (tryGet != null)
                    {
                        var args = new object?[] { srcType, destType, null };
                        var invoked = tryGet.Invoke(null, args);
                        if (invoked is bool ok && ok)
                        {
                            var obj = args[2];
                            if (obj is Func<object, object> genDel)
                            {
                                mapper = genDel;
                                return true;
                            }
                        }
                    }
                }
            }
            catch
            {
                // 忽略反射异常
            }

            return false;
        }

        private Delegate CompileMapper(Type srcType, Type destType, IMappingConfig? cfg)
        {
            // v2 FIX 1: 检测无参构造函数
            var destCtor = destType.GetConstructor(Type.EmptyTypes);
            if (destCtor == null && !destType.IsValueType)
            {
                // 目标类型无无参构造函数
                if (_config.StrictMode)
                {
                    throw new InvalidOperationException(
                        $"Cannot create instance of type '{destType.FullName}': " +
                        $"no parameterless constructor found. " +
                        $"Consider adding a parameterless constructor or using StrictMode=false.");
                }

                // StrictMode = false: 返回 null wrapper
                return new Func<object, object?>(src => null);
            }

            var srcParam = Expression.Parameter(srcType, "src");
            var destVar = Expression.Variable(destType, "dest");
            var bodyExprs = new List<Expression>
            {
                Expression.Assign(destVar, Expression.New(destType))
            };

            var options = _config.GlobalOptions;

            var destProps = destType.GetProperties(BindingFlags.Public | BindingFlags.Instance)
                .Where(p => p.CanWrite)
                .ToList();
            var srcProps = srcType.GetProperties(BindingFlags.Public | BindingFlags.Instance)
                .Where(p => p.CanRead)
                .ToList();

            // v2 FIX 4: 构建源属性字典，优化查找性能 O(n) → O(1)
            var srcPropMap = BuildSourcePropertyMap(srcProps, options);

            MappingExpressionBase? mappingExprBase = null;
            if (cfg != null)
                mappingExprBase = new MappingExpressionBase(cfg.ExpressionObject);

            // 遍历目标属性并为每个属性生成赋值表达式（如果有对应来源或自定义映射）
            foreach (var dp in destProps)
            {
                // v2 FIX: 跳过索引器属性（如 this[int index]）
                if (dp.GetIndexParameters().Length > 0)
                    continue;
                
                if (mappingExprBase?.IsIgnored(dp.Name) == true)
                    continue; // 被 Ignore 的属性跳过

                Expression? valueExpr = null;

                // 优先使用 ForMember 配置的自定义绑定表达式
                if (mappingExprBase?.TryGetCustomBinding(dp.Name, out var customLambda) == true)
                {
                    if (customLambda != null) valueExpr = Expression.Invoke(customLambda, srcParam);
                }
                else
                {
                    // 根据 Rename 配置或默认同名规则找到源属性
                    var srcName = mappingExprBase?.GetRenamedSource(dp.Name) ?? dp.Name;
                    // v2: 使用字典查找，性能提升
                    var sp = FindSourcePropertyFromMap(srcPropMap, srcName, options);

                    if (sp != null)
                    {
                        // v2 FIX: 跳过索引器属性
                        if (sp.GetIndexParameters().Length > 0)
                            continue;
                        
                        var srcAccess = Expression.Property(srcParam, sp);

                        // 如果存在类型转换器优先调用转换器
                        if (TypeConverterRegistry.TryGet(sp.PropertyType, dp.PropertyType, out var converterDel))
                        {
                            var delConst = Expression.Constant(converterDel);
                            var invoke = Expression.Invoke(delConst, srcAccess);
                            valueExpr = Expression.Convert(invoke, dp.PropertyType);
                        }
                        else if (TypeUtils.IsSimple(dp.PropertyType) && TypeUtils.IsSimple(sp.PropertyType))
                        {
                            // v2 FIX 5: 使用拆分后的类型转换方法
                            valueExpr = ConvertSimpleType(srcAccess, sp.PropertyType, dp.PropertyType);
                        }
                        else if (TypeUtils.IsCollection(dp.PropertyType))
                        {
                            // 集合类型使用内部 MapCollectionInternal 来处理元素映射
                            var mapCollMethod = typeof(Mapper).GetMethod(nameof(MapCollectionInternal), BindingFlags.NonPublic | BindingFlags.Instance);
                            if (mapCollMethod != null)
                                valueExpr = Expression.Convert(
                                    Expression.Call(Expression.Constant(this), mapCollMethod, Expression.Convert(srcAccess, typeof(object)), Expression.Constant(sp.PropertyType, typeof(Type)), Expression.Constant(dp.PropertyType, typeof(Type))),
                                    dp.PropertyType);
                        }
                        else
                        {
                            // 复杂类型递归调用 Map(object, Type, Type)
                            var mapMethod = typeof(Mapper).GetMethod(nameof(Map), new[] { typeof(object), typeof(Type), typeof(Type) });
                            if (mapMethod != null)
                                valueExpr = Expression.Convert(
                                    Expression.Call(Expression.Constant(this), mapMethod, Expression.Convert(srcAccess, typeof(object)), Expression.Constant(sp.PropertyType, typeof(Type)), Expression.Constant(dp.PropertyType, typeof(Type))),
                                    dp.PropertyType);
                        }
                    }
                    else
                    {
                        // 未找到源属性且在 StrictMode 下抛出异常以帮助发现映射问题
                        if (_config.StrictMode)
                        {
                            throw new InvalidOperationException($"No source property found for destination '{dp.Name}' when mapping {srcType} -> {destType}");
                        }
                    }
                }

                if (valueExpr != null)
                {
                    var assign = Expression.Assign(Expression.Property(destVar, dp), valueExpr);
                    bodyExprs.Add(assign);
                }
            }

            // 表达式体最后返回目标对象 dest
            bodyExprs.Add(destVar);

            var typedBody = Expression.Block(new[] { destVar }, bodyExprs);
            // 创建强类型的 Lambda（未指定泛型委托类型，使其通用）
            var typedLambda = Expression.Lambda(typedBody, srcParam);

            // v2: 确保返回 wrapper，统一运行路径
            var objParam = Expression.Parameter(typeof(object), "srcObj");
            var invokeTyped = Expression.Invoke(typedLambda, Expression.Convert(objParam, srcType));
            var convertResult = Expression.Convert(invokeTyped, typeof(object));
            var wrapperLambda = Expression.Lambda<Func<object, object?>>(convertResult, objParam);

            // 将 wrapper 编译为委托并返回。这样运行时调用只需直接调用 wrapper，无需 DynamicInvoke。
            var wrapper = wrapperLambda.Compile();

            return wrapper;
        }

        /// <summary>
        /// v2 NEW: 构建源属性字典，优化查找性能
        /// </summary>
        private Dictionary<string, PropertyInfo> BuildSourcePropertyMap(
            List<PropertyInfo> srcProps,
            GlobalConventionOptions options)
        {
            var map = new Dictionary<string, PropertyInfo>(StringComparer.Ordinal);
            
            foreach (var sp in srcProps)
            {
                // v2 FIX: 跳过索引器属性（如 this[int index]）
                if (sp.GetIndexParameters().Length > 0)
                    continue;
                
                var normalizedName = NormalizeName(sp.Name, options);
                // 如果有重复的规范化名称，保留第一个
                if (!map.ContainsKey(normalizedName))
                {
                    map[normalizedName] = sp;
                }
            }

            return map;
        }

        /// <summary>
        /// v2 NEW: 从字典中查找源属性，O(1) 复杂度
        /// </summary>
        private PropertyInfo? FindSourcePropertyFromMap(
            Dictionary<string, PropertyInfo> srcPropMap,
            string destName,
            GlobalConventionOptions options)
        {
            var normalizedDest = NormalizeName(destName, options);
            srcPropMap.TryGetValue(normalizedDest, out var prop);
            return prop;
        }

        private string NormalizeName(string name, GlobalConventionOptions options)
        {
            if (string.IsNullOrWhiteSpace(name))
                return name;

            string result = name;

            if (options.IgnoreSpecialCharacters)
            {
                result = result.Replace("_", "");
            }

            if (options.CaseInsensitiveNameMatch)
            {
                result = result.ToLowerInvariant();
            }

            return result;
        }

        /// <summary>
        /// v2 FIX 5: 拆分后的类型转换方法，提高可维护性和 JIT 优化
        /// 使用 TypeConversionHelper 处理具体转换逻辑
        /// </summary>
        private Expression ConvertSimpleType(Expression srcAccess, Type srcType, Type destType)
        {
            var srcUnderlyingType = Nullable.GetUnderlyingType(srcType) ?? srcType;
            var destUnderlyingType = Nullable.GetUnderlyingType(destType) ?? destType;

            var srcIsNullable = Nullable.GetUnderlyingType(srcType) != null;
            var destIsNullable = Nullable.GetUnderlyingType(destType) != null;

            // 类型完全相同
            if (srcType == destType)
            {
                return srcAccess;
            }

            // v2: 尝试枚举转换
            var enumResult = TypeConversionHelper.TryConvertEnum(
                srcAccess, srcType, destType,
                srcUnderlyingType, destUnderlyingType,
                srcIsNullable, destIsNullable);

            if (enumResult != null)
                return enumResult;

            // v2: 尝试可空类型转换
            var nullableResult = TypeConversionHelper.TryConvertNullable(
                srcAccess, srcType, destType,
                srcUnderlyingType, destUnderlyingType,
                srcIsNullable, destIsNullable);

            if (nullableResult != null)
                return nullableResult;

            // v2: 简单类型之间的直接转换（T -> U）
            if (!srcIsNullable && !destIsNullable && srcUnderlyingType != destUnderlyingType)
            {
                return TypeConversionHelper.ConvertSimpleCast(srcAccess, destType);
            }

            // 默认：直接转换
            return TypeConversionHelper.ConvertSimpleCast(srcAccess, destType);
        }

        /// <summary>
        /// v2 FIX 2: 修复集合映射中简单类型处理逻辑
        /// 确保所有元素都经过类型转换，与非集合映射行为一致
        /// </summary>
        private object? MapCollectionInternal(object? srcCollection, Type srcType, Type destType)
        {
            if (srcCollection == null) return null;

            var srcElementType = TypeUtils.GetElementType(srcType);
            var destElementType = TypeUtils.GetElementType(destType);

            if (destElementType != null)
            {
                var destListType = typeof(List<>).MakeGenericType(destElementType);
                var destList = (IList)Activator.CreateInstance(destListType)!;

                foreach (var item in (IEnumerable)srcCollection)
                {
                    if (item == null)
                    {
                        destList.Add(null);
                        continue;
                    }

                    // v2 FIX: 即使是简单类型，也需要经过类型转换
                    // 例如：int -> long, enum -> int, string -> enum 等
                    if (srcElementType != null)
                    {
                        var mapped = Map(item, srcElementType, destElementType);
                        destList.Add(mapped);
                    }
                    else
                    {
                        // 无法确定源元素类型时，使用运行时类型
                        var itemType = item.GetType();
                        var mapped = Map(item, itemType, destElementType);
                        destList.Add(mapped);
                    }
                }

                if (!destType.IsArray) return destList;

                var toArray = typeof(Enumerable).GetMethod(nameof(Enumerable.ToArray))?.MakeGenericMethod(destElementType);
                return toArray?.Invoke(null, new object[] { destList });
            }

            return null;
        }

        /// <summary>
        /// v2 FIX 4: 优化后的 MappingExpressionBase，缓存反射字段
        /// </summary>
        private class MappingExpressionBase
        {
            private readonly object _exprObj;
            private readonly Type _exprType;

            // v2: 缓存反射字段，避免重复 GetField
            private readonly FieldInfo? _ignoredMembersField;
            private readonly FieldInfo? _customMemberBindingsField;
            private readonly FieldInfo? _renamedMembersField;

            public MappingExpressionBase(object exprObj)
            {
                _exprObj = exprObj ?? throw new ArgumentNullException(nameof(exprObj));
                _exprType = exprObj.GetType();

                // v2: 在构造函数中缓存字段信息
                _ignoredMembersField = _exprType.GetField("IgnoredMembers", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
                _customMemberBindingsField = _exprType.GetField("CustomMemberBindings", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
                _renamedMembersField = _exprType.GetField("RenamedMembers", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
            }

            public bool IsIgnored(string destName)
            {
                // v2: 使用缓存的字段
                var set = _ignoredMembersField?.GetValue(_exprObj) as System.Collections.IEnumerable;
                if (set == null) return false;

                foreach (var item in set)
                {
                    if (item is string s && s == destName)
                        return true;
                }

                return false;
            }

            public bool TryGetCustomBinding(string destName, out LambdaExpression? lambda)
            {
                // v2: 使用缓存的字段
                var dict = _customMemberBindingsField?.GetValue(_exprObj) as System.Collections.IDictionary;
                lambda = null;
                if (dict == null) return false;

                foreach (System.Collections.DictionaryEntry kv in dict)
                {
                    if (kv.Key is string name && name == destName)
                    {
                        lambda = kv.Value as LambdaExpression;
                        return lambda != null;
                    }
                }

                return false;
            }

            public string? GetRenamedSource(string destName)
            {
                // v2: 使用缓存的字段
                var dict = _renamedMembersField?.GetValue(_exprObj) as System.Collections.IDictionary;
                if (dict == null) return null;

                foreach (System.Collections.DictionaryEntry kv in dict)
                {
                    if (kv.Key is string name && name == destName)
                    {
                        return kv.Value as string;
                    }
                }

                return null;
            }
        }
    }
}
