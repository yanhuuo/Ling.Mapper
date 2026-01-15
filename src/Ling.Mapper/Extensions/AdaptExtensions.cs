using System;
using System.Collections;
using Ling.Mapper.Models;

namespace Ling.Mapper.Extensions
{
    /// <summary>
    /// Adapt 扩展方法（最终稳定版）
    ///
    /// 设计原则：
    /// 1. 泛型顺序永远是 Adapt<TDestination>(...) 或 Adapt<TDestination, TSource>(...)
    /// 2. 强类型回调 (TDestination, TSource) 与弱类型回调 (TDestination?, object) 明确分离
    /// 3. 集合后处理只作用于“整个集合”，不做元素级反射
    /// 4. Mapper 只负责映射，所有回调语义都在 Extensions 层
    /// </summary>
    public static class AdaptExtensions
    {
        /* ============================================================
         * 一、最基础入口（无回调）
         * ============================================================ */

        public static TDestination? Adapt<TDestination>(this object source)
            => Adapt<TDestination>(
                source,
                MapperProvider.Current,
                AdaptOptions.Default,
                null);

        public static TDestination? Adapt<TDestination>(
            this object source,
            AdaptOptions options)
            => Adapt<TDestination>(
                source,
                MapperProvider.Current,
                options,
                null);

        /* ============================================================
         * 二、弱类型回调（用于“整个对象 / 整个集合”的后处理）
         * ============================================================ */

        public static TDestination? Adapt<TDestination>(
            this object source,
            Action<TDestination?, object>? afterMap)
            => Adapt<TDestination>(
                source,
                MapperProvider.Current,
                AdaptOptions.Default,
                afterMap);

        public static TDestination? Adapt<TDestination>(
            this object source,
            AdaptOptions options,
            Action<TDestination?, object>? afterMap)
            => Adapt<TDestination>(
                source,
                MapperProvider.Current,
                options,
                afterMap);

        /* ============================================================
         * 三、强类型回调（你最关心的：TDestination, TSource）
         * ============================================================ */

        public static TDestination? Adapt<TDestination, TSource>(
            this TSource source,
            Action<TDestination, TSource> afterMap)
        {
            return Adapt<TDestination, TSource>(
                source,
                MapperProvider.Current,
                AdaptOptions.Default,
                afterMap);
        }

        public static TDestination? Adapt<TDestination, TSource>(
            this TSource source,
            AdaptOptions options,
            Action<TDestination, TSource> afterMap)
        {
            return Adapt<TDestination, TSource>(
                source,
                MapperProvider.Current,
                options,
                afterMap);
        }

        /* ============================================================
         * 四、internal 强类型核心（不走 object/_，语义绝对正确）
         * ============================================================ */

        internal static TDestination? Adapt<TDestination, TSource>(
            this TSource source,
            IMapper mapper,
            AdaptOptions options,
            Action<TDestination, TSource>? afterMap)
        {
            if (source == null)
                return default;

            var destType = typeof(TDestination);

            // 集合：直接交给 Mapper（集合级后处理请用弱类型回调）
            if (destType != typeof(string) &&
                typeof(IEnumerable).IsAssignableFrom(destType) &&
                source is IEnumerable)
            {
                var result = mapper.Map(
                    source!,
                    typeof(TSource),
                    destType,
                    options);

                return (TDestination?)result;
            }

            var dest = mapper.Map<TDestination>(source!, options);

            if (dest != null && afterMap != null)
                afterMap(dest, source);

            return dest;
        }

        /* ============================================================
         * 五、internal 弱类型核心（唯一实现点）
         * ============================================================ */

        internal static TDestination? Adapt<TDestination>(
            this object source,
            IMapper mapper,
            AdaptOptions options,
            Action<TDestination?, object>? afterMap)
        {
            if (source == null)
                return default;

            var destType = typeof(TDestination);

            // 集合：一次性映射，再对“整个结果”回调
            if (destType != typeof(string) &&
                typeof(IEnumerable).IsAssignableFrom(destType) &&
                source is IEnumerable)
            {
                var result = mapper.Map(
                    source,
                    source.GetType(),
                    destType,
                    options);

                var casted = (TDestination?)result;
                afterMap?.Invoke(casted, source);
                return casted;
            }

            var dest = mapper.Map<TDestination>(source, options);
            afterMap?.Invoke(dest, source);
            return dest;
        }
    }
}
