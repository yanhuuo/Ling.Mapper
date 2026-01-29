using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Runtime.CompilerServices;
using Ling.Mapper.Configuration;
using Ling.Mapper.Helpers;
using Ling.Mapper.Models;
using Ling.Mapper.Registry;
using Ling.Mapper.Utils;

namespace Ling.Mapper.Mapper
{
    /// <summary>
    /// Ling.Mapper 核心映射器实现
    /// 采用表达式树编译技术实现高性能对象映射
    /// </summary>
    internal sealed class Mapper : IMapper
    {
        private readonly MapperConfiguration _config;

        // 核心缓存：存储编译好的转换委托，Key 由源类型、目标类型和配置选项组成
        private readonly ConcurrentDictionary<(Type, Type, AdaptOptionsKey), Func<object, object?>> _cache = new();

        // 防止递归循环编译：记录当前线程正在编译的类型对
        private readonly ThreadLocal<HashSet<(Type, Type, AdaptOptionsKey)>> _compiling =
            new(() => new HashSet<(Type, Type, AdaptOptionsKey)>());

        // 运行时的循环引用上下文：存储已处理的对象引用，防止无限递归
        private readonly ThreadLocal<Dictionary<object, object>> _mappingContext =
            new(() => new Dictionary<object, object>(ReferenceEqualityComparer.Instance));

        // 反射预热：缓存内部映射方法的 MethodInfo 供表达式树调用
        private static readonly MethodInfo MapObjectWithOptionsMethod =
            typeof(Mapper).GetMethod(nameof(MapObjectWithOptions), BindingFlags.Instance | BindingFlags.NonPublic)!;
        private static readonly MethodInfo MapCollectionWithOptionsMethod =
            typeof(Mapper).GetMethod(nameof(MapCollectionInternalWithOptions), BindingFlags.Instance | BindingFlags.NonPublic)!;
        private static readonly MethodInfo RegisterInContextMethod =
            typeof(Mapper).GetMethod(nameof(RegisterInContext), BindingFlags.Instance | BindingFlags.NonPublic)!;

        public Mapper(MapperConfiguration config)
        {
            _config = config ?? throw new ArgumentNullException(nameof(config));
            PreheatProfiles(); // 预热配置中定义的映射关系
        }

        private void PreheatProfiles()
        {
            var opt = GetDefaultOptions();
            foreach (var cfg in _config.Configs)
                _ = GetOrCompile(cfg.SourceType, cfg.DestType, opt, cfg);
        }

        #region IMapper 接口实现

        public TDestination? Map<TDestination>(object? source)
            => source == null ? default : Map<TDestination>(source, GetDefaultOptions());

        public TDestination? Map<TDestination>(object source, AdaptOptions options)
        {
            if (source == null) return default;
            var mapper = GetOrCompile(source.GetType(), typeof(TDestination), options, null);
            var result = mapper(source);
            return result == null ? default : (TDestination)result;
        }

        public object? Map(object? source, Type sourceType, Type destType)
            => Map(source, sourceType, destType, GetDefaultOptions());

        public object? Map(object? source, Type sourceType, Type destType, AdaptOptions options)
        {
            if (source == null) return null;
            var mapper = GetOrCompile(sourceType, destType, options, null);
            return mapper(source);
        }

        #endregion

        #region 内部映射逻辑

        /// <summary>
        /// 对象映射入口，支持循环引用检查
        /// </summary>
        private object? MapObjectWithOptions(object? source, Type sourceType, Type destType, AdaptOptions options)
        {
            if (source == null)
            {
                // 如果目标是值类型且不可空，返回默认实例（如 0, false）
                return destType.IsValueType && Nullable.GetUnderlyingType(destType) == null
                    ? Activator.CreateInstance(destType) : null;
            }

