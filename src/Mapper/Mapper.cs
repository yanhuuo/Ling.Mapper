using System.Collections;
using System.Collections.Concurrent;
using System.Linq.Expressions;
using System.Reflection;
using System.Runtime.CompilerServices;
using Ling.Mapper.Configuration;
using Ling.Mapper.Helpers;
using Ling.Mapper.Models;
using Ling.Mapper.Utils;

namespace Ling.Mapper.Mapper
{
    /// <summary>
    /// Ling.Mapper 核心映射器实现
    /// 采用表达式树编译技术实现高性能对象映射，支持回调织入与动态字段忽略。
    /// </summary>
    internal sealed class Mapper : IMapper
    {
        private readonly MapperConfiguration _config;

        private readonly ConcurrentDictionary<(Type, Type, AdaptOptionsKey), Func<object, Action<object, object>?, object?>> _cache = new();

        private readonly ThreadLocal<HashSet<(Type, Type, AdaptOptionsKey)>> _compiling =
            new(() => new HashSet<(Type, Type, AdaptOptionsKey)>());

        private readonly ThreadLocal<Dictionary<object, object>> _mappingContext =
            new(() => new Dictionary<object, object>(ReferenceEqualityComparer.Instance));

        private static readonly MethodInfo MapObjectWithOptionsMethod =
            typeof(Mapper).GetMethod(nameof(MapObjectWithOptions), BindingFlags.Instance | BindingFlags.NonPublic)!;
        private static readonly MethodInfo MapCollectionWithOptionsMethod =
            typeof(Mapper).GetMethod(nameof(MapCollectionInternalWithOptions), BindingFlags.Instance | BindingFlags.NonPublic)!;
        private static readonly MethodInfo RegisterInContextMethod =
            typeof(Mapper).GetMethod(nameof(RegisterInContext), BindingFlags.Instance | BindingFlags.NonPublic)!;

        public Mapper(MapperConfiguration config)
        {
            _config = config ?? throw new ArgumentNullException(nameof(config));
            PreheatProfiles();
        }

        private void PreheatProfiles()
        {
            var opt = GetDefaultOptions();
            foreach (var cfg in _config.Configs)
                _ = GetOrCompile(cfg.SourceType, cfg.DestType, opt, null);
        }

        #region IMapper 接口实现

        public TDestination? Map<TDestination>(object? source)
            => Map<TDestination>(source, GetDefaultOptions(), null, null);

        public TDestination? Map<TDestination>(object? source, AdaptOptions options, Action<object, object>? afterMapItem = null, string[]? ignoreNames = null)
        {
            if (source == null) return default;
            var mapper = GetOrCompile(source.GetType(), typeof(TDestination), options, ignoreNames);
            var result = mapper(source, afterMapItem);
            return result == null ? default : (TDestination)result;
        }

        public object? Map(object? source, Type sourceType, Type destType)
            => Map(source, sourceType, destType, GetDefaultOptions(), null, null);

        public object? Map(object? source, Type sourceType, Type destType, AdaptOptions options, Action<object, object>? afterMapItem = null, string[]? ignoreNames = null)
        {
            if (source == null) return null;
            var mapper = GetOrCompile(sourceType, destType, options, ignoreNames);
            return mapper(source, afterMapItem);
        }

        #endregion

        #region 内部映射逻辑

        private object? MapObjectWithOptions(object? source, Type sourceType, Type destType, AdaptOptions options, Action<object, object>? afterMapItem)
        {
            if (source == null)
            {
                return destType.IsValueType && Nullable.GetUnderlyingType(destType) == null
                    ? Activator.CreateInstance(destType) : null;
            }

            var needCycleCheck = !sourceType.IsValueType && !TypeUtils.IsSimple(sourceType) && !TypeUtils.IsSimple(destType);
            if (needCycleCheck)
            {
                var ctx = _mappingContext.Value!;
                if (ctx.TryGetValue(source, out var cached)) return cached;

                ctx[source] = null!;
                var mapper = GetOrCompile(sourceType, destType, options, null);
                var result = mapper(source, afterMapItem);
                ctx[source] = result!;
                return result;
            }

            return GetOrCompile(sourceType, destType, options, null)(source, afterMapItem);
        }

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

