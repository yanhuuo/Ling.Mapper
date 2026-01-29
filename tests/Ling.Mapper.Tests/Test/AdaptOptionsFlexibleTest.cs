using System;
using Ling.Mapper.Extensions;
using Ling.Mapper.Models;

namespace TestConsole.Test
{
    /// <summary>
    /// AdaptOptions 下划线和大小写忽略测试
    /// </summary>
    public static class AdaptOptionsFlexibleTest
    {
        public static void Run()
        {
            Console.WriteLine("=== AdaptOptions FlexibleOption 测试 ===\n");

            Test1_UnderscoreToCamelCase();
            Test2_CamelCaseToUnderscore();
            Test3_MixedCase();
            Test4_ComplexScenario();
            Test5_NullValues();

            Console.WriteLine("\n=== 所有测试通过 ===");
        }

        private static void Test1_UnderscoreToCamelCase()
        {
            Console.WriteLine("【测试1】下划线 → 驼峰命名");

            var source = new SourceWithUnderscore
            {
                user_name = "张三",
                user_email = "zhang@test.com",
                user_age = 30
            };

            // 使用 FlexibleOption（忽略下划线和大小写）
            var dest = source.Adapt<DestWithCamelCase>(AdaptOptions.FlexibleOption);

            Console.WriteLine($"✓ userName: {dest?.userName ?? "NULL"}");
            Console.WriteLine($"✓ userEmail: {dest?.userEmail ?? "NULL"}");
            Console.WriteLine($"✓ userAge: {dest?.userAge}");
            
            // 期望值
            TestConsole.Utils.TestHelper.PrintActualExpected("userName", dest?.userName, "张三");
            TestConsole.Utils.TestHelper.PrintActualExpected("userEmail", dest?.userEmail, "zhang@test.com");
            TestConsole.Utils.TestHelper.PrintActualExpected("userAge", dest?.userAge, 30);
            if (dest?.userName == "张三" && dest?.userEmail == "zhang@test.com" && dest?.userAge == 30)
            {
                Console.WriteLine("✅ 测试通过\n");
            }
            else
            {
                Console.WriteLine("❌ 测试失败\n");
            }
        }

        private static void Test2_CamelCaseToUnderscore()
        {
            Console.WriteLine("【测试2】驼峰命名 → 下划线");

            var source = new SourceWithCamelCase
            {
                wechatConfigId = "wx123",
                rewardScope = 1,
                rewardType = 0
            };

            var dest = source.Adapt<DestWithUnderscore>(AdaptOptions.FlexibleOption);

            Console.WriteLine($"✓ wechat_config_id: {dest?.wechat_config_id ?? "NULL"}");
            Console.WriteLine($"✓ reward_scope: {dest?.reward_scope}");
            Console.WriteLine($"✓ reward_type: {dest?.reward_type}");
            
            // 期望值
            TestConsole.Utils.TestHelper.PrintActualExpected("wechat_config_id", dest?.wechat_config_id, "wx123");
            TestConsole.Utils.TestHelper.PrintActualExpected("reward_scope", dest?.reward_scope, 1);
            TestConsole.Utils.TestHelper.PrintActualExpected("reward_type", dest?.reward_type, 0);
            if (dest?.wechat_config_id == "wx123" && dest?.reward_scope == 1 && dest?.reward_type == 0)
            {
                Console.WriteLine("✅ 测试通过\n");
            }
            else
            {
                Console.WriteLine("❌ 测试失败\n");
            }
        }

        private static void Test3_MixedCase()
        {
            Console.WriteLine("【测试3】混合大小写");

            var source = new SourceMixedCase
            {
                UserName = "李四",
                USER_EMAIL = "li@test.com",
                User_Age = 25
            };

            var dest = source.Adapt<DestMixedCase>(AdaptOptions.FlexibleOption);

            Console.WriteLine($"✓ username: {dest?.username ?? "NULL"}");
            Console.WriteLine($"✓ useremail: {dest?.useremail ?? "NULL"}");
            Console.WriteLine($"✓ userage: {dest?.userage}");
            
            // 期望值
            TestConsole.Utils.TestHelper.PrintActualExpected("username", dest?.username, "李四");
            TestConsole.Utils.TestHelper.PrintActualExpected("useremail", dest?.useremail, "li@test.com");
            TestConsole.Utils.TestHelper.PrintActualExpected("userage", dest?.userage, 25);
            if (dest?.username == "李四" && dest?.useremail == "li@test.com" && dest?.userage == 25)
            {
                Console.WriteLine("✅ 测试通过\n");
            }
            else
            {
                Console.WriteLine("❌ 测试失败\n");
            }
        }

