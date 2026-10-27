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
internal sealed partial class Mapper : IMapper, IDefaultMappingOptions
{
    private delegate object? MappingPlan(object source, Action<object, object>? callback, MappingContext context);
    private sealed class PlanEntry(Type source, Type destination, AdaptOptions options)
    {
        internal readonly Type Source = source;
        internal readonly Type Destination = destination;
        internal readonly AdaptOptions Options = options;
        internal MappingPlan Plan = null!;
        internal Func<object, Action<object, object>?, object?>? Root;
    }
    private readonly MapperConfiguration _config;
    private readonly HashSet<(Type Source, Type Destination)> _configuredMaps = new();
    private readonly HashSet<(Type Source, Type Destination)> _configuredMemberMaps = new();
    private readonly ConcurrentDictionary<(Type Source, Type Destination), bool> _configuredAssignmentChecks = new();
    private readonly ConcurrentDictionary<(Type Source, Type Destination), bool> _configuredTypeChecks = new();
    private ConcurrentDictionary<(Type Source, Type Destination), object>? _referenceScopes;
    private sealed class PlanGeneration(long version)
    {
        internal readonly long Version = version;
        internal readonly ConcurrentDictionary<(Type, Type, AdaptOptionsKey), PlanEntry> Plans = new();
        internal readonly ConcurrentDictionary<(Type, Type, AdaptOptionsKey), PlanEntry> UpdatePlans = new();
        internal readonly ConcurrentDictionary<(Type Source, Type Destination), bool> DeclaredIntent = new();
        // A monomorphic hint, not a second plan cache. Alternating shapes replace
        // this reference with already-cached entries; no per-miss wrapper/data.
        internal PlanEntry? LastRoot;
    }
    private PlanGeneration _generation = new(TypeConverterRegistry.Version);
    private static readonly MethodInfo MapMemberMethod = typeof(Mapper).GetMethod(nameof(MapMember), BindingFlags.Instance | BindingFlags.NonPublic)!;

    public Mapper(MapperConfiguration config, bool compileConfiguredMaps = true)
    {
        _config = config ?? throw new ArgumentNullException(nameof(config));
        ValidateOptions(GetDefaultOptions());
        if (config.NullValueHandling is not NullValueHandling.UseDefault and not NullValueHandling.Throw)
            throw new ArgumentOutOfRangeException(nameof(config.NullValueHandling));
        if (config.MaxDiagnosticValueLength < 0)
            throw new ArgumentOutOfRangeException(nameof(config.MaxDiagnosticValueLength));
        // Snapshot supported configuration before compiling. Mutating profiles
        // after CreateMapper is not supported; duplicate pairs use the same winner
        // as BuildObjectPlan, rather than letting a shadowed rule change identity.
        foreach (var pair in config.Configs.GroupBy(item => (item.SourceType, item.DestType)))
        {
            var selected = pair.OrderBy(item => item.IsReverseGenerated).First();
            _configuredMaps.Add(pair.Key);
            var rules = new MappingExpressionBase(selected.ExpressionObject);
            if (rules.ConfiguredPaths.Any() || rules.IgnoredPaths.Any() || rules.ConstructionFactory != null) _configuredMemberMaps.Add(pair.Key);
        }
        if (compileConfiguredMaps)
            foreach (var cfg in config.Configs)
                _ = GetOrCompile(cfg.SourceType, cfg.DestType, GetDefaultOptions(), null);
    }

    internal void ValidateConfiguredPlan(Type source, Type destination) => GetOrCompile(source, destination, GetDefaultOptions(), null);

    public TDestination? Map<TDestination>(object? source) => Map<TDestination>(source, GetDefaultOptions());
    public TDestination? Map<TDestination>(object? source, AdaptOptions options, Action<object, object>? afterMapItem = null, string[]? ignoreNames = null)
    {
        // A boxed value's runtime type and a CLR-instantiated TDestination are
        // already closed/boxable. Only explicit Type arguments need shape checks.
        var result = MapCore(source, source?.GetType() ?? typeof(object), typeof(TDestination), options, afterMapItem, ignoreNames, false);
        return result == null ? default : (TDestination)result;
    }
    public object? Map(object? source, Type sourceType, Type destType) => Map(source, sourceType, destType, GetDefaultOptions());
    public object? Map(object? source, Type sourceType, Type destType, AdaptOptions options, Action<object, object>? afterMapItem = null, string[]? ignoreNames = null)
    {
        ArgumentNullException.ThrowIfNull(sourceType);
        ArgumentNullException.ThrowIfNull(destType);
        MappingTypeValidation.Validate(sourceType, nameof(sourceType));
        MappingTypeValidation.Validate(destType, nameof(destType));
        return MapCore(source, sourceType, destType, options, afterMapItem, ignoreNames, true);
    }

