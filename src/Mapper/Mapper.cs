using System.Collections.Concurrent;
using System.Linq.Expressions;
using System.Reflection;
using System.Runtime.CompilerServices;
using Ling.Mapper.Configuration;
using Ling.Mapper.Helpers;
using Ling.Mapper.Models;
using Ling.Mapper.Utils;

namespace Ling.Mapper.Mapper;

// Reflection is used only while building plans. Interpretation is a fallback, not a
// guarantee of NativeAOT/trimming compatibility; generated plans must preserve metadata.
internal sealed partial class Mapper : IMapper
{
    private delegate object? MappingPlan(object source, Action<object, object>? callback, MappingContext context);
    private readonly MapperConfiguration _config;
    private sealed class PlanGeneration(long version)
    {
        internal readonly long Version = version;
        internal readonly ConcurrentDictionary<(Type, Type, AdaptOptionsKey), MappingPlan> Plans = new();
    }
    private PlanGeneration _generation = new(TypeConverterRegistry.Version);
    private static readonly MethodInfo MapMemberMethod = typeof(Mapper).GetMethod(nameof(MapMember), BindingFlags.Instance | BindingFlags.NonPublic)!;

    public Mapper(MapperConfiguration config)
    {
        _config = config ?? throw new ArgumentNullException(nameof(config));
        ValidateOptions(GetDefaultOptions());
        if (config.NullValueHandling is not NullValueHandling.UseDefault and not NullValueHandling.Throw)
            throw new ArgumentOutOfRangeException(nameof(config.NullValueHandling));
        if (config.MaxDiagnosticValueLength < 0)
            throw new ArgumentOutOfRangeException(nameof(config.MaxDiagnosticValueLength));
        foreach (var cfg in config.Configs)
            _ = GetOrCompile(cfg.SourceType, cfg.DestType, GetDefaultOptions(), null);
    }

    public TDestination? Map<TDestination>(object? source) => Map<TDestination>(source, GetDefaultOptions());
    public TDestination? Map<TDestination>(object? source, AdaptOptions options, Action<object, object>? afterMapItem = null, string[]? ignoreNames = null)
    {
        var result = Map(source, source?.GetType() ?? typeof(object), typeof(TDestination), options, afterMapItem, ignoreNames);
        return result == null ? default : (TDestination)result;
    }
    public object? Map(object? source, Type sourceType, Type destType) => Map(source, sourceType, destType, GetDefaultOptions());
    public object? Map(object? source, Type sourceType, Type destType, AdaptOptions options, Action<object, object>? afterMapItem = null, string[]? ignoreNames = null)
    {
        ArgumentNullException.ThrowIfNull(sourceType);
        ArgumentNullException.ThrowIfNull(destType);
        ValidateOptions(options);
        if (source != null && !TypeUtils.IsSafelyAssignableValue(source, sourceType))
            throw MappingException.Create(sourceType, destType, "$", source,
                new InvalidCastException("Source value does not match the declared source type."), _config);
        return MapValue(source, sourceType, destType, options, afterMapItem, ignoreNames, new MappingContext());
    }

    private static void ValidateOptions(AdaptOptions options)
    {
        const AdaptOptions supported = AdaptOptions.IgnoreCase | AdaptOptions.IgnoreUnderscore | AdaptOptions.IgnoreNullValues;
        if ((options & ~supported) != 0)
            throw new ArgumentException("Mapping options contain unsupported flags.", nameof(options));
    }

    private object? MapValue(object? source, Type sourceType, Type destType, AdaptOptions options, Action<object, object>? callback, string[]? ignores, MappingContext context)
    {
        try
        {
            if (source == null)
            {
                if (!destType.IsValueType || Nullable.GetUnderlyingType(destType) != null) return null;
                if (_config.NullValueHandling == NullValueHandling.Throw) throw new InvalidCastException();
                return DefaultValues.GetOrAdd(destType, t => CompileLambda(Expression.Lambda<Func<object>>(Expression.Convert(Expression.Default(t), typeof(object)))))();
            }
            if (TypeUtils.IsSafelyAssignableValue(source, destType)) sourceType = source.GetType();
            else if ((sourceType == typeof(object) || sourceType.IsInterface || sourceType.IsAbstract) &&
                !TypeConverterRegistry.TryGetEffective(sourceType, destType, out _)) sourceType = source.GetType();
            var result = GetOrCompile(sourceType, destType, options, ignores)(source, callback, context);
            if (result != null && !(TypeUtils.IsCollection(sourceType) && TypeUtils.IsCollection(destType)))
                callback?.Invoke(result, source);
            return result;
        }
        catch (MappingException error) when (ReferenceEquals(error.Origin, context.Origin)) { throw; }
        catch (Exception error) { throw MappingException.Create(sourceType, destType, context.GetPath(), source, error, _config, context.IsSensitive, context.Origin); }
    }
    private static readonly ConcurrentDictionary<Type, Func<object>> DefaultValues = new();

