using System.Linq;
using System.Collections.Generic;

namespace Ling.Mapper
{
    /// <summary>
    /// IMapper 扩展方法集合
    /// </summary>
    /// <remarks>
    /// <para>本类提供了丰富的对象映射扩展方法，简化映射操作。</para>
    /// <para><strong>主要功能：</strong></para>
    /// <list type="bullet">
    /// <item><description><strong>Adapt 系列</strong>：灵活的对象映射，支持回调函数</description></item>
    /// <item><description><strong>AdaptList 系列</strong>：集合映射，支持元素级处理</description></item>
    /// <item><description><strong>MapTo / MapInto</strong>：基础映射操作</description></item>
    /// <item><description><strong>TryMap / MapOrDefault / MapOrThrow</strong>：安全的映射方法</description></item>
    /// <item><description><strong>AdaptOptions</strong>：运行时映射规则配置</description></item>
    /// </list>
    /// <para><strong>相关文档：</strong></para>
    /// <list type="bullet">
    /// <item><description>详细使用指南：<c>docs/Adapt使用指南.md</c></description></item>
    /// <item><description>全局配置说明：<c>docs/全局配置和运行时选项指南.md</c></description></item>
    /// <item><description>功能概览：<c>docs/功能概览.md</c></description></item>
    /// <item><description>异常处理：<c>docs/异常处理快速指南.md</c></description></item>
    /// </list>
    /// </remarks>
    public static class MapperExtensions
    {
        /// <summary>
        /// 使用指定的 IMapper 将当前对象映射为目标类型 TDestination。
        /// </summary>
        /// <typeparam name="TDestination">目标类型。</typeparam>
        /// <typeparam name="TSource">源类型。</typeparam>
        /// <param name="source">源对象实例。</param>
        /// <param name="mapper">IMapper 实例。</param>
        /// <returns>映射后的目标类型实例。</returns>
        public static TDestination? MapTo<TDestination, TSource>(
            this TSource source, IMapper mapper)
        {
            return mapper.Map<TDestination>(source);
        }

        /// <summary>
        /// 映射并允许使用匿名方法对目标对象进行二次加工。
        /// </summary>
        /// <typeparam name="TDestination">目标类型</typeparam>
        /// <typeparam name="TSource">源类型</typeparam>
        /// <param name="source">源对象实例</param>
        /// <param name="mapper">IMapper 实例</param>
        /// <param name="custom">自定义处理回调</param>
        /// <returns>映射后的目标类型实例</returns>
        /// <remarks>
        /// 如果映射结果为 null 且目标类型不是值类型，将尝试创建目标类型的实例。
        /// 如果实例化失败，将抛出相应的异常，而不是返回 null。
        /// </remarks>
        /// <exception cref="System.MissingMethodException">目标类型没有无参构造函数</exception>
        /// <exception cref="System.MemberAccessException">目标类型的构造函数不可访问</exception>
        public static TDestination? Adapt<TDestination, TSource>(
            this TSource source,
            IMapper mapper,
            System.Action<TSource, TDestination?>? custom)
        {
            var dest = mapper.Map<TDestination>(source);

            if (dest == null && !typeof(TDestination).IsValueType)
            {
                dest = CreateInstance<TDestination>();
            }

            if (dest != null)
                custom?.Invoke(source, dest);

            return dest;
        }