    private object? MapCore(object? source, Type sourceType, Type destType, AdaptOptions options, Action<object, object>? afterMapItem, string[]? ignoreNames, bool validateDeclaredSource)
    {
        ValidateOptions(options);
        if (ignoreNames != null)
        {
            if (ignoreNames.Length == 0) ignoreNames = null;
            else
                foreach (var name in ignoreNames)
                    if (string.IsNullOrWhiteSpace(name))
                        throw new ArgumentException("Ignored property names cannot be null or empty.", nameof(ignoreNames));
        }
        if (validateDeclaredSource && source != null && !TypeUtils.IsSafelyAssignableValue(source, sourceType))
            throw MappingException.Create(sourceType, destType, "$", source,
                new InvalidCastException("Source value does not match the declared source type."), _config);
        if (source == null)
        {
            // Generic destination types are CLR types. Keep explicit Type inputs
            // on MapValue's diagnostic path, including user-defined Type metadata.
            if (!validateDeclaredSource && (!destType.IsValueType ||
                (destType.IsGenericType && destType.GetGenericTypeDefinition() == typeof(Nullable<>))))
                return null;
        }
        // The generic API already supplied the actual runtime source type. The
        // explicit Type API still requires exactness before using this shortcut.
        else if (!validateDeclaredSource || sourceType == source.GetType())
        {
            var generation = CurrentGeneration();
            if (ignoreNames == null)
            {
                var root = System.Threading.Volatile.Read(ref generation.LastRoot);
                if (root != null && root.Source == sourceType && root.Destination == destType && root.Options == options)
                    return root.Root!(source, afterMapItem);
            }
            // A cached eligible root has already proved this guard. Cache misses
            // retain polymorphic-clone resolution; children and MapInto never use
            // the hint, and ignored-member requests use their complete plan key.
            if (sourceType == destType || !destType.IsAssignableFrom(sourceType))
            {
                PlanEntry entry;
                try { entry = GetPlanEntry(generation, sourceType, destType, options, ignoreNames); }
                catch (Exception error) { throw RootFailure(error, sourceType, destType, source, null); }
                if (entry.Root != null)
                {
                    if (ignoreNames == null) System.Threading.Volatile.Write(ref generation.LastRoot, entry);
                    return entry.Root(source, afterMapItem);
                }
                return MapValue(source, sourceType, destType, options, afterMapItem, ignoreNames, new MappingContext(), entry.Plan);
            }
        }
        return MapValue(source, sourceType, destType, options, afterMapItem, ignoreNames, new MappingContext());
    }

    private static void ValidateOptions(AdaptOptions options)
    {
        const AdaptOptions supported = AdaptOptions.IgnoreCase | AdaptOptions.IgnoreUnderscore | AdaptOptions.IgnoreNullValues;
        if ((options & ~supported) != 0)
            throw new ArgumentException("Mapping options contain unsupported flags.", nameof(options));
    }

    private object? MapValue(object? source, Type sourceType, Type destType, AdaptOptions options, Action<object, object>? callback, string[]? ignores, MappingContext context, MappingPlan? precompiled = null)
    {
        try
        {
            if (source == null)
            {
                if (!destType.IsValueType || Nullable.GetUnderlyingType(destType) != null) return null;
                if (_config.NullValueHandling == NullValueHandling.Throw) throw new InvalidCastException();
                return DefaultValues.GetOrAdd(destType, t => CompileLambda(Expression.Lambda<Func<object>>(Expression.Convert(Expression.Default(t), typeof(object)))))();
            }
            var requestedDestination = destType;
            sourceType = ResolveEffectiveSourceType(source, sourceType, destType);
            // An unconfigured assignable reference is not an identity mapping:
            // clone its actual concrete shape, preserving derived state and
            // object/interface views of the same node within the result graph.
            var actualSource = source.GetType();
            if (destType != actualSource && TypeUtils.IsSafelyAssignable(actualSource, destType) &&
                !TypeUtils.IsImmutableAtomic(actualSource) &&
                !HasDeclaredMappingIntent(sourceType, destType))
                destType = actualSource;
            var result = context.IsUpdating && context.TryGet(source, ReferenceSourceScope(sourceType, destType), destType, out var updated, ignores)
                ? updated : (precompiled ?? GetOrCompile(sourceType, destType, options, ignores))(source,
                    callback != null && !TypeUtils.IsCollection(requestedDestination) && TypeUtils.IsCollection(destType) ? null : callback, context);
            if (callback != null && result != null && !(TypeUtils.IsCollection(sourceType) && TypeUtils.IsCollection(requestedDestination)))
                callback?.Invoke(result, source);
            return result;
        }
        catch (MappingException error) when (ReferenceEquals(error.Origin, context.Origin)) { throw; }
        catch (Exception error) { throw MappingException.Create(sourceType, destType, context.GetPath(), source, error, _config, context.IsSensitive, context.Origin); }
    }
    private Type ResolveEffectiveSourceType(object value, Type declaredSource, Type destination)
    {
        var actual = value.GetType();
        // Preserve explicit declared conversion intent, including nullable
        // operators. Otherwise all views of one source use its complete runtime
        // shape, not a traversal-order-dependent base-class projection.
        if (actual == declaredSource || Nullable.GetUnderlyingType(declaredSource) != null ||
            HasDeclaredMappingIntent(declaredSource, destination))
            return declaredSource;
        return actual;
    }

    private static readonly ConcurrentDictionary<Type, Func<object>> DefaultValues = new();

    private object? MapMember(object? value, Type sourceType, Type destType, AdaptOptions options, string name, bool sensitive, string[]? ignoreNames, MappingContext context)
    {
        context.Push(name, sensitive);
        try { return MapValue(value, sourceType, destType, options, null, ignoreNames, context); }
        finally { context.Pop(); }
    }

    private PlanGeneration CurrentGeneration()
    {
        var generation = System.Threading.Volatile.Read(ref _generation);
        var version = TypeConverterRegistry.Version;
        if (generation.Version != version)
        {
            var replacement = new PlanGeneration(version);
            var observed = System.Threading.Interlocked.CompareExchange(ref _generation, replacement, generation);
            generation = ReferenceEquals(observed, generation) ? replacement : observed;
        }
        return generation;
    }

    private MappingPlan GetOrCompile(Type sourceType, Type destType, AdaptOptions options, string[]? ignores, bool updating = false)
        => GetPlanEntry(sourceType, destType, options, ignores, updating).Plan;

    private PlanEntry GetPlanEntry(Type sourceType, Type destType, AdaptOptions options, string[]? ignores, bool updating = false)
        => GetPlanEntry(CurrentGeneration(), sourceType, destType, options, ignores, updating);

