using System;

namespace Ling.Mapper
{
    /// <summary>
    /// 全局默认映射器提供者（可选组件）。
    /// 用于快速调用 Adapt/Map 扩展方法时自动注入 IMapper。
    /// 在 DI 容器中注册或手动创建 Mapper 后，通过 FluentMapperServiceCollectionExtensions 或手动调用该实例。
    /// </summary>
    /// <remarks>
    /// <para>如果未手动设置 Mapper，在首次访问时会自动创建一个使用默认配置的 Mapper 实例。</para>
    /// <para>这样您就可以直接使用 <c>source.Adapt&lt;TargetDto&gt;()</c> 而无需事先设置全局 Mapper。</para>
    /// </remarks>
    public static class MapperProvider
    {
        private static IMapper? _current;
        private static readonly object _lock = new object();
        private static bool _isInitialized = false;

        /// <summary>
        /// 获取当前全局 Mapper 实例。如果未设置，将自动创建一个默认实例。
        /// </summary>
        public static IMapper Current
        {
            get
            {
                if (_current == null && !_isInitialized)
                {
                    lock (_lock)
                    {
                        if (_current == null && !_isInitialized)
                        {
                            _current = CreateDefaultMapper();
                            _isInitialized = true;
                        }
                    }
                }
                return _current!;
            }
        }

        /// <summary>
        /// 设置当前全局 Mapper 实例。
        /// </summary>
        /// <param name="mapper">要设置的 IMapper 实例</param>
        /// <exception cref="ArgumentNullException">mapper 为 null 时抛出</exception>
        public static void SetCurrent(IMapper mapper)
        {
            lock (_lock)
            {
                _current = mapper ?? throw new ArgumentNullException(nameof(mapper));
                _isInitialized = true;
            }
        }

        /// <summary>
        /// 清除当前全局 Mapper 实例，下次访问时将重新创建默认实例。
        /// </summary>
        public static void Clear()
        {
            lock (_lock)
            {
                _current = null;
                _isInitialized = false;
            }
        }

        /// <summary>
        /// 创建默认的 Mapper 实例
        /// </summary>
        private static IMapper CreateDefaultMapper()
        {
            var config = new MapperConfiguration();
            return config.CreateMapper();
        }
    }
}
