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

namespace Ling.Mapper.Mapper
{
    internal sealed class Mapper : IMapper
    {
        private readonly MapperConfiguration _config;

        // 🔥 超高性能缓存：直接存 Func<object, object?>
        private readonly ConcurrentDictionary<(Type, Type, AdaptOptionsKey), Func<object, object?>> _cache = new();

        private readonly ThreadLocal<HashSet<(Type, Type, AdaptOptionsKey)>> _compiling =
            new(() => new HashSet<(Type, Type, AdaptOptionsKey)>());

        private readonly ThreadLocal<Dictionary<object, object>> _mappingContext =
            new(() => new Dictionary<object, object>(ReferenceEqualityComparer.Instance));

        private static readonly MethodInfo MapObjectWithOptionsMethod =
            typeof(Mapper).GetMethod(nameof(MapObjectWithOptions), BindingFlags.Instance | BindingFlags.NonPublic)!;

        private static readonly MethodInfo MapCollectionWithOptionsMethod =
            typeof(Mapper).GetMethod(nameof(MapCollectionInternalWithOptions), BindingFlags.Instance | BindingFlags.NonPublic)!;

        public Mapper(MapperConfiguration config)
        {
            _config = config ?? throw new ArgumentNullException(nameof(config));
            PreheatProfiles();
        }

        private void PreheatProfiles()
        {
            var opt = GetDefaultOptions();
            foreach (var cfg in _config.Configs)
                _ = GetOrCompile(cfg.SourceType, cfg.DestType, opt, cfg);
        }

        // ============================================================
        // IMapper
        // ============================================================

        public TDestination? Map<TDestination>(object? source)
            => source == null ? default : Map<TDestination>(source, GetDefaultOptions());

        public TDestination? Map<TDestination>(object source, AdaptOptions options)
        {
            if (source == null) return default;

            var mapper = GetOrCompile(source.GetType(), typeof(TDestination), options, null);
            var result = mapper(source);

            if (result == null)
            {
                var t = typeof(TDestination);
                if (t.IsValueType && Nullable.GetUnderlyingType(t) == null)
                    return default;
                return default;
            }

            return (TDestination)result;
        }

        public object? Map(object? source, Type sourceType, Type destType)
            => Map(source, sourceType, destType, GetDefaultOptions());

        public object? Map(object? source, Type sourceType, Type destType, AdaptOptions options)
        {
            if (source == null) return null;
            var mapper = GetOrCompile(sourceType, destType, options, null);
            return mapper(source);
        }

        // ============================================================
        // 内部递归入口（表达式调用）
        // ============================================================

        private object? MapObjectWithOptions(object? source, Type sourceType, Type destType, AdaptOptions options)
        {
            if (source == null)
            {
                if (destType.IsValueType && Nullable.GetUnderlyingType(destType) == null)
                    return Activator.CreateInstance(destType);
                return null;
            }

            var needCycleCheck =
                !sourceType.IsValueType &&
                !TypeUtils.IsSimple(sourceType) &&
                !TypeUtils.IsSimple(destType);

            if (needCycleCheck)
            {
                var ctx = _mappingContext.Value!;
                if (ctx.TryGetValue(source, out var cached))
                    return cached;

                ctx[source] = null!;
                var mapper = GetOrCompile(sourceType, destType, options, null);
                var result = mapper(source);
                ctx[source] = result!;
                return result;
            }

            return GetOrCompile(sourceType, destType, options, null)(source);
        }

        // ============================================================
        // Compile & Cache
        // ============================================================