            // 判断是否需要循环引用检查（非简单类型且非值类型）
            var needCycleCheck = !sourceType.IsValueType && !TypeUtils.IsSimple(sourceType) && !TypeUtils.IsSimple(destType);
            if (needCycleCheck)
            {
                var ctx = _mappingContext.Value!;
                if (ctx.TryGetValue(source, out var cached)) return cached;

                ctx[source] = null!; // 占位符防止递归
                var mapper = GetOrCompile(sourceType, destType, options, null);
                var result = mapper(source);
                ctx[source] = result!; // 更新实际映射结果
                return result;
            }

            return GetOrCompile(sourceType, destType, options, null)(source);
        }

        /// <summary>
        /// 将对象引用注册到循环引用上下文中
        /// </summary>
        private void RegisterInContext(object source, object destination, Type sourceType)
        {
            if (source == null || destination == null) return;
            if (!sourceType.IsValueType && !TypeUtils.IsSimple(sourceType))
            {
                var ctx = _mappingContext.Value;
                if (ctx != null && !ctx.ContainsKey(source)) ctx[source] = destination;
            }
        }

        #endregion

        #region 表达式编译核心

        private Func<object, object?> GetOrCompile(Type srcType, Type destType, AdaptOptions options, IMappingConfig? cfg)
        {
            var key = (srcType, destType, new AdaptOptionsKey(options));
            if (_cache.TryGetValue(key, out var cached)) return cached;

            var compiling = _compiling.Value!;
            if (!compiling.Add(key)) return _ => null; // 处理递归调用冲突

            try
            {
                return _cache.GetOrAdd(key, _ =>
                {
                    // 1. 尝试从源生成映射器、注册器获取
                    if (GeneratedMapperFactoryProxy.TryGetMapper(srcType, destType, out var gen) && gen != null) return gen;
                    if (MapperRegistry.TryGetWrapper(srcType, destType, out var wrapper) && wrapper != null) return wrapper;

                    // 2. 集合到集合的特殊处理
                    if (IsCollectionButNotString(srcType) && IsCollectionButNotString(destType))
                        return src => MapCollectionInternalWithOptions(src, srcType, destType, options);

                    // 3. 构建表达式树进行编译
                    var config = cfg ?? _config.Configs.FirstOrDefault(c => c.SourceType == srcType && c.DestType == destType);
                    return CompileMapperToObjectFunc(srcType, destType, options, config);
                });
            }
            finally { compiling.Remove(key); }
        }