    private object? MapMember(object? value, Type sourceType, Type destType, AdaptOptions options, string name, bool sensitive, MappingContext context)
    {
        context.Push(name, sensitive);
        try { return MapValue(value, sourceType, destType, options, null, null, context); }
        finally { context.Pop(); }
    }

    private MappingPlan GetOrCompile(Type sourceType, Type destType, AdaptOptions options, string[]? ignores)
    {
        var generation = System.Threading.Volatile.Read(ref _generation);
        var version = TypeConverterRegistry.Version;
        if (generation.Version != version)
        {
            var replacement = new PlanGeneration(version);
            var observed = System.Threading.Interlocked.CompareExchange(ref _generation, replacement, generation);
            generation = ReferenceEquals(observed, generation) ? replacement : observed;
        }
        var key = (sourceType, destType, new AdaptOptionsKey(options, ignores));
        if (generation.Plans.TryGetValue(key, out var cached)) return cached;
        return CompileMissingPlan(generation, sourceType, destType, options, ignores);
    }

    // Keep captured locals out of GetOrCompile entirely: a capturing lambda in its
    // miss branch would still allocate a display class on every successful cache hit.
    private MappingPlan CompileMissingPlan(PlanGeneration generation, Type sourceType, Type destType, AdaptOptions options, string[]? ignores)
    {
        // Capture only on misses. Retired generations remain alive only for in-flight calls.
        var snapshot = ignores?.ToArray();
        var key = (sourceType, destType, new AdaptOptionsKey(options, snapshot));
        return generation.Plans.GetOrAdd(key, _ => BuildPlan(sourceType, destType, options, snapshot));
    }

    private MappingPlan BuildPlan(Type sourceType, Type destType, AdaptOptions options, string[]? ignores)
    {
        if (TypeUtils.IsSafelyAssignable(sourceType, destType)) return (value, callback, _) =>
        {
            if (callback != null && TypeUtils.IsCollection(sourceType) && TypeUtils.IsCollection(destType))
                foreach (var item in (System.Collections.IEnumerable)value)
                    if (item != null) callback(item, item);
            return value;
        };
        if (TypeConverterRegistry.TryGetInvoker(sourceType, destType, out var convert)) return (value, _, _) => convert!(value);
        // KeyValuePair has read-only members: treating dictionaries as ordinary
        // collections would silently produce default keys and values.
        if (TypeUtils.IsDictionary(sourceType) || TypeUtils.IsDictionary(destType))
            return (_, _, _) => throw new NotSupportedException("Cross-type dictionary mapping requires an explicit registered converter.");
        if (TypeUtils.IsSimple(sourceType) || TypeUtils.IsSimple(destType))
            return (value, _, _) => ScalarConversion.Convert(value, destType);
        if (TypeUtils.IsCollection(sourceType) || TypeUtils.IsCollection(destType))
            return BuildCollectionPlan(sourceType, destType, options, ignores);
        if (GeneratedMapperFactoryProxy.TryGetMapper(sourceType, destType, out var generated) && generated != null && ignores == null)
            return (value, _, _) => generated(value);
        return BuildObjectPlan(sourceType, destType, options, ignores);
    }

