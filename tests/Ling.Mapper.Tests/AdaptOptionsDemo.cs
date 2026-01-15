using Ling.Mapper;
using System;
using Ling.Mapper.Extensions;
using Ling.Mapper.Models;

namespace TestConsole;

/// <summary>
/// 演示 Adapt 方法配合 AdaptOptions 的功能
/// </summary>
public static class AdaptOptionsDemo
{
    public static void Run()
    {
        Console.WriteLine("\n=== AdaptOptions 映射规则功能演示 ===");

        var mapper = MapperProvider.Current ?? throw new InvalidOperationException("请先设置全局 Mapper");


        // 3. 测试组合规则（忽略大小写 + 下划线）
        TestFlexibleOption(mapper);

        // 4. 测试忽略指定属性
        TestIgnoreProperties(mapper);

        // 5. 测试忽略 null 值
        TestIgnoreNullValues(mapper);

        // 6. 测试组合所有规则
        TestCombinedOptions(mapper);
    }

    private static void TestFlexibleOption(IMapper mapper)
    {
        Console.WriteLine("\n--- 测试 FlexibleOption：忽略大小写 + 下划线 ---");

        var source = new MixedDto
        {
            User_Name = "wangwu",   // 下划线 + 大小写
            USER_ID = 3003,         // 全大写 + 下划线
            Email = "wangwu@example.com"
        };

        // 使用灵活规则（同时忽略大小写和下划线）
        var target = source.Adapt<UserDto>(AdaptOptions.FlexibleOption);
        Console.WriteLine($"灵活规则 - UserName: {target?.UserName ?? "null"}, UserId: {target?.UserId}, Email: {target?.Email}");
    }

    private static void TestIgnoreProperties(IMapper mapper)
    {
        Console.WriteLine("\n--- 测试 IgnoreProperties：忽略指定属性 ---");

        var source = new UserDto
        {
            UserName = "zhaoliu",
            UserId = 4004,
            Email = "zhaoliu@example.com",
            Password = "secret123",
            CreditCard = "1234-5678-9012-3456"
        };
    }

    private static void TestIgnoreNullValues(IMapper mapper)
    {
        Console.WriteLine("\n--- 测试 IgnoreNullValues：忽略 null 值 ---");

        var source = new UserDto
        {
            UserName = "qianqi",
            UserId = 5005,
            Email = null,        // null 值
            Password = null,     // null 值
            CreditCard = "9999-9999-9999-9999"
        };
    }

    private static void TestCombinedOptions(IMapper mapper)
    {
        Console.WriteLine("\n--- 测试组合规则：所有选项一起使用 ---");

        var source = new ComplexDto
        {
            user_name = "sunba",
            USER_ID = 6006,
            email = null,           // null 值
            password = "secret",
            credit_card = "1234"
        };
    }
}

// 测试用的 DTO 类
public class ApiResponseDto
{
    public string? username { get; set; }
    public int USERID { get; set; }
    public string? Email { get; set; }
}

public class DatabaseDto
{
    public string? user_name { get; set; }
    public int user_id { get; set; }
    public string? email_address { get; set; }
}

public class MixedDto
{
    public string? User_Name { get; set; }
    public int USER_ID { get; set; }
    public string? Email { get; set; }
}

public class ComplexDto
{
    public string? user_name { get; set; }
    public int USER_ID { get; set; }
    public string? email { get; set; }
    public string? password { get; set; }
    public string? credit_card { get; set; }
}

public class UserDto
{
    public string? UserName { get; set; }
    public int UserId { get; set; }
    public string? Email { get; set; }
    public string? Password { get; set; }
    public string? CreditCard { get; set; }
}
