using System.Collections;
using System.Collections.Concurrent;
using System.Linq.Expressions;
using System.Reflection;
using Ling.Mapper.Models;
using Ling.Mapper.Utils;

namespace Ling.Mapper.Mapper;

internal sealed partial class Mapper
{
    public TDestination MapInto<TDestination>(object? source, TDestination destination) where TDestination : class
        => MapInto(source, destination, GetDefaultOptions());

    public TDestination MapInto<TDestination>(object? source, TDestination destination, AdaptOptions options,
        Action<object, object>? afterMapItem = null, string[]? ignoreNames = null) where TDestination : class
        => (TDestination)MapInto(source, destination, source?.GetType() ?? typeof(object), typeof(TDestination), options, afterMapItem, ignoreNames);

    public object MapInto(object? source, object destination, Type sourceType, Type destinationType)
        => MapInto(source, destination, sourceType, destinationType, GetDefaultOptions());

    public object MapInto(object? source, object destination, Type sourceType, Type destinationType, AdaptOptions options,
        Action<object, object>? afterMapItem = null, string[]? ignoreNames = null)
    {
        ArgumentNullException.ThrowIfNull(destination);
        MappingTypeValidation.Validate(sourceType, nameof(sourceType));
        MappingTypeValidation.Validate(destinationType, nameof(destinationType));
        ValidateOptions(options);
        if (destinationType.IsValueType || TypeUtils.IsSimple(destinationType) || typeof(Delegate).IsAssignableFrom(destinationType))
            throw new NotSupportedException("MapInto requires an existing mutable object or supported mutable collection, not a scalar, value type, or delegate.");
        if (!TypeUtils.IsSafelyAssignableValue(destination, destinationType))
            throw new ArgumentException("Destination instance does not match its declared destination type.", nameof(destination));
        if (ignoreNames?.Any(string.IsNullOrWhiteSpace) == true)
            throw new ArgumentException("Ignored property names cannot be null or empty.", nameof(ignoreNames));
        var ignores = ignoreNames is { Length: > 0 } ? ignoreNames.ToArray() : null;
        if (source != null && !TypeUtils.IsSafelyAssignableValue(source, sourceType))
            throw MappingException.Create(sourceType, destinationType, "$", source, new InvalidCastException("Source value does not match the declared source type."), _config);
        // Null source is a no-op for the explicitly supplied root. Null members
        // still follow the ordinary per-member null/default/IgnoreNull rules.
        if (source == null) return destination;
        var context = new MappingContext { IsUpdating = true };
        return MapIntoValue(source, sourceType, destinationType, destination, options, afterMapItem, ignores, context);
    }

    private object MapIntoValue(object source, Type sourceType, Type destinationType, object destination,
        AdaptOptions options, Action<object, object>? callback, string[]? ignores, MappingContext context)
    {
        try
        {
            sourceType = ResolveEffectiveSourceType(source, sourceType, destinationType);
            if (context.HasReferences && context.TryGet(source, ReferenceSourceScope(sourceType, destinationType), destinationType, out var existing, ignores))
                return existing;
            var previous = context.ExistingDestination;
            context.ExistingDestination = destination;
            object result;
            try { result = GetOrCompile(sourceType, destinationType, options, ignores, updating: true)(source, callback, context)!; }
            finally { context.ExistingDestination = previous; }
            if (!ReferenceEquals(result, destination)) throw new InvalidOperationException("An existing-target plan replaced the supplied destination.");
            if (callback != null && !(TypeUtils.IsCollection(sourceType) && TypeUtils.IsCollection(destinationType))) callback(result, source);
            return result;
        }
        catch (MappingException error) when (ReferenceEquals(error.Origin, context.Origin)) { throw; }
        catch (Exception error) { throw MappingException.Create(sourceType, destinationType, context.GetPath(), source, error, _config, context.IsSensitive, context.Origin); }
    }

    private object? MapMemberInto(object? value, Type sourceType, Type destinationType, object? destination,
        AdaptOptions options, string name, bool sensitive, string[]? ignores, MappingContext context)
    {
        context.Push(name, sensitive);
        try
        {
            // Null resolution precedes converter/operator discovery, including
            // ambiguous user metadata that is irrelevant to an absent value.
            if (value == null) return MapValue(null, sourceType, destinationType, options, null, ignores, context);
            var effectiveSource = ResolveEffectiveSourceType(value, sourceType, destinationType);
            var opaque = TypeConverterRegistry.TryGetEffective(effectiveSource, destinationType, out _) ||
                (!HasConfiguredMap(effectiveSource, destinationType) && UserDefinedConversion.Find(effectiveSource, destinationType) != null);
            if (value != null && destination != null && !ReferenceEquals(value, destination) && !destinationType.IsValueType && !TypeUtils.IsSimple(destinationType) &&
                !TypeUtils.IsSimple(effectiveSource) && !opaque && CanReuseMemberDestination(value, destination, destinationType))
                return MapIntoValue(value, effectiveSource, destinationType, destination, options, null, ignores, context);
            return MapValue(value, sourceType, destinationType, options, null, ignores, context);
        }
        finally { context.Pop(); }
    }

