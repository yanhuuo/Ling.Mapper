using System.Collections.Generic;

namespace Ling.Mapper
{
    /// <summary>
    /// 集合类型转换的辅助扩展方法
    /// </summary>
    public static class CollectionAdaptExtensions
    {
        /// <summary>
        /// 将集合直接转换为 List，支持 page.Data.AdaptToList&lt;CustomerDto, Customer&gt;() 语法
        /// </summary>
        /// <typeparam name="TDestination">目标元素类型</typeparam>
        /// <typeparam name="TSource">源元素类型</typeparam>
        /// <param name="source">源集合</param>
        /// <param name="custom">可选的回调函数，用于对整个列表进行处理</param>
        /// <returns>映射后的目标 List 集合</returns>
        /// <remarks>
        /// <para>此扩展方法使 Adapt 支持直接进行 List 转换，无需使用 AdaptList 方法。</para>
        /// <example>
        /// 示例 1：直接转换 List
        /// <code>
        /// var customerDtos = page.Data.AdaptToList&lt;CustomerDto, Customer&gt;();
        /// </code>
        /// </example>
        /// <example>
        /// 示例 2：转换并处理整个列表
        /// <code>
        /// var customerDtos = page.Data.AdaptToList&lt;CustomerDto, Customer&gt;((list, source) =>
        /// {
        ///     for (int i = 0; i &lt; list.Count; i++)
        ///     {
        ///         list[i].RowNumber = i + 1;
        ///     }
        /// });
        /// </code>
        /// </example>
        /// </remarks>
        /// <exception cref="System.InvalidOperationException">未注册全局 Mapper</exception>
        public static List<TDestination>? AdaptToList<TDestination, TSource>(
            this IEnumerable<TSource>? source,
            System.Action<List<TDestination>?, IEnumerable<TSource>>? custom = null)
        {
            if (source == null) return null;

            var mapper = MapperProvider.Current ?? throw new System.InvalidOperationException("没有注册默认的 mapper，请先调 MapperProvider.SetCurrent(mapper) 或使用带 IMapper 参数的重载。");
            
            var result = new List<TDestination>();
            
            foreach (var item in source)
            {
                var dest = mapper.Map<TDestination>(item);
                if (dest != null)
                {
                    result.Add(dest);
                }
            }

            custom?.Invoke(result, source);
            return result;
        }

        /// <summary>
        /// 带 IMapper 参数的集合转换方法
        /// </summary>
        /// <typeparam name="TDestination">目标元素类型</typeparam>
        /// <typeparam name="TSource">源元素类型</typeparam>
        /// <param name="source">源集合</param>
        /// <param name="mapper">指定的 IMapper 实例</param>
        /// <param name="custom">可选的回调函数，用于对整个列表进行处理</param>
        /// <returns>映射后的目标 List 集合</returns>
        /// <remarks>
        /// <para>此方法使用指定的 IMapper 实例进行映射，适用于需要使用非全局 Mapper 的场景。</para>
        /// <example>
        /// 示例：使用指定的 Mapper
        /// <code>
        /// var customMapper = new MapperConfiguration().CreateMapper();
        /// var customerDtos = page.Data.AdaptToList&lt;CustomerDto, Customer&gt;(customMapper, (list, source) =>
        /// {
        ///     list.ForEach(c => c.DisplayName = FormatName(c));
        /// });
        /// </code>
        /// </example>
        /// </remarks>
        public static List<TDestination>? AdaptToList<TDestination, TSource>(
            this IEnumerable<TSource>? source,
            IMapper mapper,
            System.Action<List<TDestination>?, IEnumerable<TSource>>? custom = null)
        {
            if (source == null) return null;
            
            var result = new List<TDestination>();
            
            foreach (var item in source)
            {
                var dest = mapper.Map<TDestination>(item);
                if (dest != null)
                {
                    result.Add(dest);
                }
            }

            custom?.Invoke(result, source);
            return result;
        }
    }
}
