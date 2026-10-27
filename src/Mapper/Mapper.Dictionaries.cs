using System.Collections;
using System.Collections.Concurrent;
using System.Collections.ObjectModel;
using System.Linq.Expressions;
using System.Reflection;
using Ling.Mapper.Models;
using Ling.Mapper.Utils;

namespace Ling.Mapper.Mapper;

internal sealed partial class Mapper
{
    // Pure BCL widening only: shared code, never mapper configuration or graph data.
    private static readonly Func<int, long> WidenDictionaryInt32Key = static value => value;

    private static Type? DictionaryShape(Type type) => new[] { type }.Concat(type.GetInterfaces())
        .FirstOrDefault(t => t.IsGenericType && (t.GetGenericTypeDefinition() == typeof(IDictionary<,>) || t.GetGenericTypeDefinition() == typeof(IReadOnlyDictionary<,>)));

    // A typed delegate avoids the object parameter/return boxing of MapValue.
    // Keep all non-identity and configured cases on the normal diagnostic path.
    private Func<TSource, TDestination>? BuildAtomicIdentity<TSource, TDestination>(string[]? ignores)
    {
        var type = typeof(TSource);
        if (type != typeof(TDestination) || !TypeUtils.IsImmutableAtomic(type) ||
            (ignores is { Length: > 0 } && !TypeUtils.IsSimple(type)) ||
            HasDeclaredMappingIntent(type, type)) return null;
        Func<TSource, TSource> identity = static value => value;
        return (Func<TSource, TDestination>)(object)identity;
    }

    // A collection's exact declared element pair is stable across entries and calls.
    // Resolve lazily inside the Value path: eagerly compiling a recursive value
    // pair while building its containing collection would recurse during build.
    private object? MapKnownCollectionValue(object? source, Type sourceType, Type destination, AdaptOptions options,
        Action<object, object>? callback, string[]? ignores, MappingContext context,
        bool exactPairEligible, long version, ref MappingPlan? cachedPlan)
    {
        if (!exactPairEligible || callback != null || context.IsUpdating || source == null ||
            source.GetType() != sourceType || TypeConverterRegistry.Version != version)
            return MapValue(source, sourceType, destination, options, callback, ignores, context);
        try
        {
            var plan = System.Threading.Volatile.Read(ref cachedPlan);
            if (plan == null)
            {
                plan = GetOrCompile(sourceType, destination, options, ignores);
                System.Threading.Volatile.Write(ref cachedPlan, plan);
            }
            return plan(source, null, context);
        }
        catch (MappingException error) when (ReferenceEquals(error.Origin, context.Origin)) { throw; }
        catch (Exception error) { throw MappingException.Create(sourceType, destination, context.GetPath(), source, error, _config, context.IsSensitive, context.Origin); }
    }

    private MappingPlan BuildDictionaryPlan(Type sourceType, Type destination, AdaptOptions options, string[]? ignores)
    {
        var source = DictionaryShape(sourceType);
        var target = DictionaryShape(destination);
        if (source == null || target == null)
            return (_, _, _) => throw new NotSupportedException("Automatic dictionary mapping requires generic dictionary key/value types on both sides.");
        var arguments = source.GetGenericArguments().Concat(target.GetGenericArguments()).ToArray();
        var factory = typeof(Mapper).GetMethod(nameof(BuildDictionaryPlanCore), BindingFlags.Instance | BindingFlags.NonPublic)!.MakeGenericMethod(arguments);
        return ((Func<Type, Type, AdaptOptions, string[]?, MappingPlan>)factory.CreateDelegate(typeof(Func<Type, Type, AdaptOptions, string[]?, MappingPlan>), this))(sourceType, destination, options, ignores);
    }

