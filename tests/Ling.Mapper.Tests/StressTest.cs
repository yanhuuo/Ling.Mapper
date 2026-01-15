using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using Ling.Mapper;
using Ling.Mapper.Extensions;

namespace TestConsole;

/// <summary>
/// 压力测试 - 测试 Mapper 在极端场景下的稳定性
/// </summary>
public static class StressTest
{
    public static void Run()
    {
        Console.WriteLine("--- 压力测试 (Stress Tests) ---\n");
        Console.WriteLine("? 警告：压力测试可能需要较长时间\n");
        
        TestHighVolumeMapping();
        TestLargeObjectMapping();
        TestLargeCollectionMapping();
        TestConcurrentMapping();
        TestMemoryStability();
        
        Console.WriteLine();
    }
    
    private static void TestHighVolumeMapping()
    {
        Console.WriteLine("1. 高容量映射测试（10,000,000 次）");
        
        var source = new SimpleData { Id = 1, Name = "Test", Value = 100 };
        
        try
        {
            var sw = Stopwatch.StartNew();
            long totalAllocated = GC.GetTotalMemory(false);
            
            for (int i = 0; i < 10_000_000; i++)
            {
                _ = source.Adapt<SimpleData>();
            }
            
            sw.Stop();
            long finalAllocated = GC.GetTotalMemory(false);
            
            Console.WriteLine($"  ? 完成: 10,000,000 次映射");
            Console.WriteLine($"  ? 总耗时: {sw.ElapsedMilliseconds} ms");
            Console.WriteLine($"  ? 平均每秒: {10_000_000.0 / sw.Elapsed.TotalSeconds:N0} ops/sec");
            Console.WriteLine($"  ? 内存增长: {(finalAllocated - totalAllocated) / 1024.0 / 1024.0:F2} MB");
            
            if (sw.ElapsedMilliseconds < 10000)
            {
                Console.WriteLine($"  ? 性能测试通过 (< 10s)");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"  ? 测试失败: {ex.Message}");
        }
        
        Console.WriteLine();
    }
    
    private static void TestLargeObjectMapping()
    {
        Console.WriteLine("2. 大对象映射测试（100 个属性 x 100,000 次）");
        
        var source = new LargeObject
        {
            P01 = 1, P02 = 2, P03 = 3, P04 = 4, P05 = 5,
            P06 = 6, P07 = 7, P08 = 8, P09 = 9, P10 = 10,
            P11 = 11, P12 = 12, P13 = 13, P14 = 14, P15 = 15,
            P16 = 16, P17 = 17, P18 = 18, P19 = 19, P20 = 20,
            P21 = 21, P22 = 22, P23 = 23, P24 = 24, P25 = 25,
            P26 = 26, P27 = 27, P28 = 28, P29 = 29, P30 = 30,
            P31 = 31, P32 = 32, P33 = 33, P34 = 34, P35 = 35,
            P36 = 36, P37 = 37, P38 = 38, P39 = 39, P40 = 40,
            P41 = 41, P42 = 42, P43 = 43, P44 = 44, P45 = 45,
            P46 = 46, P47 = 47, P48 = 48, P49 = 49, P50 = 50,
            S01 = "S1", S02 = "S2", S03 = "S3", S04 = "S4", S05 = "S5",
            S06 = "S6", S07 = "S7", S08 = "S8", S09 = "S9", S10 = "S10",
            D01 = 1.1m, D02 = 2.2m, D03 = 3.3m, D04 = 4.4m, D05 = 5.5m,
            D06 = 6.6m, D07 = 7.7m, D08 = 8.8m, D09 = 9.9m, D10 = 10.10m
        };
        
        try
        {
            // 预热
            for (int i = 0; i < 100; i++)
            {
                _ = source.Adapt<LargeObject>();
            }
            
            var sw = Stopwatch.StartNew();
            
            for (int i = 0; i < 100_000; i++)
            {
                _ = source.Adapt<LargeObject>();
            }
            
            sw.Stop();
            
            Console.WriteLine($"  ? 完成: 100,000 次大对象映射");
            Console.WriteLine($"  ? 总耗时: {sw.ElapsedMilliseconds} ms");
            Console.WriteLine($"  ? 平均每次: {sw.Elapsed.TotalMilliseconds / 100_000:F6} ms");
            
            if (sw.ElapsedMilliseconds < 2000)
            {
                Console.WriteLine($"  ? 性能测试通过 (< 2000ms)");
            }
            else
            {
                Console.WriteLine($"  ? 性能警告: {sw.ElapsedMilliseconds} ms");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"  ? 测试失败: {ex.Message}");
        }
        
        Console.WriteLine();
    }
    
