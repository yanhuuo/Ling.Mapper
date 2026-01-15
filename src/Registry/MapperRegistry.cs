using System;
using System.Collections.Concurrent;

namespace Ling.Mapper.Registry
{
    /// <summary>
    /// 映射器注册表（最高优先级）。
    /// 
    /// 语义说明：
    /// - Registry 中的映射被视为“已完全定义规则”的映射
    /// - 不再叠加 AdaptOptions（IgnoreCase / IgnoreNull / IgnoreProperties 等）
    /// - 优先级高于自动 CompileMapper
    /// - 适用于：手写高性能映射、Source Generator 生成映射
    /// </summary>
    public static class MapperRegistry
    {
        /// <summary>
        /// 强类型映射委托（用于诊断 / 高级用途）
        /// key: (SourceType, DestinationType)
        /// </summary>
        private static readonly ConcurrentDictionary<(Type, Type), Delegate> _typed
            = new ConcurrentDictionary<(Type, Type), Delegate>();

        /// <summary>
        /// 统一运行路径的 wrapper 委托
        /// key: (SourceType, DestinationType)
        /// value: Func&lt;object, object?&gt;
        /// </summary>
        private static readonly ConcurrentDictionary<(Type, Type), Func<object, object?>> _wrapped
            = new ConcurrentDictionary<(Type, Type), Func<object, object?>>();

        #region 注册（强类型，推荐）

        /// <summary>
        /// 注册强类型映射委托（推荐）。
        /// 
        /// 特点：
        /// - 无反射
        /// - 无 DynamicInvoke
        /// - JIT / AOT 友好
        /// </summary>
        public static void Register<TSource, TDestination>(
            Func<TSource, TDestination> mapper)
        {
            if (mapper == null)
                throw new ArgumentNullException(nameof(mapper));

            var key = (typeof(TSource), typeof(TDestination));

            _typed[key] = mapper;

            // ⚠️ 注意：这里不能使用 static lambda
            // 因为 mapper 是参数，属于被捕获变量
            _wrapped[key] = src =>
            {
                return src is TSource typed
                    ? mapper(typed)
                    : default;
            };
        }

        #endregion

        #region 注册（非泛型，兼容场景，不推荐）

        /// <summary>
        /// 注册非泛型映射委托（兼容接口 / 运行时场景）。
        /// 
        /// 注意：
        /// - 内部使用 DynamicInvoke
        /// - 性能低于泛型版本
        /// - 仅在无法使用泛型时使用
        /// </summary>
        public static void Register(
            Type sourceType,
            Type destinationType,
            Delegate mapper)
        {
            if (sourceType == null)
                throw new ArgumentNullException(nameof(sourceType));
            if (destinationType == null)
                throw new ArgumentNullException(nameof(destinationType));
            if (mapper == null)
                throw new ArgumentNullException(nameof(mapper));

            var key = (sourceType, destinationType);

            _typed[key] = mapper;

            _wrapped[key] = src =>
            {
                // DynamicInvoke 仅在该分支出现
                return mapper.DynamicInvoke(src);
            };
        }

        #endregion

        #region 查询（供 Mapper 内部使用）

        /// <summary>
        /// 尝试获取已注册的强类型委托。
        /// 
        /// 说明：
        /// - 仅用于调试 / 特殊用途
        /// - Mapper 正常执行路径应使用 TryGetWrapper
        /// </summary>
        public static bool TryGet(
            Type sourceType,
            Type destinationType,
            out Delegate? mapper)
        {
            return _typed.TryGetValue((sourceType, destinationType), out mapper);
        }

        /// <summary>
        /// 尝试获取统一 wrapper 委托（Mapper 的主要使用入口）。
        /// </summary>
        internal static bool TryGetWrapper(
            Type sourceType,
            Type destinationType,
            out Func<object, object?>? mapper)
        {
            return _wrapped.TryGetValue((sourceType, destinationType), out mapper);
        }

        #endregion

        #region 可选：清理（测试 / 热重载）

        /// <summary>
        /// 清空注册表（通常仅用于测试或热重载）。
        /// </summary>
        public static void Clear()
        {
            _typed.Clear();
            _wrapped.Clear();
        }

        #endregion
    }
}
