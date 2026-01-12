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
    /// 映射器的内部实现类。
    /// 该类负责将 MapperConfiguration 中注册的映射规则编译为运行时可调用的委托，并在运行期间复用这些委托以提高性能。
    /// 支持功能包括：
    /// - 基于同名属性的自动映射（支持大小写不敏感和下划线忽略等约定配置）；
    /// - 对某些目标属性使用自定义表达式进行映射（ForMember）；
    /// - 忽略目标属性（Ignore）；
    /// - 重命名映射（Rename）；
    /// - 嵌套复杂类型的递归映射；
    /// - 集合类型的元素映射（List/Array/IEnumerable 简单处理）；
    /// - 注册的类型转换器支持（TypeConverterRegistry）；
    /// - 优先使用源自 Source Generator 或手动注册的高性能委托，以避免运行时装箱或 DynamicInvoke。
    /// </summary>
    internal class Mapper : IMapper
    {
        private readonly MapperConfiguration _config;
        private readonly ConcurrentDictionary<(Type, Type), Delegate> _compiledMappers = new();

        /// <summary>
        /// 使用指定的配置创建 Mapper 实例。
        /// 在构造期间会预构建配置中声明的映射（BuildAllConfiguredMappers）。
        /// </summary>
        /// <param name="config">映射器配置对象，包含 Profile 注册和全局约定。</param>
        public Mapper(MapperConfiguration config)
        {
            _config = config ?? throw new ArgumentNullException(nameof(config));
            BuildAllConfiguredMappers();
        }

        /// <summary>
        /// 遍历 MapperConfiguration 中的所有映射配置并为其编译映射委托，放入缓存。
        /// 目的是把可能的反射/表达式构建工作放到初始化阶段，运行时调用路径尽量快速。
        /// </summary>
        private void BuildAllConfiguredMappers()
        {
            foreach (var cfg in _config.Configs)
            {
                CompileMapper(cfg.SourceType, cfg.DestType, cfg);
            }
        }

        /// <summary>
        /// 将指定源对象映射为目标类型 TDestination 的实例。
        /// 如果 source 为 null，则返回默认值（null）。
        /// 优先使用已缓存的高性能 wrapper(Func&lt;object,object?&gt;) 委托来避免装箱与 DynamicInvoke。
        /// </summary>
        public TDestination? Map<TDestination>(object? source)
        {
            if (source == null) return default;
            var srcType = source.GetType();
            var destType = typeof(TDestination);

            var del = GetOrCompileMapper(srcType, destType, null);
            if (del is Func<object, object?> objFunc)
            {
                // 已经确保 source != null
                return (TDestination?)objFunc(source!);
            }

            // 兜底路径：使用 DynamicInvoke（正常情况下不应走到这里，因为我们尽量返回 wrapper 委托）
            return (TDestination?)del.DynamicInvoke(source!);
        }

        /// <summary>
        /// 通用映射接口：将 source（可空）以给定的源/目标类型进行映射并返回目标对象。
        /// 返回值为 object?，以与 IMapper 接口的可空性匹配。
        /// </summary>
        public object? Map(object? source, Type sourceType, Type destType)
        {
            if (source == null) return null;

            var del = GetOrCompileMapper(sourceType, destType, null);
            if (del is Func<object, object?> objFunc)
            {
                return objFunc(source!);
            }

            return del.DynamicInvoke(source!);
        }

        /// <summary>
        /// 获取或编译并缓存源类型到目标类型的映射委托。
        /// 调用顺序：
        /// 1) 查询用户通过 MapperRegistry 手动注册的 wrapper（Func&lt;object,object?&gt;），优先使用；
        /// 2) 查询用户注册的强类型委托并包装为 wrapper；
        /// 3) 查询由 Source Generator 生成并注册的 mapper（GeneratedMapperFactory），如存在则直接使用；
        /// 4) 回退到运行时使用表达式树编译映射逻辑（CompileMapper）。
        /// </summary>
        private Delegate GetOrCompileMapper(Type src, Type dest, IMappingConfig? config)
        {
            return _compiledMappers.GetOrAdd((src, dest), key =>
            {
                // 0) 优先使用用户在 MapperRegistry 注册的 wrapper（性能最优，避免装箱/反射）
                if (MapperRegistry.TryGetWrapper(key.Item1, key.Item2, out var wrapper) && wrapper != null)
                    return wrapper;

                // 0.5) 如果用户注册了强类型委托，则将其包装为 wrapper（若已有 wrapper 则上面已返回）
                if (MapperRegistry.TryGet(key.Item1, key.Item2, out var reg) && reg != null)
                {
                    if (reg is Func<object, object?> fo) return fo;
                    // 无法直接转换为 wrapper 时，创建一个使用 DynamicInvoke 的 wrapper 作为后备（尽量避免）
                    return new Func<object, object?>(o => (object?)reg!.DynamicInvoke(o));
                }

                // 1) 尝试通过反射调用 GeneratedMapperFactory.TryGetMapper 获取由 Source Generator 生成的映射（如果存在）
                try
                {
                    var asm = typeof(Mapper).Assembly;
                    var genType = asm.GetType("Ling.Mapper.Generated.GeneratedMapperFactory");
                    if (genType != null)
                    {
                        var tryGet = genType.GetMethod("TryGetMapper", BindingFlags.Public | BindingFlags.Static);
                        if (tryGet != null)
                        {
                            var args = new object?[] { key.Item1, key.Item2, null };
                            var invoked = tryGet.Invoke(null, args);
                            if (invoked is bool ok && ok)
                            {
                                var obj = args[2];
                                if (obj is Func<object, object> genDel)
                                    return genDel;
                            }
                        }
                    }
                }
                catch
                {
                    // 忽略任何反射引发的异常并回退到运行时编译
                }

                // 2) 最后回退到根据映射配置编译表达式树并返回 wrapper（CompileMapper 内部返回 wrapper）
                var cfg = config ?? _config.Configs.FirstOrDefault(
                    c => c.SourceType == key.Item1 && c.DestType == key.Item2);

                return CompileMapper(key.Item1, key.Item2, cfg);
            });
        }

        /// <summary>
        /// 使用表达式树构建从 srcType 到 destType 的映射逻辑并返回一个 wrapper（Func&lt;object,object?&gt;）。
        /// 具体做法：
        /// - 构造一个强类型的映射表达式（Func&lt;srcType,destType&gt;），表达式体中为创建目标对象并按规则给属性赋值；
        /// - 然后基于该强类型表达式生成一个通用 wrapper（接收 object、内部 cast 到 srcType、调用强类型表达式、再转换为 object 返回）;
        /// - 返回已编译的 wrapper，以便运行时调用时无需 DynamicInvoke，从而提升性能。
        /// </summary>
        private Delegate CompileMapper(Type srcType, Type destType, IMappingConfig? cfg)
        {
            // 构建强类型表达式参数与目标变量
            var srcParam = Expression.Parameter(srcType, "src");
            var destVar = Expression.Variable(destType, "dest");
            var bodyExprs = new List<Expression>
            {
                // dest = new TDestination();
                Expression.Assign(destVar, Expression.New(destType))
            };

            var options = _config.GlobalOptions;

            // 获取源/目标的可读/可写属性集合
            var destProps = destType.GetProperties(BindingFlags.Public | BindingFlags.Instance)
                .Where(p => p.CanWrite)
                .ToList();
            var srcProps = srcType.GetProperties(BindingFlags.Public | BindingFlags.Instance)
                .Where(p => p.CanRead)
                .ToList();

            MappingExpressionBase? mappingExprBase = null;
            if (cfg != null)
                mappingExprBase = new MappingExpressionBase(cfg.ExpressionObject);

            // 遍历目标属性并为每个属性生成赋值表达式（如果有对应来源或自定义映射）
            foreach (var dp in destProps)
            {
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
                    var sp = FindSourceProperty(srcProps, srcName, options);

                    if (sp != null)
                    {
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
                            // 简单类型转换，处理可空类型
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

            // 基于强类型 Lambda 构建 object wrapper：Func<object, object?>
            var objParam = Expression.Parameter(typeof(object), "srcObj");
            var invokeTyped = Expression.Invoke(typedLambda, Expression.Convert(objParam, srcType));
            var convertResult = Expression.Convert(invokeTyped, typeof(object));
            var wrapperLambda = Expression.Lambda<Func<object, object?>>(convertResult, objParam);

            // 将 wrapper 编译为委托并返回。这样运行时调用只需直接调用 wrapper，无需 DynamicInvoke。
            var wrapper = wrapperLambda.Compile();

            return wrapper;
        }

        /// <summary>
        /// 根据全局约定规范化属性名以便匹配。
        /// 支持忽略下划线和大小写不敏感比较。
        /// </summary>
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
        /// 处理简单类型之间的转换，特别处理可空类型。
        /// </summary>
        /// <param name="srcAccess">源属性访问表达式</param>
        /// <param name="srcType">源属性类型</param>
        /// <param name="destType">目标属性类型</param>
        /// <returns>转换表达式</returns>
        private Expression ConvertSimpleType(Expression srcAccess, Type srcType, Type destType)
        {
            // 获取底层类型（如果是可空类型）
            var srcUnderlyingType = Nullable.GetUnderlyingType(srcType) ?? srcType;
            var destUnderlyingType = Nullable.GetUnderlyingType(destType) ?? destType;

            var srcIsNullable = Nullable.GetUnderlyingType(srcType) != null;
            var destIsNullable = Nullable.GetUnderlyingType(destType) != null;

            // 情况 1: T → T（类型完全相同）
            if (srcType == destType)
            {
                return srcAccess;
            }

            // 情况 2: T → T?（非可空 → 可空）
            if (!srcIsNullable && destIsNullable && srcUnderlyingType == destUnderlyingType)
            {
                // 直接转换为可空类型
                return Expression.Convert(srcAccess, destType);
            }

            // 情况 3: T? → T（可空 → 非可空）
            if (srcIsNullable && !destIsNullable && srcUnderlyingType == destUnderlyingType)
            {
                // 使用 GetValueOrDefault() 或条件表达式
                // 优先使用 GetValueOrDefault() 方法
                var getValueMethod = srcType.GetMethod("GetValueOrDefault", Type.EmptyTypes);
                if (getValueMethod != null)
                {
                    return Expression.Call(srcAccess, getValueMethod);
                }
                
                // 回退到条件表达式：src.HasValue ? src.Value : default(T)
                var hasValueProp = srcType.GetProperty("HasValue");
                var valueProp = srcType.GetProperty("Value");
                if (hasValueProp != null && valueProp != null)
                {
                    return Expression.Condition(
                        Expression.Property(srcAccess, hasValueProp),
                        Expression.Property(srcAccess, valueProp),
                        Expression.Default(destType)
                    );
                }
            }

            // 情况 4: T? → U?（可空到可空，但底层类型不同）
            if (srcIsNullable && destIsNullable)
            {
                // 先转换底层类型，再包装为可空
                var hasValueProp = srcType.GetProperty("HasValue");
                var valueProp = srcType.GetProperty("Value");
                
                if (hasValueProp != null && valueProp != null)
                {
                    var convertedValue = Expression.Convert(
                        Expression.Property(srcAccess, valueProp),
                        destUnderlyingType
                    );
                    
                    return Expression.Condition(
                        Expression.Property(srcAccess, hasValueProp),
                        Expression.Convert(convertedValue, destType),
                        Expression.Default(destType)
                    );
                }
            }

            // 情况 5: T → U（非可空类型之间的转换）
            if (!srcIsNullable && !destIsNullable && srcUnderlyingType != destUnderlyingType)
            {
                return Expression.Convert(srcAccess, destType);
            }

            // 情况 6: T → U?（非可空转换为不同类型的可空）
            if (!srcIsNullable && destIsNullable && srcUnderlyingType != destUnderlyingType)
            {
                var converted = Expression.Convert(srcAccess, destUnderlyingType);
                return Expression.Convert(converted, destType);
            }

            // 情况 7: T? → U（可空转换为不同类型的非可空）
            if (srcIsNullable && !destIsNullable && srcUnderlyingType != destUnderlyingType)
            {
                var getValueMethod = srcType.GetMethod("GetValueOrDefault", Type.EmptyTypes);
                if (getValueMethod != null)
                {
                    var value = Expression.Call(srcAccess, getValueMethod);
                    return Expression.Convert(value, destType);
                }
            }

            // 默认：尝试直接转换
            return Expression.Convert(srcAccess, destType);
        }

        /// <summary>
        /// 在源属性列表中查找与目标属性名匹配的属性，依据 NormalizeName 的规则对比。
        /// 如果找不到返回 null。
        /// </summary>
        private PropertyInfo? FindSourceProperty(List<PropertyInfo> srcProps, string destName, GlobalConventionOptions options)
        {
            string normalizedDest = NormalizeName(destName, options);

            foreach (var sp in srcProps)
            {
                string normalizedSrc = NormalizeName(sp.Name, options);

                if (normalizedSrc == normalizedDest)
                    return sp;
            }

            return null;
        }

        /// <summary>
        /// 内部集合映射实现：将 srcCollection 中的每个元素映射为目标元素类型并装入目标集合（List 或数组）。
        /// 注意：对于目标为数组的情况，会将临时 List 转为数组返回；对于 IEnumerable 或 List 会直接返回 List&lt;T&gt;。
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
                    if (TypeUtils.IsSimple(destElementType))
                    {
                        // 简单元素类型直接添加（假设可赋值）
                        destList.Add(item);
                    }
                    else
                    {
                        // 复杂元素类型递归映射
                        if (srcElementType != null)
                        {
                            var mapped = Map(item, srcElementType, destElementType);
                            destList.Add(mapped);
                        }
                    }
                }

                if (!destType.IsArray) return destList;

                // 如果目标为数组，则将 List<T> 转为 T[] 返回
                var toArray = typeof(Enumerable).GetMethod(nameof(Enumerable.ToArray))?.MakeGenericMethod(destElementType);
                return toArray?.Invoke(null, new object[] { destList });
            }

            return null;
        }

        /// <summary>
        /// 映射表达式包装类：用于在运行时通过反射读取 MappingExpression 内部的数据结构（IgnoredMembers、CustomMemberBindings、RenamedMembers），
        /// 将不同具体泛型 MappingExpression 的访问统一到运行时代码中使用。
        /// 注意：该类依赖反射访问内部字段，仅在运行时回退路径中使用；当使用 Source Generator 生成代码时可完全避免反射。
        /// </summary>
        private class MappingExpressionBase
        {
            private readonly object _exprObj;
            private readonly Type _exprType;

            /// <summary>
            /// 使用具体的 MappingExpression 实例构造包装器。
            /// exprObj 不得为 null。
            /// </summary>
            public MappingExpressionBase(object exprObj)
            {
                _exprObj = exprObj ?? throw new ArgumentNullException(nameof(exprObj));
                _exprType = exprObj.GetType();
            }

            /// <summary>
            /// 检查指定的目标属性名是否在忽略集合中。
            /// </summary>
            /// <param name="destName">目标属性名。</param>
            /// <returns>若被忽略返回 true，否则 false。</returns>
            public bool IsIgnored(string destName)
            {
                var field = _exprType.GetField("IgnoredMembers", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
                var set = field?.GetValue(_exprObj) as System.Collections.IEnumerable;
                if (set == null) return false;

                foreach (var item in set)
                {
                    if (item is string s && s == destName)
                        return true;
                }

                return false;
            }

            /// <summary>
            /// 尝试从 CustomMemberBindings 中获取指定目标属性的自定义绑定表达式。
            /// </summary>
            /// <param name="destName">目标属性名。</param>
            /// <param name="lambda">输出的 LambdaExpression（若存在）。</param>
            /// <returns>若存在则返回 true，否则 false。</returns>
            public bool TryGetCustomBinding(string destName, out LambdaExpression? lambda)
            {
                var field = _exprType.GetField("CustomMemberBindings", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
                var dict = field?.GetValue(_exprObj) as System.Collections.IDictionary;
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

            /// <summary>
            /// 获取重命名规则中指定目标属性对应的源属性名，如果没有配置返回 null。
            /// </summary>
            public string? GetRenamedSource(string destName)
            {
                var field = _exprType.GetField("RenamedMembers", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
                var dict = field?.GetValue(_exprObj) as System.Collections.IDictionary;
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