    private static void TestLargeCollectionMapping()
    {
        Console.WriteLine("3. 大集合映射测试（10,000 元素 x 1,000 次）");
        
        var source = Enumerable.Range(1, 10_000)
            .Select(i => new SimpleData { Id = i, Name = $"Item {i}", Value = i * 10 })
            .ToList();
        
        try
        {
            // 预热
            for (int i = 0; i < 10; i++)
            {
                _ = source.Adapt<List<SimpleData>>();
            }
            
            var sw = Stopwatch.StartNew();
            
            for (int i = 0; i < 1_000; i++)
            {
                _ = source.Adapt<List<SimpleData>>();
            }
            
            sw.Stop();
            
            var totalElements = 10_000 * 1_000;
            
            Console.WriteLine($"  ? 完成: {totalElements:N0} 个元素映射");
            Console.WriteLine($"  ? 总耗时: {sw.ElapsedMilliseconds} ms");
            Console.WriteLine($"  ? 吞吐量: {totalElements / sw.Elapsed.TotalSeconds:N0} elements/sec");
            
            if (sw.ElapsedMilliseconds < 5000)
            {
                Console.WriteLine($"  ? 性能测试通过 (< 5000ms)");
            }
            else
            {
                Console.WriteLine($"  ? 性能警告: {sw.ElapsedMilliseconds} ms");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"  ? 测试失败: {ex.Message}");
        }
        
        Console.WriteLine();
    }
    
    private static void TestConcurrentMapping()
    {
        Console.WriteLine("4. 并发映射测试（10 线程 x 100,000 次）");
        
        var source = new SimpleData { Id = 1, Name = "Test", Value = 100 };
        
        try
        {
            var sw = Stopwatch.StartNew();
            
            Parallel.For(0, 10, threadId =>
            {
                for (int i = 0; i < 100_000; i++)
                {
                    _ = source.Adapt<SimpleData>();
                }
            });
            
            sw.Stop();
            
            var totalOps = 10 * 100_000;
            
            Console.WriteLine($"  ? 完成: {totalOps:N0} 次并发映射");
            Console.WriteLine($"  ? 总耗时: {sw.ElapsedMilliseconds} ms");
            Console.WriteLine($"  ? 吞吐量: {totalOps / sw.Elapsed.TotalSeconds:N0} ops/sec");
            
            if (sw.ElapsedMilliseconds < 5000)
            {
                Console.WriteLine($"  ? 并发测试通过 (< 5000ms)");
            }
            else
            {
                Console.WriteLine($"  ? 性能警告: {sw.ElapsedMilliseconds} ms");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"  ? 测试失败: {ex.Message}");
        }
        
        Console.WriteLine();
    }
    
