using System.Collections;
using System.Collections.Concurrent;
using System.Collections.ObjectModel;
using System.Reflection;
using Ling.Mapper.Models;
using Ling.Mapper.Utils;

namespace Ling.Mapper.Mapper;

internal sealed partial class Mapper
{
    // Known framework containers expose enough public construction/mutation
    // semantics to preserve comparers, ordering and cycles without copying fields.
    private MappingPlan? TryBuildKnownMutableCollectionPlan(Type sourceType, Type destination, Type element,
        AdaptOptions options, string[]? ignores)
    {
        if (!destination.IsGenericType) return null;
        var definition = destination.GetGenericTypeDefinition();
        if (definition != typeof(HashSet<>) && definition != typeof(SortedSet<>) &&
            definition != typeof(Queue<>) && definition != typeof(Stack<>) &&
            definition != typeof(ConcurrentQueue<>) && definition != typeof(ConcurrentStack<>) &&
            definition != typeof(ReadOnlyCollection<>) && definition != typeof(Collection<>))
            return null;
        var factory = typeof(Mapper).GetMethod(nameof(BuildKnownMutableCollectionPlan), BindingFlags.NonPublic | BindingFlags.Instance)!.MakeGenericMethod(element);
        return ((Func<Type, Type, AdaptOptions, string[]?, MappingPlan>)factory.CreateDelegate(typeof(Func<Type, Type, AdaptOptions, string[]?, MappingPlan>), this))
            (sourceType, destination, options, ignores);
    }

    private MappingPlan BuildKnownMutableCollectionPlan<T>(Type sourceType, Type destination, AdaptOptions options, string[]? ignores)
    {
        var sourceElement = TypeUtils.GetElementType(sourceType) ?? typeof(object);
        var scope = ReferenceSourceScope(sourceType, destination);
        var shape = destination.GetGenericTypeDefinition();
        var stack = shape == typeof(Stack<>) || shape == typeof(ConcurrentStack<>);
        var sourceIsStack = sourceType.IsGenericType && (sourceType.GetGenericTypeDefinition() == typeof(Stack<>) ||
            sourceType.GetGenericTypeDefinition() == typeof(ConcurrentStack<>));
        return (source, callback, context) =>
        {
            if (context.TryGet(source, scope, destination, out var existing, ignores)) return existing;
            var count = source is ICollection counted ? counted.Count : 0;
            if (count < 0) throw new InvalidOperationException("Collection Count cannot be negative.");
            List<T>? backing = null;
            object output;
            if (shape == typeof(HashSet<>))
                output = new HashSet<T>(source is HashSet<T> original ? original.Comparer : null);
            else if (shape == typeof(SortedSet<>))
                output = new SortedSet<T>(source is SortedSet<T> original ? original.Comparer : null);
            else if (shape == typeof(Queue<>)) output = new Queue<T>(count);
            else if (shape == typeof(Stack<>)) output = new Stack<T>(count);
            else if (shape == typeof(ConcurrentQueue<>)) output = new ConcurrentQueue<T>();
            else if (shape == typeof(ConcurrentStack<>)) output = new ConcurrentStack<T>();
            else
            {
                backing = new List<T>(count);
                output = shape == typeof(ReadOnlyCollection<>) ? new ReadOnlyCollection<T>(backing) : new Collection<T>(backing);
            }
            context.Register(source, scope, destination, output, ignores);
            var stackValues = stack ? new List<T>(count) : null;
            var index = 0;
            foreach (var item in (IEnumerable)source)
            {
                context.Push(index++);
                try
                {
                    var mapped = (T)MapValue(item, sourceElement, typeof(T), options, callback, ignores, context)!;
                    if (stackValues != null) stackValues.Add(mapped);
                    else if (backing != null) backing.Add(mapped);
                    else if (output is ICollection<T> collection) collection.Add(mapped);
                    else if (output is Queue<T> queue) queue.Enqueue(mapped);
                    else ((ConcurrentQueue<T>)output).Enqueue(mapped);
                }
                finally { context.Pop(); }
            }
            if (stackValues != null)
            {
                // Copy a stack's pop/enumeration order; ordinary sequence→stack
                // conversion keeps the existing constructor's push-order semantics.
                if (sourceIsStack) stackValues.Reverse();
                foreach (var item in stackValues)
                    if (output is Stack<T> ordinary) ordinary.Push(item);
                    else ((ConcurrentStack<T>)output).Push(item);
            }
            return output;
        };
    }
}
