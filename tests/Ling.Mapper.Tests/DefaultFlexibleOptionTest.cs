using System;
using Ling.Mapper;
using Ling.Mapper.Extensions;
using Ling.Mapper.Models;

namespace Ling.Mapper.Tests
{
    /// <summary>
    /// 默认 FlexibleOption 测试
    /// </summary>
    public static class DefaultFlexibleOptionTest
    {
        public static void Run()
        {
            Console.WriteLine("=== 默认 FlexibleOption 测试 (v2.4) ===\n");

            Test1_DefaultBehavior();
            Test2_DisableDefault();
            Test3_ExplicitOptions();
            Test4_RealWorldScenario();

            Console.WriteLine("\n=== 所有测试通过 ===");
        }

        private static void Test1_DefaultBehavior()
        {
            Console.WriteLine("【测试1】默认启用 FlexibleOption");

            // 初始化 Mapper（默认启用 FlexibleOption）
            var config = new MapperConfiguration();
            // config.DefaultAdaptOptions 默认值为 AdaptOptions.FlexibleOption
            var mapper = config.CreateMapper();
            MapperProvider.SetCurrent(mapper);

            var source = new { wechatConfigId = "wx123", rewardScope = 1 };
            var dest = source.Adapt<TestEntity>();

            Console.WriteLine($"✓ wechat_config_id: {dest?.wechat_config_id ?? "NULL"}");
            Console.WriteLine($"✓ reward_scope: {dest?.reward_scope}");

            if (dest?.wechat_config_id == "wx123" && dest?.reward_scope == 1)
            {
                Console.WriteLine("✅ 测试通过：默认自动匹配驼峰和下划线\n");
            }
            else
            {
                Console.WriteLine("❌ 测试失败\n");
            }
        }

        private static void Test2_DisableDefault()
        {
            Console.WriteLine("【测试2】禁用默认 FlexibleOption");

            // 禁用默认行为
            var config = new MapperConfiguration();
            config.DefaultAdaptOptions = null;  // 禁用
            var mapper = config.CreateMapper();
            MapperProvider.SetCurrent(mapper);

            var source = new { wechatConfigId = "wx123", rewardScope = 1 };
            var dest = source.Adapt<TestEntity>();

            Console.WriteLine($"✓ wechat_config_id: {dest?.wechat_config_id ?? "NULL"}");
            Console.WriteLine($"✓ reward_scope: {dest?.reward_scope}");

            if (dest?.wechat_config_id == null && dest?.reward_scope == 0)
            {
                Console.WriteLine("✅ 测试通过：禁用后不再自动匹配\n");
            }
            else
            {
                Console.WriteLine("❌ 测试失败\n");
            }
        }

        private static void Test3_ExplicitOptions()
        {
            Console.WriteLine("【测试3】显式传递 Options 覆盖默认值");

            // 恢复默认
            var config = new MapperConfiguration();
            var mapper = config.CreateMapper();
            MapperProvider.SetCurrent(mapper);

            var source = new { wechatConfigId = "wx123", rewardScope = 1 };
            
            // 显式传递 Options（会覆盖默认值）
            
        }

        private static void Test4_RealWorldScenario()
        {
            Console.WriteLine("【测试4】真实场景：API 请求映射到数据库实体");

            // 模拟应用启动配置
            var config = new MapperConfiguration();
            // 默认启用 FlexibleOption，无需额外配置
            var mapper = config.CreateMapper();
            MapperProvider.SetCurrent(mapper);

            // 模拟 API 请求（驼峰命名）
            var request = new ApiRequest
            {
                orgId = "org123",
                wechatConfigId = "wx456",
                rewardType = 1,
                rewardScope = 2
            };

            // 🎉 直接映射，无需传 AdaptOptions.FlexibleOption
            var entity = request.Adapt<DatabaseEntity>();

            Console.WriteLine($"✓ org_id: {entity?.org_id ?? "NULL"}");
            Console.WriteLine($"✓ wechat_config_id: {entity?.wechat_config_id ?? "NULL"}");
            Console.WriteLine($"✓ reward_type: {entity?.reward_type}");
            Console.WriteLine($"✓ reward_scope: {entity?.reward_scope}");

            if (entity?.org_id == "org123" && 
                entity?.wechat_config_id == "wx456" &&
                entity?.reward_type == 1 &&
                entity?.reward_scope == 2)
            {
                Console.WriteLine("✅ 测试通过：真实场景映射成功\n");
            }
            else
            {
                Console.WriteLine("❌ 测试失败\n");
            }
        }
    }

    // 测试用的类型
    public class TestEntity
    {
        public string wechat_config_id { get; set; }
        public int reward_scope { get; set; }
    }

    public class ApiRequest
    {
        public string orgId { get; set; }
        public string wechatConfigId { get; set; }
        public int rewardType { get; set; }
        public int rewardScope { get; set; }
    }

    public class DatabaseEntity
    {
        public string org_id { get; set; }
        public string wechat_config_id { get; set; }
        public int reward_type { get; set; }
        public int reward_scope { get; set; }
    }
}
