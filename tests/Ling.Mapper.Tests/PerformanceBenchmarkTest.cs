using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Ling.Mapper;
using TestConsole.Utils;

namespace TestConsole;

/// <summary>
/// 性能基准测试 - 测试不同场景下的映射性能
/// </summary>
public static class PerformanceBenchmarkTest
{
    public static void Run()
    {
        Console.WriteLine("--- 性能基准测试 ---\n");
        Console.WriteLine("测试配置：");
        Console.WriteLine($"  - CPU: {Environment.ProcessorCount} 核");
        Console.WriteLine($"  - .NET: {Environment.Version}");
        Console.WriteLine();
        
        TestSimpleMappingPerformance();
        TestComplexMappingPerformance();
        TestCollectionMappingPerformance();
        TestEnumConversionPerformance();
        TestNullableConversionPerformance();
        
        Console.WriteLine();
    }
    
    private static void TestSimpleMappingPerformance()
    {
        Console.WriteLine("1. 简单对象映射性能（1,000,000 次）");
        
        var source = new SimpleSource { Id = 1, Name = "Test", Value = 100 };
        
        // 预热
        Console.Write("  预热中...");
        for (int i = 0; i < 1000; i++)
        {
            _ = source.Adapt<SimpleTarget>();
        }
        Console.WriteLine(" 完成");
        
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();
        
        // 性能测试（带进度条）
        const int totalIterations = 1_000_000;
        using var progressBar = new ProgressBar(totalIterations, width: 50, prefix: "  进度");
        
        var sw = Stopwatch.StartNew();
        for (int i = 0; i < totalIterations; i++)
        {
            _ = source.Adapt<SimpleTarget>();
            
            // 每 10000 次更新一次进度条
            if (i % 10_000 == 0 || i == totalIterations - 1)
            {
                progressBar.Update(i + 1);
            }
        }
        sw.Stop();
        
        var opsPerSecond = 1_000_000.0 / sw.Elapsed.TotalSeconds;
        
        Console.WriteLine($"  ⏱ 总耗时: {sw.ElapsedMilliseconds} ms");
        Console.WriteLine($"  📊 平均每次: {sw.Elapsed.TotalMilliseconds / 1_000_000:F6} ms");
        Console.WriteLine($"  🚀 吞吐量: {opsPerSecond:N0} ops/sec");
        
        if (sw.ElapsedMilliseconds < 1000)
        {
            Console.WriteLine($"  ✅ 性能测试通过 (< 1000ms)");
        }
        else
        {
            Console.WriteLine($"  ⚠️ 性能警告: {sw.ElapsedMilliseconds} ms");
        }
        
        Console.WriteLine();
    }
    
    private static void TestComplexMappingPerformance()
    {
        Console.WriteLine("2. 复杂对象映射性能（100,000 次）");
        
        var source = new ComplexSource
        {
            Id = 1,
            Name = "Test",
            Nested = new NestedSource
            {
                Value1 = 100,
                Value2 = "Nested",
                DeepNested = new DeepNestedSource
                {
                    Data = "Deep Value"
                }
            },
            Items = new List<ItemSource>
            {
                new ItemSource { ItemId = 1, ItemName = "Item 1" },
                new ItemSource { ItemId = 2, ItemName = "Item 2" },
                new ItemSource { ItemId = 3, ItemName = "Item 3" }
            }
        };
        
        // 预热
        Console.Write("  预热中...");
        for (int i = 0; i < 100; i++)
        {
            try
            {
                _ = source.Adapt<ComplexTarget>();
            }
            catch(Exception ex)
            {
                Console.WriteLine(ex);
            }
        }
        Console.WriteLine(" 完成");
        
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();
        
        // 性能测试（带进度条）
        const int totalIterations = 100_000;
        using var progressBar = new ProgressBar(totalIterations, width: 50, prefix: "  进度");
        
        var sw = Stopwatch.StartNew();
        for (int i = 0; i < totalIterations; i++)
        {
            try
            {
                _ = source.Adapt<ComplexTarget>();
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex);
            }
            
            // 每 1000 次更新一次进度条
            if (i % 1_000 == 0 || i == totalIterations - 1)
            {
                progressBar.Update(i + 1);
            }
        }
        sw.Stop();
        
