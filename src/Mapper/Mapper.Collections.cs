using System.Collections;
using System.Linq.Expressions;
using Ling.Mapper.Models;
using Ling.Mapper.Utils;

namespace Ling.Mapper.Mapper;

internal sealed partial class Mapper
{
    private MappingPlan BuildCollectionPlan(Type sourceType, Type destType, AdaptOptions options, string[]? ignores)
    {
        var elementType = TypeUtils.GetElementType(destType);
        var sourceElementType = TypeUtils.GetElementType(sourceType) ?? typeof(object);
        if (!TypeUtils.IsCollection(sourceType) || !TypeUtils.IsCollection(destType) || elementType == null ||
            (destType.IsArray && destType.GetArrayRank() != 1))
            return (_, _, _) => throw new InvalidCastException("Unsupported collection mapping.");
        var listType = typeof(List<>).MakeGenericType(elementType);
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
        else if (TypeUtils.IsSafelyAssignable(listType, destType)) finish = list => list;
        else
        {
            var enumerableType = typeof(IEnumerable<>).MakeGenericType(elementType);
            var constructor = destType.GetConstructor(new[] { enumerableType });
            if (constructor == null) return (_, _, _) => throw new InvalidCastException("Unsupported destination collection. Register an explicit converter.");
            var list = Expression.Parameter(typeof(IList));
            finish = CompileLambda(Expression.Lambda<Func<IList, object>>(Expression.Convert(Expression.New(constructor, Expression.Convert(list, enumerableType)), typeof(object)), list));
        }
        return (source, callback, context) =>
        {
            if (context.TryGet(source, destType, out var cached, ignores)) return cached;
            var countedSource = source as ICollection;
            var sourceCount = countedSource?.Count;
            if (sourceCount < 0) throw new InvalidOperationException("Collection Count cannot be negative.");
            var canRegisterEarly = TypeUtils.IsSafelyAssignable(listType, destType) || (destType.IsArray && countedSource != null);
            // Pre-registered lists/arrays already resolve back-edges through TryGet.
            // Only constructor-based or uncounted-array destinations need pending detection.
            if (!canRegisterEarly) context.EnterCollection(source, destType);
            try
            {
                var list = createList(destType.IsArray && countedSource != null ? 0 : sourceCount ?? 0);
                // A list can be registered before its children; arrays are allocated up front
                // where a count is available so self-references can resolve correctly.
                Array? array = destType.IsArray && countedSource != null ? createArray!(sourceCount!.Value) : null;
                if (array != null) context.Register(source, destType, array, ignores);
                else if (TypeUtils.IsSafelyAssignable(listType, destType)) context.Register(source, destType, list, ignores);
                var index = 0;
                foreach (var item in (IEnumerable)source)
                {
                    context.Push(index);
                    try
                    {
                        var value = MapValue(item, item?.GetType() ?? sourceElementType, elementType, options, callback, ignores, context);
                        if (array != null) setArrayElement!(array, index, value); else list.Add(value);
                    }
                    finally { context.Pop(); }
                    index++;
                }
                if (array != null && index != array.Length)
                    throw new InvalidOperationException("Collection Count does not match its enumeration length.");
                var result = array ?? finish(list);
                context.Register(source, destType, result, ignores);
                return result;
            }
            finally
            {
                if (!canRegisterEarly) context.ExitCollection(source, destType);
            }
        };
    }
}