        private Func<object, Action<object, object>?, object?> GetOrCompile(Type srcType, Type destType, AdaptOptions options, string[]? ignoreNames)
        {
            var key = (srcType, destType, new AdaptOptionsKey(options, ignoreNames));
            if (_cache.TryGetValue(key, out var cached)) return cached;

            var compiling = _compiling.Value!;
            if (!compiling.Add(key)) return (_, _) => null;

            try
            {
                return _cache.GetOrAdd(key, _ =>
                {
                    if (ignoreNames == null)
                    {
                        if (GeneratedMapperFactoryProxy.TryGetMapper(srcType, destType, out var gen) && gen != null)
                            return (s, _) => gen(s);
                    }

                    if (IsCollectionButNotString(srcType) && IsCollectionButNotString(destType))
                        return (src, cb) => MapCollectionInternalWithOptions(src, srcType, destType, options, cb, ignoreNames);

                    var config = _config.Configs.FirstOrDefault(c => c.SourceType == srcType && c.DestType == destType);
                    return CompileMapperToObjectFunc(srcType, destType, options, config, ignoreNames);
                });
            }
            finally { compiling.Remove(key); }
        }

        private Func<object, Action<object, object>?, object?> CompileMapperToObjectFunc(Type srcType, Type destType, AdaptOptions options, IMappingConfig? cfg, string[]? ignoreNames)
        {
            var ctor = destType.GetConstructor(Type.EmptyTypes);
            if (ctor == null && !destType.IsValueType)
            {
                if (_config.StrictMode) throw new InvalidOperationException($"类型 {destType.FullName} 没有无参构造函数");
                return (_, _) => null;
            }

            var ignoreCase = options.HasFlag(AdaptOptions.IgnoreCase);
            var ignoreUnderscore = options.HasFlag(AdaptOptions.IgnoreUnderscore);
            var ignoreNullValues = options.HasFlag(AdaptOptions.IgnoreNullValues);
            var exprBase = cfg != null ? new MappingExpressionBase(cfg.ExpressionObject) : null;
            var finalIgnores = ignoreNames ?? Array.Empty<string>();

            var srcObj = Expression.Parameter(typeof(object), "srcObj");
            var callback = Expression.Parameter(typeof(Action<object, object>), "cb");
            var srcTyped = Expression.Convert(srcObj, srcType);
            var destVar = Expression.Variable(destType, "dest");

            var body = new List<Expression> { Expression.Assign(destVar, Expression.New(destType)) };

            if (!srcType.IsValueType && !TypeUtils.IsSimple(srcType) && !TypeUtils.IsSimple(destType))
            {
                body.Add(Expression.Call(Expression.Constant(this), RegisterInContextMethod,
                    srcObj, Expression.Convert(destVar, typeof(object)), Expression.Constant(srcType, typeof(Type))));
            }

            var srcProps = srcType.GetProperties(BindingFlags.Public | BindingFlags.Instance).Where(p => p.CanRead && p.GetIndexParameters().Length == 0).ToArray();
            var destProps = destType.GetProperties(BindingFlags.Public | BindingFlags.Instance).Where(p => p.CanWrite && p.GetIndexParameters().Length == 0).ToArray();
            var srcMap = BuildSourcePropertyMap(srcProps, ignoreCase, ignoreUnderscore);

            foreach (var dp in destProps)
            {
                if (exprBase?.IsIgnored(dp.Name) == true || finalIgnores.Contains(dp.Name)) continue;

                var srcName = exprBase?.GetRenamedSource(dp.Name) ?? dp.Name;
                Expression? srcValueExpr;
                Type srcValueType;

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

                if (IsCollectionButNotString(srcValueType) && IsCollectionButNotString(dp.PropertyType))
                {
                    var call = Expression.Call(Expression.Constant(this), MapCollectionWithOptionsMethod,
                        Expression.Convert(srcValueExpr, typeof(object)),
                        Expression.Constant(srcValueType, typeof(Type)),
                        Expression.Constant(dp.PropertyType, typeof(Type)),
                        Expression.Constant(options),
                        Expression.Constant(null, typeof(Action<object, object>)),
                        Expression.Constant(null, typeof(string[])));

                    valueConverted = !srcValueType.IsValueType
                        ? Expression.Condition(Expression.Equal(Expression.Convert(srcValueExpr, typeof(object)), Expression.Constant(null)), Expression.Default(dp.PropertyType), Expression.Convert(call, dp.PropertyType))
                        : (Expression)Expression.Convert(call, dp.PropertyType);
                }
                else if (TypeUtils.IsSimple(srcValueType) && TypeUtils.IsSimple(dp.PropertyType))
                {
                    valueConverted = ConvertValueExpression(srcValueExpr, srcValueType, dp.PropertyType);
                }
                else
                {
                    valueConverted = Expression.Convert(Expression.Call(Expression.Constant(this), MapObjectWithOptionsMethod,
                        Expression.Convert(srcValueExpr, typeof(object)),
                        Expression.Constant(srcValueType, typeof(Type)),
                        Expression.Constant(dp.PropertyType, typeof(Type)),
                        Expression.Constant(options),
                        Expression.Constant(null, typeof(Action<object, object>))), dp.PropertyType);
                }

                if (valueConverted == null) continue;

                if (ignoreNullValues && !dp.PropertyType.IsValueType)
                {
                    var destProp = Expression.Property(destVar, dp);
                    valueConverted = Expression.Condition(Expression.Equal(Expression.Convert(valueConverted, typeof(object)), Expression.Constant(null, typeof(object))), destProp, valueConverted);
                }

                body.Add(Expression.Assign(Expression.Property(destVar, dp), valueConverted));
            }

            body.Add(Expression.IfThen(
                Expression.NotEqual(callback, Expression.Constant(null)),
                Expression.Invoke(callback, Expression.Convert(destVar, typeof(object)), srcObj)
            ));
            body.Add(destVar);

            var lambda = Expression.Lambda<Func<object, Action<object, object>?, object?>>(
                Expression.Convert(Expression.Block(new[] { destVar }, body), typeof(object)), srcObj, callback);

            return CompileLambda(lambda);
        }

