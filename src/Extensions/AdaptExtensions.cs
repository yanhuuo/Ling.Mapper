using System;
using System.Collections;
using Ling.Mapper.Models;
using Ling.Mapper.Provider;

namespace Ling.Mapper.Extensions
{
    /// <summary>
    /// Adapt 扩展方法集合。
    /// 修复了匿名对象属性推导、接口实例化、Lambda 二义性等所有已知问题。
    /// </summary>
    public static class AdaptExtensions
    {
        /* ============================================================
         * 一、 核心基础入口 (处理基础映射与选项)
         * ============================================================ */

        extension(object? source)
        {
            /// <summary>
            /// 基础映射：user.Adapt&lt;UserDto&gt;()
            /// </summary>
            public TDestination Adapt<TDestination>()
                => source.InternalAdapt<TDestination>(AdaptOptions.Default, null, null);

            /// <summary>
            /// 带选项映射：user.Adapt&lt;UserDto&gt;(AdaptOptions.FlexibleOption)
            /// </summary>
            public TDestination Adapt<TDestination>(AdaptOptions options)
                => source.InternalAdapt<TDestination>(options, null, null);

            /// <summary>
            /// 忽略字段映射
            /// </summary>
            public TDestination Adapt<TDestination>(string firstIgnore, params string[] otherIgnores)
            {
                ArgumentNullException.ThrowIfNull(firstIgnore);
                ArgumentNullException.ThrowIfNull(otherIgnores);
                if (string.IsNullOrWhiteSpace(firstIgnore)) throw new ArgumentException("Ignored property name cannot be empty.", nameof(firstIgnore));
                if (otherIgnores.Any(string.IsNullOrWhiteSpace)) throw new ArgumentException("Ignored property names cannot be null or empty.", nameof(otherIgnores));
                var ignores = new string[otherIgnores.Length + 1];
                ignores[0] = firstIgnore;
                Array.Copy(otherIgnores, 0, ignores, 1, otherIgnores.Length);
                return source.InternalAdapt<TDestination>(AdaptOptions.Default, null, ignores);
            }
        }

        /* ============================================================
         * 二、 核心回调入口 (支持测试 6 的关键)
         * ============================================================ */

        /// <summary>
        /// 匿名函数处理
        /// </summary>
        public static TDestination Adapt<TDestination, TSource>(this TSource source, Action<TDestination, TSource> afterMapItem)
        {
            ArgumentNullException.ThrowIfNull(afterMapItem);
            if (source == null) return source.InternalAdapt<TDestination>(AdaptOptions.Default, null, null);

            var result = MapperProvider.Current.Map<TDestination>(source, AdaptOptions.Default, (d, s) =>
            {
                if (d is TDestination dest && s is TSource src)
                    afterMapItem(dest, src);
            });

            return result ?? CreateFallback<TDestination>()!;
        }

        /// <summary>
        /// 针对【结果级回调】：source.Adapt&lt;List&lt;Target&gt;&gt;(list => { ... })
        /// </summary>
        public static TDestination Adapt<TDestination>(this object? source, Action<TDestination> afterMap)
        {
            ArgumentNullException.ThrowIfNull(afterMap);
            var result = source.InternalAdapt<TDestination>(AdaptOptions.Default, null, null);
            afterMap(result);
            return result;
        }

        /// <summary>
        /// 针对【集合项级回调】：sourceList.Adapt&lt;TargetItem, SourceItem&gt;((d, s) => { ... })
        /// </summary>
        public static List<TTargetItem> Adapt<TTargetItem, TSourceItem>(this IEnumerable<TSourceItem> source, Action<TTargetItem, TSourceItem> afterMapItem)
            where TTargetItem : class, new()
        {
            ArgumentNullException.ThrowIfNull(afterMapItem);
            if (source == null) return new List<TTargetItem>();
            return MapperProvider.Current.Map<List<TTargetItem>>(source, AdaptOptions.Default, (d, s) =>
            {
                if (d is TTargetItem dt && s is TSourceItem sr)
                    afterMapItem(dt, sr);
            }) ?? new List<TTargetItem>();
        }

        /* ============================================================
         * 三、 Internal 内部实现 (核心非空保障与实例化逻辑)
         * ============================================================ */

        internal static TDestination InternalAdapt<TDestination>(
            this object? source,
            AdaptOptions options,
            Action<object, object>? afterMap,
            string[]? ignores)
        {
            // Generic Adapt shares IMapper's null policy. Only the explicitly typed
            // collection-item overload above promises an empty list for a null source.
            if (source == null)
                return MapperProvider.Current.Map<TDestination>(null, options, afterMap, ignores)!;

            var result = MapperProvider.Current.Map<TDestination>(source, options, afterMap, ignores);
            return result ?? CreateFallback<TDestination>()!;
        }

        /// <summary>
        /// 内部 fallback：解决 IList/IEnumerable 等接口无法 new() 的问题
        /// </summary>
        private static T? CreateFallback<T>()
        {
            var type = typeof(T);
            if (type == typeof(string)) return default;

            if (typeof(IEnumerable).IsAssignableFrom(type))
                return (T)CreateEmptyEnumerable(type);

            try
            {
                return (T)Activator.CreateInstance(type)!;
            }
            catch
            {
                // 对应测试 2：如果实在无法实例化（没有无参构造函数），此处会抛出异常
                // 如果你希望由 Mapper 抛出，就在这里 throw 
                throw;
            }
        }

        private static object CreateEmptyEnumerable(Type type)
        {
            if (type.IsArray) return Array.CreateInstance(type.GetElementType() ?? typeof(object), 0);
            if (type.IsInterface || type.IsAbstract)
            {
                var itemType = type.IsGenericType ? type.GetGenericArguments()[0] : typeof(object);
                return Activator.CreateInstance(typeof(List<>).MakeGenericType(itemType))!;
            }
            return Activator.CreateInstance(type)!;
        }
    }
}