    private static readonly ConcurrentDictionary<Type, Func<object, bool>> ReadOnlyCollectionChecks = new();
    private static readonly ConcurrentDictionary<Type, bool> MutableObjectShapes = new();

    private bool CanReuseMemberDestination(object source, object destination, Type destinationType)
    {
        if (destination is Array array)
            return array.Rank == 1 && array.GetLowerBound(0) == 0 && source is ICollection counted && counted.Count == array.Length;
        if (TypeUtils.IsCollection(destinationType))
        {
            var check = ReadOnlyCollectionChecks.GetOrAdd(destination.GetType(), static type =>
            {
                var shape = type.GetInterfaces().Concat(new[] { type }).FirstOrDefault(candidate => candidate.IsGenericType &&
                    candidate.GetGenericTypeDefinition() == typeof(ICollection<>));
                if (shape == null) return _ => true;
                var value = Expression.Parameter(typeof(object));
                return CompileLambda(Expression.Lambda<Func<object, bool>>(
                    Expression.Property(Expression.Convert(value, shape), "IsReadOnly"), value));
            });
            return !check(destination);
        }
        return MutableObjectShapes.GetOrAdd(destinationType, static type =>
        {
            var properties = type.GetProperties(BindingFlags.Public | BindingFlags.Instance);
            return !properties.Any(property => property.GetMethod?.IsPublic == true) ||
                properties.Any(property => property.SetMethod?.IsPublic == true && !property.SetMethod.ReturnParameter.GetRequiredCustomModifiers()
                    .Contains(typeof(System.Runtime.CompilerServices.IsExternalInit)));
        });
    }

    private MappingPlan BuildUpdatePlan(Type sourceType, Type destinationType, AdaptOptions options, string[]? ignores)
    {
        if (TypeConverterRegistry.TryGetEffective(sourceType, destinationType, out _) ||
            (!HasConfiguredMap(sourceType, destinationType) && UserDefinedConversion.Find(sourceType, destinationType) != null))
            return (_, _, _) => throw new NotSupportedException("A whole-type converter/operator cannot update a supplied destination in place. Use member rules or map to a new result.");
        if (typeof(IDisposable).IsAssignableFrom(destinationType) || typeof(IAsyncDisposable).IsAssignableFrom(destinationType) ||
            typeof(System.Threading.Tasks.Task).IsAssignableFrom(destinationType))
            return (_, _, _) => throw new NotSupportedException("Resource objects are not supported by automatic existing-target mapping.");
        if (TypeUtils.IsSimple(sourceType) || TypeUtils.IsSimple(destinationType))
            return (_, _, _) => throw new NotSupportedException("A scalar conversion cannot update a supplied object in place.");
        if (TypeUtils.IsDictionary(sourceType) || TypeUtils.IsDictionary(destinationType))
            return BuildUpdateDictionaryPlan(sourceType, destinationType, options, ignores);
        if (TypeUtils.IsCollection(sourceType) || TypeUtils.IsCollection(destinationType))
            return BuildUpdateCollectionPlan(sourceType, destinationType, options, ignores);
        if (destinationType.GetProperties(BindingFlags.Public | BindingFlags.Instance).Any(property => property.GetMethod?.IsPublic == true) &&
            !destinationType.GetProperties(BindingFlags.Public | BindingFlags.Instance).Any(property => property.SetMethod?.IsPublic == true && !property.SetMethod.ReturnParameter.GetRequiredCustomModifiers().Contains(typeof(System.Runtime.CompilerServices.IsExternalInit))))
            return (_, _, _) => throw new NotSupportedException("An immutable/read-only destination cannot be updated in place.");
        return BuildObjectPlan(sourceType, destinationType, options, ignores, updating: true);
    }

    private MappingPlan BuildUpdateCollectionPlan(Type sourceType, Type destinationType, AdaptOptions options, string[]? ignores)
    {
        var element = TypeUtils.GetElementType(destinationType);
        if (!TypeUtils.IsCollection(sourceType) || !TypeUtils.IsCollection(destinationType) || element == null ||
            (destinationType.IsArray && destinationType.GetArrayRank() != 1))
            return (_, _, _) => throw new NotSupportedException("Existing collection mapping requires a one-dimensional array or mutable ICollection<T> destination.");
        var factory = typeof(Mapper).GetMethod(nameof(BuildUpdateCollectionCore), BindingFlags.Instance | BindingFlags.NonPublic)!.MakeGenericMethod(element);
        return ((Func<Type, Type, AdaptOptions, string[]?, MappingPlan>)factory.CreateDelegate(typeof(Func<Type, Type, AdaptOptions, string[]?, MappingPlan>), this))(sourceType, destinationType, options, ignores);
    }