    private MappingPlan BuildDictionaryPlanCore<TSourceKey, TSourceValue, TKey, TValue>(Type sourceType, Type destination, AdaptOptions options, string[]? ignores) where TSourceKey : notnull where TKey : notnull
    {
        // Capture before discovering intent. A registry change during plan build,
        // enumeration, or comparer calls must disable these frozen identities.
        var atomicVersion = TypeConverterRegistry.Version;
        var copyKey = BuildAtomicIdentity<TSourceKey, TKey>(null);
        // The frozen delegate remains eligible only while the original per-key
        // registry guard holds. All other key pairs retain their normal dispatch.
        if (typeof(TSourceKey) == typeof(int) && typeof(TKey) == typeof(long) &&
            !HasDeclaredMappingIntent(typeof(TSourceKey), typeof(TKey)))
            copyKey = (Func<TSourceKey, TKey>)(object)WidenDictionaryInt32Key;
        var copyValue = BuildAtomicIdentity<TSourceValue, TValue>(ignores);
        // Exact runtime types need no effective-source resolution. The remaining
        // guard excludes MapValue's unconfigured assignable-target redirection.
        var exactValuePair = typeof(TSourceValue) == typeof(TValue) ||
            !TypeUtils.IsSafelyAssignable(typeof(TSourceValue), typeof(TValue)) ||
            TypeUtils.IsImmutableAtomic(typeof(TSourceValue)) ||
            HasDeclaredMappingIntent(typeof(TSourceValue), typeof(TValue));
        MappingPlan? valuePlan = null;
        var referenceScope = ReferenceSourceScope(sourceType, destination);
        var mutable = typeof(IDictionary<TKey, TValue>);
        var standard = typeof(Dictionary<TKey, TValue>);
        // Comparers are strategy objects, not graph values to clone. Read only known
        // public comparer contracts, and never apply a comparer across key types.
        IEqualityComparer<TKey>? EqualityComparer(object source) => typeof(TSourceKey) != typeof(TKey) ? null : source switch
        {
            Dictionary<TKey, TSourceValue> dictionary => dictionary.Comparer,
            ConcurrentDictionary<TKey, TSourceValue> dictionary => dictionary.Comparer,
            SortedDictionary<TKey, TSourceValue> dictionary => dictionary.Comparer as IEqualityComparer<TKey>,
            SortedList<TKey, TSourceValue> dictionary => dictionary.Comparer as IEqualityComparer<TKey>,
            _ => null
        };
        IComparer<TKey>? OrderingComparer(object source) => typeof(TSourceKey) != typeof(TKey) ? null : source switch
        {
            SortedDictionary<TKey, TSourceValue> dictionary => dictionary.Comparer,
            SortedList<TKey, TSourceValue> dictionary => dictionary.Comparer,
            Dictionary<TKey, TSourceValue> dictionary => dictionary.Comparer as IComparer<TKey>,
            ConcurrentDictionary<TKey, TSourceValue> dictionary => dictionary.Comparer as IComparer<TKey>,
            _ => null
        };
        IDictionary<TKey, TValue> CreateBacking(object source, bool allowSorted)
        {
            // Interface destinations and readonly wrappers can retain a sorted
            // source's ordering without changing a requested concrete hash type.
            if (allowSorted && typeof(TSourceKey) == typeof(TKey) &&
                (source is SortedDictionary<TKey, TSourceValue> || source is SortedList<TKey, TSourceValue>))
                return new SortedDictionary<TKey, TValue>(OrderingComparer(source));
            return new Dictionary<TKey, TValue>(EqualityComparer(source));
        }
        Func<object, IDictionary<TKey, TValue>> create;
        Func<IDictionary<TKey, TValue>, object> finish = dictionary => dictionary;
        var registerEarly = true;
        if (destination.IsAssignableFrom(standard))
            create = source => CreateBacking(source, destination.IsAssignableFrom(typeof(SortedDictionary<TKey, TValue>)));
        else if (destination == typeof(SortedDictionary<TKey, TValue>))
            create = source => new SortedDictionary<TKey, TValue>(OrderingComparer(source));
        else if (destination == typeof(SortedList<TKey, TValue>))
            create = source => new SortedList<TKey, TValue>(OrderingComparer(source));
        else if (destination == typeof(ConcurrentDictionary<TKey, TValue>))
            create = source => new ConcurrentDictionary<TKey, TValue>(EqualityComparer(source) ?? System.Collections.Generic.EqualityComparer<TKey>.Default);
        else if (mutable.IsAssignableFrom(destination) && destination.GetConstructor(Type.EmptyTypes) is { } empty)
        {
            var construct = CompileLambda(Expression.Lambda<Func<IDictionary<TKey, TValue>>>(Expression.Convert(Expression.New(empty), mutable)));
            create = _ => construct();
        }
        else
        {
            create = source => CreateBacking(source, destination == typeof(ReadOnlyDictionary<TKey, TValue>));
            var constructor = destination.GetConstructor(new[] { mutable }) ?? destination.GetConstructor(new[] { typeof(IEnumerable<KeyValuePair<TKey, TValue>>) });
            if (constructor == null)
                return (_, _, _) => throw new NotSupportedException("Dictionary destination needs a public parameterless or dictionary/enumerable constructor.");
            var input = Expression.Parameter(mutable);
            finish = CompileLambda(Expression.Lambda<Func<IDictionary<TKey, TValue>, object>>(Expression.Convert(Expression.New(constructor, Expression.Convert(input, constructor.GetParameters()[0].ParameterType)), typeof(object)), input));
            registerEarly = false;
        }
        return (source, callback, context) =>
        {
            if (context.TryGet(source, referenceScope, destination, out var cached, ignores)) return cached;
            if (!registerEarly) context.EnterCollection(source, referenceScope, destination);
            try
            {
                var dictionary = create(source);
                if (registerEarly) context.Register(source, referenceScope, destination, dictionary, ignores);
                var index = 0;
                var entries = new DictionaryCursor<TSourceKey, TSourceValue>(source);
                try
                {
                    while (entries.MoveNext())
                    {
                        var pair = entries.Current;
                        context.Push(index++);
                        try
                        {
                            TKey key;
                            context.Push("Key", true);
                            try
                            {
                                key = copyKey != null && TypeConverterRegistry.Version == atomicVersion
                                    ? copyKey(pair.Key)
                                    : (TKey)MapValue(pair.Key, typeof(TSourceKey), typeof(TKey), options, null, null, context)!;
                                if (key is null || dictionary.ContainsKey(key)) throw new ArgumentException("Dictionary key is null or collides after conversion.");
                            }
                            catch (MappingException error) when (ReferenceEquals(error.Origin, context.Origin)) { throw; }
                            catch (Exception error) { throw MappingException.Create(typeof(TSourceKey), typeof(TKey), context.GetPath(), null, error, _config, true, context.Origin); }
                            finally { context.Pop(); }
                            context.Push("Value");
                            TValue value;
                            try
                            {
                                // ContainsKey above may run user code that updates the
                                // registry, so recheck immediately before value copying.
                                value = copyValue != null && callback == null && TypeConverterRegistry.Version == atomicVersion
                                    ? copyValue(pair.Value)
                                    : (TValue)MapKnownCollectionValue(pair.Value, typeof(TSourceValue), typeof(TValue), options, callback, ignores, context, exactValuePair, atomicVersion, ref valuePlan)!;
                            }
                            finally { context.Pop(); }
                            context.Push("Key", true);
                            try { dictionary.Add(key, value); }
                            catch (Exception error) { throw MappingException.Create(typeof(TSourceKey), typeof(TKey), context.GetPath(), null, error, _config, true, context.Origin); }
                            finally { context.Pop(); }
                        }
                        finally { context.Pop(); }
                    }
                }
                finally { entries.Dispose(); }
                var result = finish(dictionary);
                context.Register(source, referenceScope, destination, result, ignores);
                return result;
            }
            finally { if (!registerEarly) context.ExitCollection(source, referenceScope, destination); }
        };
    }
}