        /// <summary>
        /// 将源映射到已有目标实例（不会创建新实例）。
        /// 要求 mapper 实现 Map(object, Type, Type, object) 或通过反射设置属性。
        /// </summary>
        /// <typeparam name="TDestination">目标类型</typeparam>
        /// <param name="mapper">IMapper 实例</param>
        /// <param name="source">源对象</param>
        /// <param name="destination">目标对象实例</param>
        /// <returns>更新后的目标对象</returns>
        /// <exception cref="System.ArgumentNullException">mapper 或 destination 为 null</exception>
        public static TDestination MapInto<TDestination>(
            this IMapper mapper, object source, TDestination destination)
        {
            if (mapper == null) throw new System.ArgumentNullException(nameof(mapper));
            if (destination == null) throw new System.ArgumentNullException(nameof(destination));

            // 如果 mapper 支持直接 Map(object, Type, Type, object) 的重载，可直接调用（当前实现通过 Map +复制属性）
            var mapped = mapper.Map<object>(source);
            if (mapped == null) return destination;

            // 简单地将 mapped 的可写属性复制到 destination
            var destType = typeof(TDestination);
            var srcType = mapped.GetType();

            foreach (var dp in destType.GetProperties(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance))
            {
                if (!dp.CanWrite) continue;
                var sp = srcType.GetProperty(dp.Name, System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
                if (sp == null || !sp.CanRead) continue;
                var val = sp.GetValue(mapped);
                dp.SetValue(destination, val);
            }

            return destination;
        }

        /// <summary>
        /// 尝试映射，避免抛出异常，返回 bool 并输出目标实例。
        /// </summary>
        /// <typeparam name="TDestination">目标类型</typeparam>
        /// <param name="mapper">IMapper 实例</param>
        /// <param name="source">源对象</param>
        /// <param name="destination">映射结果</param>
        /// <returns>映射是否成功</returns>
        public static bool TryMap<TDestination>(
            this IMapper mapper, object? source, out TDestination? destination)
        {
            destination = default;
            try
            {
                var d = mapper.Map<TDestination>(source);
                destination = d;
                return d != null;
            }
            catch (System.Exception)
            {
                return false;
            }
        }

        /// <summary>
        /// 重新引入一个简单的 Adapt 重载，使用全局的 MapperProvider（源优先形式）
        /// </summary>
        /// <typeparam name="TDestination">目标类型</typeparam>
        /// <typeparam name="TSource">源类型</typeparam>
        /// <param name="source">源对象</param>
        /// <param name="custom">自定义处理回调</param>
        /// <returns>映射后的目标对象</returns>
        /// <exception cref="System.MissingMethodException">目标类型没有无参构造函数</exception>
        /// <exception cref="System.MemberAccessException">目标类型的构造函数不可访问</exception>
        /// <remarks>
        /// 如果映射结果为 null 且目标类型不是值类型，将尝试创建目标类型的实例。
        /// 如果实例化失败，将抛出相应的异常，而不是返回 null。
        /// </remarks>
        public static TDestination? Adapt<TDestination, TSource>(
            this TSource source, System.Action<TSource, TDestination?>? custom)
        {
            var mapper = MapperProvider.Current;
            var dest = mapper.Map<TDestination>(source);

            if (dest == null && !typeof(TDestination).IsValueType)
            {
                dest = CreateInstance<TDestination>();
            }

            if (dest != null)
                custom?.Invoke(source, dest);

            return dest;
        }

        /// <summary>
        /// 提供接受 mapper 参数并直接返回映射结果的 Adapt 重载（无自定义回调）。
        /// </summary>
        /// <typeparam name="TDestination">目标类型</typeparam>
        /// <typeparam name="TSource">源类型</typeparam>
        /// <param name="source">源对象</param>
        /// <param name="mapper">IMapper 实例</param>
        /// <returns>映射后的目标对象</returns>
        public static TDestination? Adapt<TDestination, TSource>(
            this TSource source, IMapper mapper)
        {
            return mapper.Map<TDestination>(source);
        }

        /// <summary>
        /// 新的重载：目标优先的回调签名（参数顺序为 (dest, src)），与旧 API 保持兼容。
        /// </summary>
        /// <typeparam name="TDestination">目标类型</typeparam>
        /// <typeparam name="TSource">源类型</typeparam>
        /// <param name="source">源对象</param>
        /// <param name="custom">自定义处理回调，参数顺序为 (destination, source)</param>
        /// <returns>映射后的目标对象</returns>
        /// <exception cref="System.MissingMethodException">目标类型没有无参构造函数</exception>
        /// <exception cref="System.MemberAccessException">目标类型的构造函数不可访问</exception>
        /// <remarks>
        /// 如果映射结果为 null 且目标类型不是值类型，将尝试创建目标类型的实例。
        /// 如果实例化失败，将抛出相应的异常，而不是返回 null。
        /// </remarks>
        public static TDestination? Adapt<TDestination, TSource>(
            this TSource source, System.Action<TDestination?, TSource>? custom)
        {
            var mapper = MapperProvider.Current;
            var dest = mapper.Map<TDestination>(source);

            if (dest == null && !typeof(TDestination).IsValueType)
            {
                dest = CreateInstance<TDestination>();
            }

            if (dest != null)
                custom?.Invoke(dest, source);

            return dest;
        }

        /// <summary>
        /// 可选的目标优先形式的 Adapt 重载，接受 mapper 参数和回调。
        /// </summary>
        /// <typeparam name="TDestination">目标类型</typeparam>
        /// <typeparam name="TSource">源类型</typeparam>
        /// <param name="source">源对象</param>
        /// <param name="mapper">IMapper 实例</param>
        /// <param name="custom">自定义处理回调，参数顺序为 (destination, source)</param>
        /// <returns>映射后的目标对象</returns>
        /// <exception cref="System.MissingMethodException">目标类型没有无参构造函数</exception>
        /// <exception cref="System.MemberAccessException">目标类型的构造函数不可访问</exception>
        /// <remarks>
        /// 如果映射结果为 null 且目标类型不是值类型，将尝试创建目标类型的实例。
        /// 如果实例化失败，将抛出相应的异常，而不是返回 null。
        /// </remarks>
        public static TDestination? Adapt<TDestination, TSource>(
            this TSource source, IMapper mapper, System.Action<TDestination?, TSource>? custom)
        {
            var dest = mapper.Map<TDestination>(source);
            
            if (dest == null && !typeof(TDestination).IsValueType)
            {
                dest = CreateInstance<TDestination>();
            }

            if (dest != null)
                custom?.Invoke(dest, source);

            return dest;
        }

        /// <summary>
        /// 便捷扩展方法
        /// </summary>
        /// <typeparam name="TDestination">目标类型</typeparam>
        /// <param name="mapper">IMapper 实例</param>
        /// <param name="source">源对象</param>
        /// <param name="defaultValue">默认值</param>
        /// <returns>映射结果或默认值</returns>
        public static TDestination? MapOrDefault<TDestination>(this IMapper mapper, object? source, TDestination? defaultValue = default)
        {
            var d = mapper.Map<TDestination>(source);
            return d ?? defaultValue;
        }

        /// <summary>
        /// 映射对象，如果结果为 null 则抛出异常
        /// </summary>
        /// <typeparam name="TDestination">目标类型</typeparam>
        /// <param name="mapper">IMapper 实例</param>
        /// <param name="source">源对象</param>
        /// <returns>映射后的对象</returns>
        /// <exception cref="System.InvalidOperationException">映射结果为 null</exception>
        public static TDestination MapOrThrow<TDestination>(this IMapper mapper, object? source)
        {
            var d = mapper.Map<TDestination>(source);
            if (d == null) throw new System.InvalidOperationException("映射结果为 null");
            return d;
        }

        /// <summary>
        /// 简化的 Adapt 方法，自动推断源类型，回调参数为 (destination, source)
        /// </summary>
        /// <typeparam name="TDestination">目标类型</typeparam>
        /// <param name="source">源对象</param>
        /// <param name="custom">可选的回调函数，用于对映射结果进行特殊处理。参数为 (destination, source)</param>
        /// <returns>映射后的目标对象</returns>
        /// <remarks>
        /// <para>此方法使用全局的 MapperProvider.Current 进行映射，并支持在映射完成后通过匿名函数进行额外处理。</para>
        /// <para>如果映射结果为 null 且目标类型不是值类型，将尝试创建目标类型的实例。</para>
        /// <para>如果实例化失败（例如没有无参构造函数），将抛出异常。</para>
        /// <para><strong>🆕 v2.3 自动集合识别：</strong></para>
        /// <para>现在支持自动识别集合类型（List&lt;T&gt;, IEnumerable&lt;T&gt;, IList&lt;T&gt; 等），无需显式调用 AdaptList。</para>
        /// <para>特别适用于需要对映射结果进行二次加工的场景，例如：</para>
        /// <list type="bullet">
        /// <item><description>循环处理分页结果中的列表项</description></item>
        /// <item><description>根据原始数据计算派生字段</description></item>
        /// <item><description>对映射结果进行条件判断和修改</description></item>
        /// </list>
        /// <example>
        /// 示例 1：处理分页结果中的列表数据
        /// <code>
        /// var page = await query
        ///     .ToPageResultAsync(dto.page ?? 1, dto.size ?? 1)
        ///     .Adapt&lt;PageResult&lt;GetCustomerRewardConditionPageRes&gt;&gt;((res, dis) =>
        ///     {
        ///         // res 是映射后的 PageResult&lt;GetCustomerRewardConditionPageRes&gt;
        ///         // dis 是原始的源对象
        ///         
        ///         // 循环处理列表中的每一项
        ///         if (res.Items != null)
        ///         {
        ///             foreach (var item in res.Items)
        ///             {
        ///                 // 对每个项进行特殊处理
        ///                 item.SomeProperty = CalculateValue(item);
        ///                 item.AnotherProperty = GetExtraData(item.Id);
        ///             }
        ///         }
        ///         
        ///         // 也可以修改分页信息
        ///         res.Total = res.Items?.Count ?? 0;
        ///     });
        /// </code>
        /// </example>
        /// <example>
        /// 示例 2：使用 LINQ 批量处理
        /// <code>
        /// var page = await query
        ///     .ToPageResultAsync(dto.page ?? 1, dto.size ?? 1)
        ///     .Adapt&lt;PageResult&lt;CustomerDto&gt;&gt;((res, dis) =>
        ///     {
        ///         if (res.Items != null)
        ///         {
        ///             res.Items = res.Items
        ///                 .Select((item, index) => 
        ///                 {
        ///                     item.RowNumber = index + 1;
        ///                     item.DisplayName = FormatName(item);
        ///                     return item;
        ///                 })
        ///                 .ToList();
        ///         }
        ///     });
        /// </code>
        /// </example>
        /// <example>
        /// 示例 3：不需要特殊处理时，省略匿名函数
        /// <code>
        /// var page = await query
        ///     .ToPageResultAsync(dto.page ?? 1, dto.size ?? 1)
        ///     .Adapt&lt;PageResult&lt;CustomerDto&gt;&gt;();
        /// </code>
        /// </example>
        /// <example>
        /// 示例 4：🆕 自动集合映射
        /// <code>
        /// var entities = GetEntities(); // List&lt;UserEntity&gt;
        /// 
        /// // 自动识别为集合映射，无需调用 AdaptList
        /// var dtos = entities.Adapt&lt;List&lt;UserDto&gt;&gt;();
        /// 
        /// // 也支持 IEnumerable, IList 等
        /// var dtos = entities.Adapt&lt;IEnumerable&lt;UserDto&gt;&gt;();
        /// </code>
        /// </example>
        /// </remarks>
        /// <exception cref="System.MissingMethodException">目标类型没有无参构造函数</exception>
        /// <exception cref="System.MemberAccessException">目标类型的构造函数不可访问</exception>
        public static TDestination? Adapt<TDestination>(
            this object source, System.Action<TDestination?, object>? custom = null)
        {
            var mapper = MapperProvider.Current;
            var destType = typeof(TDestination);
            
            // 🆕 v2.3: 自动检测集合类型
            if (IsCollectionType(destType) && source is System.Collections.IEnumerable sourceEnumerable)
            {
                var result = AdaptCollectionInternal<TDestination>(sourceEnumerable, destType, mapper);
                if (result != null)
                    custom?.Invoke(result, source);
                return result;
            }
            
            // 原有的单对象映射逻辑
            var dest = mapper.Map<TDestination>(source);

            if (dest == null && !typeof(TDestination).IsValueType)
            {
                dest = CreateInstance<TDestination>();
            }

            if (dest != null)
                custom?.Invoke(dest, source);

            return dest;
        }

        /// <summary>
        /// 带 IMapper 参数的简化 Adapt 方法，回调参数为 (destination, source)
        /// </summary>
        /// <typeparam name="TDestination">目标类型</typeparam>
        /// <param name="source">源对象</param>
        /// <param name="mapper">指定的 IMapper 实例</param>
        /// <param name="custom">可选的回调函数，用于对映射结果进行特殊处理。参数为 (destination, source)</param>
        /// <returns>映射后的目标对象</returns>
        /// <remarks>
        /// <para>此方法使用指定的 IMapper 实例进行映射，并支持在映射完成后通过匿名函数进行额外处理。</para>
        /// <para>与无参数版本的区别是可以指定特定的 Mapper 实例，适用于需要使用非全局 Mapper 的场景。</para>
        /// <para>如果映射结果为 null 且目标类型不是值类型，将尝试创建目标类型的实例。</para>
        /// <para>如果实例化失败（例如没有无参构造函数），将抛出异常。</para>
        /// <para><strong>🆕 v2.3 自动集合识别：</strong></para>
        /// <para>支持自动识别集合类型（List&lt;T&gt;, IEnumerable&lt;T&gt;, IList&lt;T&gt; 等），无需显式调用 AdaptList。</para>
        /// <example>
        /// 示例 1：指定特定的 Mapper 实例
        /// <code>
        /// var customMapper = new MapperConfiguration().CreateMapper();
        /// var result = sourceData
        ///     .Adapt&lt;TargetDto&gt;(customMapper, (res, dis) =>
        ///     {
        ///         // 对结果进行特殊处理
        ///         res.CalculatedField = res.Value * 2;
        ///         
        ///         // 循环处理集合
        ///         if (res.Items != null)
        ///         {
        ///             for (int i = 0; i &lt; res.Items.Count; i++)
        ///             {
        ///                 res.Items[i].Index = i + 1;
        ///             }
        ///         }
        ///     });
        /// </code>
        /// </example>
        /// <example>
        /// 示例 2：🆕 自动集合映射
        /// <code>
        /// var customMapper = new MapperConfiguration().CreateMapper();
        /// var entities = GetEntities();
        /// 
        /// // 自动识别为集合映射
        /// var dtos = entities.Adapt&lt;List&lt;UserDto&gt;&gt;(customMapper);
        /// </code>
        /// </example>
        /// </remarks>
        /// <exception cref="System.MissingMethodException">目标类型没有无参构造函数</exception>
        /// <exception cref="System.MemberAccessException">目标类型的构造函数不可访问</exception>
        public static TDestination? Adapt<TDestination>(
            this object source, IMapper mapper, System.Action<TDestination?, object>? custom = null)
        {
            var destType = typeof(TDestination);
            
            // 🆕 v2.3: 自动检测集合类型
            if (IsCollectionType(destType) && source is System.Collections.IEnumerable sourceEnumerable)
            {
                var result = AdaptCollectionInternal<TDestination>(sourceEnumerable, destType, mapper);
                if (result != null)
                    custom?.Invoke(result, source);
                return result;
            }
            
            // 原有的单对象映射逻辑
            var dest = mapper.Map<TDestination>(source);
            
            if (dest == null && !typeof(TDestination).IsValueType)
            {
                dest = CreateInstance<TDestination>();
            }

            if (dest != null)
                custom?.Invoke(dest, source);

            return dest;
        }

        /// <summary>
        /// 将 List 集合映射为目标类型的 List 集合，支持对每个元素进行自定义处理
        /// </summary>
        /// <typeparam name="TDestination">目标元素类型</typeparam>
        /// <typeparam name="TSource">源元素类型</typeparam>
        /// <param name="source">源 List 集合</param>
        /// <param name="custom">可选的回调函数，对每个映射后的元素进行处理。参数为 (destination, source, index)</param>
        /// <returns>映射后的目标 List 集合</returns>
        /// <remarks>
        /// <para>此方法使用全局 Mapper 将 List 集合中的每个元素映射为目标类型，并支持对每个元素进行额外处理。</para>
        /// <example>
        /// 示例 1：基本 List 转换
        /// <code>
        /// var sourceList = new List&lt;SourceDto&gt; { ... };
        /// var targetList = sourceList.AdaptList&lt;TargetDto, SourceDto&gt;();
        /// </code>
        /// </example>
        /// <example>
        /// 示例 2：转换时对每个元素进行处理
        /// <code>
        /// var targetList = sourceList.AdaptList&lt;TargetDto, SourceDto&gt;((target, source, index) =>
        /// {
        ///     target.RowNumber = index + 1;
        ///     target.DisplayName = $"{source.FirstName} {source.LastName}";
        /// });
        /// </code>
        /// </example>
        /// <example>
        /// 示例 3：嵌套对象的 List 转换
        /// <code>
        /// var orders = sourceOrders.AdaptList&lt;OrderDto, OrderEntity&gt;((order, source, index) =>
        /// {
        ///     // 订单项也会自动映射
        ///     if (order.Items != null)
        ///     {
        ///         foreach (var item in order.Items)
        ///         {
        ///             item.ParentOrderId = order.Id;
        ///         }
        ///     }
        /// });
        /// </code>
        /// </example>
        /// </remarks>
        public static List<TDestination>? AdaptList<TDestination, TSource>(
            this IEnumerable<TSource>? source,
            System.Action<TDestination?, TSource, int>? custom = null)
        {
            if (source == null) return null;

            var mapper = MapperProvider.Current;
            
            var result = new List<TDestination>();
            int index = 0;
            
            foreach (var item in source)
            {
                var dest = mapper.Map<TDestination>(item);
                if (dest != null)
                {
                    custom?.Invoke(dest, item, index);
                    result.Add(dest);
                }
                index++;
            }

            return result;
        }

        /// <summary>
        /// 带 IMapper 参数的 List 映射方法，支持对每个元素进行自定义处理
        /// </summary>
        /// <typeparam name="TDestination">目标元素类型</typeparam>
        /// <typeparam name="TSource">源元素类型</typeparam>
        /// <param name="source">源 List 集合</param>
        /// <param name="mapper">指定的 IMapper 实例</param>
        /// <param name="custom">可选的回调函数，对每个映射后的元素进行处理。参数为 (destination, source, index)</param>
        /// <returns>映射后的目标 List 集合</returns>
        public static List<TDestination>? AdaptList<TDestination, TSource>(
            this IEnumerable<TSource>? source,
            IMapper mapper,
            System.Action<TDestination?, TSource, int>? custom = null)
        {
            if (source == null) return null;

            var result = new List<TDestination>();
            int index = 0;
            
            foreach (var item in source)
            {
                var dest = mapper.Map<TDestination>(item);
                if (dest != null)
                {
                    custom?.Invoke(dest, item, index);
                    result.Add(dest);
                }
                index++;
            }

            return result;
        }

        /// <summary>
        /// 简化的 List 映射方法，自动推断源类型
        /// </summary>
        /// <typeparam name="TDestination">目标元素类型</typeparam>
        /// <param name="source">源集合</param>
        /// <param name="custom">可选的回调函数，对每个映射后的元素进行处理。参数为 (destination, source, index)</param>
        /// <returns>映射后的目标 List 集合</returns>
        /// <remarks>
        /// <example>
        /// 示例：简化的 List 转换写法
        /// <code>
        /// var targetList = sourceList.AdaptList&lt;TargetDto&gt;((target, source, index) =>
        /// {
        ///     target.Index = index;
        /// });
        /// </code>
        /// </example>
        /// </remarks>
        public static List<TDestination>? AdaptList<TDestination>(
            this System.Collections.IEnumerable? source,
            System.Action<TDestination?, object, int>? custom = null)
        {
            if (source == null) return null;

            var mapper = MapperProvider.Current;
            
            var result = new List<TDestination>();
            int index = 0;
            
            foreach (var item in source)
            {
                var dest = mapper.Map<TDestination>(item);
                if (dest != null)
                {
                    custom?.Invoke(dest, item, index);
                    result.Add(dest);
                }
                index++;
            }

            return result;
        }

        /// <summary>
        /// 创建目标类型的实例，失败时抛出异常
        /// </summary>
        /// <typeparam name="T">要创建的类型</typeparam>
        /// <returns>创建的实例</returns>
        /// <exception cref="System.MissingMethodException">类型没有无参构造函数</exception>
        /// <exception cref="System.MemberAccessException">构造函数不可访问（例如私有构造函数）</exception>
        /// <exception cref="System.Exception">实例化过程中发生其他异常</exception>
        /// <remarks>
        /// 此方法要求目标类型必须有一个可访问的无参构造函数。
        /// 如果实例化失败，将抛出明确的异常，帮助开发者在调试时快速定位问题。
        /// </remarks>
        private static T CreateInstance<T>()
        {
            try
            {
                return (T)System.Activator.CreateInstance(typeof(T))!;
            }
            catch (System.MissingMethodException ex)
            {
                throw new System.MissingMethodException(
                    $"无法创建类型 '{typeof(T).FullName}' 的实例：该类型没有无参构造函数。" +
                    $"请为 DTO 类型添加无参构造函数，或确保 Mapper 配置正确返回非 null 实例。", ex);
            }
            catch (System.MemberAccessException ex)
            {
                throw new System.MemberAccessException(
                    $"无法创建类型 '{typeof(T).FullName}' 的实例：构造函数不可访问（可能是私有或受保护的）。" +
                    $"请确保目标类型有一个公共的无参构造函数。", ex);
            }
            catch (System.Exception ex)
            {
                throw new System.InvalidOperationException(
                    $"创建类型 '{typeof(T).FullName}' 的实例时发生异常：{ex.Message}" +
                    $"请检查构造函数是否抛出了异常，或目标类型是否可以正常实例化。", ex);
            }
        }

        #region Collection Auto Detection Helpers (v2.3)

        /// <summary>
        /// 检测类型是否为支持的集合类型
        /// </summary>
        /// <param name="type">要检测的类型</param>
        /// <returns>如果是集合类型返回 true，否则返回 false</returns>
        /// <remarks>
        /// 支持的集合类型：
        /// - List&lt;T&gt;
        /// - IList&lt;T&gt;
        /// - ICollection&lt;T&gt;
        /// - IEnumerable&lt;T&gt;
        /// - T[]
        /// </remarks>
        private static bool IsCollectionType(System.Type type)
        {
            // 数组类型
            if (type.IsArray)
                return true;

            // 非泛型集合（如 ArrayList）不支持自动检测
            if (!type.IsGenericType)
                return false;

            var genericDef = type.GetGenericTypeDefinition();

            // 检查常见的泛型集合类型
            return genericDef == typeof(List<>) ||
                   genericDef == typeof(IList<>) ||
                   genericDef == typeof(ICollection<>) ||
                   genericDef == typeof(IEnumerable<>);
        }

        /// <summary>
        /// 内部集合映射方法
        /// </summary>
        private static TDestination? AdaptCollectionInternal<TDestination>(
            System.Collections.IEnumerable source,
            System.Type destType,
            IMapper mapper)
        {
            if (source == null) return default;

            // 获取元素类型
            var elementType = GetCollectionElementType(destType);
            if (elementType == null)
                return default;

            // 创建结果列表（泛型）
            var listType = typeof(List<>).MakeGenericType(elementType);
            var resultList = (System.Collections.IList)System.Activator.CreateInstance(listType)!;

            // 映射每个元素
            foreach (var item in source)
            {
                var mappedItem = mapper.Map(item, item?.GetType() ?? typeof(object), elementType);
                if (mappedItem != null)
                {
                    resultList.Add(mappedItem);
                }
            }

            // 根据目标类型返回合适的集合
            if (destType.IsArray)
            {
                // 转换为数组
                var array = System.Array.CreateInstance(elementType, resultList.Count);
                resultList.CopyTo(array, 0);
                return (TDestination)(object)array;
            }
            else if (destType.GetGenericTypeDefinition() == typeof(List<>))
            {
                // 返回 List<T>
                return (TDestination)resultList;
            }
            else
            {
                // IEnumerable<T>, IList<T>, ICollection<T> 都可以返回 List<T>
                return (TDestination)resultList;
            }
        }

        /// <summary>
        /// 获取集合的元素类型
        /// </summary>
        private static System.Type? GetCollectionElementType(System.Type collectionType)
        {
            // 数组
            if (collectionType.IsArray)
                return collectionType.GetElementType();

            // 泛型集合
            if (collectionType.IsGenericType)
            {
                var genericArgs = collectionType.GetGenericArguments();
                if (genericArgs.Length == 1)
                    return genericArgs[0];
            }

            // IEnumerable 接口
            var enumerableInterface = collectionType.GetInterfaces()
                .FirstOrDefault(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IEnumerable<>));
            
            if (enumerableInterface != null)
            {
                return enumerableInterface.GetGenericArguments()[0];
            }

            return null;
        }

