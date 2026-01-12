using Ling.Mapper;
using System;

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

        // 1. 测试忽略大小写匹配
        TestIgnoreCase(mapper);

        // 2. 测试忽略下划线匹配
        TestIgnoreUnderscore(mapper);

        // 3. 测试组合规则（忽略大小写 + 下划线）
        TestFlexibleOption(mapper);

        // 4. 测试忽略指定属性
        TestIgnoreProperties(mapper);

        // 5. 测试忽略 null 值
        TestIgnoreNullValues(mapper);

        // 6. 测试组合所有规则
        TestCombinedOptions(mapper);
    }

    private static void TestIgnoreCase(IMapper mapper)
    {
        Console.WriteLine("\n--- 测试 IgnoreCase：忽略大小写匹配 ---");

        var source = new ApiResponseDto
        {
            username = "zhangsan",  // 小写
            USERID = 1001,          // 大写
            Email = "zhangsan@example.com"
        };

        // 默认情况下（不忽略大小写）
        var target1 = source.Adapt<UserDto>(mapper, AdaptOptions.Default);
        Console.WriteLine($"默认规则 - UserName: {target1?.UserName ?? "null"}, UserId: {target1?.UserId}");

        // 启用忽略大小写
        var target2 = source.Adapt<UserDto>(mapper, AdaptOptions.IgnoreCaseOption);
        Console.WriteLine($"忽略大小写 - UserName: {target2?.UserName ?? "null"}, UserId: {target2?.UserId}");
    }

    private static void TestIgnoreUnderscore(IMapper mapper)
    {
        Console.WriteLine("\n--- 测试 IgnoreUnderscore：忽略下划线匹配 ---");

        var source = new DatabaseDto
        {
            user_name = "lisi",
            user_id = 2002,
            email_address = "lisi@example.com"
        };

        // 默认情况下（不忽略下划线）
        var target1 = source.Adapt<UserDto>(mapper, AdaptOptions.Default);
        Console.WriteLine($"默认规则 - UserName: {target1?.UserName ?? "null"}, UserId: {target1?.UserId}");

        // 启用忽略下划线
        var target2 = source.Adapt<UserDto>(mapper, AdaptOptions.IgnoreUnderscoreOption);
        Console.WriteLine($"忽略下划线 - UserName: {target2?.UserName ?? "null"}, UserId: {target2?.UserId}");
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
        var target = source.Adapt<UserDto>(mapper, AdaptOptions.FlexibleOption);
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

        // 忽略敏感字段
        var target = source.Adapt<UserDto>(mapper, new AdaptOptions
        {
            IgnoreProperties = new[] { nameof(UserDto.Password), nameof(UserDto.CreditCard) }
        });

        Console.WriteLine($"UserName: {target?.UserName}");
        Console.WriteLine($"Email: {target?.Email}");
        Console.WriteLine($"Password (应该为 null): {target?.Password ?? "null"}");
        Console.WriteLine($"CreditCard (应该为 null): {target?.CreditCard ?? "null"}");
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

        // 忽略 null 值属性
        var target = source.Adapt<UserDto>(mapper, new AdaptOptions
        {
            IgnoreNullValues = true
        });

        Console.WriteLine($"UserName: {target?.UserName}");
        Console.WriteLine($"UserId: {target?.UserId}");
        Console.WriteLine($"Email (应该为 null): {target?.Email ?? "null"}");
        Console.WriteLine($"Password (应该为 null): {target?.Password ?? "null"}");
        Console.WriteLine($"CreditCard: {target?.CreditCard}");
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

        // 组合多个规则
        var target = source.Adapt<UserDto>(mapper, new AdaptOptions
        {
            IgnoreCase = true,
            IgnoreUnderscore = true,
            IgnoreNullValues = true,
            IgnoreProperties = new[] { "Password", "CreditCard" }
        }, (dest, src) =>
        {
            // 自定义回调
            Console.WriteLine("  -> 执行自定义回调");
            if (dest != null)
            {
                dest.Email = dest.Email ?? "default@example.com";
            }
        });

        Console.WriteLine($"UserName: {target?.UserName}");
        Console.WriteLine($"UserId: {target?.UserId}");
        Console.WriteLine($"Email (应该为 default): {target?.Email}");
        Console.WriteLine($"Password (应该为 null): {target?.Password ?? "null"}");
        Console.WriteLine($"CreditCard (应该为 null): {target?.CreditCard ?? "null"}");
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