        /// <summary>
        /// 核心：构建并编译表达式树
        /// </summary>
        private Func<object, object?> CompileMapperToObjectFunc(Type srcType, Type destType, AdaptOptions options, IMappingConfig? cfg)
        {
            // 构造函数检查
            var ctor = destType.GetConstructor(Type.EmptyTypes);
            if (ctor == null && !destType.IsValueType)
            {
                if (_config.StrictMode) throw new InvalidOperationException($"类型 {destType.FullName} 没有无参构造函数");
                return _ => null;
            }

            var ignoreCase = options.HasFlag(AdaptOptions.IgnoreCase);
            var ignoreUnderscore = options.HasFlag(AdaptOptions.IgnoreUnderscore);
            var ignoreNullValues = options.HasFlag(AdaptOptions.IgnoreNullValues);
            var exprBase = cfg != null ? new MappingExpressionBase(cfg.ExpressionObject) : null;

            // 参数定义：(object srcObj) => ...
            var srcObj = Expression.Parameter(typeof(object), "srcObj");
            var srcTyped = Expression.Convert(srcObj, srcType);
            var destVar = Expression.Variable(destType, "dest");

            // 1. 实例化目标对象: dest = new DestType()
            var body = new List<Expression> { Expression.Assign(destVar, Expression.New(destType)) };

            // 2. 注册循环引用上下文
            if (!srcType.IsValueType && !TypeUtils.IsSimple(srcType) && !TypeUtils.IsSimple(destType))
            {
                body.Add(Expression.Call(Expression.Constant(this), RegisterInContextMethod, srcObj, Expression.Convert(destVar, typeof(object)), Expression.Constant(srcType, typeof(Type))));
            }

            // 获取属性映射关系
            var srcProps = srcType.GetProperties(BindingFlags.Public | BindingFlags.Instance).Where(p => p.CanRead && p.GetIndexParameters().Length == 0).ToArray();
            var destProps = destType.GetProperties(BindingFlags.Public | BindingFlags.Instance).Where(p => p.CanWrite && p.GetIndexParameters().Length == 0).ToArray();
            var srcMap = BuildSourcePropertyMap(srcProps, ignoreCase, ignoreUnderscore);

            foreach (var dp in destProps)
            {
                if (exprBase?.IsIgnored(dp.Name) == true) continue;
                var srcName = exprBase?.GetRenamedSource(dp.Name) ?? dp.Name;

                Expression? srcValueExpr;
                Type srcValueType;

                // A. 获取源值（处理嵌套或直接访问）
                if (srcName.IndexOf('.') >= 0)
                {
                    srcValueExpr = BuildNullSafeNestedAccess(srcTyped, srcType, srcName, ignoreCase, ignoreUnderscore, out var finalType);
                    if (srcValueExpr == null) continue;
                    srcValueType = finalType!;
                }
                else
                {
                    if (!srcMap.TryGetValue(NormalizeNameStatic(srcName, ignoreCase, ignoreUnderscore), out var sp)) continue;
                    srcValueExpr = Expression.Property(srcTyped, sp);
                    srcValueType = sp.PropertyType;
                }

                Expression? valueConverted = null;

                // B. 构建映射与转换逻辑
                if (IsCollectionButNotString(srcValueType) && IsCollectionButNotString(dp.PropertyType))
                {
                    // 集合转换
                    var call = Expression.Call(Expression.Constant(this), MapCollectionWithOptionsMethod, Expression.Convert(srcValueExpr, typeof(object)), Expression.Constant(srcValueType, typeof(Type)), Expression.Constant(dp.PropertyType, typeof(Type)), Expression.Constant(options));
                    valueConverted = !srcValueType.IsValueType
                        ? Expression.Condition(Expression.Equal(Expression.Convert(srcValueExpr, typeof(object)), Expression.Constant(null)), Expression.Default(dp.PropertyType), Expression.Convert(call, dp.PropertyType))
                        : (Expression)Expression.Convert(call, dp.PropertyType);
                }
                else if (TypeUtils.IsSimple(srcValueType) && TypeUtils.IsSimple(dp.PropertyType))
                {
                    // 🔥 简单值/可空/枚举 静默转换
                    valueConverted = ConvertValueExpression(srcValueExpr, srcValueType, dp.PropertyType);
                }
                else
                {
                    // 复杂对象递归转换
                    valueConverted = Expression.Convert(Expression.Call(Expression.Constant(this), MapObjectWithOptionsMethod, Expression.Convert(srcValueExpr, typeof(object)), Expression.Constant(srcValueType, typeof(Type)), Expression.Constant(dp.PropertyType, typeof(Type)), Expression.Constant(options)), dp.PropertyType);
                }

                // 🔥 关键：如果不兼容，ConvertValueExpression 返回 null，直接跳过赋值
                if (valueConverted == null) continue;

                // C. 处理 IgnoreNullValues 选项
                if (ignoreNullValues && !dp.PropertyType.IsValueType)
                {
                    var destProp = Expression.Property(destVar, dp);
                    valueConverted = Expression.Condition(Expression.Equal(Expression.Convert(valueConverted, typeof(object)), Expression.Constant(null, typeof(object))), destProp, valueConverted);
                }

                // D. 赋值到目标属性
                body.Add(Expression.Assign(Expression.Property(destVar, dp), valueConverted));
            }

            body.Add(destVar); // 返回实例

            // 编译 Lambda 表达式为委托
            var lambda = Expression.Lambda<Func<object, object?>>(Expression.Convert(Expression.Block(new[] { destVar }, body), typeof(object)), srcObj);
            return CompileLambda(lambda);
        }

