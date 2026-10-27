using System.Reflection;
using Ling.Mapper.Models;
using Ling.Mapper.Utils;

namespace Ling.Mapper.Mapper;

internal sealed partial class Mapper
{
    private MappingPlan? TryBuildTypedCollectionPlan(Type sourceType, Type destination, AdaptOptions options,
        string[]? ignores, MappingPlan fallback)
    {
        var sourceElement = TypeUtils.GetElementType(sourceType);
        var element = TypeUtils.GetElementType(destination);
        if (sourceElement == null || element == null ||
            !(sourceType.IsSZArray || sourceType == typeof(List<>).MakeGenericType(sourceElement)) ||
            !(destination.IsSZArray || destination.IsAssignableFrom(typeof(List<>).MakeGenericType(element)))) return null;
        var factory = typeof(Mapper).GetMethod(nameof(BuildTypedCollectionPlan), BindingFlags.NonPublic | BindingFlags.Instance)!
            .MakeGenericMethod(sourceElement, element);
        return ((Func<Type, Type, AdaptOptions, string[]?, MappingPlan, MappingPlan>)factory.CreateDelegate(
            typeof(Func<Type, Type, AdaptOptions, string[]?, MappingPlan, MappingPlan>), this))(sourceType, destination, options, ignores, fallback);
    }

    private MappingPlan BuildTypedCollectionPlan<TSource, TDestination>(Type sourceType, Type destination,
        AdaptOptions options, string[]? ignores, MappingPlan fallback)
    {
        var version = TypeConverterRegistry.Version;
        var copy = BuildAtomicIdentity<TSource, TDestination>(ignores);
        var exactPair = typeof(TSource) == typeof(TDestination) ||
            !TypeUtils.IsSafelyAssignable(typeof(TSource), typeof(TDestination)) ||
            TypeUtils.IsImmutableAtomic(typeof(TSource)) || HasDeclaredMappingIntent(typeof(TSource), typeof(TDestination));
        MappingPlan? itemPlan = null;
        var scope = ReferenceSourceScope(sourceType, destination);
        return (source, callback, context) =>
        {
            // Subclasses may reimplement IEnumerable: never bypass their enumerator.
            if (source.GetType() != sourceType) return fallback(source, callback, context);
            if (context.TryGet(source, scope, destination, out var cached, ignores)) return cached;
            var sourceArray = source as TSource[];
            var sourceList = source as List<TSource>;
            var count = sourceArray?.Length ?? sourceList!.Count;
            var array = destination.IsSZArray ? new TDestination[count] : null;
            var list = array == null ? new List<TDestination>(count) : null;
            object output = (object?)array ?? list!;
            context.Register(source, scope, destination, output, ignores);
            // The list's struct enumerator retains mutation checks without boxing.
            var enumerator = sourceList == null ? default : sourceList.GetEnumerator();
            var index = 0;
            try
            {
                while (sourceArray != null ? index < count : enumerator.MoveNext())
                {
                    var item = sourceArray != null ? sourceArray[index] : enumerator.Current;
                    context.Push(index);
                    try
                    {
                        var value = copy != null && callback == null && TypeConverterRegistry.Version == version
                            ? copy(item)
                            : (TDestination)MapKnownCollectionValue(item, typeof(TSource), typeof(TDestination), options,
                                callback, ignores, context, exactPair, version, ref itemPlan)!;
                        if (array != null) array[index] = value; else list!.Add(value);
                    }
                    finally { context.Pop(); }
                    index++;
                }
            }
            finally { enumerator.Dispose(); }
            return output;
        };
    }

    // Only an exact framework dictionary can safely bypass interface enumeration.
    // A subclass can reimplement IEnumerable even though GetEnumerator is nonvirtual.
    // This cursor stays on the stack; neither its struct arm nor Current is boxed.
    private struct DictionaryCursor<TKey, TValue> : IDisposable where TKey : notnull
    {
        private Dictionary<TKey, TValue>.Enumerator _standard;
        private readonly bool _exact;
        private readonly IEnumerator<KeyValuePair<TKey, TValue>>? _custom;
        public DictionaryCursor(object source)
        {
            var exact = _exact = source.GetType() == typeof(Dictionary<TKey, TValue>);
            _standard = exact ? ((Dictionary<TKey, TValue>)source).GetEnumerator() : default;
            _custom = exact ? null : ((IEnumerable<KeyValuePair<TKey, TValue>>)source).GetEnumerator();
        }
        public KeyValuePair<TKey, TValue> Current => _exact ? _standard.Current : _custom!.Current;
        public bool MoveNext() => _exact ? _standard.MoveNext() : _custom!.MoveNext();
        public void Dispose() { if (_exact) _standard.Dispose(); else _custom?.Dispose(); }
    }
}