    private MappingPlan BuildUpdateCollectionCore<T>(Type sourceType, Type destinationType, AdaptOptions options, string[]? ignores)
    {
        var sourceElement = TypeUtils.GetElementType(sourceType) ?? typeof(object);
        var referenceScope = ReferenceSourceScope(sourceType, destinationType);
        return (source, callback, context) =>
        {
            var destination = context.ExistingDestination!;
            if (destination is not ICollection<T> collection || (collection.IsReadOnly && destination is not T[]))
                throw new NotSupportedException("The supplied collection is not a mutable ICollection<T>.");
            if (destination is Array arrayShape && (arrayShape.Rank != 1 || arrayShape.GetLowerBound(0) != 0))
                throw new NotSupportedException("Existing arrays must be zero-based and one-dimensional.");
            context.Register(source, referenceScope, destinationType, destination, ignores);
            var mapped = new List<T>();
            var index = 0;
            foreach (var item in (IEnumerable)source)
            {
                context.Push(index++);
                try
                {
                    mapped.Add((T)MapValue(item, sourceElement, typeof(T), options, callback, ignores, context)!);
                }
                finally { context.Pop(); }
            }
            // Stage before mutating: source and destination may be the very same
            // collection. Element failures/count mismatch leave its contents intact.
            if (destination is T[] array)
            {
                if (mapped.Count != array.Length) throw new InvalidOperationException("Existing array length must equal the mapped source count.");
                var actualElement = array.GetType().GetElementType()!;
                if (mapped.Any(item => item != null && !TypeUtils.IsSafelyAssignableValue(item, actualElement)))
                    throw new ArrayTypeMismatchException("Mapped element does not fit the supplied runtime array type.");
                mapped.CopyTo(array);
            }
            else
            {
                collection.Clear();
                foreach (var item in mapped) collection.Add(item);
            }
            return destination;
        };
    }

    private MappingPlan BuildUpdateDictionaryPlan(Type sourceType, Type destinationType, AdaptOptions options, string[]? ignores)
    {
        var source = DictionaryShape(sourceType); var target = DictionaryShape(destinationType);
        if (source == null || target == null)
            return (_, _, _) => throw new NotSupportedException("Existing dictionary mapping requires generic dictionary key/value types.");
        var factory = typeof(Mapper).GetMethod(nameof(BuildUpdateDictionaryCore), BindingFlags.Instance | BindingFlags.NonPublic)!
            .MakeGenericMethod(source.GetGenericArguments().Concat(target.GetGenericArguments()).ToArray());
        return ((Func<Type, Type, AdaptOptions, string[]?, MappingPlan>)factory.CreateDelegate(typeof(Func<Type, Type, AdaptOptions, string[]?, MappingPlan>), this))(sourceType, destinationType, options, ignores);
    }

    private MappingPlan BuildUpdateDictionaryCore<TSourceKey, TSourceValue, TKey, TValue>(Type sourceType, Type destinationType, AdaptOptions options, string[]? ignores) where TKey : notnull
    {
        var referenceScope = ReferenceSourceScope(sourceType, destinationType);
        return (source, callback, context) =>
        {
            if (context.ExistingDestination is not IDictionary<TKey, TValue> destination || destination.IsReadOnly)
                throw new NotSupportedException("The supplied dictionary is not a mutable IDictionary<TKey,TValue>.");
            context.Register(source, referenceScope, destinationType, destination, ignores);
            IDictionary<TKey, TValue> staged = destination switch
            {
                Dictionary<TKey, TValue> dictionary => new Dictionary<TKey, TValue>(dictionary.Comparer),
                ConcurrentDictionary<TKey, TValue> concurrent => new Dictionary<TKey, TValue>(concurrent.Comparer),
                SortedDictionary<TKey, TValue> sorted => new SortedDictionary<TKey, TValue>(sorted.Comparer),
                SortedList<TKey, TValue> sorted => new SortedDictionary<TKey, TValue>(sorted.Comparer),
                _ => new Dictionary<TKey, TValue>()
            };
            var index = 0;
            foreach (var pair in (IEnumerable<KeyValuePair<TSourceKey, TSourceValue>>)source)
            {
                context.Push(index++);
                try
                {
                    TKey key;
                    context.Push("Key", true);
                    try
                    {
                        key = (TKey)MapValue(pair.Key, typeof(TSourceKey), typeof(TKey), options, null, null, context)!;
                        if (key == null || staged.ContainsKey(key)) throw new ArgumentException("Dictionary key is null or collides after conversion.");
                    }
                    catch (MappingException error) when (ReferenceEquals(error.Origin, context.Origin)) { throw; }
                    catch (Exception error) { throw MappingException.Create(typeof(TSourceKey), typeof(TKey), context.GetPath(), null, error, _config, true, context.Origin); }
                    finally { context.Pop(); }
                    context.Push("Value");
                    TValue value;
                    try { value = (TValue)MapValue(pair.Value, typeof(TSourceValue), typeof(TValue), options, callback, ignores, context)!; }
                    finally { context.Pop(); }
                    staged.Add(key, value);
                }
                finally { context.Pop(); }
            }
            destination.Clear();
            foreach (var pair in staged) destination.Add(pair.Key, pair.Value);
            return destination;
        };
    }
}
