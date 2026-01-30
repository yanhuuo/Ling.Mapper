using Ling.Mapper;
using Ling.Mapper.Extensions;
using System;
using Ling.Mapper.Provider;

namespace TestConsole.Test;

/// <summary>
/// 演示 MapperExtensions 中新增的忽略属性映射功能
/// </summary>
public static class IgnorePropertiesDemo
{
    public static void Run()
    {
        Console.WriteLine("\n=== 忽略属性映射功能演示 ===");

        var mapper = MapperProvider.Current ?? throw new InvalidOperationException("请先设置全局 Mapper");
            
            // 示例 1: 在 Adapt 时指定要忽略的目标字段
            var src = new UserSourceDto
            {
                Name = "测试用户",
                Password = "secret",
                CreditCard = "1111-2222-3333-4444",
                Age = 30,
                Email = "test@example.com"
            };

            // 忽略 Password 和 CreditCard 字段
            var dest = src.Adapt<UserTargetDto>("Password", "CreditCard");

            Console.WriteLine($"源 Name: {src.Name} => 目标 Name: {dest?.Name} (期望: {src.Name})");
            TestConsole.Utils.TestHelper.PrintActualExpected("Password", dest?.Password ?? "NULL", "NULL");
            TestConsole.Utils.TestHelper.PrintActualExpected("CreditCard", dest?.CreditCard ?? "NULL", "NULL");
            TestConsole.Utils.TestHelper.PrintActualExpected("Age", dest?.Age, src.Age);
            TestConsole.Utils.TestHelper.PrintActualExpected("Email", dest?.Email, src.Email);

            if (dest?.Password == null && dest?.CreditCard == null)
                Console.WriteLine("  ✅ 忽略字段映射成功\n");
            else
                Console.WriteLine("  ❌ 忽略字段映射失败\n");

            // 示例 2: 忽略不存在的字段名（应安全忽略，不抛异常）
            var dest2 = src.Adapt<UserTargetDto>("NonExistField");
            Console.WriteLine($"忽略不存在字段 -> 目标 Name: {dest2?.Name} (期望: {src.Name})");
    }

    
}

// 测试用的 DTO 类
public class UserSourceDto
{
    public string? Name { get; set; }
    public string? Password { get; set; }
    public string? CreditCard { get; set; }
    public int Age { get; set; }
    public string? Email { get; set; }
}

public class UserTargetDto
{
    public string? Name { get; set; }
    public string? Password { get; set; }
    public string? CreditCard { get; set; }
    public int Age { get; set; }
    public string? Email { get; set; }
}
