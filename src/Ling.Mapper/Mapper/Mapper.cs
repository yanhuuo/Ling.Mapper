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

                // 使用占位符标记正在处理（避免无限递归）
                ctx[source] = null!;
                
                var mapper = GetOrCompile(sourceType, destType, options, null);
                var result = mapper(source);
                
                // 更新缓存为实际结果
                ctx[source] = result!;
                return result;
            }

            return GetOrCompile(sourceType, destType, options, null)(source);
        }

        // 🔥 在编译的表达式中调用此方法来注册循环引用上下文
        private void RegisterInContext(object source, object destination, Type sourceType)
        {
            if (source == null || destination == null)
                return;

            var needCycleCheck =
                !sourceType.IsValueType &&
                !TypeUtils.IsSimple(sourceType);

            if (needCycleCheck)
            {
                var ctx = _mappingContext.Value;
                if (ctx != null && !ctx.ContainsKey(source))
                {
                    ctx[source] = destination;
                }
            }
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

                    // 🔥 集合到集合的映射：直接使用集合映射方法
                    if (IsCollectionButNotString(srcType) && IsCollectionButNotString(destType))
                    {
                        return src => MapCollectionInternalWithOptions(src, srcType, destType, options);
                    }

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

            // 🔥 循环引用处理：创建对象后立即注册到上下文
            var needsCycleCheck = !srcType.IsValueType && !TypeUtils.IsSimple(srcType) && !TypeUtils.IsSimple(destType);
            if (needsCycleCheck)
            {
                body.Add(
                    Expression.Call(
                        Expression.Constant(this),
                        RegisterInContextMethod,
                        srcObj,
                        Expression.Convert(destVar, typeof(object)),
                        Expression.Constant(srcType, typeof(Type))
                    )
                );
            }

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
                    var callExpr = Expression.Call(
                        Expression.Constant(this),
                        MapCollectionWithOptionsMethod,
                        Expression.Convert(srcValue, typeof(object)),
                        Expression.Constant(srcValueType, typeof(Type)),
                        Expression.Constant(dp.PropertyType, typeof(Type)),
                        Expression.Constant(options)
                    );

                    // 添加 null 检查和安全的类型转换
                    if (!srcValueType.IsValueType)
                    {
                        // 源类型是引用类型，需要 null 检查
                        var srcBoxed = Expression.Convert(srcValue, typeof(object));
                        value = Expression.Condition(
                            Expression.Equal(srcBoxed, Expression.Constant(null, typeof(object))),
                            Expression.Default(dp.PropertyType),
                            Expression.Convert(callExpr, dp.PropertyType)
                        );
                    }
                    else
                    {
                        value = Expression.Convert(callExpr, dp.PropertyType);
                    }
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

        // Nullable-aware + Enum/String conversion support
        private static Expression ConvertValueExpression(
            Expression value,
            Type srcType,
            Type destType)
        {
            var destUnderlying = Nullable.GetUnderlyingType(destType);
            var srcUnderlying = Nullable.GetUnderlyingType(srcType);

            // 获取真实的非可空类型
            var actualSrcType = srcUnderlying ?? srcType;
            var actualDestType = destUnderlying ?? destType;

            // ===== 特殊情况：enum <-> string 转换 =====
            if ((actualSrcType.IsEnum && actualDestType == typeof(string)) ||
                (actualSrcType == typeof(string) && actualDestType.IsEnum))
            {
                return BuildEnumStringConversion(value, srcType, destType, actualSrcType, actualDestType);
            }

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

        // 处理 enum <-> string 转换
        private static Expression BuildEnumStringConversion(
            Expression value,
            Type srcType,
            Type destType,
            Type actualSrcType,
            Type actualDestType)
        {
            var srcIsNullable = Nullable.GetUnderlyingType(srcType) != null;
            var destIsNullable = Nullable.GetUnderlyingType(destType) != null;

            // ===== Enum -> String =====
            if (actualSrcType.IsEnum && actualDestType == typeof(string))
            {
                // 使用 Enum.ToString() 方法
                var toStringMethod = typeof(object).GetMethod(nameof(ToString))!;

                if (srcIsNullable)
                {
                    // Nullable<Enum> -> String
                    var enumValue = Expression.Property(value, "Value");
                    var boxed = Expression.Convert(enumValue, typeof(object));
                    var toString = Expression.Call(boxed, toStringMethod);

                    return Expression.Condition(
                        Expression.Property(value, "HasValue"),
                        toString,
                        Expression.Constant(string.Empty, typeof(string))
                    );
                }
                else
                {
                    // Enum -> String
                    var boxed = Expression.Convert(value, typeof(object));
                    return Expression.Call(boxed, toStringMethod);
                }
            }

            // ===== String -> Enum =====
            if (actualSrcType == typeof(string) && actualDestType.IsEnum)
            {
                // 使用 Enum.Parse 方法
                var enumParseMethod = typeof(Enum).GetMethod(
                    nameof(Enum.Parse),
                    new[] { typeof(Type), typeof(string), typeof(bool) })!;

                var parseCall = Expression.Call(
                    enumParseMethod,
                    Expression.Constant(actualDestType, typeof(Type)),
                    value,
                    Expression.Constant(true) // ignoreCase = true
                );

                var convertedEnum = Expression.Convert(parseCall, actualDestType);

                if (destIsNullable)
                {
                    // String -> Nullable<Enum>
                    // 需要处理空字符串的情况
                    var nullCheck = Expression.Call(
                        typeof(string).GetMethod(nameof(string.IsNullOrEmpty), new[] { typeof(string) })!,
                        value
                    );

                    return Expression.Condition(
                        nullCheck,
                        Expression.Default(destType),
                        Expression.Convert(convertedEnum, destType)
                    );
                }
                else
                {
                    // String -> Enum (非空)
                    return convertedEnum;
                }
            }

            throw new InvalidOperationException("Unexpected enum/string conversion scenario");
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

            var listType = typeof(List<>).MakeGenericType(destElemType);
            var list = (IList)Activator.CreateInstance(listType)!;

            // 🔥 处理可空类型：获取底层类型
            var srcUnderlyingType = srcElemType != null ? Nullable.GetUnderlyingType(srcElemType) ?? srcElemType : null;
            var destUnderlyingType = Nullable.GetUnderlyingType(destElemType) ?? destElemType;

            // 检查元素类型是否为简单类型（string, int, enum 等）
            var srcElemIsSimple = srcUnderlyingType != null && TypeUtils.IsSimple(srcUnderlyingType);
            var destElemIsSimple = TypeUtils.IsSimple(destUnderlyingType);

            foreach (var item in (IEnumerable)srcCollection)
            {
                // 处理 null 元素
                if (item == null)
                {
                    // 如果目标类型是可空类型或引用类型，可以接受 null
                    if (!destElemType.IsValueType || Nullable.GetUnderlyingType(destElemType) != null)
                    {
                        list.Add(null);
                    }
                    else
                    {
                        // 目标类型是不可空的值类型，添加默认值
                        list.Add(Activator.CreateInstance(destElemType));
                    }
                    continue;
                }

                var actualSrcType = srcElemType ?? item.GetType();
                var actualSrcUnderlyingType = Nullable.GetUnderlyingType(actualSrcType) ?? actualSrcType;

                // 🎯 简单类型到简单类型：直接转换或复制
                if (srcElemIsSimple && destElemIsSimple)
                {
                    try
                    {
                        // 类型完全相同（包括可空性）
                        if (actualSrcType == destElemType)
                        {
                            list.Add(item);
                        }
                        // 底层类型相同，但可空性不同（如 int -> int? 或 int? -> int）
                        else if (actualSrcUnderlyingType == destUnderlyingType)
                        {
                            list.Add(item);
                        }
                        // 需要类型转换（如 int -> long, int? -> long?, enum -> int）
                        else
                        {
                            var targetType = destUnderlyingType;
                            var converted = Convert.ChangeType(item, targetType);
                            list.Add(converted);
                        }
                    }
                    catch
                    {
                        // 转换失败，添加默认值
                        if (destElemType.IsValueType && Nullable.GetUnderlyingType(destElemType) == null)
                        {
                            // 不可空值类型：添加默认值
                            list.Add(Activator.CreateInstance(destElemType));
                        }
                        else
                        {
                            // 可空类型或引用类型：添加 null
                            list.Add(null);
                        }
                    }
                }
                // 🎯 复杂类型：走完整的映射逻辑
                else
                {
                    var mapped = MapObjectWithOptions(
                        item,
                        actualSrcType,
                        destElemType,
                        options);

                    list.Add(mapped);
                }
            }

            // 如果目标是数组
            if (destType.IsArray)
            {
                var toArray = typeof(Enumerable)
                    .GetMethod(nameof(Enumerable.ToArray))!
                    .MakeGenericMethod(destElemType);

                return toArray.Invoke(null, new object[] { list });
            }

            // 如果目标类型是具体的 List<T> 或其可分配的类型，直接返回
            if (destType.IsAssignableFrom(listType))
            {
                return list;
            }

            // 如果目标类型是其他具体集合类型，尝试通过构造函数或方法转换
            // 例如：HashSet<T>, Queue<T> 等
            if (!destType.IsInterface && !destType.IsAbstract)
            {
                try
                {
                    // 尝试使用接受 IEnumerable<T> 的构造函数
                    var ctor = destType.GetConstructor(new[] { typeof(IEnumerable<>).MakeGenericType(destElemType) });
                    if (ctor != null)
                    {
                        return ctor.Invoke(new object[] { list });
                    }
                }
                catch
                {
                    // 构造失败，返回 List<T>
                }
            }

            // 默认返回 List<T>（兼容大多数接口：IEnumerable<T>, ICollection<T>, IList<T>）
            return list;
        }


        private AdaptOptions GetDefaultOptions()
            => _config.DefaultAdaptOptions ?? AdaptOptions.Default;
    }
}
