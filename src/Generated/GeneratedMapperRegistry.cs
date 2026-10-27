using System.Collections.Concurrent;
using Ling.Mapper.Configuration;
using Ling.Mapper.Mapper;
using Ling.Mapper.Models;
using Ling.Mapper.Utils;

namespace Ling.Mapper;

/// <summary>Reflection-free registration and dispatch for explicitly generated pairs.</summary>
public static class GeneratedMapperRegistry
{
    internal delegate object? MapDelegate(object source, AdaptOptions options, string[]? ignores, GeneratedMappingContext context);
    private static readonly ConcurrentDictionary<(Type, Type), MapDelegate> Maps = new();
    private static readonly ConcurrentDictionary<(Type, Type), byte> DeclaredPairs = new();
    private static readonly ConcurrentDictionary<Type, Func<object?>> Defaults = new();
    private static readonly ConcurrentDictionary<Type, Type> ElementTypes = new();

    /// <summary>Called by generated module initializers. May also register a hand-written AOT-safe mapping.</summary>
    public static void Register<TSource, TDestination>(Func<TSource, AdaptOptions, string[]?, GeneratedMappingContext, TDestination> map)
    {
        ArgumentNullException.ThrowIfNull(map);
        DeclaredPairs[(typeof(TSource), typeof(TDestination))] = 0;
        if (Nullable.GetUnderlyingType(typeof(TSource)) is { } underlyingSource)
            DeclaredPairs[(underlyingSource, typeof(TDestination))] = 0;
        Add<TSource, TDestination>((source, options, ignores, context) => map((TSource)source, options, ignores, context));
        RegisterCollectionShapes<TSource, TDestination>();
    }
    /// <summary>Generated infrastructure: register statically known array/list containers without treating dynamic element objects as identity mappings.</summary>
    public static void RegisterCollectionShapes<TSource, TDestination>()
    {
        RegisterElements<TSource>();
        RegisterElements<TDestination>();
        Add<List<TSource>, List<TDestination>>(List<TSource, TDestination>);
        Add<TSource[], List<TDestination>>(List<TSource, TDestination>);
        Add<List<TSource>, TDestination[]>(Array<TSource, TDestination>);
        Add<TSource[], TDestination[]>(Array<TSource, TDestination>);
        Add<List<TSource>, IEnumerable<TDestination>>(List<TSource, TDestination>);
        Add<TSource[], IEnumerable<TDestination>>(List<TSource, TDestination>);
        Add<List<TSource>, IList<TDestination>>(List<TSource, TDestination>);
        Add<TSource[], IList<TDestination>>(List<TSource, TDestination>);
        Add<List<TSource>, IReadOnlyList<TDestination>>(List<TSource, TDestination>);
        Add<TSource[], IReadOnlyList<TDestination>>(List<TSource, TDestination>);
    }
    private static void RegisterElements<T>()
    {
        ElementTypes[typeof(T[])] = typeof(T);
        ElementTypes[typeof(List<T>)] = typeof(T);
        ElementTypes[typeof(IEnumerable<T>)] = typeof(T);
        ElementTypes[typeof(IList<T>)] = typeof(T);
        ElementTypes[typeof(IReadOnlyList<T>)] = typeof(T);
    }
    internal static bool SafelyAssignable(Type source, Type destination)
        => SafelyAssignable(source, destination, null);
    private static bool SafelyAssignable(Type source, Type destination, HashSet<(Type, Type)>? visited)
    {
        if (source == destination) return true;
        if (!destination.IsAssignableFrom(source)) return false;
        if (source == typeof(string) || destination == typeof(string) ||
            !typeof(System.Collections.IEnumerable).IsAssignableFrom(source) ||
            !typeof(System.Collections.IEnumerable).IsAssignableFrom(destination)) return true;
        Type? sourceItem = source.IsArray ? source.GetElementType() : ElementTypes.TryGetValue(source, out var se) ? se : null;
        Type? destinationItem = destination.IsArray ? destination.GetElementType() : ElementTypes.TryGetValue(destination, out var de) ? de : null;
        // Unknown collection covariance is not guessed through reflection in AOT.
        if (sourceItem == null || destinationItem == null) return false;
        if (sourceItem == destinationItem) return true;
        if (sourceItem.IsValueType || destinationItem.IsValueType) return false;
        visited ??= new HashSet<(Type, Type)>();
        if (!visited.Add((source, destination))) return true;
        return SafelyAssignable(sourceItem, destinationItem, visited);
    }
    private static void Add<TSource, TDestination>(MapDelegate map)
    {
        Maps[(typeof(TSource), typeof(TDestination))] = map;
        // Non-null Nullable<T> boxes as T; registered nullable sources need this alias.
        var underlyingSource = Nullable.GetUnderlyingType(typeof(TSource));
        if (underlyingSource != null) Maps[(underlyingSource, typeof(TDestination))] = map;
        Defaults[typeof(TDestination)] = () => default(TDestination);
    }
    private static object List<TSource, TDestination>(object source, AdaptOptions options, string[]? ignores, GeneratedMappingContext context)
        => context.CopyList<TSource, TDestination>((IEnumerable<TSource>)source, options, ignores);
    private static object Array<TSource, TDestination>(object source, AdaptOptions options, string[]? ignores, GeneratedMappingContext context)
        => context.CopyArray<TSource, TDestination>((ICollection<TSource>)source, options, ignores);
    internal static bool TryGet(Type source, Type destination, out MapDelegate? map) => Maps.TryGetValue((source, destination), out map);
    internal static bool HasDeclaredPair(Type source, Type destination) => DeclaredPairs.ContainsKey((source, destination));
    internal static object? Default(Type destination) => Defaults.TryGetValue(destination, out var factory) ? factory() :
        throw new NotSupportedException($"No generated destination registered for {destination.FullName}. Declare an assembly GeneratedMap pair.");

