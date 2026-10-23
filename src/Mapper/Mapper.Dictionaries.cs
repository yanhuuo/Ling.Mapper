using System.Collections;
using System.Linq.Expressions;
using System.Reflection;
using Ling.Mapper.Models;

namespace Ling.Mapper.Mapper;

internal sealed partial class Mapper
{
    private static Type? DictionaryShape(Type type) => new[] { type }.Concat(type.GetInterfaces())
        .FirstOrDefault(t => t.IsGenericType && (t.GetGenericTypeDefinition() == typeof(IDictionary<,>) || t.GetGenericTypeDefinition() == typeof(IReadOnlyDictionary<,>)));

    private MappingPlan BuildDictionaryPlan(Type sourceType, Type destination, AdaptOptions options, string[]? ignores)
    {
        var source = DictionaryShape(sourceType);
        var target = DictionaryShape(destination);
        if (source == null || target == null)
            return (_, _, _) => throw new NotSupportedException("Automatic dictionary mapping requires generic dictionary key/value types on both sides.");
        var arguments = source.GetGenericArguments().Concat(target.GetGenericArguments()).ToArray();
        var factory = typeof(Mapper).GetMethod(nameof(BuildDictionaryPlanCore), BindingFlags.Instance | BindingFlags.NonPublic)!.MakeGenericMethod(arguments);
        return ((Func<Type, AdaptOptions, string[]?, MappingPlan>)factory.CreateDelegate(typeof(Func<Type, AdaptOptions, string[]?, MappingPlan>), this))(destination, options, ignores);
    }

    private MappingPlan BuildDictionaryPlanCore<TSourceKey, TSourceValue, TKey, TValue>(Type destination, AdaptOptions options, string[]? ignores) where TKey : notnull
    {
        var mutable = typeof(IDictionary<TKey, TValue>);
        var standard = typeof(Dictionary<TKey, TValue>);
        Func<IDictionary<TKey, TValue>> create;
        Func<IDictionary<TKey, TValue>, object> finish = dictionary => dictionary;
        var registerEarly = true;
        if (destination.IsAssignableFrom(standard)) create = () => new Dictionary<TKey, TValue>();
        else if (mutable.IsAssignableFrom(destination) && destination.GetConstructor(Type.EmptyTypes) is { } empty)
            create = CompileLambda(Expression.Lambda<Func<IDictionary<TKey, TValue>>>(Expression.Convert(Expression.New(empty), mutable)));
        else
        {
            create = () => new Dictionary<TKey, TValue>();
            var constructor = destination.GetConstructor(new[] { mutable }) ?? destination.GetConstructor(new[] { typeof(IEnumerable<KeyValuePair<TKey, TValue>>) });
            if (constructor == null)
                return (_, _, _) => throw new NotSupportedException("Dictionary destination needs a public parameterless or dictionary/enumerable constructor.");
            var input = Expression.Parameter(mutable);
            finish = CompileLambda(Expression.Lambda<Func<IDictionary<TKey, TValue>, object>>(Expression.Convert(Expression.New(constructor, Expression.Convert(input, constructor.GetParameters()[0].ParameterType)), typeof(object)), input));
            registerEarly = false;
        }
        return (source, callback, context) =>
        {
            if (context.TryGet(source, destination, out var cached, ignores)) return cached;
            if (!registerEarly) context.EnterCollection(source, destination);
            try
            {
                var dictionary = create();
                if (registerEarly) context.Register(source, destination, dictionary, ignores);
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
                            if (key is null || dictionary.ContainsKey(key)) throw new ArgumentException("Dictionary key is null or collides after conversion.");
                        }
                        catch (MappingException error) when (ReferenceEquals(error.Origin, context.Origin)) { throw; }
                        catch (Exception error) { throw MappingException.Create(typeof(TSourceKey), typeof(TKey), context.GetPath(), null, error, _config, true, context.Origin); }
                        finally { context.Pop(); }
                        context.Push("Value");
                        TValue value;
                        try { value = (TValue)MapValue(pair.Value, typeof(TSourceValue), typeof(TValue), options, callback, ignores, context)!; }
                        finally { context.Pop(); }
                        context.Push("Key", true);
                        try { dictionary.Add(key, value); }
                        catch (Exception error) { throw MappingException.Create(typeof(TSourceKey), typeof(TKey), context.GetPath(), null, error, _config, true, context.Origin); }
                        finally { context.Pop(); }
                    }
                    finally { context.Pop(); }
                }
                var result = finish(dictionary);
                context.Register(source, destination, result, ignores);
                return result;
            }
            finally { if (!registerEarly) context.ExitCollection(source, destination); }
        };
    }
}
