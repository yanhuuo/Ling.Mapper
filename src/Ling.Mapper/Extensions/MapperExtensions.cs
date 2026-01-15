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
    /// <item><description><strong>Adapt 系列</strong>：灵活的对象映射，支持回调函数和自动集合识别</description></item>
    /// <item><description><strong>MapTo / MapInto</strong>：基础映射操作</description></item>
    /// <item><description><strong>TryMap / MapOrDefault / MapOrThrow</strong>：安全的映射方法</description></item>
    /// <item><description><strong>AdaptOptions</strong>：运行时映射规则配置（默认启用 FlexibleOption）</description></item>
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
        /// 映射对象并允许对目标对象进行二次加工
        /// </summary>
        /// <typeparam name="TDestination">目标类型</typeparam>
        /// <typeparam name="TSource">源类型</typeparam>
        /// <param name="source">源对象实例</param>
        /// <param name="mapper">IMapper 实例</param>
        /// <param name="custom">自定义处理回调，参数为 (source, destination)</param>
        /// <returns>映射后的目标类型实例</returns>
        /// <exception cref="System.MissingMethodException">目标类型没有无参构造函数</exception>
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
        /// 使用全局 Mapper 映射对象，支持自定义回调处理
        /// </summary>
        /// <typeparam name="TDestination">目标类型</typeparam>
        /// <typeparam name="TSource">源类型</typeparam>
        /// <param name="source">源对象</param>
        /// <param name="custom">自定义处理回调，参数为 (source, destination)</param>
        /// <returns>映射后的目标对象</returns>
        /// <exception cref="System.MissingMethodException">目标类型没有无参构造函数</exception>
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
        /// 使用全局 Mapper 映射对象，支持自定义回调处理（回调参数为 destination, source）
        /// </summary>
        /// <typeparam name="TDestination">目标类型</typeparam>
        /// <typeparam name="TSource">源类型</typeparam>
        /// <param name="source">源对象</param>
        /// <param name="custom">自定义处理回调，参数为 (destination, source)</param>
        /// <returns>映射后的目标对象</returns>
        /// <remarks>
        /// 默认启用 FlexibleOption（忽略大小写和下划线），支持驼峰与下划线互转。
        /// </remarks>
        /// <exception cref="System.MissingMethodException">目标类型没有无参构造函数</exception>
        public static TDestination? Adapt<TDestination, TSource>(
            this TSource source, System.Action<TDestination?, TSource>? custom)
        {
            // 使用默认的 FlexibleOption
            var mapper = MapperProvider.Current;
            return source.Adapt<TDestination, TSource>(mapper, AdaptOptions.FlexibleOption, custom);
        }

        /// <summary>
        /// 使用指定 Mapper 映射对象，支持自定义回调处理（回调参数为 destination, source）
        /// </summary>
        /// <typeparam name="TDestination">目标类型</typeparam>
        /// <typeparam name="TSource">源类型</typeparam>
        /// <param name="source">源对象</param>
        /// <param name="mapper">IMapper 实例</param>
        /// <param name="custom">自定义处理回调，参数为 (destination, source)</param>
        /// <returns>映射后的目标对象</returns>
        /// <exception cref="System.MissingMethodException">目标类型没有无参构造函数</exception>
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
        /// 将对象映射为目标类型，支持自定义回调处理
        /// </summary>
        /// <typeparam name="TDestination">目标类型</typeparam>
        /// <param name="source">源对象</param>
        /// <param name="custom">可选的回调函数，参数为 (destination, source)</param>
        /// <returns>映射后的目标对象</returns>
        /// <remarks>
        /// 默认启用 FlexibleOption（忽略大小写和下划线），支持驼峰与下划线互转。
        /// 支持自动识别集合类型（List, IEnumerable, 数组等）。
        /// </remarks>
        /// <exception cref="System.MissingMethodException">目标类型没有无参构造函数</exception>
        public static TDestination? Adapt<TDestination>(
            this object source, System.Action<TDestination?, object>? custom = null)
        {
            var mapper = MapperProvider.Current;
            var destType = typeof(TDestination);
            
            // 自动检测集合类型
            if (IsCollectionType(destType) && source is System.Collections.IEnumerable sourceEnumerable)
            {
                var result = AdaptCollectionInternal<TDestination>(sourceEnumerable, destType, mapper);
                if (result != null)
                    custom?.Invoke(result, source);
                return result;
            }
            
            // 🚀 性能优化：快速路径 - 无自定义回调且无默认选项时
            if (custom == null)
            {
                var config = GetMapperConfiguration(mapper);
                if (config?.DefaultAdaptOptions == null)
                {
                    // 直接返回映射结果，避免额外检查
                    return mapper.Map<TDestination>(source);
                }
            }
            
            // 单对象映射
            var dest = mapper.Map<TDestination>(source);

            if (dest == null && !typeof(TDestination).IsValueType)
            {
                dest = CreateInstance<TDestination>();
            }

            // 自动应用默认 AdaptOptions（仅在有配置时）
            var config2 = GetMapperConfiguration(mapper);
            if (config2?.DefaultAdaptOptions != null && dest != null)
            {
                ApplyAdaptOptions(source, dest, config2.DefaultAdaptOptions);
            }

            if (dest != null)
                custom?.Invoke(dest, source);

            return dest;
        }

        /// <summary>
        /// 使用指定 IMapper 将对象映射为目标类型，支持自定义回调处理
        /// </summary>
        /// <typeparam name="TDestination">目标类型</typeparam>
        /// <param name="source">源对象</param>
        /// <param name="mapper">指定的 IMapper 实例</param>
        /// <param name="custom">可选的回调函数，参数为 (destination, source)</param>
        /// <returns>映射后的目标对象</returns>
        /// <remarks>
        /// 默认启用 FlexibleOption（忽略大小写和下划线），支持驼峰与下划线互转。
        /// 支持自动识别集合类型（List, IEnumerable, 数组等）。
        /// </remarks>
        /// <exception cref="System.MissingMethodException">目标类型没有无参构造函数</exception>
        public static TDestination? Adapt<TDestination>(
            this object source, IMapper mapper, System.Action<TDestination?, object>? custom = null)
        {
            var destType = typeof(TDestination);
            
            // 自动检测集合类型
            if (IsCollectionType(destType) && source is System.Collections.IEnumerable sourceEnumerable)
            {
                var result = AdaptCollectionInternal<TDestination>(sourceEnumerable, destType, mapper);
                if (result != null)
                    custom?.Invoke(result, source);
                return result;
            }
            
            // 🚀 性能优化：快速路径 - 无自定义回调且无默认选项时
            if (custom == null)
            {
                var config = GetMapperConfiguration(mapper);
                if (config?.DefaultAdaptOptions == null)
                {
                    // 直接返回映射结果，避免额外检查
                    return mapper.Map<TDestination>(source);
                }
            }
            
            // 单对象映射
            var dest = mapper.Map<TDestination>(source);
            
            if (dest == null && !typeof(TDestination).IsValueType)
            {
                dest = CreateInstance<TDestination>();
            }

            // 自动应用默认 AdaptOptions（仅在有配置时）
            var config2 = GetMapperConfiguration(mapper);
            if (config2?.DefaultAdaptOptions != null && dest != null)
            {
                ApplyAdaptOptions(source, dest, config2.DefaultAdaptOptions);
            }

            if (dest != null)
                custom?.Invoke(dest, source);

            return dest;
        }

        /// <summary>
        /// 创建目标类型的实例
        /// </summary>
        /// <typeparam name="T">要创建的类型</typeparam>
        /// <returns>创建的实例</returns>
        /// <exception cref="System.MissingMethodException">类型没有无参构造函数</exception>
        /// <exception cref="System.MemberAccessException">构造函数不可访问</exception>
        private static T CreateInstance<T>()
        {
            try
            {
                return (T)System.Activator.CreateInstance(typeof(T))!;
            }
            catch (System.MissingMethodException ex)
            {
                throw new System.MissingMethodException(
                    $"无法创建类型 '{typeof(T).FullName}' 的实例：该类型没有无参构造函数。", ex);
            }
            catch (System.MemberAccessException ex)
            {
                throw new System.MemberAccessException(
                    $"无法创建类型 '{typeof(T).FullName}' 的实例：构造函数不可访问。", ex);
            }
            catch (System.Exception ex)
            {
                throw new System.InvalidOperationException(
                    $"创建类型 '{typeof(T).FullName}' 的实例时发生异常：{ex.Message}", ex);
            }
        }

        #region 集合自动识别辅助方法

        /// <summary>
        /// 检测类型是否为支持的集合类型（List, IEnumerable, 数组等）
        /// </summary>
        private static bool IsCollectionType(System.Type type)
        {
            // 🚀 性能优化：从缓存中获取结果
            return _collectionTypeCache.GetOrAdd(type, t =>
            {
                if (t.IsArray)
                    return true;

                if (!t.IsGenericType)
                    return false;

                var genericDef = t.GetGenericTypeDefinition();

                return genericDef == typeof(List<>) ||
                       genericDef == typeof(IList<>) ||
                       genericDef == typeof(ICollection<>) ||
                       genericDef == typeof(IEnumerable<>);
            });
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

        #region 默认 AdaptOptions 辅助方法

        // 🚀 性能优化：缓存 FieldInfo 避免重复反射
        private static readonly System.Reflection.FieldInfo? _cachedConfigField = 
            typeof(Mapper).GetField("_config", 
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

        // 🚀 性能优化：缓存集合类型检测结果
        private static readonly System.Collections.Concurrent.ConcurrentDictionary<System.Type, bool> _collectionTypeCache 
            = new System.Collections.Concurrent.ConcurrentDictionary<System.Type, bool>();

        /// <summary>
        /// 从 IMapper 实例获取关联的 MapperConfiguration
        /// </summary>
        private static MapperConfiguration? GetMapperConfiguration(IMapper mapper)
        {
            if (mapper is Mapper internalMapper && _cachedConfigField != null)
            {
                return _cachedConfigField.GetValue(internalMapper) as MapperConfiguration;
            }
            return null;
        }

        #endregion

        #region 带 AdaptOptions 的 Adapt 扩展方法

        /// <summary>
        /// 使用映射规则选项进行映射
        /// </summary>
        /// <typeparam name="TDestination">目标类型</typeparam>
        /// <typeparam name="TSource">源类型</typeparam>
        /// <param name="source">源对象</param>
        /// <param name="mapper">IMapper 实例</param>
        /// <param name="options">映射规则选项（支持忽略大小写、下划线、指定属性等）</param>
        /// <param name="custom">可选的回调函数</param>
        /// <returns>映射后的目标对象</returns>
        public static TDestination? Adapt<TDestination, TSource>(
            this TSource source,
            IMapper mapper,
            AdaptOptions options,
            System.Action<TDestination?, TSource>? custom = null)
        {
            if (source == null) return default;
            if (options == null) options = AdaptOptions.Default;

            var dest = mapper.Map<TDestination>(source);

            if (dest == null && !typeof(TDestination).IsValueType)
            {
                dest = CreateInstance<TDestination>();
            }

            if (dest == null) return default;

            ApplyAdaptOptions(source, dest, options);

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
        /// 使用映射规则选项进行映射（自动推断源类型）
        /// </summary>
        /// <typeparam name="TDestination">目标类型</typeparam>
        /// <param name="source">源对象</param>
        /// <param name="mapper">IMapper 实例</param>
        /// <param name="options">映射规则选项（支持忽略大小写、下划线、指定属性等）</param>
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

            var dest = mapper.Map<TDestination>(source);

            if (dest == null && !typeof(TDestination).IsValueType)
            {
                dest = CreateInstance<TDestination>();
            }

            if (dest == null) return default;

            ApplyAdaptOptions(source, dest, options);

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

            // 🔥 如果没有启用特殊匹配规则，直接返回
            if (!options.IgnoreCase && !options.IgnoreUnderscore && 
                (options.IgnoreProperties == null || options.IgnoreProperties.Length == 0))
            {
                return;
            }

            var destType = typeof(TDestination);
            // 🔥 修复：使用 GetType() 获取运行时类型，而不是 typeof(TSource)
            var srcType = source.GetType();

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

                // 3. 获取源值
                var srcValue = srcProp.GetValue(source);
                
                // 4. 处理 IgnoreNullValues
                if (options.IgnoreNullValues && srcValue == null)
                {
                    // 不覆盖目标属性
                    continue;
                }

                // 5. 🔥 修复：总是尝试赋值（不仅仅是非 null 时）
                if (options.IgnoreCase || options.IgnoreUnderscore)
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
                        else if (srcValue != null)
                        {
                            // 尝试使用 Convert 转换
                            var converted = System.Convert.ChangeType(srcValue, destProp.PropertyType);
                            destProp.SetValue(dest, converted);
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
