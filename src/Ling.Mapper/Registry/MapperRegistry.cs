using System;
using System.Collections.Concurrent;

namespace Ling.Mapper
{
    /// <summary>
    /// 映射器注册表，用于手动注册高性能的映射委托。
    /// 支持注册强类型委托和包装后的 object 委托。
    /// </summary>
    public static class MapperRegistry
    {
        private static readonly ConcurrentDictionary<(Type, Type), Delegate> _typed = new();
        private static readonly ConcurrentDictionary<(Type, Type), Func<object, object?>> _wrapped = new();

        /// <summary>
        /// 注册强类型映射委托。
        /// </summary>
        /// <typeparam name="TSource">源类型</typeparam>
        /// <typeparam name="TDestination">目标类型</typeparam>
        /// <param name="func">映射函数</param>
        /// <exception cref="ArgumentNullException">func 为 null 时抛出</exception>
        public static void Register<TSource, TDestination>(Func<TSource, TDestination> func)
        {
            if (func == null) throw new ArgumentNullException(nameof(func));
            var key = (typeof(TSource), typeof(TDestination));
            _typed[key] = func;
            _wrapped[key] = (object o) => (object?)func((TSource)o);
        }

        /// <summary>
        /// 注册类型映射委托（非泛型版本）。
        /// </summary>
        /// <param name="src">源类型</param>
        /// <param name="dest">目标类型</param>
        /// <param name="func">映射委托</param>
        /// <exception cref="ArgumentNullException">参数为 null 时抛出</exception>
        public static void Register(Type src, Type dest, Delegate func)
        {
            if (src == null) throw new ArgumentNullException(nameof(src));
            if (dest == null) throw new ArgumentNullException(nameof(dest));
            if (func == null) throw new ArgumentNullException(nameof(func));
            var key = (src, dest);
            _typed[key] = func;
            _wrapped[key] = (object o) => (object?)func.DynamicInvoke(o);
        }

        /// <summary>
        /// 尝试获取已注册的强类型映射委托。
        /// </summary>
        /// <param name="src">源类型</param>
        /// <param name="dest">目标类型</param>
        /// <param name="func">输出的映射委托</param>
        /// <returns>找到则返回 true，否则返回 false</returns>
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