    /// <summary>Create an AOT-safe mapper. Runtime Profiles and arbitrary conventions are not interpreted.</summary>
    public static IMapper CreateMapper(MapperConfiguration? configuration = null)
    {
        configuration ??= new MapperConfiguration();
        if (TypeConverterRegistry.Version != 0) throw new NotSupportedException("Generated mapping does not interpret TypeConverterRegistry registrations. Use explicit generated mapping delegates instead.");
        if (configuration.HasExplicitConventions) throw new NotSupportedException("Generated mapping does not interpret arbitrary runtime conventions. Use explicit AdaptOptions instead.");
        if (configuration.Configs.Any()) throw new NotSupportedException("Generated mappings do not interpret runtime Profiles. Declare GeneratedMap pairs instead.");
        if (configuration.NullValueHandling != NullValueHandling.UseDefault && configuration.NullValueHandling != NullValueHandling.Throw)
            throw new ArgumentOutOfRangeException(nameof(configuration.NullValueHandling));
        if (configuration.MaxDiagnosticValueLength < 0) throw new ArgumentOutOfRangeException(nameof(configuration.MaxDiagnosticValueLength));
        return new GeneratedMapper(configuration);
    }
    private sealed class GeneratedMapper(MapperConfiguration configuration) : IMapper, IDefaultMappingOptions, Ling.Mapper.Extensions.IRuntimeContextUnsupported
    {
        public AdaptOptions DefaultOptions => configuration.EffectiveDefaultOptions;
        public TDestination? Map<TDestination>(object? source) => Map<TDestination>(source, configuration.EffectiveDefaultOptions);
        public TDestination? Map<TDestination>(object? source, AdaptOptions options, Action<object, object>? afterMapItem = null, string[]? ignoreNames = null)
        {
            Validate(options, ignoreNames);
            var context = new GeneratedMappingContext(configuration, afterMapItem);
            if (source == null) return context.Null<TDestination>(typeof(object));
            return (TDestination?)context.Dispatch(source, source.GetType(), typeof(TDestination), options, ignoreNames);
        }
        public object? Map(object? source, Type sourceType, Type destType) => Map(source, sourceType, destType, configuration.EffectiveDefaultOptions);
        public object? Map(object? source, Type sourceType, Type destType, AdaptOptions options, Action<object, object>? afterMapItem = null, string[]? ignoreNames = null)
        {
            MappingTypeValidation.Validate(sourceType, nameof(sourceType));
            MappingTypeValidation.Validate(destType, nameof(destType));
            Validate(options, ignoreNames);
            if (source != null && !GeneratedMapperRegistry.SafelyAssignable(source.GetType(), sourceType)) throw MappingException.Create(sourceType, destType, "$", source, new InvalidCastException("Source value does not match the declared source type."), configuration);
            return new GeneratedMappingContext(configuration, afterMapItem).Dispatch(source, source?.GetType() ?? sourceType, destType, options, ignoreNames);
        }
        private static void Validate(AdaptOptions options, string[]? ignores)
        {
            var callContext = MappingCallContext.Current;
            if (callContext.Parameters.Count != 0 || callContext.Services != null)
                throw new NotSupportedException("Generated mapping does not support per-call parameters or service resolution. Use runtime mapping for context-dependent profiles.");
            if ((options & ~(AdaptOptions.Default | AdaptOptions.IgnoreNullValues)) != 0) throw new ArgumentOutOfRangeException(nameof(options));
            if (ignores?.Any(name => name != null && name.Contains('.')) == true) throw new NotSupportedException("Generated mapping currently supports only top-level ignored names, not dotted paths.");
            if (ignores?.Any(string.IsNullOrWhiteSpace) == true) throw new ArgumentException("Ignored names must be nonempty.", nameof(ignores));
        }
    }
}