        #endregion

        #region 带 AdaptOptions 的 Adapt 扩展方法

        /// <summary>
        /// 使用映射规则选项进行映射（带 mapper 参数）
        /// </summary>
        /// <typeparam name="TDestination">目标类型</typeparam>
        /// <typeparam name="TSource">源类型</typeparam>
        /// <param name="source">源对象</param>
        /// <param name="mapper">IMapper 实例</param>
        /// <param name="options">映射规则选项</param>
        /// <param name="custom">可选的回调函数</param>
        /// <returns>映射后的目标对象</returns>
        /// <remarks>
        /// <para>此方法支持在运行时指定映射规则，包括：</para>
        /// <list type="bullet">
        /// <item><description>IgnoreCase: 忽略属性名称大小写</description></item>
        /// <item><description>IgnoreUnderscore: 忽略属性名称中的下划线</description></item>
        /// <item><description>IgnoreProperties: 忽略指定的属性</description></item>
        /// <item><description>IgnoreNullValues: 忽略 null 值属性</description></item>
        /// </list>
        /// <example>
        /// 示例 1：忽略大小写匹配
        /// <code>
        /// var target = source.Adapt&lt;TargetDto, SourceDto&gt;(mapper, 
        ///     AdaptOptions.IgnoreCaseOption);
        /// </code>
        /// </example>
        /// <example>
        /// 示例 2：自定义规则
        /// <code>
        /// var target = source.Adapt&lt;TargetDto, SourceDto&gt;(mapper, 
        ///     new AdaptOptions 
        ///     { 
        ///         IgnoreCase = true,
        ///         IgnoreUnderscore = true,
        ///         IgnoreProperties = new[] { "Password", "CreditCard" }
        ///     });
        /// </code>
        /// </example>
        /// <example>
        /// 示例 3：配合回调函数
        /// <code>
        /// var target = source.Adapt&lt;TargetDto, SourceDto&gt;(mapper, 
        ///     AdaptOptions.FlexibleOption,
        ///     (dest, src) => dest.FullName = $"{src.FirstName} {src.LastName}");
        /// </code>
        /// </example>
        /// </remarks>
        public static TDestination? Adapt<TDestination, TSource>(
            this TSource source,
            IMapper mapper,
            AdaptOptions options,
            System.Action<TDestination?, TSource>? custom = null)
        {
            if (source == null) return default;
            if (options == null) options = AdaptOptions.Default;

            // 1. 先执行基础映射
            var dest = mapper.Map<TDestination>(source);

            if (dest == null && !typeof(TDestination).IsValueType)
            {
                dest = CreateInstance<TDestination>();
            }

            if (dest == null) return default;

            // 2. 应用映射规则
            ApplyAdaptOptions(source, dest, options);

            // 3. 执行自定义回调
            if (dest != null)
                custom?.Invoke(dest, source);

            return dest;
        }