    private MappingPlan BuildObjectPlan(Type srcType, Type destType, AdaptOptions options, string[]? ignoreNames)
    {
        if (destType.IsValueType && !destType.GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Any(property => property.SetMethod?.IsPublic == true && !property.SetMethod.IsStatic && property.GetIndexParameters().Length == 0))
            return (_, _, _) => throw new NotSupportedException("Mapping to an immutable or empty struct requires an explicit registered converter.");
        if (destType.IsAbstract || destType.IsInterface || (!destType.IsValueType && destType.GetConstructor(Type.EmptyTypes) == null))
            return (_, _, _) => _config.StrictMode
                ? throw new InvalidOperationException("Destination requires a public parameterless constructor.")
                : null;
        var cfg = _config.Configs.FirstOrDefault(c => c.SourceType == srcType && c.DestType == destType);
        var rules = cfg == null ? null : new MappingExpressionBase(cfg.ExpressionObject);
        var ignoreCase = options.HasFlag(AdaptOptions.IgnoreCase);
        var ignoreUnderscore = options.HasFlag(AdaptOptions.IgnoreUnderscore);
        var srcObj = Expression.Parameter(typeof(object), "source");
        var callback = Expression.Parameter(typeof(Action<object, object>), "callback");
        var context = Expression.Parameter(typeof(MappingContext), "context");
        var src = Expression.Convert(srcObj, srcType);
        var dest = Expression.Variable(destType, "destination");
        var body = new List<Expression> { Expression.Assign(dest, Expression.New(destType)) };
        var needsReferenceTracking = false;
        var properties = BuildSourcePropertyMap(srcType.GetProperties().Where(p => p.GetMethod?.IsPublic == true && !p.GetMethod.IsStatic && p.GetIndexParameters().Length == 0).ToArray(), ignoreCase, ignoreUnderscore);
        foreach (var property in destType.GetProperties().Where(p => p.SetMethod?.IsPublic == true && !p.SetMethod.IsStatic && p.GetIndexParameters().Length == 0))
        {
            if (rules?.IsIgnored(property.Name) == true || ignoreNames?.Contains(property.Name) == true) continue;
            Expression? value;
            Type? valueType;
            var sensitive = MappingException.IsSensitivePath(property.Name);
            var custom = rules?.GetCustomBinding(property.Name);
            if (custom != null)
            {
                value = new ParameterSubstitution(custom.Parameters[0], src).Visit(custom.Body)!;
                valueType = custom.ReturnType;
                sensitive |= MappingException.IsSensitivePath(custom.Body.ToString());
            }
            else
            {
                var name = rules?.GetRenamedSource(property.Name) ?? property.Name;
                sensitive |= MappingException.IsSensitivePath(name);
                if (name.Contains('.')) value = BuildNullSafeNestedAccess(src, srcType, name, ignoreCase, ignoreUnderscore, out valueType);
                else if (properties.TryGetValue(NormalizeNameStatic(name, ignoreCase, ignoreUnderscore), out var sp)) { value = Expression.Property(src, sp); valueType = sp.PropertyType; }
                else continue;
                if (value == null) continue;
            }
            needsReferenceTracking |= !TypeUtils.IsSafelyAssignable(valueType!, property.PropertyType) &&
                !TypeUtils.IsSimple(valueType!) && !TypeUtils.IsSimple(property.PropertyType) &&
                !TypeConverterRegistry.TryGetEffective(valueType!, property.PropertyType, out _);
            var temporary = Expression.Variable(valueType!, "value");
            var converted = BuildMemberConversion(temporary, property.PropertyType, options, property.Name, sensitive, context);
            Expression assignment = Expression.Assign(Expression.Property(dest, property), converted);
            if (options.HasFlag(AdaptOptions.IgnoreNullValues) && (!valueType!.IsValueType || Nullable.GetUnderlyingType(valueType) != null))
                assignment = Expression.IfThen(Expression.NotEqual(temporary, Expression.Default(valueType)), assignment);
            var error = Expression.Parameter(typeof(Exception), "error");
            var fail = Expression.Call(Expression.Constant(this), nameof(MemberFailure), null, error,
                Expression.Constant(valueType!, typeof(Type)), Expression.Constant(property.PropertyType),
                Expression.Constant(property.Name), Expression.Constant(sensitive), Expression.Convert(temporary, typeof(object)), context);
            // Typed locals avoid boxing on direct assignments and numeric conversions.
            // Evaluate each getter once; build the full path only when an exception occurs.
            var readFailure = Expression.Call(Expression.Constant(this), nameof(MemberFailure), null, error,
                Expression.Constant(custom != null ? srcType : valueType!, typeof(Type)), Expression.Constant(property.PropertyType),
                Expression.Constant(property.Name), Expression.Constant(sensitive), Expression.Constant(null, typeof(object)), context);
            var read = Expression.TryCatch(Expression.Assign(temporary, value), Expression.Catch(error, Expression.Throw(readFailure, valueType!)));
            var operation = Expression.Block(assignment, Expression.Empty());
            body.Add(Expression.Block(new[] { temporary }, read, Expression.TryCatch(operation,
                Expression.Catch(error, Expression.Throw(fail)))));
        }
        body.Add(Expression.Convert(dest, typeof(object)));
        if (needsReferenceTracking)
            body.Insert(1, Expression.Call(context, nameof(MappingContext.Register), null, srcObj, Expression.Constant(destType), Expression.Convert(dest, typeof(object))));
        var compiled = CompileLambda(Expression.Lambda<MappingPlan>(Expression.Block(new[] { dest }, body), srcObj, callback, context));
        return needsReferenceTracking
            ? (source, cb, ctx) => ctx.TryGet(source, destType, out var existing) ? existing : compiled(source, cb, ctx)
            : (source, cb, ctx) =>
            {
                if (ctx.TryGet(source, destType, out var existing)) return existing;
                var result = compiled(source, cb, ctx);
                if (ctx.HasReferences && result != null) ctx.Register(source, destType, result);
                return result;
            };
    }
    private Expression BuildMemberConversion(Expression value, Type destination, AdaptOptions options, string name, bool sensitive, ParameterExpression context)
    {
        var source = value.Type;
        if (TypeUtils.IsSafelyAssignable(source, destination)) return Expression.Convert(value, destination);
        var fallback = Expression.Convert(Expression.Call(Expression.Constant(this), MapMemberMethod, Expression.Convert(value, typeof(object)),
            Expression.Constant(source), Expression.Constant(destination), Expression.Constant(options), Expression.Constant(name), Expression.Constant(sensitive), context), destination);
        // Runtime assignability wins over rules even when a member is declared object
        // or an interface. MapValue also resolves its concrete runtime mapping type.
        if (source == typeof(object) || source.IsInterface || source.IsAbstract) return fallback;
        Expression converted;
        if (TypeConverterRegistry.TryGetEffective(source, destination, out var converter))
        {
            var parameterType = converter!.GetType().GetMethod("Invoke")!.GetParameters()[0].ParameterType;
            converted = Expression.Convert(Expression.Invoke(Expression.Constant(converter), Expression.Convert(value, parameterType)), destination);
        }
        else if (TypeUtils.IsSimple(source) && TypeUtils.IsSimple(destination))
        {
            var actualSource = Nullable.GetUnderlyingType(source) ?? source;
            var actualDestination = Nullable.GetUnderlyingType(destination) ?? destination;
            Expression input = Nullable.GetUnderlyingType(source) != null ? Expression.Property(value, "Value") : value;
            var numericMethod = TypeUtils.IsNumeric(actualSource) && TypeUtils.IsNumeric(actualDestination)
                ? typeof(Convert).GetMethod("To" + actualDestination.Name, new[] { actualSource }) : null;
            converted = numericMethod != null
                ? Expression.Convert(Expression.Call(numericMethod, input), destination)
                : Expression.Convert(Expression.Call(typeof(ScalarConversion), nameof(ScalarConversion.Convert), null,
                    Expression.Convert(input, typeof(object)), Expression.Constant(destination)), destination);
        }
        else return fallback;
        if (!source.IsValueType || Nullable.GetUnderlyingType(source) != null)
            converted = Expression.Condition(Expression.Equal(value, Expression.Default(source)), fallback, converted);
        // A concrete base-class member may already contain the requested derived type.
        // Keep its registered converter inline, but preserve actual-assignment priority.
        if (!source.IsValueType && !source.IsSealed && !destination.IsValueType)
            converted = Expression.Condition(Expression.Call(typeof(TypeUtils), nameof(TypeUtils.IsSafelyAssignableValue), null, Expression.Convert(value, typeof(object)), Expression.Constant(destination)),
                Expression.Convert(Expression.Convert(value, typeof(object)), destination), converted);
        return converted;
    }
    private MappingException MemberFailure(Exception error, Type sourceType, Type destType, string name, bool sensitive, object? value, MappingContext context)
    {
        if (error is MappingException mapped && ReferenceEquals(mapped.Origin, context.Origin)) return mapped;
        context.Push(name, sensitive);
        try { return MappingException.Create(sourceType, destType, context.GetPath(), value, error, _config, context.IsSensitive, context.Origin); }
        finally { context.Pop(); }
    }
    private sealed class ParameterSubstitution(ParameterExpression parameter, Expression replacement) : ExpressionVisitor
    {
        protected override Expression VisitParameter(ParameterExpression node) => node == parameter ? replacement : base.VisitParameter(node);
    }
    private static T CompileLambda<T>(Expression<T> lambda) where T : Delegate
        => RuntimeFeature.IsDynamicCodeSupported ? lambda.Compile() : lambda.Compile(preferInterpretation: true);
        private static Expression? BuildNullSafeNestedAccess(Expression src, Type srcType, string path, bool ignoreCase, bool ignoreUnderscore, out Type? finalType)
        {
            finalType = null;
            Expression currentExpr = src;
            Type currentType = srcType;
            var segments = path.Split('.');
            var locals = new List<ParameterExpression>();
            var steps = new List<Expression>();

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
                var local = Expression.Variable(prop.PropertyType, "nested");
                locals.Add(local);
                steps.Add(Expression.Assign(local, accessExpr));
                currentExpr = local;
                currentType = prop.PropertyType;
            }
            finalType = currentType;
            steps.Add(currentExpr);
            return Expression.Block(locals, steps);
        }

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

}