    private PlanEntry GetPlanEntry(PlanGeneration generation, Type sourceType, Type destType, AdaptOptions options, string[]? ignores, bool updating = false)
    {
        var key = (sourceType, destType, new AdaptOptionsKey(options, ignores));
        if ((updating ? generation.UpdatePlans : generation.Plans).TryGetValue(key, out var cached)) return cached;
        return CompileMissingPlan(generation, sourceType, destType, options, ignores, updating);
    }

    // Keep captured locals out of GetOrCompile entirely: a capturing lambda in its
    // miss branch would still allocate a display class on every successful cache hit.
    private PlanEntry CompileMissingPlan(PlanGeneration generation, Type sourceType, Type destType, AdaptOptions options, string[]? ignores, bool updating)
    {
        // Capture only on misses. Retired generations remain alive only for in-flight calls.
        var snapshot = ignores?.ToArray();
        var key = (sourceType, destType, new AdaptOptionsKey(options, snapshot));
        return (updating ? generation.UpdatePlans : generation.Plans).GetOrAdd(key, _ =>
        {
            var entry = new PlanEntry(sourceType, destType, options);
            entry.Plan = updating ? BuildUpdatePlan(sourceType, destType, options, snapshot) : BuildPlan(sourceType, destType, options, snapshot, entry);
            return entry;
        });
    }

    private bool HasConfiguredMembers(Type source, Type destination) => _configuredMemberMaps.Contains(
        (Nullable.GetUnderlyingType(source) ?? source, Nullable.GetUnderlyingType(destination) ?? destination));

    private bool HasConfiguredMap(Type source, Type destination) => _configuredMaps.Contains(
        (Nullable.GetUnderlyingType(source) ?? source, Nullable.GetUnderlyingType(destination) ?? destination));

    private bool HasConfiguredMapOrItems(Type source, Type destination)
    {
        if (_configuredMaps.Count == 0) return false;
        return _configuredTypeChecks.GetOrAdd((source, destination), static (pair, mapper) =>
            mapper.RequiresConfiguredMappingCore(pair.Source, pair.Destination, new HashSet<(Type, Type)>(), false), this);
    }

    // Container covariance must not discard an explicit element/key/value
    // conversion before MapValue sees the declared item type. Cache this alongside
    // plans so a registry replacement also retires earlier false intent results.
    private bool HasDeclaredMappingIntent(Type source, Type destination) =>
        CurrentGeneration().DeclaredIntent.GetOrAdd((source, destination), static (pair, mapper) =>
            mapper.HasDeclaredMappingIntentCore(pair.Source, pair.Destination, new HashSet<(Type, Type)>()), this);

    private bool HasDeclaredMappingIntentCore(Type source, Type destination, HashSet<(Type, Type)> visited)
    {
        if (!visited.Add((source, destination))) return false;
        if (HasConfiguredMapOrItems(source, destination) ||
            TypeConverterRegistry.TryGetEffective(source, destination, out _) ||
            UserDefinedConversion.Find(source, destination) != null) return true;
        if (TypeUtils.IsDictionary(source) && TypeUtils.IsDictionary(destination))
        {
            var from = DictionaryShape(source)?.GetGenericArguments();
            var to = DictionaryShape(destination)?.GetGenericArguments();
            return from != null && to != null &&
                (HasDeclaredMappingIntentCore(from[0], to[0], visited) || HasDeclaredMappingIntentCore(from[1], to[1], visited));
        }
        if (!TypeUtils.IsCollection(source) || !TypeUtils.IsCollection(destination)) return false;
        Type? fromElement; Type? toElement;
        try { fromElement = TypeUtils.GetElementType(source); toElement = TypeUtils.GetElementType(destination); }
        catch (NotSupportedException) { return false; } // The collection builder reports unsupported shapes.
        return fromElement != null && toElement != null &&
            HasDeclaredMappingIntentCore(fromElement, toElement, visited);
    }

    // A stable token carries the complete effective source-plan/destination pair.
    // Keep each per-object reference-cache entry at two references, rather than
    // copying the same source/destination metadata into every graph entry.
    // Ordinary plans share the destination Type token to preserve base/derived
    // back-edges; explicit source plans receive distinct, non-Type tokens.
    private object ReferenceSourceScope(Type source, Type destination) =>
        HasDeclaredMappingIntent(source, destination)
            ? System.Threading.LazyInitializer.EnsureInitialized(ref _referenceScopes, static () => new())
                .GetOrAdd((source, destination), static _ => new object())
            : destination;

    private bool CanAssignDirectly(Type source, Type destination) =>
        TypeUtils.IsImmutableAtomic(source) && TypeUtils.IsSafelyAssignable(source, destination) && !RequiresConfiguredMapping(source, destination);

    private static bool UsesRegisteredConverter(Type source, Type destination) =>
        TypeConverterRegistry.TryGetEffective(source, destination, out _);

    private bool RequiresConfiguredMapping(Type source, Type destination)
    {
        if (_configuredMemberMaps.Count == 0) return false;
        return _configuredAssignmentChecks.GetOrAdd((source, destination), static (pair, mapper) =>
            mapper.RequiresConfiguredMappingCore(pair.Source, pair.Destination, new HashSet<(Type, Type)>()), this);
    }

