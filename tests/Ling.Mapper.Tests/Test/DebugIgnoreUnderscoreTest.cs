using System;
using Ling.Mapper.Extensions;
using Ling.Mapper.Models;

namespace TestConsole.Test;

/// <summary>
/// 调试 IgnoreUnderscore 选项
/// </summary>
public static class DebugIgnoreUnderscoreTest
{
    public static void Run()
    {
        Console.WriteLine("\n=== Debug IgnoreUnderscore 测试 ===\n");

        // 测试 1: 基础下划线忽略
        Test1_BasicUnderscore();

        // 测试 2: 大小写 + 下划线
        Test2_CaseAndUnderscore();

        // 测试 3: 多个下划线
        Test3_MultipleUnderscores();

        Console.WriteLine("\n=== 测试完成 ===\n");
    }

    private static void Test1_BasicUnderscore()
    {
        Console.WriteLine("【测试1】基础下划线忽略");
        Console.WriteLine("说明: IgnoreUnderscore 只移除下划线，不改变大小写");
        Console.WriteLine();

        // ✅ 正确案例：大小写匹配
        var source1 = new { Id = 1, User_Name = "Jane" }; // User_Name (Pascal Case)
        Console.WriteLine($"源属性 (正确): Id={source1.Id}, User_Name={source1.User_Name}");

        var target1 = source1.Adapt<TestTarget>(AdaptOptions.IgnoreUnderscore);
        Console.WriteLine($"IgnoreUnderscore: Id={target1?.Id}, UserName={target1?.UserName ?? "null"}");
        
        if (target1?.UserName == "Jane")
        {
            Console.WriteLine("  ✅ 成功映射 User_Name → UserName");
        }
        else
        {
            Console.WriteLine($"  ❌ 失败: 期望 'Jane', 实际 '{target1?.UserName ?? "null"}'");
        }
        Console.WriteLine();

        // ❌ 错误案例：大小写不匹配
        var source2 = new { Id = 2, user_name = "Bob" }; // user_name (小写)
        Console.WriteLine($"源属性 (错误): Id={source2.Id}, user_name={source2.user_name}");
        
        var target2 = source2.Adapt<TestTarget>(AdaptOptions.IgnoreUnderscore);
        Console.WriteLine($"IgnoreUnderscore: Id={target2?.Id}, UserName={target2?.UserName ?? "null"}");
        
        if (target2?.UserName == null || target2?.UserName == "")
        {
            Console.WriteLine("  ✅ 正确: user_name 因大小写不匹配而未映射");
        }
        else
        {
            Console.WriteLine($"  ❌ 错误: user_name 不应该映射到 UserName");
        }
        Console.WriteLine();

        // ✅ 正确方案：使用 FlexibleOption
        var source3 = new { Id = 3, user_name = "Alice" }; // user_name (小写)
        Console.WriteLine($"源属性 (FlexibleOption): Id={source3.Id}, user_name={source3.user_name}");
        
        var target3 = source3.Adapt<TestTarget>(AdaptOptions.FlexibleOption);
        Console.WriteLine($"FlexibleOption: Id={target3?.Id}, UserName={target3?.UserName ?? "null"}");
        
        if (target3?.UserName == "Alice")
        {
            Console.WriteLine("  ✅ FlexibleOption 成功映射 user_name → UserName");
        }
        else
        {
            Console.WriteLine($"  ❌ 失败");
        }

        Console.WriteLine();
    }

    private static void Test2_CaseAndUnderscore()
    {
        Console.WriteLine("【测试2】大小写 + 下划线");

        // 小写 + 下划线
        var source = new { id = 2, user_name = "Bob", first_NAME = "Alice" };
        
        Console.WriteLine($"源属性: id={source.id}, user_name={source.user_name}, first_NAME={source.first_NAME}");

        // 只忽略下划线（不忽略大小写）
        var target1 = source.Adapt<TestTarget2>(AdaptOptions.IgnoreUnderscore);
        Console.WriteLine($"IgnoreUnderscore: Id={target1?.Id}, UserName={target1?.UserName ?? "null"}, FirstName={target1?.FirstName ?? "null"}");
        
        // 忽略大小写 + 下划线
        var target2 = source.Adapt<TestTarget2>(AdaptOptions.FlexibleOption);
        Console.WriteLine($"FlexibleOption: Id={target2?.Id}, UserName={target2?.UserName ?? "null"}, FirstName={target2?.FirstName ?? "null"}");
        
        if (target2?.UserName == "Bob" && target2?.FirstName == "Alice")
        {
            Console.WriteLine("  ✅ FlexibleOption 成功");
        }
        else
        {
            Console.WriteLine($"  ❌ FlexibleOption 失败");
        }

        Console.WriteLine();
    }

    private static void Test3_MultipleUnderscores()
    {
        Console.WriteLine("【测试3】多个下划线");

        var source = new { user___name = "Multiple", _prefix = "Pre", suffix_ = "Post" };
        
        Console.WriteLine($"源属性: user___name={source.user___name}, _prefix={source._prefix}, suffix_={source.suffix_}");

        var target = source.Adapt<TestTarget3>(AdaptOptions.IgnoreUnderscore);
        Console.WriteLine($"结果: UserName={target?.UserName ?? "null"}, Prefix={target?.Prefix ?? "null"}, Suffix={target?.Suffix ?? "null"}");
        
        if (target?.UserName == "Multiple")
        {
            Console.WriteLine("  ✅ 多个下划线处理成功");
        }
        else
        {
            Console.WriteLine($"  ❌ 失败: user___name 未映射到 UserName");
        }

        Console.WriteLine();
    }

    // 测试模型
    private class TestTarget
    {
        public int Id { get; set; }
        public string? UserName { get; set; }
    }

    private class TestTarget2
    {
        public int Id { get; set; }
        public string? UserName { get; set; }
        public string? FirstName { get; set; }
    }

    private class TestTarget3
    {
        public string? UserName { get; set; }
        public string? Prefix { get; set; }
        public string? Suffix { get; set; }
    }
}