        /// <summary>
        /// 使用映射规则选项进行映射（使用全局 Mapper）
        /// </summary>
        /// <typeparam name="TDestination">目标类型</typeparam>
        /// <typeparam name="TSource">源类型</typeparam>
        /// <param name="source">源对象</param>
        /// <param name="options">映射规则选项</param>
        /// <param name="custom">可选的回调函数</param>
        /// <returns>映射后的目标对象</returns>
        public static TDestination? Adapt<TDestination, TSource>(
            this TSource source,
            AdaptOptions options,
            System.Action<TDestination?, TSource>? custom = null)
        {
            var mapper = MapperProvider.Current;
            return source.Adapt<TDestination, TSource>(mapper, options, custom);
        }

        /// <summary>
        /// 使用映射规则选项进行映射（简化版本，自动推断源类型）
        /// </summary>
        /// <typeparam name="TDestination">目标类型</typeparam>
        /// <param name="source">源对象</param>
        /// <param name="mapper">IMapper 实例</param>
        /// <param name="options">映射规则选项</param>
        /// <param name="custom">可选的回调函数</param>
        /// <returns>映射后的目标对象</returns>
        public static TDestination? Adapt<TDestination>(
            this object source,
            IMapper mapper,
            AdaptOptions options,
            System.Action<TDestination?, object>? custom = null)
        {
            if (source == null) return default;
            if (options == null) options = AdaptOptions.Default;

            // 1. 先执行基础映射
            var dest = mapper.Map<TDestination>(source);

            if (dest == null && !typeof(TDestination).IsValueType)
            {
                dest = CreateInstance<TDestination>();
            }

            if (dest == null) return default;

            // 2. 应用映射规则
            ApplyAdaptOptions(source, dest, options);

            // 3. 执行自定义回调
            if (dest != null)
                custom?.Invoke(dest, source);

            return dest;
        }