    private static void TestMemoryStability()
    {
        Console.WriteLine("5. 内存稳定性测试（连续 GC 观察）");
        
        var source = new ComplexData
        {
            Id = 1,
            Name = "Test",
            Items = Enumerable.Range(1, 100)
                .Select(i => new SimpleData { Id = i, Name = $"Item {i}", Value = i })
                .ToList()
        };
        
        try
        {
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();
            
            var initialMemory = GC.GetTotalMemory(false);
            var gen0Before = GC.CollectionCount(0);
            var gen1Before = GC.CollectionCount(1);
            var gen2Before = GC.CollectionCount(2);
            
            Console.WriteLine($"  初始内存: {initialMemory / 1024.0 / 1024.0:F2} MB");
            
            for (int iteration = 0; iteration < 5; iteration++)
            {
                for (int i = 0; i < 100_000; i++)
                {
                    _ = source.Adapt<ComplexData>();
                }
                
                var currentMemory = GC.GetTotalMemory(false);
                Console.WriteLine($"  迭代 {iteration + 1}: {currentMemory / 1024.0 / 1024.0:F2} MB");
            }
            
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();
            
            var finalMemory = GC.GetTotalMemory(false);
            var gen0After = GC.CollectionCount(0);
            var gen1After = GC.CollectionCount(1);
            var gen2After = GC.CollectionCount(2);
            
            Console.WriteLine($"  最终内存: {finalMemory / 1024.0 / 1024.0:F2} MB");
            Console.WriteLine($"  内存增长: {(finalMemory - initialMemory) / 1024.0 / 1024.0:F2} MB");
            Console.WriteLine($"  GC Gen0: {gen0After - gen0Before} 次");
            Console.WriteLine($"  GC Gen1: {gen1After - gen1Before} 次");
            Console.WriteLine($"  GC Gen2: {gen2After - gen2Before} 次");
            
            var memoryGrowth = (finalMemory - initialMemory) / 1024.0 / 1024.0;
            if (memoryGrowth < 50)
            {
                Console.WriteLine($"  ? 内存稳定性测试通过 (增长 < 50MB)");
            }
            else
            {
                Console.WriteLine($"  ? 内存警告: 增长 {memoryGrowth:F2} MB");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"  ? 测试失败: {ex.Message}");
        }
        
        Console.WriteLine();
    }
    
    #region Test Models
    
    public class SimpleData
    {
        public int Id { get; set; }
        public string? Name { get; set; }
        public decimal Value { get; set; }
    }
    
    public class ComplexData
    {
        public int Id { get; set; }
        public string? Name { get; set; }
        public List<SimpleData>? Items { get; set; }
    }
    
    // 100 个属性的大对象
    public class LargeObject
    {
        public int P01 { get; set; }
        public int P02 { get; set; }
        public int P03 { get; set; }
        public int P04 { get; set; }
        public int P05 { get; set; }
        public int P06 { get; set; }
        public int P07 { get; set; }
        public int P08 { get; set; }
        public int P09 { get; set; }
        public int P10 { get; set; }
        public int P11 { get; set; }
        public int P12 { get; set; }
        public int P13 { get; set; }
        public int P14 { get; set; }
        public int P15 { get; set; }
        public int P16 { get; set; }
        public int P17 { get; set; }
        public int P18 { get; set; }
        public int P19 { get; set; }
        public int P20 { get; set; }
        public int P21 { get; set; }
        public int P22 { get; set; }
        public int P23 { get; set; }
        public int P24 { get; set; }
        public int P25 { get; set; }
        public int P26 { get; set; }
        public int P27 { get; set; }
        public int P28 { get; set; }
        public int P29 { get; set; }
        public int P30 { get; set; }
        public int P31 { get; set; }
        public int P32 { get; set; }
        public int P33 { get; set; }
        public int P34 { get; set; }
        public int P35 { get; set; }
        public int P36 { get; set; }
        public int P37 { get; set; }
        public int P38 { get; set; }
        public int P39 { get; set; }
        public int P40 { get; set; }
        public int P41 { get; set; }
        public int P42 { get; set; }
        public int P43 { get; set; }
        public int P44 { get; set; }
        public int P45 { get; set; }
        public int P46 { get; set; }
        public int P47 { get; set; }
        public int P48 { get; set; }
        public int P49 { get; set; }
        public int P50 { get; set; }
        
        public string? S01 { get; set; }
        public string? S02 { get; set; }
        public string? S03 { get; set; }
        public string? S04 { get; set; }
        public string? S05 { get; set; }
        public string? S06 { get; set; }
        public string? S07 { get; set; }
        public string? S08 { get; set; }
        public string? S09 { get; set; }
        public string? S10 { get; set; }
        
        public decimal D01 { get; set; }
        public decimal D02 { get; set; }
        public decimal D03 { get; set; }
        public decimal D04 { get; set; }
        public decimal D05 { get; set; }
        public decimal D06 { get; set; }
        public decimal D07 { get; set; }
        public decimal D08 { get; set; }
        public decimal D09 { get; set; }
        public decimal D10 { get; set; }
    }
    
    #endregion
}
