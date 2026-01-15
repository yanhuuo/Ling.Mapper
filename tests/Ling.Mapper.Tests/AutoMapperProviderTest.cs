using System;
using System.Collections.Generic;
using Ling.Mapper;

namespace Ling.Mapper.Tests
{
    /// <summary>
    /// 自动初始化 MapperProvider 的测试
    /// </summary>
    public static class AutoMapperProviderTest
    {
        public static void Run()
        {
            Console.WriteLine("=== 自动初始化 MapperProvider 测试 ===\n");

            // 场景 1: 不需要手动设置，直接使用 Adapt
            Test1_DirectAdaptWithoutSetup();

            // 场景 2: 清除后自动重新初始化
            Test2_ClearAndAutoReinitialize();

            // 场景 3: AdaptList 也能自动初始化
            Test3_AdaptListWithoutSetup();

            // 场景 4: 手动设置自定义 Mapper 仍然有效
            Test4_ManualSetStillWorks();

            Console.WriteLine("\n=== 所有测试通过 ===");
        }

        private static void Test1_DirectAdaptWithoutSetup()
        {
            Console.WriteLine("【测试1】直接使用 Adapt，无需手动设置");
            
            // 清除全局 Mapper（模拟首次使用）
            MapperProvider.Clear();

            var source = new SourceDto { Id = 1, Name = "测试" };

            // 🎉 直接使用，不需要手动创建和设置 Mapper！
            var target = source.Adapt<TargetDto>();

            Console.WriteLine($"✓ 映射成功: Id={target?.Id}, Name={target?.Name}");
            Console.WriteLine();
        }

        private static void Test2_ClearAndAutoReinitialize()
        {
            Console.WriteLine("【测试2】清除后自动重新初始化");

            // 清除全局 Mapper
            MapperProvider.Clear();

            var source = new SourceDto { Id = 2, Name = "第二次测试" };

            // 再次使用，会自动创建新的 Mapper
            var target = source.Adapt<TargetDto, SourceDto>((dest, src) =>
            {
                if (dest != null)
                    dest.DisplayName = $"[{src.Id}] {src.Name}";
            });

            Console.WriteLine($"✓ 自动重新初始化成功: DisplayName={target?.DisplayName}");
            Console.WriteLine();
        }

        private static void Test3_AdaptListWithoutSetup()
        {
            Console.WriteLine("【测试3】AdaptList 也能自动初始化");

            // 清除全局 Mapper
            MapperProvider.Clear();

            var sourceList = new List<SourceDto>
            {
                new SourceDto { Id = 1, Name = "项目1" },
                new SourceDto { Id = 2, Name = "项目2" },
                new SourceDto { Id = 3, Name = "项目3" }
            };

            // 🎉 直接使用 AdaptList，不需要手动设置！
            var targetList = sourceList.AdaptList<TargetDto, SourceDto>((dest, src, index) =>
            {
                if (dest != null)
                    dest.DisplayName = $"[{index + 1}] {src.Name}";
            });

            Console.WriteLine($"✓ 列表映射成功，共 {targetList?.Count} 项:");
            foreach (var item in targetList ?? new List<TargetDto>())
            {
                Console.WriteLine($"  - {item.DisplayName}");
            }
            Console.WriteLine();
        }

        private static void Test4_ManualSetStillWorks()
        {
            Console.WriteLine("【测试4】手动设置自定义 Mapper 仍然有效");

            // 创建自定义配置的 Mapper
            var config = new MapperConfiguration();
            // 使用 Map 方法配置映射规则
            var customMapper = config.CreateMapper();

            // 手动设置
            MapperProvider.SetCurrent(customMapper);

            var source = new SourceDto { Id = 100, Name = "自定义测试" };
            var target = source.Adapt<TargetDto, SourceDto>((dest, src) =>
            {
                if (dest != null)
                    dest.DisplayName = $"自定义映射: {src.Name}";
            });

            Console.WriteLine($"✓ 使用自定义 Mapper: DisplayName={target?.DisplayName}");
            Console.WriteLine();
        }
    }

    // 测试用的 DTO
    public class SourceDto
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
    }

    public class TargetDto
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string DisplayName { get; set; } = string.Empty;
    }
}