        private Func<object, object?> GetOrCompile(Type srcType, Type destType, AdaptOptions options, IMappingConfig? cfg)
        {
            var key = (srcType, destType, new AdaptOptionsKey(options));

            if (_cache.TryGetValue(key, out var cached))
                return cached;

            var compiling = _compiling.Value!;
            if (!compiling.Add(key))
                return _ => null;

            try
            {
                return _cache.GetOrAdd(key, _ =>
                {
                    if (GeneratedMapperFactoryProxy.TryGetMapper(srcType, destType, out var gen) && gen != null)
                        return gen;

                    if (MapperRegistry.TryGetWrapper(srcType, destType, out var wrapper) && wrapper != null)
                        return wrapper;

                    var config = cfg ?? _config.Configs.FirstOrDefault(c =>
                        c.SourceType == srcType && c.DestType == destType);

                    return CompileMapperToObjectFunc(srcType, destType, options, config);
                });
            }
            finally
            {
                compiling.Remove(key);
            }
        }

        // ============================================================
        // 核心：编译为 Func<object, object?>
        // ============================================================

        private Func<object, object?> CompileMapperToObjectFunc(
    Type srcType,
    Type destType,
    AdaptOptions options,
    IMappingConfig? cfg)
        {
            var ctor = destType.GetConstructor(Type.EmptyTypes);
            if (ctor == null && !destType.IsValueType)
            {
                if (_config.StrictMode)
                    throw new InvalidOperationException($"No parameterless ctor for {destType.FullName}");
                return _ => null;
            }

            var ignoreCase = options.HasFlag(AdaptOptions.IgnoreCase);
            var ignoreUnderscore = options.HasFlag(AdaptOptions.IgnoreUnderscore);
            var ignoreNullValues = options.HasFlag(AdaptOptions.IgnoreNullValues);

            var exprBase = cfg != null ? new MappingExpressionBase(cfg.ExpressionObject) : null;

            var srcObj = Expression.Parameter(typeof(object), "srcObj");
            var srcTyped = Expression.Convert(srcObj, srcType);

            var destVar = Expression.Variable(destType, "dest");
            var body = new List<Expression>
    {
        Expression.Assign(destVar, Expression.New(destType))
    };

            var srcProps = srcType.GetProperties(BindingFlags.Public | BindingFlags.Instance)
                .Where(p => p.CanRead && p.GetIndexParameters().Length == 0)
                .ToArray();

            var destProps = destType.GetProperties(BindingFlags.Public | BindingFlags.Instance)
                .Where(p => p.CanWrite && p.GetIndexParameters().Length == 0)
                .ToArray();

            var srcMap = BuildSourcePropertyMap(srcProps, ignoreCase, ignoreUnderscore);

            foreach (var dp in destProps)
            {
                if (exprBase?.IsIgnored(dp.Name) == true)
                    continue;

                var srcName = exprBase?.GetRenamedSource(dp.Name) ?? dp.Name;

                Expression srcValue;
                Type srcValueType;

                // ===== 1. 取源值（支持 A.B.C）=====
                if (srcName.IndexOf('.') >= 0)
                {
                    var nested = BuildNullSafeNestedAccess(
                        srcTyped,
                        srcType,
                        srcName,
                        ignoreCase,
                        ignoreUnderscore,
                        out var finalType);

                    if (nested == null)
                    {
                        if (_config.StrictMode)
                            throw new InvalidOperationException($"Invalid nested path '{srcName}'");
                        continue;
                    }

                    srcValue = nested;
                    srcValueType = finalType!;
                }
                else
                {
                    if (!srcMap.TryGetValue(
                            NormalizeNameStatic(srcName, ignoreCase, ignoreUnderscore),
                            out var sp))
                        continue;

                    srcValue = Expression.Property(srcTyped, sp);
                    srcValueType = sp.PropertyType;
                }

                Expression value;

                // ===== 2. 集合 → 集合（必须逐元素 Mapper）=====
                if (IsCollectionButNotString(srcValueType) &&
                    IsCollectionButNotString(dp.PropertyType))
                {
                    value = Expression.Convert(
                        Expression.Call(
                            Expression.Constant(this),
                            MapCollectionWithOptionsMethod,
                            Expression.Convert(srcValue, typeof(object)),
                            Expression.Constant(srcValueType, typeof(Type)),
                            Expression.Constant(dp.PropertyType, typeof(Type)),
                            Expression.Constant(options)
                        ),
                        dp.PropertyType
                    );
                }
                // ===== 3. 简单类型（值 / Nullable / string / enum）=====
                else if (TypeUtils.IsSimple(srcValueType) &&
                         TypeUtils.IsSimple(dp.PropertyType))
                {
                    value = ConvertValueExpression(srcValue, srcValueType, dp.PropertyType);
                }
                // ===== 4. 复杂对象 → 递归 Mapper（❗禁止 Convert）=====
                else
                {
                    value = Expression.Convert(
                        Expression.Call(
                            Expression.Constant(this),
                            MapObjWithOptionsMethod,
                            Expression.Convert(srcValue, typeof(object)),
                            Expression.Constant(srcValueType, typeof(Type)),
                            Expression.Constant(dp.PropertyType, typeof(Type)),
                            Expression.Constant(options)
                        ),
                        dp.PropertyType
                    );
                }

                // ===== 5. IgnoreNullValues（仅引用类型）=====
                if (ignoreNullValues && !dp.PropertyType.IsValueType)
                {
                    var destProp = Expression.Property(destVar, dp);
                    var boxed = Expression.Convert(value, typeof(object));

                    value = Expression.Condition(
                        Expression.Equal(boxed, Expression.Constant(null, typeof(object))),
                        destProp,
                        value
                    );
                }

                body.Add(Expression.Assign(Expression.Property(destVar, dp), value));
            }

            body.Add(destVar);

            var lambda = Expression.Lambda<Func<object, object?>>(
                Expression.Convert(
                    Expression.Block(new[] { destVar }, body),
                    typeof(object)),
                srcObj);

            return CompileLambda(lambda);
        }
        // 复杂对象递归映射入口（供表达式树调用）
        private static readonly MethodInfo MapObjWithOptionsMethod =
            typeof(Mapper).GetMethod(
                nameof(MapObjectWithOptions),
                BindingFlags.Instance | BindingFlags.NonPublic
            )!;