        /// <summary>
        /// 使用映射规则选项进行映射（简化版本，使用全局 Mapper）
        /// </summary>
        /// <typeparam name="TDestination">目标类型</typeparam>
        /// <param name="source">源对象</param>
        /// <param name="options">映射规则选项</param>
        /// <param name="custom">可选的回调函数</param>
        /// <returns>映射后的目标对象</returns>
        public static TDestination? Adapt<TDestination>(
            this object source,
            AdaptOptions options,
            System.Action<TDestination?, object>? custom = null)
        {
            var mapper = MapperProvider.Current;
            return source.Adapt<TDestination>(mapper, options, custom);
        }

        /// <summary>
        /// 应用 AdaptOptions 规则到映射结果
        /// </summary>
        private static void ApplyAdaptOptions<TDestination, TSource>(
            TSource source,
            TDestination dest,
            AdaptOptions options)
        {
            if (source == null || dest == null || options == null) return;

            var destType = typeof(TDestination);
            var srcType = typeof(TSource);

            var destProps = destType.GetProperties(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
            var srcProps = srcType.GetProperties(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance)
                .ToDictionary(p => p.Name, p => p);

            foreach (var destProp in destProps)
            {
                if (!destProp.CanWrite) continue;

                // 1. 处理忽略属性
                if (options.IgnoreProperties != null && 
                    options.IgnoreProperties.Contains(destProp.Name, System.StringComparer.OrdinalIgnoreCase))
                {
                    var defaultValue = destProp.PropertyType.IsValueType
                        ? System.Activator.CreateInstance(destProp.PropertyType)
                        : null;
                    destProp.SetValue(dest, defaultValue);
                    continue;
                }

                // 2. 查找匹配的源属性（支持 IgnoreCase 和 IgnoreUnderscore）
                System.Reflection.PropertyInfo? srcProp = null;
                string destPropName = destProp.Name;

                if (options.IgnoreUnderscore)
                {
                    destPropName = destPropName.Replace("_", "");
                }

                foreach (var kvp in srcProps)
                {
                    string srcPropName = kvp.Key;
                    if (options.IgnoreUnderscore)
                    {
                        srcPropName = srcPropName.Replace("_", "");
                    }

                    var comparison = options.IgnoreCase
                        ? System.StringComparison.OrdinalIgnoreCase
                        : System.StringComparison.Ordinal;

                    if (string.Equals(srcPropName, destPropName, comparison))
                    {
                        srcProp = kvp.Value;
                        break;
                    }
                }

                if (srcProp == null || !srcProp.CanRead) continue;

                // 3. 处理 IgnoreNullValues
                var srcValue = srcProp.GetValue(source);
                if (options.IgnoreNullValues && srcValue == null)
                {
                    var defaultValue = destProp.PropertyType.IsValueType
                        ? System.Activator.CreateInstance(destProp.PropertyType)
                        : null;
                    destProp.SetValue(dest, defaultValue);
                    continue;
                }

                // 4. 如果启用了特殊匹配规则，需要手动赋值（因为 mapper 可能没有匹配）
                if ((options.IgnoreCase || options.IgnoreUnderscore) && srcValue != null)
                {
                    try
                    {
                        // 尝试类型转换
                        if (destProp.PropertyType.IsAssignableFrom(srcProp.PropertyType))
                        {
                            destProp.SetValue(dest, srcValue);
                        }
                        else if (destProp.PropertyType == srcProp.PropertyType)
                        {
                            destProp.SetValue(dest, srcValue);
                        }
                    }
                    catch
                    {
                        // 类型不兼容，跳过
                    }
                }
            }
        }

        #endregion
    }
}
