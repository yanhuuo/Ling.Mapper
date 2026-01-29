using Ling.Mapper;
using Ling.Mapper.Extensions;

namespace TestConsole.Test;

/// <summary>
/// 测试异常处理行为
/// </summary>
public static class ExceptionHandlingTest
{
    public static void Run()
    {
        Console.WriteLine("\n=== 异常处理测试 ===\n");

        // 测试 1：正常情况 - DTO 有无参构造函数
        Test1_NormalCase();

        // 测试 2：DTO 没有无参构造函数
        Test2_NoParameterlessConstructor();

        // 测试 3：属性部分匹配
        Test3_PartialPropertyMatch();

        // 测试 4：属性完全不匹配
        Test4_NoPropertyMatch();
    }

    /// <summary>
    /// 测试 1：正常情况 - DTO 有无参构造函数
    /// </summary>
    private static void Test1_NormalCase()
    {
        Console.WriteLine("【测试 1】正常情况 - DTO 有无参构造函数");

        var source = new SourceDto1
        {
            Id = 1,
            Name = "Test"
        };

        try
        {
            var mapper = new MapperConfiguration().CreateMapper();
            var result = source.Adapt<DestDto1>();

            Console.WriteLine($"? 成功：Id={result?.Id}, Name={result?.Name}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"? 失败：{ex.Message}");
        }

        Console.WriteLine();
    }

    /// <summary>
    /// 测试 2：DTO 没有无参构造函数
    /// </summary>
    private static void Test2_NoParameterlessConstructor()
    {
        Console.WriteLine("【测试 2】DTO 没有无参构造函数（应该在 Mapper 内部就抛出异常）");

        var source = new SourceDto1
        {
            Id = 2,
            Name = "Test2"
        };

        try
        {
            var result = source.Adapt<DestDtoNoConstructor>();

            Console.WriteLine($"? 成功：Id={result?.Id}, Name={result?.Name}");
        }
        catch (System.MissingMethodException ex)
        {
            Console.WriteLine($"?? 抛出 MissingMethodException：{ex.Message}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"?? 抛出异常：{ex.GetType().Name} - {ex.Message}");
        }

        Console.WriteLine();
    }

    /// <summary>
    /// 测试 3：属性部分匹配
    /// </summary>
    private static void Test3_PartialPropertyMatch()
    {
        Console.WriteLine("【测试 3】属性部分匹配（只有 Id 匹配，Name 不匹配）");

        var source = new SourceDto2
        {
            Id = 3,
            FullName = "Partial Match"  // 注意：这里是 FullName，不是 Name
        };

        try
        {
            var mapper = new MapperConfiguration().CreateMapper();
            var result = source.Adapt<DestDto1>();

            Console.WriteLine($"? 成功：Id={result?.Id}, Name={result?.Name ?? "(null)"}");
            Console.WriteLine("   说明：只转换了匹配的属性 (Id)，不匹配的属性 (Name) 保持默认值");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"? 失败：{ex.Message}");
        }

        Console.WriteLine();
    }

    /// <summary>
    /// 测试 4：属性完全不匹配
    /// </summary>
    private static void Test4_NoPropertyMatch()
    {
        Console.WriteLine("【测试 4】属性完全不匹配（所有属性名都不一样）");

        var source = new SourceDto3
        {
            Value1 = 4,
            Value2 = "No Match"
        };

        try
        {
            var mapper = new MapperConfiguration().CreateMapper();
            var result = source.Adapt<DestDto1>();

            Console.WriteLine($"? 成功：Id={result?.Id}, Name={result?.Name ?? "(null)"}");
            Console.WriteLine("   说明：虽然属性完全不匹配，但仍然创建了实例（所有属性为默认值）");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"? 失败：{ex.Message}");
        }

        Console.WriteLine();
    }
}

// 测试用的 DTO 类

public class SourceDto1
{
    public int Id { get; set; }
    public string? Name { get; set; }
}

public class SourceDto2
{
    public int Id { get; set; }
    public string? FullName { get; set; }  // 不同的属性名
}

public class SourceDto3
{
    public int Value1 { get; set; }
    public string? Value2 { get; set; }
}

public class DestDto1
{
    public int Id { get; set; }
    public string? Name { get; set; }
}

// 没有无参构造函数的 DTO
public class DestDtoNoConstructor
{
    public DestDtoNoConstructor(int id, string name)
    {
        Id = id;
        Name = name;
    }

    public int Id { get; }
    public string? Name { get; }
}
