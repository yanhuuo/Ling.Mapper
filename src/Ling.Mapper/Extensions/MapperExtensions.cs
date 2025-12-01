namespace Ling.Mapper
{
    /// <summary>
    /// IMapper 扩展方法集合
    /// </summary>
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
        public static TDestination? Adapt<TDestination, TSource>(
            this TSource source,
            IMapper mapper,
            System.Action<TSource, TDestination?>? custom)
        {
            var dest = mapper.Map<TDestination>(source);

            if (dest == null)
            {
                // 尝试创建目标实例（如果可能）
                if (!typeof(TDestination).IsValueType)
                {
                    try { dest = (TDestination?)System.Activator.CreateInstance(typeof(TDestination)); }
                    catch { /* 忽略 */ }
                }
            }

            if (dest != null)
                custom?.Invoke(source, dest);

            return dest;
        }

        /// <summary>
        /// 将源映射到已有目标实例（不会创建新实例）。
        /// 要求 mapper 实现 Map(object, Type, Type, object) 或通过反射设置属性。
        /// </summary>
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
            catch
            {
                return false;
            }
        }

        //重新引入一个简单的 Adapt 重载，使用全局的 MapperProvider（源优先形式）
        public static TDestination? Adapt<TDestination, TSource>(
            this TSource source, System.Action<TSource, TDestination?>? custom)
        {
            var mapper = MapperProvider.Current ?? throw new System.InvalidOperationException("没有注册默认的 mapper。请调用 MapperProvider.SetCurrent(mapper) 或使用带 IMapper 参数的重载.");
            var dest = mapper.Map<TDestination>(source);

            if (dest == null)
            {
                if (!typeof(TDestination).IsValueType)
                {
                    try { dest = (TDestination?)System.Activator.CreateInstance(typeof(TDestination)); }
                    catch { }
                }
            }

            if (dest != null)
                custom?.Invoke(source, dest);

            return dest;
        }

        /// <summary>
        /// 提供接受 mapper 参数并直接返回映射结果的 Adapt 重载（无自定义回调）。
        /// </summary>
        public static TDestination? Adapt<TDestination, TSource>(
            this TSource source, IMapper mapper)
        {
            return mapper.Map<TDestination>(source);
        }

        /// <summary>
        /// 新的重载：目标优先的回调签名（参数顺序为 (dest, src)），与旧 API 保持兼容。
        /// </summary>
        /// <exception cref="System.InvalidOperationException"></exception>
        public static TDestination? Adapt<TDestination, TSource>(
            this TSource source, System.Action<TDestination?, TSource>? custom)
        {
            var mapper = MapperProvider.Current ?? throw new System.InvalidOperationException("没有注册默认的 mapper。请调用 MapperProvider.SetCurrent(mapper) 或使用带 IMapper 参数的重载.");
            var dest = mapper.Map<TDestination>(source);

            if (dest == null)
            {
                if (!typeof(TDestination).IsValueType)
                {
                    try { dest = (TDestination?)System.Activator.CreateInstance(typeof(TDestination)); }
                    catch { }
                }
            }

            if (dest != null)
                custom?.Invoke(dest, source);

            return dest;
        }

        /// <summary>
        /// 可选的目标优先形式的 Adapt 重载，接受 mapper 参数和回调。
        /// </summary>
        public static TDestination? Adapt<TDestination, TSource>(
            this TSource source, IMapper mapper, System.Action<TDestination?, TSource>? custom)
        {
            var dest = mapper.Map<TDestination>(source);
            if (dest == null)
            {
                if (!typeof(TDestination).IsValueType)
                {
                    try { dest = (TDestination?)System.Activator.CreateInstance(typeof(TDestination)); }
                    catch { }
                }
            }

            if (dest != null)
                custom?.Invoke(dest, source);

            return dest;
        }

        /// <summary>
        ///便捷扩展方法
        /// </summary>
        /// <typeparam name="TDestination"></typeparam>
        /// <param name="mapper"></param>
        /// <param name="source"></param>
        /// <param name="defaultValue"></param>
        /// <returns></returns>
        public static TDestination? MapOrDefault<TDestination>(this IMapper mapper, object? source, TDestination? defaultValue = default)
        {
            var d = mapper.Map<TDestination>(source);
            return d ?? defaultValue;
        }

        public static TDestination MapOrThrow<TDestination>(this IMapper mapper, object? source)
        {
            var d = mapper.Map<TDestination>(source);
            if (d == null) throw new InvalidOperationException("映射结果为 null");
            return d;
        }
    }
}