    private bool RequiresConfiguredMappingCore(Type source, Type destination, HashSet<(Type, Type)> visited, bool membersOnly = true)
    {
        if (membersOnly ? HasConfiguredMembers(source, destination) : HasConfiguredMap(source, destination)) return true;
        if (!visited.Add((source, destination))) return false;
        // Same-type collections must still run explicitly configured item rules.
        // This check describes frozen Profile intent, not mutable graph identity.
        if (TypeUtils.IsDictionary(source) && TypeUtils.IsDictionary(destination))
        {
            var sourceShape = DictionaryShape(source); var destinationShape = DictionaryShape(destination);
            if (sourceShape == null || destinationShape == null) return false;
            var sourceArguments = sourceShape.GetGenericArguments(); var destinationArguments = destinationShape.GetGenericArguments();
            return RequiresConfiguredMappingCore(sourceArguments[0], destinationArguments[0], visited, membersOnly) ||
                RequiresConfiguredMappingCore(sourceArguments[1], destinationArguments[1], visited, membersOnly);
        }
        if (!TypeUtils.IsCollection(source) || !TypeUtils.IsCollection(destination)) return false;
        Type? sourceElement; Type? destinationElement;
        try { sourceElement = TypeUtils.GetElementType(source); destinationElement = TypeUtils.GetElementType(destination); }
        catch (NotSupportedException)
        {
            // An unrelated profile must not invalidate an otherwise legal identity
            // assignment for a collection with no unambiguous generic item shape.
            return false;
        }
        return sourceElement != null && destinationElement != null &&
            RequiresConfiguredMappingCore(sourceElement, destinationElement, visited, membersOnly);
    }

    private MappingPlan BuildPlan(Type sourceType, Type destType, AdaptOptions options, string[]? ignores, PlanEntry? entry = null)
    {
        if (CanAssignDirectly(sourceType, destType) && (ignores == null || ignores.Length == 0 || TypeUtils.IsSimple(destType))) return (value, callback, _) =>
        {
            if (callback != null && TypeUtils.IsCollection(sourceType) && TypeUtils.IsCollection(destType))
                foreach (var item in (System.Collections.IEnumerable)value)
                    if (item != null) callback(item, item);
            return value;
        };
        if (TypeConverterRegistry.TryGetInvoker(sourceType, destType, out var convert))
        {
            if (ignores is { Length: > 0 })
                return (_, _, _) => throw new NotSupportedException("Member ignore projections cannot be applied inside an opaque type converter. Configure the converter or map the members explicitly.");
            return (value, _, _) => convert!(value);
        }
        // Explicit object-map configuration owns its member resolution. An
        // automatically discovered operator must not bypass ForMember/Rename/Ignore.
        var configuredObjectMap = HasConfiguredMap(sourceType, destType);
        if (!configuredObjectMap && UserDefinedConversion.Find(sourceType, destType) is { } conversionOperator)
        {
            if (ignores is { Length: > 0 })
                return (_, _, _) => throw new NotSupportedException("Member ignore projections cannot be applied inside a user-defined conversion. Configure an explicit member mapping instead.");
            var value = Expression.Parameter(typeof(object), "source");
            // Call directly, not MethodInfo.Invoke: preserve the operator's actual
            // exception type and keep it outside custom source-expression null handling.
            return CompileLambda(Expression.Lambda<MappingPlan>(Expression.Convert(
                Expression.Call(conversionOperator, Expression.Convert(value, conversionOperator.GetParameters()[0].ParameterType)), typeof(object)),
                value, Expression.Parameter(typeof(Action<object, object>)), Expression.Parameter(typeof(MappingContext))));
        }
        if (typeof(Delegate).IsAssignableFrom(destType) || typeof(IDisposable).IsAssignableFrom(destType) || typeof(IAsyncDisposable).IsAssignableFrom(destType) ||
            typeof(System.Threading.Tasks.Task).IsAssignableFrom(destType))
            throw new NotSupportedException("Resource/delegate objects cannot be automatically constructed or cloned. Configure an explicit converter.");
        if (sourceType == destType && !TypeUtils.IsImmutableAtomic(sourceType) &&
            sourceType.GetFields(BindingFlags.Public | BindingFlags.Instance).Any())
            throw new NotSupportedException("Automatic independent copies map public properties, not public instance fields. Configure an explicit converter for field-bearing types.");
        // KeyValuePair has read-only members: treating dictionaries as ordinary
        // collections would silently produce default keys and values.
        if (TypeUtils.IsDictionary(sourceType) || TypeUtils.IsDictionary(destType))
            return BuildDictionaryPlan(sourceType, destType, options, ignores);
        if (Nullable.GetUnderlyingType(destType) is { } nullableDestination && !TypeUtils.IsSimple(destType))
            return (value, _, context) => MapValue(value, Nullable.GetUnderlyingType(sourceType) ?? sourceType, nullableDestination, options, null, ignores, context);
        if (Nullable.GetUnderlyingType(sourceType) is { } nullableSource && !TypeUtils.IsSimple(sourceType))
            return BuildPlan(nullableSource, destType, options, ignores);
        if (TypeUtils.IsSimple(sourceType) || TypeUtils.IsSimple(destType))
            return (value, _, _) => ScalarConversion.Convert(value, destType);
        if (TypeUtils.IsCollection(sourceType) || TypeUtils.IsCollection(destType))
            return BuildCollectionPlan(sourceType, destType, options, ignores);
        if (sourceType == destType && !TypeUtils.IsImmutableAtomic(sourceType))
            ValidateAutomaticCloneShape(sourceType);
        return BuildObjectPlan(sourceType, destType, options, ignores, entry: entry);
    }

    private static void ValidateAutomaticCloneShape(Type type)
    {
        if (typeof(Delegate).IsAssignableFrom(type) || typeof(IDisposable).IsAssignableFrom(type) || typeof(IAsyncDisposable).IsAssignableFrom(type) ||
            typeof(System.Threading.Tasks.Task).IsAssignableFrom(type))
            throw new NotSupportedException("Resource/delegate objects cannot be automatically cloned. Configure an explicit converter.");
        if (type.GetFields(BindingFlags.Public | BindingFlags.Instance).Any())
            throw new NotSupportedException("Automatic independent copies map public properties, not public instance fields. Configure an explicit converter for field-bearing types.");
        if (type != typeof(object) && !type.GetProperties(BindingFlags.Public | BindingFlags.Instance).Any(property =>
            property.GetMethod?.IsPublic == true && property.GetIndexParameters().Length == 0) &&
            type.GetFields(BindingFlags.NonPublic | BindingFlags.Instance).Any())
            throw new NotSupportedException("An object with only private state cannot be safely copied through public-property mapping. Configure an explicit converter.");
    }

