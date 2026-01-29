using Ling.Mapper;
using System;
using Ling.Mapper.Extensions;

namespace TestConsole.Test;

/// <summary>
/// 演示可空类型映射功能
/// </summary>
public static class NullableTypeDemo
{
    public static void Run()
    {
        Console.WriteLine("\n=== 可空类型映射功能演示 ===");

        var mapper = MapperProvider.Current ?? throw new InvalidOperationException("请先设置全局 Mapper");

        // 1. 测试 int? → int 转换
        TestNullableToNonNullable();

        // 2. 测试 int → int? 转换
        TestNonNullableToNullable();

        // 3. 测试 int? → int? 转换
        TestNullableToNullable();

        // 4. 测试 string? 转换
        TestNullableString();

        // 5. 测试混合场景
        TestMixedScenario();
    }

    private static void TestNullableToNonNullable()
    {
        Console.WriteLine("\n--- 测试 int? → int 转换 ---");

        var mapper = MapperProvider.Current!;

        // 有值的情况
        var source1 = new NullableSource { NullableId = 100, Name = "Test" };
        var target1 = source1.Adapt<NonNullableTarget>();
        TestConsole.Utils.TestHelper.PrintActualExpected("有值: NullableId -> Id", target1?.Id, 100);

        // null 的情况（应该转换为默认值 0）
        var source2 = new NullableSource { NullableId = null, Name = "Test" };
        var target2 = source2.Adapt<NonNullableTarget>();
        TestConsole.Utils.TestHelper.PrintActualExpected("null: NullableId -> Id", target2?.Id, 0);
    }

    private static void TestNonNullableToNullable()
    {
        Console.WriteLine("\n--- 测试 int → int? 转换 ---");

        var mapper = MapperProvider.Current!;

        var source = new NonNullableSource { Id = 200, Name = "Test" };
        var target = source.Adapt<NullableTarget>();
        Console.WriteLine($"Id = {source.Id} → NullableId = {target?.NullableId}");
    }

    private static void TestNullableToNullable()
    {
        Console.WriteLine("\n--- 测试 int? → int? 转换 ---");

        var mapper = MapperProvider.Current!;

        // 有值的情况
        var source1 = new NullableSource { NullableId = 300, Name = "Test" };
        var target1 = source1.Adapt<NullableTarget>();
        Console.WriteLine($"有值: NullableId = {source1.NullableId} → NullableId = {target1?.NullableId}");

        // null 的情况
        var source2 = new NullableSource { NullableId = null, Name = "Test" };
        var target2 = source2.Adapt<NullableTarget>();
        Console.WriteLine($"null: NullableId = {source2.NullableId} → NullableId = {target2?.NullableId}");
    }

    private static void TestNullableString()
    {
        Console.WriteLine("\n--- 测试 string? 转换 ---");

        var mapper = MapperProvider.Current!;

        // string? → string?
        var source1 = new StringSource { Name = "Hello", Description = null };
        var target1 = source1.Adapt<StringTarget>();
        Console.WriteLine($"Name = '{source1.Name}' → Name = '{target1?.Name}'");
        Console.WriteLine($"Description = {(source1.Description == null ? "null" : $"'{source1.Description}'")} → Description = {(target1?.Description == null ? "null" : $"'{target1.Description}'")}");
    }

    private static void TestMixedScenario()
    {
        Console.WriteLine("\n--- 测试混合场景 ---");

        var mapper = MapperProvider.Current!;

        var source = new MixedSource
        {
            IntValue = 100,
            NullableIntValue = 200,
            StringValue = "Test",
            NullableStringValue = null,
            DecimalValue = 99.99m,
            NullableDecimalValue = null
        };

        var target = source.Adapt<MixedTarget>();
        
        Console.WriteLine($"IntValue = {source.IntValue} → NullableIntValue = {target?.NullableIntValue}");
        Console.WriteLine($"NullableIntValue = {source.NullableIntValue} → IntValue = {target?.IntValue}");
        Console.WriteLine($"StringValue = '{source.StringValue}' → NullableStringValue = '{target?.NullableStringValue}'");
        Console.WriteLine($"NullableStringValue = {(source.NullableStringValue == null ? "null" : $"'{source.NullableStringValue}'")} → StringValue = '{target?.StringValue}'");
        Console.WriteLine($"DecimalValue = {source.DecimalValue} → NullableDecimalValue = {target?.NullableDecimalValue}");
        Console.WriteLine($"NullableDecimalValue = {(source.NullableDecimalValue?.ToString() ?? "null")} → DecimalValue = {target?.DecimalValue}");
    }
}

// 测试用的 DTO 类
public class NullableSource
{
    public int? NullableId { get; set; }
    public string? Name { get; set; }
}

public class NonNullableTarget
{
    public int Id { get; set; }
    public string? Name { get; set; }
}

public class NonNullableSource
{
    public int Id { get; set; }
    public string? Name { get; set; }
}

public class NullableTarget
{
    public int? NullableId { get; set; }
    public string? Name { get; set; }
}

public class StringSource
{
    public string? Name { get; set; }
    public string? Description { get; set; }
}

public class StringTarget
{
    public string? Name { get; set; }
    public string? Description { get; set; }
}

public class MixedSource
{
    public int IntValue { get; set; }
    public int? NullableIntValue { get; set; }
    public string? StringValue { get; set; }
    public string? NullableStringValue { get; set; }
    public decimal DecimalValue { get; set; }
    public decimal? NullableDecimalValue { get; set; }
}

public class MixedTarget
{
    public int? NullableIntValue { get; set; }
    public int IntValue { get; set; }
    public string? NullableStringValue { get; set; }
    public string? StringValue { get; set; }
    public decimal? NullableDecimalValue { get; set; }
    public decimal DecimalValue { get; set; }
}