        private static Expression? BuildNullSafeNestedAccess(Expression src, Type srcType, string path, bool ignoreCase, bool ignoreUnderscore, out Type? finalType)
        {
            finalType = null;
            Expression currentExpr = src;
            Type currentType = srcType;
            var segments = path.Split('.');

            foreach (var seg in segments)
            {
                var normalized = NormalizeNameStatic(seg, ignoreCase, ignoreUnderscore);
                var prop = currentType.GetProperties(BindingFlags.Public | BindingFlags.Instance)
                    .FirstOrDefault(p => p.CanRead && p.GetIndexParameters().Length == 0 && NormalizeNameStatic(p.Name, ignoreCase, ignoreUnderscore) == normalized);

                if (prop == null) return null;

                Expression accessExpr = Expression.Property(currentExpr, prop);
                if (!currentType.IsValueType)
                {
                    accessExpr = Expression.Condition(
                        Expression.Equal(currentExpr, Expression.Constant(null, currentType)),
                        Expression.Default(prop.PropertyType),
                        accessExpr
                    );
                }
                currentExpr = accessExpr;
                currentType = prop.PropertyType;
            }
            finalType = currentType;
            return currentExpr;
        }

        private static Expression? ConvertValueExpression(Expression value, Type srcType, Type destType)
        {
            var srcUnderlying = Nullable.GetUnderlyingType(srcType);
            var destUnderlying = Nullable.GetUnderlyingType(destType);
            var actualSrc = srcUnderlying ?? srcType;
            var actualDest = destUnderlying ?? destType;

            if (destType.IsAssignableFrom(srcType)) return Expression.Convert(value, destType);

            if (TypeUtils.IsNumeric(actualSrc) && TypeUtils.IsNumeric(actualDest))
            {
                Expression valExpr = srcUnderlying != null ? Expression.Property(value, "Value") : value;
                Expression convExpr = Expression.Convert(valExpr, actualDest);
                if (destUnderlying != null) convExpr = Expression.Convert(convExpr, destType);
                if (srcUnderlying != null)
                {
                    return Expression.Condition(Expression.Property(value, "HasValue"), convExpr, Expression.Default(destType));
                }
                return convExpr;
            }

            if (actualSrc.IsEnum || actualDest.IsEnum)
            {
                if (actualSrc == typeof(string) || actualDest == typeof(string))
                    return BuildEnumStringConversion(value, srcType, destType, actualSrc, actualDest);

                if (TypeUtils.IsNumeric(actualSrc) || TypeUtils.IsNumeric(actualDest))
                {
                    Expression val = srcUnderlying != null ? Expression.Property(value, "Value") : value;
                    Expression conv = Expression.Convert(Expression.Convert(val, actualDest), destType);
                    if (srcUnderlying != null)
                    {
                        return Expression.Condition(Expression.Property(value, "HasValue"), conv, Expression.Default(destType));
                    }
                    return conv;
                }
            }
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

        #endregion

        #region 集合映射处理

        private object? MapCollectionInternalWithOptions(object? srcCollection, Type srcType, Type destType, AdaptOptions options, Action<object, object>? afterMapItem, string[]? ignoreNames)
        {
            if (srcCollection == null) return null;
            var destElemType = TypeUtils.GetElementType(destType);
            if (destElemType == null) return null;

            var list = (IList)Activator.CreateInstance(typeof(List<>).MakeGenericType(destElemType))!;
            foreach (var item in (IEnumerable)srcCollection)
            {
                var mapped = MapObjectWithOptions(item, item?.GetType() ?? destElemType, destElemType, options, afterMapItem);
                list.Add(mapped);
            }
            return destType.IsArray ? EnumerableToArray(list, destElemType) : list;
        }

        private object EnumerableToArray(IList list, Type elementType)
        {
            var method = typeof(Enumerable).GetMethod("ToArray")!.MakeGenericMethod(elementType);
            return method.Invoke(null, new object[] { list })!;
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