/// <summary>Per-root generated mapping context; no reflection construction or dynamic compilation.</summary>
public sealed class GeneratedMappingContext
{
    private readonly MapperConfiguration configuration;
    private readonly Action<object, object>? callback;
    private readonly Dictionary<(object Source, Type Destination, string Projection), object> seen = new(ReferenceProjectionComparer.Instance);
    private static readonly string[] EmptyProjections = { "0:", "1:", "2:", "3:", "4:", "5:", "6:", "7:" };
    private string? memberName;
    private List<string>? parentNames;
    private string path
    {
        get
        {
            var result = new System.Text.StringBuilder("$");
            if (parentNames != null) foreach (var name in parentNames) Append(name);
            if (memberName != null) Append(memberName);
            return result.ToString();
            void Append(string name) { if (!name.StartsWith("[", StringComparison.Ordinal)) result.Append('.'); result.Append(name); }
        }
    }
    private string projection = "";
    private bool sensitive;
    private bool invokeItemCallbacks;
    private HashSet<(object Source, Type Destination, string Projection)>? pendingValues;
    private sealed class ReferenceProjectionComparer : IEqualityComparer<(object Source, Type Destination, string Projection)>
    {
        internal static readonly ReferenceProjectionComparer Instance = new();
        public bool Equals((object Source, Type Destination, string Projection) left, (object Source, Type Destination, string Projection) right)
            => ReferenceEquals(left.Source, right.Source) && left.Destination == right.Destination && left.Projection == right.Projection;
        public int GetHashCode((object Source, Type Destination, string Projection) value)
            => HashCode.Combine(System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(value.Source), value.Destination, value.Projection);
    }
    internal Type DestinationType { get; private set; } = typeof(object);
    internal GeneratedMappingContext(MapperConfiguration configuration, Action<object, object>? callback) { this.configuration = configuration; this.callback = callback; }
    /// <summary>Register a destination before mapping children, preserving cycles.</summary>
    public void Remember(object source, Type destinationType, object destination)
    {
        seen[(source, destinationType, projection)] = destination;
    }
    /// <summary>Map a declared nested pair using the same root context.</summary>
    public TDestination Map<TSource, TDestination>(TSource source, AdaptOptions options)
        => source is null ? Null<TDestination>(typeof(TSource)) : (TDestination)Dispatch(source, source.GetType(), typeof(TDestination), options, null, false)!;
    internal TDestination MapItem<TSource, TDestination>(TSource source, AdaptOptions options, string[]? ignores)
        => source is null ? Null<TDestination>(typeof(TSource)) : (TDestination)Dispatch(source, source.GetType(), typeof(TDestination), options, ignores, invokeItemCallbacks)!;
    /// <summary>Generated infrastructure: create an independent list and preserve repeated references and cycles within this graph.</summary>
    public List<TDestination> CopyList<TSource, TDestination>(IEnumerable<TSource> source, AdaptOptions options, string[]? ignores)
    {
        // Do not introduce Count calls on arbitrary hand-written IEnumerable/ICollection implementations.
        var result = source switch
        {
            TSource[] array => new List<TDestination>(array.Length),
            List<TSource> list => new List<TDestination>(list.Count),
            _ => new List<TDestination>()
        };
        Remember(source, DestinationType, result);
        int index = 0;
        foreach (var item in source)
        {
            TDestination mapped;
            using (EnterMember("[" + index++ + "]", false))
            {
                try { mapped = MapItem<TSource, TDestination>(item, options, ignores); }
                catch (Exception error) when (!OwnsException(error)) { throw MemberException(typeof(TSource), typeof(TDestination), error); }
            }
            result.Add(mapped);
        }
        return result;
    }
    /// <summary>Generated infrastructure: create an independent array using a statically supported counted source.</summary>
    public TDestination[] CopyArray<TSource, TDestination>(ICollection<TSource> source, AdaptOptions options, string[]? ignores)
    {
        var result = new TDestination[source.Count];
        Remember(source, DestinationType, result);
        int index = 0;
        foreach (var item in source)
        {
            int position = index++;
            using var scope = EnterMember("[" + position + "]", false);
            try { result[position] = MapItem<TSource, TDestination>(item, options, ignores); }
            catch (Exception error) when (!OwnsException(error)) { throw MemberException(typeof(TSource), typeof(TDestination), error); }
        }
        return result;
    }
    /// <summary>Apply configured null policy without reflection-based default construction.</summary>
    public TDestination Null<TDestination>(Type sourceType)
    {
        if (default(TDestination) is not null && configuration.NullValueHandling == NullValueHandling.Throw)
            throw MappingException.Create(sourceType, typeof(TDestination), path, null, new InvalidCastException(), configuration, sensitive, origin: this);
        return default!;
    }
    internal object? Dispatch(object? source, Type sourceType, Type destinationType, AdaptOptions options, string[]? ignores, bool invokeCallback = true)
    {
        // Opaque assignable slots (object/interface/base) clone the actual mutable type
        // unless an explicitly registered pair owns the result. Never return an unknown object as-is.
        if (source != null && !TypeUtils.IsImmutableAtomic(sourceType) &&
            GeneratedMapperRegistry.SafelyAssignable(sourceType, destinationType) &&
            !GeneratedMapperRegistry.HasDeclaredPair(sourceType, destinationType))
            destinationType = sourceType;
        var previousDestination = DestinationType;
        string previousProjection = projection;
        projection = ignores == null || ignores.Length == 0
            ? unchecked((uint)options) < (uint)EmptyProjections.Length ? EmptyProjections[(int)options] : ((int)options).ToString(System.Globalization.CultureInfo.InvariantCulture) + ":"
            : ((int)options).ToString(System.Globalization.CultureInfo.InvariantCulture) + ":" +
                string.Concat(ignores.Distinct(StringComparer.Ordinal).OrderBy(name => name, StringComparer.Ordinal).Select(name => name.Length + ":" + name));
        bool previousCallbacks = invokeItemCallbacks;
        invokeItemCallbacks = invokeCallback;
        DestinationType = destinationType;
        try
        {
            if (TypeConverterRegistry.Version != 0) throw new NotSupportedException("Runtime converter registrations are unsupported by generated mapping.");
            if (source == null)
            {
                var value = GeneratedMapperRegistry.Default(destinationType);
                if (value != null && configuration.NullValueHandling == NullValueHandling.Throw) throw new InvalidCastException();
                return value;
            }
            if ((ignores == null || ignores.Length == 0) && TypeUtils.IsImmutableAtomic(sourceType) && GeneratedMapperRegistry.SafelyAssignable(sourceType, destinationType))
            {
                if (invokeCallback && callback != null)
                {
                    if (source is System.Collections.IEnumerable items && source is not string)
                    {
                        int index = 0;
                        foreach (var item in items)
                        {
                            var current = item;
                            Member(current?.GetType() ?? typeof(object), current?.GetType() ?? typeof(object), "[" + index++ + "]", () => { if (current != null) callback(current, current); });
                        }
                    }
                    else callback(source, source);
                }
                return source;
            }
            if (seen.TryGetValue((source, destinationType, projection), out var cached))
            {
                if (invokeCallback && callback != null && (source is not System.Collections.IEnumerable || source is string)) callback(cached, source);
                return cached;
            }
            if (!GeneratedMapperRegistry.TryGet(sourceType, destinationType, out var map))
                throw new NotSupportedException("No generated mapping pair registered. Declare an assembly GeneratedMap pair; reflection fallback is disabled.");
            object? result;
            if (sourceType.IsValueType || destinationType.IsValueType)
            {
                var key = (source, destinationType, projection);
                if (!(pendingValues ??= new(ReferenceProjectionComparer.Instance)).Add(key))
                    throw new NotSupportedException("A generated value-type graph cannot preserve this boxed circular reference before construction completes.");
                try { result = map!(source, options, ignores, this); }
                finally { pendingValues.Remove(key); }
            }
            else result = map!(source, options, ignores, this);
            if (result != null) Remember(source, destinationType, result);
            if (result != null && invokeCallback && callback != null && (source is not System.Collections.IEnumerable || source is string)) callback(result, source);
            return result;
        }
        catch (MappingException error) when (ReferenceEquals(error.Origin, this)) { throw; }
        catch (Exception error) { throw MappingException.Create(sourceType, destinationType, path, source, error, configuration, sensitive, origin: this); }
        finally { DestinationType = previousDestination; invokeItemCallbacks = previousCallbacks; projection = previousProjection; }
    }
    /// <summary>Generated infrastructure: track a member without allocating a closure or a full path.</summary>
    public MemberScope EnterMember(string name, bool isSensitive)
    {
        var scope = new MemberScope(this, memberName, sensitive);
        if (memberName != null) (parentNames ??= new List<string>()).Add(memberName);
        memberName = name;
        sensitive |= isSensitive;
        return scope;
    }
    /// <summary>Generated infrastructure: preserve exceptions already attributed to this graph.</summary>
    public bool OwnsException(Exception error) => error is MappingException mapping && ReferenceEquals(mapping.Origin, this);
    /// <summary>Generated infrastructure: attribute a getter/conversion/setter failure before leaving its member scope.</summary>
    public MappingException MemberException(Type sourceType, Type destinationType, Exception error)
        => MappingException.Create(sourceType, destinationType, path, null, error, configuration, sensitive, origin: this);
    /// <summary>Value-type generated path scope. Dispose exactly once, in reverse entry order.</summary>
    public readonly struct MemberScope : IDisposable
    {
        private readonly GeneratedMappingContext context;
        private readonly string? previousName;
        private readonly bool previousSensitive;
        internal MemberScope(GeneratedMappingContext context, string? previousName, bool previousSensitive)
        { this.context = context; this.previousName = previousName; this.previousSensitive = previousSensitive; }
        /// <summary>Restore the enclosing member path and sensitivity.</summary>
        public void Dispose()
        {
            if (previousName != null) context.parentNames!.RemoveAt(context.parentNames.Count - 1);
            context.memberName = previousName;
            context.sensitive = previousSensitive;
        }
    }
    /// <summary>Wrap getter/conversion/setter failures with a safe full destination path.</summary>
    public void Member(Type sourceType, Type destinationType, string name, Action action, bool sensitiveSource = false)
    {
        using var scope = EnterMember(name, sensitiveSource || MappingException.IsSensitivePath(name));
        try { action(); }
        catch (Exception error) when (!OwnsException(error)) { throw MemberException(sourceType, destinationType, error); }
    }
    /// <summary>Same scalar semantics as runtime mapping, without object reflection mapping.</summary>
    public TDestination Scalar<TSource, TDestination>(TSource source)
    {
        try { return source is null ? Null<TDestination>(typeof(TSource)) : (TDestination)ScalarConversion.Convert(source, typeof(TDestination)); }
        catch (MappingException error) when (ReferenceEquals(error.Origin, this)) { throw; }
        catch (Exception error) { throw MappingException.Create(typeof(TSource), typeof(TDestination), path, source, error, configuration, sensitive, origin: this); }
    }
    /// <summary>Apply explicit name matching flags; generated exact-name candidates are emitted first.</summary>
    public static bool Matches(string source, string destination, AdaptOptions options)
    {
        if ((options & AdaptOptions.IgnoreUnderscore) != 0) { source = source.Replace("_", ""); destination = destination.Replace("_", ""); }
        return string.Equals(source, destination, (options & AdaptOptions.IgnoreCase) != 0 ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal);
    }
}