    private MappingPlan BuildObjectPlan(Type srcType, Type destType, AdaptOptions options, string[]? ignoreNames, bool updating = false, PlanEntry? entry = null)
    {
        var referenceScope = ReferenceSourceScope(srcType, destType);
        var cfg = _config.Configs.Where(c => c.SourceType == srcType && c.DestType == destType).OrderBy(c => c.IsReverseGenerated).FirstOrDefault();
        var rules = cfg == null ? null : new MappingExpressionBase(cfg.ExpressionObject);
        if (!updating && rules?.ConstructionFactory == null && (destType.IsAbstract || destType.IsInterface))
            return (_, _, _) => _config.StrictMode ? throw new InvalidOperationException("Destination requires a concrete type or ConstructUsing factory.") : null;
        var ignoreCase = options.HasFlag(AdaptOptions.IgnoreCase);
        var ignoreUnderscore = options.HasFlag(AdaptOptions.IgnoreUnderscore);
        var srcObj = Expression.Parameter(typeof(object), "source");
        var callback = Expression.Parameter(typeof(Action<object, object>), "callback");
        var context = Expression.Parameter(typeof(MappingContext), "context");
        var src = Expression.Convert(srcObj, srcType);
        var dest = Expression.Variable(destType, "destination");
        var boundMembers = new HashSet<string>(StringComparer.Ordinal);
        var guardedConstruction = false;
        Expression creation;
        if (updating) creation = Expression.Convert(Expression.Property(context, nameof(MappingContext.ExistingDestination)), destType);
        else if (rules?.ConstructionFactory is { } factory)
        {
            creation = SubstituteSource(factory, src);
            guardedConstruction = true;
        }
        else if ((!destType.IsValueType && destType.GetConstructor(Type.EmptyTypes) != null) ||
                 (destType.IsValueType && destType.GetProperties(BindingFlags.Public | BindingFlags.Instance).Any(property => property.SetMethod?.IsPublic == true)))
            creation = Expression.New(destType);
        else
        {
            try { creation = BuildConstructorExpression(srcType, destType, src, options, ignoreNames, rules, context, out boundMembers); }
            catch (MissingMethodException) when (!_config.StrictMode && cfg == null && !destType.IsValueType && srcType != destType) { return (_, _, _) => null; }
            guardedConstruction = true;
        }
        if (!updating && srcType == destType && rules?.ConstructionFactory == null)
        {
            var unbound = destType.GetProperties(BindingFlags.Public | BindingFlags.Instance).FirstOrDefault(property =>
                property.GetMethod?.IsPublic == true && property.GetIndexParameters().Length == 0 &&
                property.SetMethod?.IsPublic != true && !boundMembers.Contains(property.Name) &&
                rules?.IsIgnored(property.Name) != true && ignoreNames?.Contains(property.Name, StringComparer.Ordinal) != true);
            if (unbound != null)
                throw new NotSupportedException($"Automatic copy cannot preserve read-only member '{unbound.Name}' without constructor binding. Configure ConstructUsing or an explicit converter.");
        }
        var body = new List<Expression> { Expression.Assign(dest, creation) };
        if (!destType.IsValueType && rules?.ConstructionFactory != null && !updating)
            body.Add(Expression.IfThen(Expression.Equal(dest, Expression.Constant(null, destType)),
                Expression.Throw(Expression.New(typeof(InvalidOperationException).GetConstructor(new[] { typeof(string) })!, Expression.Constant("ConstructUsing returned null.")))));
        if (!destType.IsValueType && rules?.ConstructionFactory != null && !updating)
            body.Add(Expression.IfThen(Expression.ReferenceEqual(Expression.Convert(dest, typeof(object)), srcObj),
                Expression.Throw(Expression.New(typeof(InvalidOperationException).GetConstructor(new[] { typeof(string) })!, Expression.Constant("ConstructUsing must return a fresh destination, not the source instance. Use an explicit final converter to retain identity.")))));
        var needsReferenceTracking = updating || guardedConstruction;
        var paths = destType.GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(property => property.SetMethod?.IsPublic == true && !property.SetMethod.IsStatic && property.GetIndexParameters().Length == 0)
            .Select(property => property.Name).Concat(rules?.ConfiguredPaths ?? Enumerable.Empty<string>()).Distinct(StringComparer.Ordinal).ToArray();
        var protectedPaths = (rules?.ConfiguredPaths ?? Enumerable.Empty<string>()).Concat(rules?.IgnoredPaths ?? Enumerable.Empty<string>()).Concat(ignoreNames ?? Array.Empty<string>()).ToArray();
        foreach (var destinationPath in paths)
        {
            if (boundMembers.Contains(destinationPath) ||
                rules?.IgnoredPaths.Any(ignored => destinationPath == ignored || destinationPath.StartsWith(ignored + ".", StringComparison.Ordinal)) == true ||
                ignoreNames?.Any(ignored => destinationPath == ignored || destinationPath.StartsWith(ignored + ".", StringComparison.Ordinal)) == true) continue;
            var property = ResolveDestinationProperty(destType, destinationPath);
            if (updating)
            {
                var ownerType = destType;
                foreach (var segment in destinationPath.Split('.'))
                {
                    var segmentProperty = ownerType.GetProperty(segment, BindingFlags.Public | BindingFlags.Instance)!;
                    if (segmentProperty.SetMethod?.ReturnParameter.GetRequiredCustomModifiers().Contains(typeof(IsExternalInit)) == true)
                        throw new NotSupportedException("Existing-target mapping cannot mutate init-only members or write through init-only paths. Ignore that member or map to a new destination.");
                    ownerType = segmentProperty.PropertyType;
                }
            }
            var childIgnores = protectedPaths.Where(path => path.StartsWith(destinationPath + ".", StringComparison.Ordinal)).Select(path => path.Substring(destinationPath.Length + 1)).ToArray();
            var nestedIgnores = childIgnores.Length == 0 ? null : childIgnores;
            Expression? value;
            Type? valueType;
            var sensitive = MappingException.IsSensitivePath(destinationPath);
            var custom = rules?.GetCustomBinding(destinationPath);
            var rule = rules?.GetMemberOptions(destinationPath);
            if (custom != null)
            {
                value = NullSafeCustomResolution(SubstituteSource(custom, src));
                valueType = custom.ReturnType;
                sensitive |= MappingException.IsSensitivePath(custom.Body.ToString());
            }
            else
            {
                var renamed = rules?.GetRenamedSource(destinationPath);
                var name = renamed ?? destinationPath;
                sensitive |= MappingException.IsSensitivePath(name);
                value = TryBuildSourceAccess(src, srcType, name, ignoreCase, ignoreUnderscore, renamed == null, out valueType);
                if (value == null)
                {
                    if (renamed != null) throw new NotSupportedException("The configured source path does not exist or is not publicly readable.");
                    continue;
                }
            }
            needsReferenceTracking |= (nestedIgnores != null || !CanAssignDirectly(valueType!, property.PropertyType)) &&
                !TypeUtils.IsSimple(valueType!) && !TypeUtils.IsSimple(property.PropertyType) && !UsesRegisteredConverter(valueType!, property.PropertyType);
            var temporary = Expression.Variable(valueType!, "value");
            var candidate = Expression.Variable(property.PropertyType, "mappedValue");
            Expression converted = rule?.FinalConverter is { } finalConverter
                ? BuildFinalMemberConversion(temporary, finalConverter, property.PropertyType, options, destinationPath, sensitive, context, nestedIgnores)
                : BuildMemberConversion(temporary, property.PropertyType, options, destinationPath, sensitive, context, nestedIgnores, !updating);
            if (updating && custom == null && rule?.IsFinalConverter != true && rule?.Condition == null && !property.PropertyType.IsValueType && !TypeUtils.IsSimple(property.PropertyType))
            {
                var current = BuildNullSafeNestedAccess(dest, destType, destinationPath, false, false, out _);
                if (current != null)
                    converted = Expression.Convert(Expression.Call(Expression.Constant(this), nameof(MapMemberInto), null,
                        Expression.Convert(temporary, typeof(object)), Expression.Constant(valueType!, typeof(Type)), Expression.Constant(property.PropertyType),
                        Expression.Convert(current, typeof(object)), Expression.Constant(options), Expression.Constant(destinationPath), Expression.Constant(sensitive),
                        Expression.Constant(nestedIgnores, typeof(string[])), context), property.PropertyType);
            }
            var nullableSource = !valueType!.IsValueType || Nullable.GetUnderlyingType(valueType) != null;
            if (nullableSource && rule?.HasNullSubstitute == true)
            {
                if (rule.NullSubstitute != null && !TypeUtils.IsSafelyAssignableValue(rule.NullSubstitute, property.PropertyType))
                    throw new NotSupportedException("NullSubstitute must be assignable to its destination member.");
                Expression substitute = rule.NullSubstitute == null ? Expression.Default(property.PropertyType) :
                    BuildMemberConversion(Expression.Convert(Expression.Constant(rule.NullSubstitute), property.PropertyType),
                        property.PropertyType, options, destinationPath, sensitive, context, nestedIgnores, !updating);
                converted = Expression.Condition(Expression.Equal(temporary, Expression.Default(valueType)), substitute, converted);
            }
            Expression assignment = BuildDestinationAssignment(dest, destinationPath, candidate);
            if (rule?.Condition is { } condition)
            {
                var test = new ParameterSubstitution(condition.Parameters[0], src).Visit(condition.Body)!;
                test = new ParameterSubstitution(condition.Parameters[1], dest).Visit(test)!;
                test = new ParameterSubstitution(condition.Parameters[2], Expression.Convert(candidate, condition.Parameters[2].Type)).Visit(test)!;
                assignment = Expression.IfThen(test, Expression.Block(assignment, Expression.Empty()));
            }
            Expression operation = Expression.Block(Expression.Assign(candidate, converted), assignment, Expression.Empty());
            if (nullableSource && rule?.HasNullSubstitute != true && (rule?.IgnoreNullValues ?? options.HasFlag(AdaptOptions.IgnoreNullValues)))
                operation = Expression.IfThen(Expression.NotEqual(temporary, Expression.Default(valueType)), operation);
            var error = Expression.Parameter(typeof(Exception), "error");
            var fail = Expression.Call(Expression.Constant(this), nameof(MemberFailure), null, error,
                Expression.Constant(valueType, typeof(Type)), Expression.Constant(property.PropertyType), Expression.Constant(destinationPath),
                Expression.Constant(sensitive), Expression.Convert(temporary, typeof(object)), context);
            var readFailure = Expression.Call(Expression.Constant(this), nameof(MemberFailure), null, error,
                Expression.Constant(custom != null ? srcType : valueType, typeof(Type)), Expression.Constant(property.PropertyType),
                Expression.Constant(destinationPath), Expression.Constant(sensitive), Expression.Constant(null, typeof(object)), context);
            var read = Expression.TryCatch(Expression.Assign(temporary, value), Expression.Catch(error, Expression.Throw(readFailure, valueType)));
            Expression member = Expression.Block(new[] { temporary, candidate }, read,
                Expression.TryCatch(operation, Expression.Catch(error, Expression.Throw(fail))));
            if (rule?.PreCondition is { } precondition)
            {
                var test = new ParameterSubstitution(precondition.Parameters[0], src).Visit(precondition.Body)!;
                test = Expression.TryCatch(test, Expression.Catch(error, Expression.Throw(readFailure, typeof(bool))));
                member = Expression.IfThen(test, member);
            }
            body.Add(member);
        }
        body.Add(Expression.Convert(dest, typeof(object)));
        if (needsReferenceTracking && !destType.IsValueType)
            body.Insert(!destType.IsValueType && rules?.ConstructionFactory != null && !updating ? 3 : 1,
                Expression.Call(context, nameof(MappingContext.Register), null, srcObj, Expression.Constant(referenceScope), Expression.Constant(destType), Expression.Convert(dest, typeof(object)), Expression.Constant(ignoreNames, typeof(string[]))));
        var objectBody = Expression.Block(new[] { dest }, body);
        if (entry != null && !needsReferenceTracking && !guardedConstruction && !updating && !destType.IsValueType)
            entry.Root = TryCompileRootPlan(objectBody, srcObj, callback, context, srcType, destType);
        var compiled = CompileLambda(Expression.Lambda<MappingPlan>(objectBody, srcObj, callback, context));
        if (destType.IsValueType && (needsReferenceTracking || guardedConstruction))
        {
            // Never cache a boxed copy of the still-default struct local. Build
            // once, then cache and return that exact completed box. A back-edge
            // before completion is rejected by the constructor/pending guard.
            var valueCompiled = compiled;
            MappingPlan completeValue = (source, cb, ctx) =>
            {
                var result = valueCompiled(source, cb, ctx);
                if (result != null) ctx.Register(source, referenceScope, destType, result, ignoreNames);
                return result;
            };
            return GuardConstructorPlan(completeValue, srcType, destType, ignoreNames);
        }
        if (guardedConstruction) return GuardConstructorPlan(compiled, srcType, destType, ignoreNames);
        return needsReferenceTracking
            ? (source, cb, ctx) => ctx.TryGet(source, referenceScope, destType, out var existing, ignoreNames) ? existing : compiled(source, cb, ctx)
            : (source, cb, ctx) =>
            {
                if (ctx.TryGet(source, referenceScope, destType, out var existing, ignoreNames)) return existing;
                var result = compiled(source, cb, ctx);
                if (ctx.HasReferences && result != null) ctx.Register(source, referenceScope, destType, result, ignoreNames);
                return result;
            };
    }