        /// <summary>
        /// 核心转换逻辑：处理 Nullable、数值强转及枚举
        /// 如果物理类型不兼容（如 string -> int），返回 null 以便静默跳过
        /// </summary>
        private static Expression? ConvertValueExpression(Expression value, Type srcType, Type destType)
        {
            var destUnderlying = Nullable.GetUnderlyingType(destType);
            var srcUnderlying = Nullable.GetUnderlyingType(srcType);
            var actualSrc = srcUnderlying ?? srcType;
            var actualDest = destUnderlying ?? destType;

            // 1. 完全赋值兼容
            if (destType.IsAssignableFrom(srcType)) return Expression.Convert(value, destType);

            // 2. 数值类型转换 (物理强转支持)
            if (TypeUtils.IsNumeric(actualSrc) && TypeUtils.IsNumeric(actualDest))
            {
                Expression valExpr = srcUnderlying != null ? Expression.Property(value, "Value") : value;
                Expression convExpr = Expression.Convert(valExpr, actualDest);

                if (destUnderlying != null) convExpr = Expression.Convert(convExpr, destType);

                if (srcUnderlying != null)
                {
                    return Expression.Condition(
                        Expression.Property(value, "HasValue"),
                        convExpr,
                        Expression.Default(destType)
                    );
                }
                return convExpr;
            }

            // 3. 枚举转换
            if (actualSrc.IsEnum || actualDest.IsEnum)
            {
                // 枚举 <-> 字符串
                if (actualSrc == typeof(string) || actualDest == typeof(string))
                    return BuildEnumStringConversion(value, srcType, destType, actualSrc, actualDest);

                // 枚举 <-> 数值
                if (TypeUtils.IsNumeric(actualSrc) || TypeUtils.IsNumeric(actualDest))
                {
                    Expression val = srcUnderlying != null ? Expression.Property(value, "Value") : value;
                    Expression conv = Expression.Convert(Expression.Convert(val, actualDest), destType);
                    if (srcUnderlying != null)
                        return Expression.Condition(Expression.Property(value, "HasValue"), conv, Expression.Default(destType));
                    return conv;
                }
            }

            // 4. 其他不兼容类型 (如 string -> int) 静默忽略
            return null;
        }

        private static Expression BuildEnumStringConversion(Expression value, Type srcType, Type destType, Type actualSrc, Type actualDest)
        {
            var srcIsNullable = Nullable.GetUnderlyingType(srcType) != null;
            var destIsNullable = Nullable.GetUnderlyingType(destType) != null;

            if (actualSrc.IsEnum && actualDest == typeof(string))
            {
                var toStringMethod = typeof(object).GetMethod(nameof(ToString))!;
                if (srcIsNullable)
                {
                    return Expression.Condition(Expression.Property(value, "HasValue"),
                        Expression.Call(Expression.Convert(Expression.Property(value, "Value"), typeof(object)), toStringMethod),
                        Expression.Constant(null, typeof(string)));
                }
                return Expression.Call(Expression.Convert(value, typeof(object)), toStringMethod);
            }

            if (actualSrc == typeof(string) && actualDest.IsEnum)
            {
                var parseMethod = typeof(Enum).GetMethod(nameof(Enum.Parse), new[] { typeof(Type), typeof(string), typeof(bool) })!;
                var parseCall = Expression.Convert(Expression.Call(parseMethod, Expression.Constant(actualDest), value, Expression.Constant(true)), actualDest);

                Expression finalExpr = destIsNullable ? Expression.Convert(parseCall, destType) : parseCall;
                var isNullOrEmpty = Expression.Call(typeof(string).GetMethod(nameof(string.IsNullOrEmpty))!, value);
                return Expression.Condition(isNullOrEmpty, Expression.Default(destType), finalExpr);
            }
            return Expression.Default(destType);
        }

