using System;
using System.Linq;
using System.Reflection;
using Microsoft.Extensions.DependencyInjection;

namespace Ling.Mapper
{
    /// <summary>
    /// Microsoft.Extensions.DependencyInjection 扩展方法。
    /// 用于将 FluentMapper 注册到依赖注入容器中并自动扫描 Profile。
    /// </summary>
    public static class FluentMapperServiceCollectionExtensions
    {
        /// <summary>
        /// 向 DI 容器中注册 FluentMapper。
        /// 支持传入配置委托并可自动扫描指定程序集中的 MapperProfile。
        /// </summary>
        /// <param name="services">服务集合。</param>
        /// <param name="configAction">用于配置 MapperConfiguration 的委托。</param>
        /// <param name="scanAssemblies">要扫描 Profile 的程序集（可选）。</param>
        /// <returns>服务集合本身，方便链式调用。</returns>
        public static IServiceCollection AddFluentMapper(
            this IServiceCollection services,
            Action<MapperConfiguration>? configAction = null,
            params Assembly[] scanAssemblies)
        {
            var cfg = new MapperConfiguration();
            configAction?.Invoke(cfg);

            // 扫描程序集中的 MapperProfile 并注册
            if (scanAssemblies == null || scanAssemblies.Length == 0)
            {
                scanAssemblies = new[] { Assembly.GetEntryAssembly() ?? Assembly.GetCallingAssembly() };
            }

            foreach (var asm in scanAssemblies.Where(a => a != null))
            {
                foreach (var type in asm!.GetTypes())
                {
                    if (!type.IsAbstract && typeof(MapperProfile).IsAssignableFrom(type))
                    {
                        if (Activator.CreateInstance(type) is MapperProfile profile)
                        {
                            cfg.AddProfile(profile);
                        }
                    }
                }
            }

            var mapper = cfg.CreateMapper();

            services.AddSingleton(cfg);
            services.AddSingleton<IMapper>(mapper);

            // register global provider for convenience
            try
            {
                MapperProvider.SetCurrent(mapper);
            }
            catch
            {
                // ignore if unable to set
            }

            return services;
        }
    }
}
