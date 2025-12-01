using System;
using System.Collections.Concurrent;

namespace Ling.Mapper
{
    /// <summary>
    /// Ó³ÉäÆ÷×¢²á±í
    /// </summary>
    public static class MapperRegistry
    {
        private static readonly ConcurrentDictionary<(Type, Type), Delegate> _typed = new();
        private static readonly ConcurrentDictionary<(Type, Type), Func<object, object?>> _wrapped = new();

        public static void Register<TSource, TDestination>(Func<TSource, TDestination> func)
        {
            if (func == null) throw new ArgumentNullException(nameof(func));
            var key = (typeof(TSource), typeof(TDestination));
            _typed[key] = func;
            _wrapped[key] = (object o) => (object?)func((TSource)o);
        }

        public static void Register(Type src, Type dest, Delegate func)
        {
            if (src == null) throw new ArgumentNullException(nameof(src));
            if (dest == null) throw new ArgumentNullException(nameof(dest));
            if (func == null) throw new ArgumentNullException(nameof(func));
            var key = (src, dest);
            _typed[key] = func;
            _wrapped[key] = (object o) => (object?)func.DynamicInvoke(o);
        }

        public static bool TryGet(Type src, Type dest, out Delegate? func)
        {
            return _typed.TryGetValue((src, dest), out func);
        }

        internal static bool TryGetWrapper(Type src, Type dest, out Func<object, object?>? func)
        {
            return _wrapped.TryGetValue((src, dest), out func);
        }
    }
}