        private static Expression? BuildNullSafeNestedAccess(Expression src, Type srcType, string path, bool ignoreCase, bool ignoreUnderscore, out Type? finalType)
        {
            finalType = null;
            Expression current = src;
            Type currentType = srcType;
            var segments = path.Split('.');

            foreach (var seg in segments)
            {
                var normalized = NormalizeNameStatic(seg, ignoreCase, ignoreUnderscore);
                var prop = currentType.GetProperties(BindingFlags.Public | BindingFlags.Instance)
                    .FirstOrDefault(p => p.CanRead && p.GetIndexParameters().Length == 0 && NormalizeNameStatic(p.Name, ignoreCase, ignoreUnderscore) == normalized);

                if (prop == null) return null;

                Expression access = Expression.Property(current, prop);
                if (!currentType.IsValueType)
                {
                    access = Expression.Condition(Expression.Equal(current, Expression.Constant(null, currentType)), Expression.Default(prop.PropertyType), access);
                }
                current = access;
                currentType = prop.PropertyType;
            }
            finalType = currentType;
            return current;
        }

        #endregion

        #region 集合映射处理

        private object? MapCollectionInternalWithOptions(object? srcCollection, Type srcType, Type destType, AdaptOptions options)
        {
            if (srcCollection == null) return null;

            var srcElemType = TypeUtils.GetElementType(srcType);
            var destElemType = TypeUtils.GetElementType(destType);
            if (destElemType == null) return null;

            var listType = typeof(List<>).MakeGenericType(destElemType);
            var list = (IList)Activator.CreateInstance(listType)!;

            foreach (var item in (IEnumerable)srcCollection)
            {
                if (item == null)
                {
                    list.Add(!destElemType.IsValueType || Nullable.GetUnderlyingType(destElemType) != null ? null : Activator.CreateInstance(destElemType));
                    continue;
                }

                var actualSrcType = item.GetType();
                var srcBase = Nullable.GetUnderlyingType(actualSrcType) ?? actualSrcType;
                var destBase = Nullable.GetUnderlyingType(destElemType) ?? destElemType;

                if (TypeUtils.IsSimple(srcBase) && TypeUtils.IsSimple(destBase))
                {
                    // 仅支持数值/同类型物理强转，string 自动被 Convert.ChangeType 过滤
                    if (actualSrcType == destElemType || (TypeUtils.IsNumeric(srcBase) && TypeUtils.IsNumeric(destBase)))
                    {
                        try { list.Add(Convert.ChangeType(item, destBase)); } catch { }
                    }
                }
                else
                {
                    list.Add(MapObjectWithOptions(item, actualSrcType, destElemType, options));
                }
            }

            if (destType.IsArray)
            {
                var toArray = typeof(Enumerable).GetMethod(nameof(Enumerable.ToArray))!.MakeGenericMethod(destElemType);
                return toArray.Invoke(null, new object[] { list });
            }

            return list;
        }

        #endregion

        #region 辅助工具

        private static bool IsCollectionButNotString(Type type) => type != typeof(string) && typeof(IEnumerable).IsAssignableFrom(type);

        private static T CompileLambda<T>(Expression<T> lambda) where T : Delegate
            => RuntimeFeature.IsDynamicCodeSupported ? lambda.Compile() : lambda.Compile(preferInterpretation: true);

        private static Dictionary<string, PropertyInfo> BuildSourcePropertyMap(PropertyInfo[] props, bool ignoreCase, bool ignoreUnderscore)
        {
            var map = new Dictionary<string, PropertyInfo>(StringComparer.Ordinal);
            foreach (var p in props) map[NormalizeNameStatic(p.Name, ignoreCase, ignoreUnderscore)] = p;
            return map;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static string NormalizeNameStatic(string name, bool ignoreCase, bool ignoreUnderscore)
        {
            Span<char> buf = name.Length <= 256 ? stackalloc char[name.Length] : new char[name.Length];
            var idx = 0;
            foreach (var c in name)
            {
                if (ignoreUnderscore && c == '_') continue;
                buf[idx++] = ignoreCase ? char.ToLowerInvariant(c) : c;
            }
            return new string(buf[..idx]);
        }

        private AdaptOptions GetDefaultOptions() => _config.DefaultAdaptOptions ?? AdaptOptions.Default;

        #endregion
    }
}