        // ============================================================
        // Helpers
        // ============================================================

        private static bool IsCollectionButNotString(Type type)
            => type != typeof(string) && typeof(IEnumerable).IsAssignableFrom(type);

        private static T CompileLambda<T>(Expression<T> lambda) where T : Delegate
            => RuntimeFeature.IsDynamicCodeSupported
                ? lambda.Compile()
                : lambda.Compile(preferInterpretation: true);

        private static Dictionary<string, PropertyInfo> BuildSourcePropertyMap(
            PropertyInfo[] props, bool ignoreCase, bool ignoreUnderscore)
        {
            var map = new Dictionary<string, PropertyInfo>(StringComparer.Ordinal);
            foreach (var p in props)
                map[NormalizeNameStatic(p.Name, ignoreCase, ignoreUnderscore)] = p;
            return map;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static string NormalizeNameStatic(string name, bool ignoreCase, bool ignoreUnderscore)
        {
            Span<char> buf = stackalloc char[name.Length];
            var idx = 0;
            foreach (var c in name)
            {
                if (ignoreUnderscore && c == '_') continue;
                buf[idx++] = ignoreCase ? char.ToLowerInvariant(c) : c;
            }
            return new string(buf[..idx]);
        }

        // Nullable-aware
        private static Expression ConvertValueExpression(
            Expression value,
            Type srcType,
            Type destType)
        {
            var destUnderlying = Nullable.GetUnderlyingType(destType);
            var srcUnderlying = Nullable.GetUnderlyingType(srcType);

            // ===== dest: 非 Nullable 值类型 =====
            if (destType.IsValueType && destUnderlying == null)
            {
                // src: Nullable<T>
                if (srcUnderlying != null)
                {
                    return Expression.Condition(
                        Expression.Property(value, "HasValue"),
                        Expression.Convert(Expression.Property(value, "Value"), destType),
                        Expression.Default(destType)
                    );
                }

                // src: 引用 / 普通值
                var boxed = Expression.Convert(value, typeof(object));
                return Expression.Condition(
                    Expression.Equal(boxed, Expression.Constant(null)),
                    Expression.Default(destType),
                    Expression.Convert(value, destType)
                );
            }

            // ===== dest: Nullable<T> =====
            if (destUnderlying != null)
            {
                if (srcType == destUnderlying || srcUnderlying == destUnderlying)
                    return Expression.Convert(value, destType);

                var boxed = Expression.Convert(value, typeof(object));
                return Expression.Condition(
                    Expression.Equal(boxed, Expression.Constant(null)),
                    Expression.Default(destType),
                    Expression.Convert(value, destType)
                );
            }

            // ===== 引用类型 =====
            // ❗这里只允许 assignable 的情况
            if (destType.IsAssignableFrom(srcType))
                return Expression.Convert(value, destType);

            // ❌ 其他情况禁止 Convert（必须走 Mapper）
            throw new InvalidOperationException(
                $"Invalid ConvertValueExpression: {srcType.FullName} -> {destType.FullName}");
        }


        private static Expression? BuildNullSafeNestedAccess(
            Expression src,
            Type srcType,
            string path,
            bool ignoreCase,
            bool ignoreUnderscore,
            out Type? finalType)
        {
            finalType = null;

            Expression current = src;          // ✅ 永远用 Expression
            Type currentType = srcType;

            var segments = path.Split('.');
            if (segments.Length == 0)
                return null;

            foreach (var seg in segments)
            {
                var normalized = NormalizeNameStatic(seg, ignoreCase, ignoreUnderscore);

                var prop = currentType
                    .GetProperties(BindingFlags.Public | BindingFlags.Instance)
                    .FirstOrDefault(p =>
                        p.CanRead &&
                        p.GetIndexParameters().Length == 0 &&
                        NormalizeNameStatic(p.Name, ignoreCase, ignoreUnderscore) == normalized);

                if (prop == null)
                    return null;

                // ⚠️ 注意：这里类型就是 Expression，不是 MemberExpression
                Expression access = Expression.Property(current, prop);

                // 仅对“引用类型”做 null-safe
                if (!currentType.IsValueType)
                {
                    access = Expression.Condition(
                        Expression.Equal(
                            current,
                            Expression.Constant(null, currentType)
                        ),
                        Expression.Default(prop.PropertyType),
                        access
                    );
                }

                current = access;
                currentType = prop.PropertyType;
            }

            finalType = currentType;
            return current;
        }


        private object? MapCollectionInternalWithOptions(
            object? srcCollection,
            Type srcType,
            Type destType,
            AdaptOptions options)
        {
            if (srcCollection == null)
                return null;

            var srcElemType = TypeUtils.GetElementType(srcType);
            var destElemType = TypeUtils.GetElementType(destType);
            if (destElemType == null)
                return null;

            var list = (IList)Activator.CreateInstance(
                typeof(List<>).MakeGenericType(destElemType))!;

            foreach (var item in (IEnumerable)srcCollection)
            {
                if (item == null)
                {
                    list.Add(null);
                    continue;
                }

                var actualSrcType = srcElemType ?? item.GetType();

                // 🔥 关键：必须走 MapObjectWithOptions
                var mapped = MapObjectWithOptions(
                    item,
                    actualSrcType,
                    destElemType,
                    options);

                list.Add(mapped);
            }

            // 如果目标是数组
            if (destType.IsArray)
            {
                var toArray = typeof(Enumerable)
                    .GetMethod(nameof(Enumerable.ToArray))!
                    .MakeGenericMethod(destElemType);

                return toArray.Invoke(null, new object[] { list });
            }

            return list;
        }


        private AdaptOptions GetDefaultOptions()
            => _config.DefaultAdaptOptions ?? AdaptOptions.Default;
    }
}