    private static Expression SubstituteSource(LambdaExpression expression, Expression source)
    {
        var result = new ParameterSubstitution(expression.Parameters[0], source).Visit(expression.Body)!;
        if (expression.Parameters.Count == 2)
            result = new ParameterSubstitution(expression.Parameters[1], Expression.Property(null, typeof(MappingCallContext), nameof(MappingCallContext.Current))).Visit(result)!;
        return result;
    }
    private Expression BuildFinalMemberConversion(Expression value, LambdaExpression converter, Type destination,
        AdaptOptions options, string name, bool sensitive, ParameterExpression context, string[]? ignoreNames)
    {
        if (!TypeUtils.IsSafelyAssignable(value.Type, converter.Parameters[0].Type))
            throw new NotSupportedException($"Final converter input does not match the selected source type at '{name}'. Select a compatible source with MapFrom; implicit input coercion is not performed.");
        if (!TypeUtils.IsSafelyAssignable(converter.ReturnType, destination))
            throw new NotSupportedException($"Final converter output does not match the destination type at '{name}'.");
        Expression final = Expression.Convert(SubstituteSource(converter, Expression.Convert(value, converter.Parameters[0].Type)), destination);
        if (!value.Type.IsValueType || Nullable.GetUnderlyingType(value.Type) != null)
            final = Expression.Condition(Expression.Equal(value, Expression.Default(value.Type)),
                BuildMemberConversion(value, destination, options, name, sensitive, context, ignoreNames, false), final);
        return final;
    }

