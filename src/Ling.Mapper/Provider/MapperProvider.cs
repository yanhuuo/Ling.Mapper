using System;

namespace Ling.Mapper
{
    /// <summary>
    /// 全局默认映射器提供者（可选组件）。
    /// 用于快速调用 Adapt/Map 扩展方法时自动注入 IMapper。
    /// 在 DI 容器中注册或手动创建 Mapper 后，通过 FluentMapperServiceCollectionExtensions 或手动调用该实例。
    /// </summary>
    public static class MapperProvider
    {
        private static IMapper? _current;

        /// <summary>
        /// 获取当前全局 Mapper 实例。
        /// </summary>
        public static IMapper? Current => _current;

        /// <summary>
        /// 设置当前全局 Mapper 实例。
        /// </summary>
        /// <param name="mapper">要设置的 IMapper 实例</param>
        /// <exception cref="ArgumentNullException">mapper 为 null 时抛出</exception>
        public static void SetCurrent(IMapper mapper)
        {
            _current = mapper ?? throw new ArgumentNullException(nameof(mapper));
        }

        /// <summary>
        /// 清除当前全局 Mapper 实例。
        /// </summary>
        public static void Clear() => _current = null;
    }
}
