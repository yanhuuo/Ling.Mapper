using System;
using System.Collections;
using Ling.Mapper.Models;

namespace Ling.Mapper.Extensions
{
    /// <summary>
    /// Adapt 扩展方法集合（最终稳定版本）
    /// 提供对象到对象的映射功能，支持集合映射、自定义选项和映射后回调处理。
    /// </summary>
    /// <remarks>
    /// <para><b>设计原则：</b></para>
    /// <list type="number">
    /// <item><description>泛型顺序永远是 Adapt&lt;TDestination&gt;(...) 或 Adapt&lt;TDestination, TSource&gt;(...)</description></item>
    /// <item><description>强类型回调 (TDestination, TSource) 与弱类型回调 (TDestination?, object) 明确分离</description></item>
    /// <item><description>集合后处理只作用于整个集合，不做元素级反射</description></item>
    /// <item><description>Mapper 只负责映射，所有回调语义都在 Extensions 层实现</description></item>
    /// </list>
    /// <para><b>使用示例：</b></para>
    /// <code>
    /// // 基本映射
    /// var dto = source.Adapt&lt;TargetDto&gt;();
    /// 
    /// // 使用自定义选项
    /// var dto = source.Adapt&lt;TargetDto&gt;(AdaptOptions.IgnoreCase);
    /// 
    /// // 使用强类型回调
    /// var dto = source.Adapt&lt;TargetDto, SourceEntity&gt;((dest, src) =&gt; {
    ///     dest.FullName = $"{src.FirstName} {src.LastName}";
    /// });
    /// 
    /// // 映射集合
    /// var dtoList = sourceList.Adapt&lt;List&lt;TargetDto&gt;&gt;();
    /// </code>
    /// </remarks>
    public static class AdaptExtensions
    {
        /* ============================================================
         * 一、最基础入口（无回调）
         * ============================================================ */

        /// <param name="source">源对象。</param>
        extension(object source)
        {
            /// <summary>
            /// 将源对象映射为目标类型的实例（使用默认选项）。
            /// </summary>
            /// <typeparam name="TDestination">目标类型。</typeparam>
            /// <returns>映射后的目标类型实例，如果源对象为 null 则返回 default(TDestination)。</returns>
            /// <example>
            /// <code>
            /// var userDto = userEntity.Adapt&lt;UserDto&gt;();
            /// var productList = products.Adapt&lt;List&lt;ProductDto&gt;&gt;();
            /// </code>
            /// </example>
            public TDestination? Adapt<TDestination>()
                =>
                    source.Adapt<TDestination>(MapperProvider.Current,
                        AdaptOptions.Default,
                        null);

            /// <summary>
            /// 将源对象映射为目标类型的实例（使用指定选项）。
            /// </summary>
            /// <typeparam name="TDestination">目标类型。</typeparam>
            /// <param name="options">映射选项，用于控制映射行为（如忽略大小写、忽略下划线等）。</param>
            /// <returns>映射后的目标类型实例，如果源对象为 null 则返回 default(TDestination)。</returns>
            /// <example>
            /// <code>
            /// // 使用严格模式映射
            /// var dto = source.Adapt&lt;TargetDto&gt;(AdaptOptions.Strict);
            /// 
            /// // 组合多个选项
            /// var dto = source.Adapt&lt;TargetDto&gt;(AdaptOptions.IgnoreCase | AdaptOptions.IgnoreNullValues);
            /// </code>
            /// </example>
            public TDestination? Adapt<TDestination>(AdaptOptions options)
                =>
                    source.Adapt<TDestination>(MapperProvider.Current,
                        options,
                        null);

            /// <summary>
            /// 将源对象映射为目标类型的实例，并在映射完成后执行弱类型回调。
            /// 适用于对整个对象或整个集合进行后处理。
            /// </summary>
            /// <typeparam name="TDestination">目标类型。</typeparam>
            /// <param name="afterMap">映射完成后的回调函数，接收目标对象和源对象作为参数。</param>
            /// <returns>映射后的目标类型实例，如果源对象为 null 则返回 default(TDestination)。</returns>
            /// <remarks>
            /// 弱类型回调适合以下场景：
            /// <list type="bullet">
            /// <item><description>需要对整个集合进行统一处理</description></item>
            /// <item><description>源对象类型在编译时不确定</description></item>
            /// <item><description>需要同时访问源对象和目标对象</description></item>
            /// </list>
            /// </remarks>
            /// <example>
            /// <code>
            /// var dto = source.Adapt&lt;TargetDto&gt;((dest, src) =&gt; {
            ///     if (dest != null) {
            ///         dest.MappedAt = DateTime.Now;
            ///     }
            /// });
            /// </code>
            /// </example>
            public TDestination? Adapt<TDestination>(Action<TDestination?, object>? afterMap)
                =>
                    source.Adapt<TDestination>(MapperProvider.Current,
                        AdaptOptions.Default,
                        afterMap);

            /// <summary>
            /// 将源对象映射为目标类型的实例，使用指定选项，并在映射完成后执行弱类型回调。
            /// </summary>
            /// <typeparam name="TDestination">目标类型。</typeparam>
            /// <param name="options">映射选项。</param>
            /// <param name="afterMap">映射完成后的回调函数。</param>
            /// <returns>映射后的目标类型实例，如果源对象为 null 则返回 default(TDestination)。</returns>
            /// <example>
            /// <code>
            /// var dto = source.Adapt&lt;TargetDto&gt;(
            ///     AdaptOptions.IgnoreCase,
            ///     (dest, src) =&gt; {
            ///         dest?.Validate();
            ///     });
            /// </code>
            /// </example>
            public TDestination? Adapt<TDestination>(AdaptOptions options,
                Action<TDestination?, object>? afterMap)
                =>
                    source.Adapt<TDestination>(MapperProvider.Current,
                        options,
                        afterMap);
        }

