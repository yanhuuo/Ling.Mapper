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
    private static readonly ConcurrentDictionary<Type, Func<object?>> Defaults = new();
    private static readonly ConcurrentDictionary<Type, Type> ElementTypes = new();

    /// <summary>Called by generated module initializers. May also register a hand-written AOT-safe mapping.</summary>
    public static void Register<TSource, TDestination>(Func<TSource, AdaptOptions, string[]?, GeneratedMappingContext, TDestination> map)
    {
        ArgumentNullException.ThrowIfNull(map);
        RegisterElements<TSource>();
        RegisterElements<TDestination>();
        Add<TSource, TDestination>((source, options, ignores, context) => map((TSource)source, options, ignores, context));
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
        => SafelyAssignable(source, destination, new HashSet<(Type, Type)>());
    private static bool SafelyAssignable(Type source, Type destination, HashSet<(Type, Type)> visited)
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
    {
        var result = new List<TDestination>();
        context.Remember(source, context.DestinationType, result);
        int index = 0;
        foreach (var item in (IEnumerable<TSource>)source)
        {
            TDestination mapped = default!;
            context.Member(typeof(TSource), typeof(TDestination), "[" + index++ + "]", () => mapped = context.MapItem<TSource, TDestination>(item, options, ignores));
            result.Add(mapped);
        }
        return result;
    }
    private static object Array<TSource, TDestination>(object source, AdaptOptions options, string[]? ignores, GeneratedMappingContext context)
    {
        // Both registered source shapes have a non-reflective Count.
        var input = (ICollection<TSource>)source;
        var result = new TDestination[input.Count];
        context.Remember(source, typeof(TDestination[]), result);
        int index = 0;
        foreach (var item in input)
        {
            int position = index++;
            context.Member(typeof(TSource), typeof(TDestination), "[" + position + "]", () => result[position] = context.MapItem<TSource, TDestination>(item, options, ignores));
        }
        return result;
    }
    internal static bool TryGet(Type source, Type destination, out MapDelegate? map) => Maps.TryGetValue((source, destination), out map);
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
    private sealed class GeneratedMapper(MapperConfiguration configuration) : IMapper, IDefaultMappingOptions
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
    private readonly Dictionary<object, Dictionary<(Type Destination, string Projection), object>> seen = new(System.Collections.Generic.ReferenceEqualityComparer.Instance);
    private string path = "$";
    private string projection = "";
    private bool sensitive;
    private bool invokeItemCallbacks;
    internal Type DestinationType { get; private set; } = typeof(object);
    internal GeneratedMappingContext(MapperConfiguration configuration, Action<object, object>? callback) { this.configuration = configuration; this.callback = callback; }
    /// <summary>Register a destination before mapping children, preserving cycles.</summary>
    public void Remember(object source, Type destinationType, object destination)
    {
        if (!seen.TryGetValue(source, out var destinations)) seen[source] = destinations = new();
        destinations[(destinationType, projection)] = destination;
    }
    /// <summary>Map a declared nested pair using the same root context.</summary>
    public TDestination Map<TSource, TDestination>(TSource source, AdaptOptions options)
        => source is null ? Null<TDestination>(typeof(TSource)) : (TDestination)Dispatch(source, source.GetType(), typeof(TDestination), options, null, false)!;
    internal TDestination MapItem<TSource, TDestination>(TSource source, AdaptOptions options, string[]? ignores)
        => source is null ? Null<TDestination>(typeof(TSource)) : (TDestination)Dispatch(source, source.GetType(), typeof(TDestination), options, ignores, invokeItemCallbacks)!;
    /// <summary>Apply configured null policy without reflection-based default construction.</summary>
    public TDestination Null<TDestination>(Type sourceType)
    {
        if (default(TDestination) is not null && configuration.NullValueHandling == NullValueHandling.Throw)
            throw MappingException.Create(sourceType, typeof(TDestination), path, null, new InvalidCastException(), configuration, sensitive, origin: this);
        return default!;
    }
    internal object? Dispatch(object? source, Type sourceType, Type destinationType, AdaptOptions options, string[]? ignores, bool invokeCallback = true)
    {
        var previousDestination = DestinationType;
        string previousProjection = projection;
        projection = ((int)options).ToString(System.Globalization.CultureInfo.InvariantCulture) + ":" +
            (ignores == null ? "" : string.Concat(ignores.Distinct(StringComparer.Ordinal).OrderBy(name => name, StringComparer.Ordinal).Select(name => name.Length + ":" + name)));
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
            if ((ignores == null || ignores.Length == 0) && GeneratedMapperRegistry.SafelyAssignable(sourceType, destinationType))
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
            if (seen.TryGetValue(source, out var destinations) && destinations.TryGetValue((destinationType, projection), out var cached))
            {
                if (invokeCallback && callback != null && (source is not System.Collections.IEnumerable || source is string)) callback(cached, source);
                return cached;
            }
            if (!GeneratedMapperRegistry.TryGet(sourceType, destinationType, out var map))
                throw new NotSupportedException("No generated mapping pair registered. Declare an assembly GeneratedMap pair; reflection fallback is disabled.");
            var result = map!(source, options, ignores, this);
            if (result != null) Remember(source, destinationType, result);
            if (result != null && invokeCallback && callback != null && (source is not System.Collections.IEnumerable || source is string)) callback(result, source);
            return result;
        }
        catch (MappingException error) when (ReferenceEquals(error.Origin, this)) { throw; }
        catch (Exception error) { throw MappingException.Create(sourceType, destinationType, path, source, error, configuration, sensitive, origin: this); }
        finally { DestinationType = previousDestination; invokeItemCallbacks = previousCallbacks; projection = previousProjection; }
    }
    /// <summary>Wrap getter/conversion/setter failures with a safe full destination path.</summary>
    public void Member(Type sourceType, Type destinationType, string name, Action action, bool sensitiveSource = false)
    {
        string previous = path; bool previousSensitive = sensitive;
        path += name.StartsWith("[", StringComparison.Ordinal) ? name : "." + name;
        sensitive |= sensitiveSource || MappingException.IsSensitivePath(name);
        try { action(); }
        catch (MappingException error) when (ReferenceEquals(error.Origin, this)) { throw; }
        catch (Exception error) { throw MappingException.Create(sourceType, destinationType, path, null, error, configuration, sensitive, origin: this); }
        finally { path = previous; sensitive = previousSensitive; }
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
