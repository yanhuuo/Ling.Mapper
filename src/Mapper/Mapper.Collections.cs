using System.Collections;
using System.Linq.Expressions;
using Ling.Mapper.Models;
using Ling.Mapper.Utils;

namespace Ling.Mapper.Mapper;

internal sealed partial class Mapper
{
    private MappingPlan BuildCollectionPlan(Type sourceType, Type destType, AdaptOptions options, string[]? ignores)
    {
        var referenceScope = ReferenceSourceScope(sourceType, destType);
        var elementType = TypeUtils.GetElementType(destType);
        var sourceElementType = TypeUtils.GetElementType(sourceType) ?? typeof(object);
        if (!TypeUtils.IsCollection(sourceType) || !TypeUtils.IsCollection(destType) || elementType == null ||
            (destType.IsArray && destType.GetArrayRank() != 1))
            return (_, _, _) => throw new InvalidCastException("Unsupported collection mapping.");
        if (TryBuildKnownMutableCollectionPlan(sourceType, destType, elementType, options, ignores) is { } knownPlan)
            return knownPlan;
        // Only an exact SZArray of immutable atoms can be copied without visiting
        // its elements. Keep declared profile/converter/operator intent on the
        // normal path; this decision belongs to the versioned mapping plan.
        var canCopyAtomicArray = sourceType == destType && destType.IsSZArray &&
            TypeUtils.IsImmutableAtomic(elementType) && !HasDeclaredMappingIntent(elementType, elementType);
        var listType = typeof(List<>).MakeGenericType(elementType);
        var canRegisterList = TypeUtils.IsSafelyAssignable(listType, destType);
        var capacity = Expression.Parameter(typeof(int), "capacity");
        var createList = CompileLambda(Expression.Lambda<Func<int, IList>>(
            Expression.Convert(Expression.New(listType.GetConstructor(new[] { typeof(int) })!, capacity), typeof(IList)), capacity));
        Func<IList, object> finish;
        Func<int, Array>? createArray = null;
        Action<Array, int, object?>? setArrayElement = null;
        if (destType.IsArray)
        {
            var count = Expression.Parameter(typeof(int));
            createArray = CompileLambda(Expression.Lambda<Func<int, Array>>(Expression.Convert(Expression.NewArrayBounds(elementType, count), typeof(Array)), count));
            var array = Expression.Parameter(typeof(Array));
            var index = Expression.Parameter(typeof(int));
            var value = Expression.Parameter(typeof(object));
            setArrayElement = CompileLambda(Expression.Lambda<Action<Array, int, object?>>(Expression.Assign(Expression.ArrayAccess(Expression.Convert(array, destType), index), Expression.Convert(value, elementType)), array, index, value));
            var list = Expression.Parameter(typeof(IList));
            finish = CompileLambda(Expression.Lambda<Func<IList, object>>(Expression.Convert(Expression.Call(Expression.Convert(list, listType), listType.GetMethod("ToArray")!), typeof(object)), list));
        }
        else if (canRegisterList) finish = list => list;
        else
        {
            var enumerableType = typeof(IEnumerable<>).MakeGenericType(elementType);
            var constructor = destType.GetConstructor(new[] { enumerableType });
            if (constructor == null) return (_, _, _) => throw new InvalidCastException("Unsupported destination collection. Register an explicit converter.");
            var list = Expression.Parameter(typeof(IList));
            finish = CompileLambda(Expression.Lambda<Func<IList, object>>(Expression.Convert(Expression.New(constructor, Expression.Convert(list, enumerableType)), typeof(object)), list));
        }
        MappingPlan fallback = (source, callback, context) =>
        {
            if (context.TryGet(source, referenceScope, destType, out var cached, ignores)) return cached;
            // A callback needs the original per-item dispatch and indexed error
            // path. Clone still allocates an empty array and registers aliases;
            // never share the mutable container or bypass projection scopes.
            if (canCopyAtomicArray && callback == null && source.GetType() == destType)
            {
                var copy = ((Array)source).Clone();
                context.Register(source, referenceScope, destType, copy, ignores);
                return copy;
            }
            var countedSource = source as ICollection;
            var sourceCount = countedSource?.Count;
            if (sourceCount < 0) throw new InvalidOperationException("Collection Count cannot be negative.");
            var canRegisterEarly = canRegisterList || (destType.IsArray && countedSource != null);
            // Pre-registered lists/arrays already resolve back-edges through TryGet.
            // Only constructor-based or uncounted-array destinations need pending detection.
            if (!canRegisterEarly) context.EnterCollection(source, referenceScope, destType);
            try
            {
                var list = createList(destType.IsArray && countedSource != null ? 0 : sourceCount ?? 0);
                // A list can be registered before its children; arrays are allocated up front
                // where a count is available so self-references can resolve correctly.
                Array? array = destType.IsArray && countedSource != null ? createArray!(sourceCount!.Value) : null;
                if (array != null) context.Register(source, referenceScope, destType, array, ignores);
                else if (canRegisterList) context.Register(source, referenceScope, destType, list, ignores);
                var index = 0;
                foreach (var item in (IEnumerable)source)
                {
                    context.Push(index);
                    try
                    {
                        // Keep declared nullable/operator and explicit member-rule intent;
                        // boxing or a derived runtime item must not erase that type pair.
                        var value = MapValue(item, sourceElementType, elementType, options, callback, ignores, context);
                        if (array != null) setArrayElement!(array, index, value); else list.Add(value);
                    }
                    finally { context.Pop(); }
                    index++;
                }
                if (array != null && index != array.Length)
                    throw new InvalidOperationException("Collection Count does not match its enumeration length.");
                var result = array ?? finish(list);
                context.Register(source, referenceScope, destType, result, ignores);
                return result;
            }
            finally
            {
                if (!canRegisterEarly) context.ExitCollection(source, referenceScope, destType);
            }
        };
        return canCopyAtomicArray ? fallback : TryBuildTypedCollectionPlan(sourceType, destType, options, ignores, fallback) ?? fallback;
    }
}