        var opsPerSecond = 100_000.0 / sw.Elapsed.TotalSeconds;
        
        Console.WriteLine($"  ⏱ 总耗时: {sw.ElapsedMilliseconds} ms");
        Console.WriteLine($"  📊 平均每次: {sw.Elapsed.TotalMilliseconds / 100_000:F6} ms");
        Console.WriteLine($"  🚀 吞吐量: {opsPerSecond:N0} ops/sec");
        
        if (sw.ElapsedMilliseconds < 500)
        {
            Console.WriteLine($"  ✅ 性能测试通过 (< 500ms)");
        }
        else
        {
            Console.WriteLine($"  ⚠️ 性能警告: {sw.ElapsedMilliseconds} ms");
        }
        
        Console.WriteLine();
    }
    
    private static void TestCollectionMappingPerformance()
    {
        Console.WriteLine("3. 集合映射性能（10,000 次 x 100 元素）");
        
        var source = Enumerable.Range(1, 100)
            .Select(i => new SimpleSource { Id = i, Name = $"Item {i}", Value = i * 10 })
            .ToList();
        
        // 预热
        Console.Write("  预热中...");
        for (int i = 0; i < 100; i++)
        {
            _ = source.Adapt<List<SimpleTarget>>();
        }
        Console.WriteLine(" 完成");
        
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();
        
        // 性能测试（带进度条）
        const int totalIterations = 10_000;
        using var progressBar = new ProgressBar(totalIterations, width: 50, prefix: "  进度");
        
        var sw = Stopwatch.StartNew();
        for (int i = 0; i < totalIterations; i++)
        {
            _ = source.Adapt<List<SimpleTarget>>();
            
            // 每 100 次更新一次进度条
            if (i % 100 == 0 || i == totalIterations - 1)
            {
                progressBar.Update(i + 1);
            }
        }
        sw.Stop();
        
        var totalElements = 10_000 * 100;
        var elementsPerSecond = totalElements / sw.Elapsed.TotalSeconds;
        
        Console.WriteLine($"  ⏱ 总耗时: {sw.ElapsedMilliseconds} ms");
        Console.WriteLine($"  📊 总元素数: {totalElements:N0}");
        Console.WriteLine($"  🚀 吞吐量: {elementsPerSecond:N0} elements/sec");
        
        if (sw.ElapsedMilliseconds < 1000)
        {
            Console.WriteLine($"  ✅ 性能测试通过 (< 1000ms)");
        }
        else
        {
            Console.WriteLine($"  ⚠️ 性能警告: {sw.ElapsedMilliseconds} ms");
        }
        
        Console.WriteLine();
    }
    
    private static void TestEnumConversionPerformance()
    {
        Console.WriteLine("4. 枚举转换性能（1,000,000 次）");
        
        var source = new EnumSource { Status = TestStatus.Active };
        
        // 预热
        Console.Write("  预热中...");
        for (int i = 0; i < 1000; i++)
        {
            _ = source.Adapt<EnumTarget>();
        }
        Console.WriteLine(" 完成");
        
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();
        
        // 性能测试（带进度条）
        const int totalIterations = 1_000_000;
        using var progressBar = new ProgressBar(totalIterations, width: 50, prefix: "  进度");
        
        var sw = Stopwatch.StartNew();
        for (int i = 0; i < totalIterations; i++)
        {
            _ = source.Adapt<EnumTarget>();
            
            // 每 10000 次更新一次进度条
            if (i % 10_000 == 0 || i == totalIterations - 1)
            {
                progressBar.Update(i + 1);
            }
        }
        sw.Stop();
        
        var opsPerSecond = 1_000_000.0 / sw.Elapsed.TotalSeconds;
        
        Console.WriteLine($"  ⏱ 总耗时: {sw.ElapsedMilliseconds} ms");
        Console.WriteLine($"  📊 平均每次: {sw.Elapsed.TotalMilliseconds / 1_000_000:F6} ms");
        Console.WriteLine($"  🚀 吞吐量: {opsPerSecond:N0} ops/sec");
        
        Console.WriteLine();
    }
    
    private static void TestNullableConversionPerformance()
    {
        Console.WriteLine("5. 可空类型转换性能（1,000,000 次）");
        
        var source = new NullableSource { Value = 42, Name = "Test" };
        
        // 预热
        Console.Write("  预热中...");
        for (int i = 0; i < 1000; i++)
        {
            _ = source.Adapt<NullableTarget>();
        }
        Console.WriteLine(" 完成");
        
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();
        
        // 性能测试（带进度条）
        const int totalIterations = 1_000_000;
        using var progressBar = new ProgressBar(totalIterations, width: 50, prefix: "  进度");
        
        var sw = Stopwatch.StartNew();
        for (int i = 0; i < totalIterations; i++)
        {
            _ = source.Adapt<NullableTarget>();
            
            // 每 10000 次更新一次进度条
            if (i % 10_000 == 0 || i == totalIterations - 1)
            {
                progressBar.Update(i + 1);
            }
        }
        sw.Stop();
        
        var opsPerSecond = 1_000_000.0 / sw.Elapsed.TotalSeconds;
        
        Console.WriteLine($"  ⏱ 总耗时: {sw.ElapsedMilliseconds} ms");
        Console.WriteLine($"  📊 平均每次: {sw.Elapsed.TotalMilliseconds / 1_000_000:F6} ms");
        Console.WriteLine($"  🚀 吞吐量: {opsPerSecond:N0} ops/sec");
        
        Console.WriteLine();
    }
    
    #region Test Models
    
    public class SimpleSource
    {
        public int Id { get; set; }
        public string? Name { get; set; }
        public decimal Value { get; set; }
    }
    
    public class SimpleTarget
    {
        public int Id { get; set; }
        public string? Name { get; set; }
        public decimal Value { get; set; }
    }
    
    public class ComplexSource
    {
        public int Id { get; set; }
        public string? Name { get; set; }
        public NestedSource? Nested { get; set; }
        public List<ItemSource>? Items { get; set; }
    }
    
    public class ComplexTarget
    {
        public int Id { get; set; }
        public string? Name { get; set; }
        public NestedTarget? Nested { get; set; }
        public List<ItemTarget>? Items { get; set; }
    }
    
    public class NestedSource
    {
        public int Value1 { get; set; }
        public string? Value2 { get; set; }
        public DeepNestedSource? DeepNested { get; set; }
    }
    
    public class NestedTarget
    {
        public int Value1 { get; set; }
        public string? Value2 { get; set; }
        public DeepNestedTarget? DeepNested { get; set; }
    }
    
    public class DeepNestedSource
    {
        public string? Data { get; set; }
    }
    
    public class DeepNestedTarget
    {
        public string? Data { get; set; }
    }
    
    public class ItemSource
    {
        public int ItemId { get; set; }
        public string? ItemName { get; set; }
    }
    
    public class ItemTarget
    {
        public int ItemId { get; set; }
        public string? ItemName { get; set; }
    }
    
    public enum TestStatus
    {
        Inactive = 0,
        Active = 1,
        Pending = 2
    }
    
    public class EnumSource
    {
        public TestStatus Status { get; set; }
    }
    
    public class EnumTarget
    {
        public int Status { get; set; }
    }
    
    public class NullableSource
    {
        public int? Value { get; set; }
        public string? Name { get; set; }
    }
    
    public class NullableTarget
    {
        public int Value { get; set; }
        public string? Name { get; set; }
    }
    
    #endregion
}
