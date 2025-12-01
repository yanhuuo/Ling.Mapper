using System;

namespace Ling.Mapper
{
    /// <summary>
    /// 全局默认映射器提供者（可选）。
    /// 用于快速调用 Adapt/Map 无需显式传入 IMapper。
    /// 在 DI 注册或者手动创建 Mapper 后可通过 FluentMapperServiceCollectionExtensions 或者手动设置该实例。
    /// </summary>
    public static class MapperProvider
    {
        private static IMapper? _current;

        public static IMapper? Current => _current;

        public static void SetCurrent(IMapper mapper)
        {
            _current = mapper ?? throw new ArgumentNullException(nameof(mapper));
        }

        public static void Clear() => _current = null;
    }
}