        /* ============================================================
         * 二、弱类型回调（用于整个对象或整个集合的后处理）
         * ============================================================ */

        /* ============================================================
         * 三、强类型回调（核心功能：TDestination, TSource）
         * ============================================================ */

        /// <summary>
        /// 将源对象映射为目标类型的实例，并在映射完成后执行强类型回调。
        /// 这是推荐的方式，提供类型安全和更好的智能提示。
        /// </summary>
        /// <typeparam name="TDestination">目标类型。</typeparam>
        /// <typeparam name="TSource">源类型。</typeparam>
        /// <param name="source">源对象。</param>
        /// <param name="afterMap">映射完成后的强类型回调函数，接收强类型的目标对象和源对象。</param>
        /// <returns>映射后的目标类型实例，如果源对象为 null 则返回 default(TDestination)。</returns>
        /// <remarks>
        /// <para>强类型回调的优势：</para>
        /// <list type="bullet">
        /// <item><description>编译时类型检查，更安全</description></item>
        /// <item><description>完整的智能提示支持</description></item>
        /// <item><description>性能更优（无需类型转换）</description></item>
        /// <item><description>适合单个对象的映射后处理</description></item>
        /// </list>
        /// <para><b>注意：</b>对于集合映射，回调不会作用于每个元素，请使用弱类型回调。</para>
        /// </remarks>
        /// <example>
        /// <code>
        /// var userDto = userEntity.Adapt&lt;UserDto, User&gt;((dto, entity) =&gt; {
        ///     dto.FullName = $"{entity.FirstName} {entity.LastName}";
        ///     dto.Age = DateTime.Now.Year - entity.BirthYear;
        /// });
        /// </code>
        /// </example>
        public static TDestination? Adapt<TDestination, TSource>(
            this TSource source,
            Action<TDestination, TSource> afterMap)
        {
            return source.Adapt<TDestination, TSource>(MapperProvider.Current,
                AdaptOptions.Default,
                afterMap);
        }

        /// <summary>
        /// 将源对象映射为目标类型的实例，使用指定选项，并在映射完成后执行强类型回调。
        /// </summary>
        /// <typeparam name="TDestination">目标类型。</typeparam>
        /// <typeparam name="TSource">源类型。</typeparam>
        /// <param name="source">源对象。</param>
        /// <param name="options">映射选项。</param>
        /// <param name="afterMap">映射完成后的强类型回调函数。</param>
        /// <returns>映射后的目标类型实例，如果源对象为 null 则返回 default(TDestination)。</returns>
        /// <example>
        /// <code>
        /// var dto = entity.Adapt&lt;OrderDto, Order&gt;(
        ///     AdaptOptions.IgnoreNullValues,
        ///     (dto, entity) =&gt; {
        ///         dto.TotalAmount = entity.Items.Sum(i =&gt; i.Price * i.Quantity);
        ///     });
        /// </code>
        /// </example>
        public static TDestination? Adapt<TDestination, TSource>(
            this TSource source,
            AdaptOptions options,
            Action<TDestination, TSource> afterMap)
        {
            return source.Adapt<TDestination, TSource>(MapperProvider.Current,
                options,
                afterMap);
        }

        /* ============================================================
         * 四、internal 强类型核心（不使用 object/_，语义完全清晰）
         * ============================================================ */

        /// <summary>
        /// 内部强类型核心方法：执行类型安全的映射，支持强类型回调。
        /// 集合映射会直接委托给 Mapper，不执行回调。
        /// </summary>
        /// <typeparam name="TDestination">目标类型。</typeparam>
        /// <typeparam name="TSource">源类型。</typeparam>
        /// <param name="source">源对象。</param>
        /// <param name="mapper">映射器实例。</param>
        /// <param name="options">映射选项。</param>
        /// <param name="afterMap">映射完成后的回调函数（可选）。</param>
        /// <returns>映射后的目标类型实例。</returns>
        internal static TDestination? Adapt<TDestination, TSource>(
            this TSource source,
            IMapper mapper,
            AdaptOptions options,
            Action<TDestination, TSource>? afterMap)
        {
            if (source == null)
                return default;

            var destType = typeof(TDestination);

            // 集合类型：直接交给 Mapper 处理（集合级后处理请使用弱类型回调）
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

        /// <summary>
        /// 内部弱类型核心方法：执行映射并支持弱类型回调。
        /// 这是唯一的实现点，所有其他重载最终都会调用此方法。
        /// </summary>
        /// <typeparam name="TDestination">目标类型。</typeparam>
        /// <param name="source">源对象（可以是任意类型）。</param>
        /// <param name="mapper">映射器实例。</param>
        /// <param name="options">映射选项。</param>
        /// <param name="afterMap">映射完成后的回调函数（可选），对整个结果（包括集合）执行。</param>
        /// <returns>映射后的目标类型实例。</returns>
        internal static TDestination? Adapt<TDestination>(
            this object? source,
            IMapper mapper,
            AdaptOptions options,
            Action<TDestination?, object>? afterMap)
        {
            if (source == null)
                return default;

            var destType = typeof(TDestination);

            // 集合类型：一次性映射，然后对整个结果执行回调
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