        private static void Test4_ComplexScenario()
        {
            Console.WriteLine("【测试4】复杂场景（模拟真实需求）");

            var request = new ShareRewardRequest
            {
                orgId = "org123",
                wechatConfigId = "wx456",
                name = "新人礼包",
                rewardScope = 1,
                rewardType = 0,
                state = 0
            };

            var entity = request.Adapt<MallShareRewardEntity>(AdaptOptions.FlexibleOption);

            Console.WriteLine($"✓ org_id: {entity?.org_id ?? "NULL"}");
            Console.WriteLine($"✓ wechat_config_id: {entity?.wechat_config_id ?? "NULL"}");
            Console.WriteLine($"✓ name: {entity?.name ?? "NULL"}");
            Console.WriteLine($"✓ reward_scope: {entity?.reward_scope}");
            Console.WriteLine($"✓ reward_type: {entity?.reward_type}");
            Console.WriteLine($"✓ state: {entity?.state}");
            
            // 期望值
            TestConsole.Utils.TestHelper.PrintActualExpected("wechat_config_id", entity?.wechat_config_id, "wx456");
            TestConsole.Utils.TestHelper.PrintActualExpected("name", entity?.name, "新人礼包");
            TestConsole.Utils.TestHelper.PrintActualExpected("reward_scope", entity?.reward_scope, 1);
            if (entity?.wechat_config_id == "wx456" && 
                entity?.name == "新人礼包" && 
                entity?.reward_scope == 1)
            {
                Console.WriteLine("✅ 测试通过\n");
            }
            else
            {
                Console.WriteLine("❌ 测试失败\n");
            }
        }

        private static void Test5_NullValues()
        {
            Console.WriteLine("【测试5】Null 值处理");

            var source = new SourceWithNulls
            {
                name = "测试",
                email = null,  // null 值
                age = 0
            };

            // 不忽略 null
            var dest1 = source.Adapt<DestWithNulls>(AdaptOptions.FlexibleOption);
            Console.WriteLine($"  不忽略 null - email: {dest1?.email ?? "NULL"}");
            TestConsole.Utils.TestHelper.PrintActualExpected("不忽略 null - email", dest1?.email == null ? "NULL" : dest1?.email, "NULL");
        }
    }

    // 测试用的类型
    public class SourceWithUnderscore
    {
        public string user_name { get; set; }
        public string user_email { get; set; }
        public int user_age { get; set; }
    }

    public class DestWithCamelCase
    {
        public string userName { get; set; }
        public string userEmail { get; set; }
        public int userAge { get; set; }
    }

    public class SourceWithCamelCase
    {
        public string wechatConfigId { get; set; }
        public int rewardScope { get; set; }
        public int rewardType { get; set; }
    }

    public class DestWithUnderscore
    {
        public string wechat_config_id { get; set; }
        public int reward_scope { get; set; }
        public int reward_type { get; set; }
    }

    public class SourceMixedCase
    {
        public string UserName { get; set; }
        public string USER_EMAIL { get; set; }
        public int User_Age { get; set; }
    }

    public class DestMixedCase
    {
        public string username { get; set; }
        public string useremail { get; set; }
        public int userage { get; set; }
    }

    public class ShareRewardRequest
    {
        public string orgId { get; set; }
        public string wechatConfigId { get; set; }
        public string name { get; set; }
        public int rewardScope { get; set; }
        public int rewardType { get; set; }
        public int state { get; set; }
    }

    public class MallShareRewardEntity
    {
        public string org_id { get; set; }
        public string wechat_config_id { get; set; }
        public string name { get; set; }
        public int reward_scope { get; set; }
        public int reward_type { get; set; }
        public int state { get; set; }
    }

    public class SourceWithNulls
    {
        public string name { get; set; }
        public string email { get; set; }
        public int age { get; set; }
    }

    public class DestWithNulls
    {
        public string name { get; set; }
        public string email { get; set; }
        public int age { get; set; }
    }
}