    private Expression BuildMemberConversion(Expression value, Type destination, AdaptOptions options, string name, bool sensitive, ParameterExpression context, string[]? ignoreNames = null, bool allowCachedPlan = true)
    {
        // Freeze before intent discovery; user code can change the registry before
        // this member executes. The guarded helper must then use normal dispatch.
        var memberVersion = TypeConverterRegistry.Version;
        var source = value.Type;
        // Ordinary selectors are source selection, not final conversion. Only
        // immutable scalar values may be assigned directly; mutable values map.
        if (ignoreNames == null && CanAssignDirectly(source, destination)) return Expression.Convert(value, destination);
        Expression fallback = Expression.Convert(Expression.Call(Expression.Constant(this), MapMemberMethod, Expression.Convert(value, typeof(object)),
            Expression.Constant(source), Expression.Constant(destination), Expression.Constant(options), Expression.Constant(name), Expression.Constant(sensitive), Expression.Constant(ignoreNames, typeof(string[])), context), destination);
        // MapValue returns immediately for null -> reference/nullable, before
        // resolving runtime types, converters, reference tracking or callbacks.
        // Members/constructor arguments are already resolved into locals; keep
        // this shortcut inside conversion so rule ordering and setter/ctor error
        // handling stay unchanged. Never invoke a user-defined equality operator.
        Expression? isNull = !source.IsValueType
            ? Expression.ReferenceEqual(value, Expression.Default(source))
            : Nullable.GetUnderlyingType(source) != null
                ? Expression.Not(Expression.Property(value, "HasValue")) : null;
        Expression nullResult = fallback;
        if (isNull != null && (!destination.IsValueType || Nullable.GetUnderlyingType(destination) != null))
        {
            nullResult = Expression.Default(destination);
            fallback = Expression.Condition(isNull, nullResult, fallback);
        }
        // Runtime assignability wins over rules even when a member is declared object
        // or an interface. MapValue also resolves its concrete runtime mapping type.
        if (ignoreNames != null || source == typeof(object) || source.IsInterface || source.IsAbstract ||
            (TypeUtils.IsSafelyAssignable(source, destination) && RequiresConfiguredMapping(source, destination))) return fallback;
        Expression converted;
        if (TypeConverterRegistry.TryGetEffective(source, destination, out var converter))
        {
            var parameterType = converter!.GetType().GetMethod("Invoke")!.GetParameters()[0].ParameterType;
            converted = Expression.Convert(Expression.Invoke(Expression.Constant(converter), Expression.Convert(value, parameterType)), destination);
        }
        else if (TypeUtils.IsSimple(source) && TypeUtils.IsSimple(destination))
        {
            var actualDestination = Nullable.GetUnderlyingType(destination) ?? destination;
            Expression input = Nullable.GetUnderlyingType(source) != null ? Expression.Property(value, "Value") : value;
            var scalar = ScalarConversionExpression.TryBuild(input, actualDestination);
            converted = scalar != null
                ? Expression.Convert(scalar, destination)
                : Expression.Convert(Expression.Call(typeof(ScalarConversion), nameof(ScalarConversion.Convert), null,
                    Expression.Convert(input, typeof(object)), Expression.Constant(destination)), destination);
        }
        else
        {
            if (allowCachedPlan && ignoreNames == null && !source.IsValueType && !destination.IsValueType &&
                source != typeof(object) && destination != typeof(object) &&
                !source.IsInterface && !source.IsAbstract && !destination.IsInterface && !destination.IsAbstract &&
                !TypeUtils.IsSimple(source) && !TypeUtils.IsSimple(destination) &&
                (source == destination || !TypeUtils.IsSafelyAssignable(source, destination)))
            {
                // Store code only. Eager child compilation here would recurse for
                // self-referential types; execution resolves the full graph plan lazily.
                fallback = Expression.Convert(Expression.Call(Expression.Constant(this), nameof(MapKnownMember), null,
                    Expression.Convert(value, typeof(object)), Expression.Constant(source), Expression.Constant(destination),
                    Expression.Constant(options), Expression.Constant(name), Expression.Constant(sensitive), context,
                    Expression.Constant(memberVersion), Expression.Constant(new MemberPlanSlot())), destination);
                // Retain the existing null shortcut outside the guarded child plan.
                return Expression.Condition(isNull!, Expression.Default(destination), fallback);
            }
            return fallback;
        }
        if (isNull != null)
        {
            // For the inline conversion path, preserve MapValue's non-nullable
            // null policy without graph dispatch. Complex fallback plans retain
            // the narrower reference/nullable shortcut above.
            if (destination.IsValueType && Nullable.GetUnderlyingType(destination) == null)
                nullResult = _config.NullValueHandling == NullValueHandling.Throw
                    ? Expression.Throw(Expression.New(typeof(InvalidCastException)), destination)
                    : Expression.Default(destination);
            converted = Expression.Condition(isNull, nullResult, converted);
        }
        // A concrete base-class member may already contain the requested derived type.
        // Keep its registered converter inline, but preserve actual-assignment priority.
        if (!source.IsValueType && !source.IsSealed && !destination.IsValueType)
            converted = Expression.Condition(Expression.Call(typeof(TypeUtils), nameof(TypeUtils.IsSafelyAssignableValue), null, Expression.Convert(value, typeof(object)), Expression.Constant(destination)),
                fallback, converted);
        return converted;
    }
    private MappingException MemberFailure(Exception error, Type sourceType, Type destType, string name, bool sensitive, object? value, MappingContext context)
    {
        if (error is MappingException mapped && ReferenceEquals(mapped.Origin, context.Origin)) return mapped;
        context.Push(name, sensitive);
        try { return MappingException.Create(sourceType, destType, context.GetPath(), value, error, _config, context.IsSensitive, context.Origin); }
        finally { context.Pop(); }
    }
    // Only expression resolution has this compatibility contract. Never wrap
    // conversion, constructor invocation, destination setters, or callbacks here.
    private static Expression NullSafeCustomResolution(Expression value) => Expression.TryCatch(value,
        Expression.Catch(typeof(NullReferenceException), Expression.Default(value.Type)),
        Expression.Catch(typeof(ArgumentNullException), Expression.Default(value.Type)));

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
                var readable = BuildSourcePropertyMap(currentType.GetProperties(BindingFlags.Public | BindingFlags.Instance)
                    .Where(p => p.GetMethod?.IsPublic == true && !p.GetMethod.IsStatic && p.GetIndexParameters().Length == 0).ToArray(), ignoreCase, ignoreUnderscore);
                readable.TryGetValue(normalized, out var prop);
                var underlying = Nullable.GetUnderlyingType(currentType);
                Expression owner = currentExpr;
                if (prop == null && underlying != null)
                {
                    readable = BuildSourcePropertyMap(underlying.GetProperties(BindingFlags.Public | BindingFlags.Instance)
                        .Where(p => p.GetMethod?.IsPublic == true && !p.GetMethod.IsStatic && p.GetIndexParameters().Length == 0).ToArray(), ignoreCase, ignoreUnderscore);
                    readable.TryGetValue(normalized, out prop);
                    owner = Expression.Property(currentExpr, "Value");
                }
                if (prop == null) return null;

                Expression accessExpr = Expression.Property(owner, prop);
                if (underlying != null)
                    accessExpr = Expression.Condition(Expression.Property(currentExpr, "HasValue"), accessExpr, Expression.Default(prop.PropertyType));
                else if (!currentType.IsValueType)
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
            foreach (var p in props)
                if (!map.TryAdd(NormalizeNameStatic(p.Name, ignoreCase, ignoreUnderscore), p))
                    throw new NotSupportedException("Source property names are ambiguous under the selected naming options.");
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

        public AdaptOptions DefaultOptions => GetDefaultOptions();
        private AdaptOptions GetDefaultOptions() => _config.EffectiveDefaultOptions;

}
