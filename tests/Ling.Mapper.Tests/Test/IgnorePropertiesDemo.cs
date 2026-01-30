using Ling.Mapper;
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
