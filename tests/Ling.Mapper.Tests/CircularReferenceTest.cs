using System;
using System.Collections.Generic;
using Ling.Mapper;

namespace TestConsole;

/// <summary>
/// 循环引用测试 - 测试对象之间的循环引用场景
/// </summary>
public static class CircularReferenceTest
{
    public static void Run()
    {
        Console.WriteLine("--- 循环引用测试 ---\n");
        
        TestSimpleCircularReference();
        TestCollectionCircularReference();
        
        Console.WriteLine();
    }
    
    private static void TestSimpleCircularReference()
    {
        Console.WriteLine("1. 简单循环引用 (A -> B -> A) - 运行时");
        
        var nodeA = new NodeSource { Id = 1, Name = "Node A" };
        var nodeB = new NodeSource { Id = 2, Name = "Node B" };
        
        // 创建运行时循环引用
        nodeA.RelatedNode = nodeB;
        nodeB.RelatedNode = nodeA;  // 循环！
        
        try
        {
            var target = nodeA.Adapt<NodeTarget>();
            
            if (target != null)
            {
                Console.WriteLine($"  ? Node A 映射成功: {target.Name}");
                Console.WriteLine($"  ? Related Node: {target.RelatedNode?.Name}");
                
                // v2.1.3: 循环引用应该被检测到并打破
                if (target.RelatedNode?.RelatedNode != null)
                {
                    if (ReferenceEquals(target, target.RelatedNode.RelatedNode))
                    {
                        Console.WriteLine($"  ? 循环引用已正确处理（引用相同对象）");
                    }
                    else
                    {
                        Console.WriteLine($"  ? 循环引用处理异常（不同对象）");
                    }
                }
                else
                {
                    Console.WriteLine($"  ? 循环引用已被打破（RelatedNode.RelatedNode = null）");
                }
                
                Console.WriteLine($"  ? 运行时循环引用保护生效");
            }
        }
        catch (StackOverflowException)
        {
            Console.WriteLine($"  ? StackOverflow：运行时循环引用保护失效！");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"  ? 异常: {ex.Message}");
        }
        
        Console.WriteLine();
    }
    
    private static void TestCollectionCircularReference()
    {
        Console.WriteLine("2. 集合中的循环引用");
        
        var parent = new ParentSource { Id = 1, Name = "Parent" };
        var child1 = new ChildSource { Id = 2, Name = "Child 1", Parent = parent };
        var child2 = new ChildSource { Id = 3, Name = "Child 2", Parent = parent };
        
        parent.Children = new List<ChildSource> { child1, child2 };
        
        try
        {
            var target = parent.Adapt<ParentTarget>();
            
            if (target != null)
            {
                Console.WriteLine($"  ? Parent 映射成功: {target.Name}");
                Console.WriteLine($"  ? Children Count: {target.Children?.Count}");
                
                if (target.Children != null)
                {
                    foreach (var child in target.Children)
                    {
                        Console.WriteLine($"    ? Child: {child.Name}");
                        
                        // 检查是否保留了父引用
                        if (child.Parent != null)
                        {
                            Console.WriteLine($"      ? Parent Reference: {child.Parent.Name}");
                        }
                    }
                }
            }
        }
        catch (StackOverflowException)
        {
            Console.WriteLine($"  ? StackOverflow：循环引用保护失效");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"  ? 异常: {ex.Message}");
        }
        
        Console.WriteLine();
    }
    
    #region Test Models
    
    // Simple Circular Reference Models
    public class NodeSource
    {
        public int Id { get; set; }
        public string? Name { get; set; }
        public NodeSource? RelatedNode { get; set; }
    }
    
    public class NodeTarget
    {
        public int Id { get; set; }
        public string? Name { get; set; }
        public NodeTarget? RelatedNode { get; set; }
    }
    
    // Parent-Child Circular Reference Models
    public class ParentSource
    {
        public int Id { get; set; }
        public string? Name { get; set; }
        public List<ChildSource>? Children { get; set; }
    }
    
    public class ParentTarget
    {
        public int Id { get; set; }
        public string? Name { get; set; }
        public List<ChildTarget>? Children { get; set; }
    }
    
    public class ChildSource
    {
        public int Id { get; set; }
        public string? Name { get; set; }
        public ParentSource? Parent { get; set; }
    }
    
    public class ChildTarget
    {
        public int Id { get; set; }
        public string? Name { get; set; }
        public ParentTarget? Parent { get; set; }
    }
    
    #endregion
}

/// <summary>
/// 深度嵌套测试 - 测试极深的对象嵌套
/// </summary>
public static class DeepNestingTest
{
    public static void Run()
    {
        Console.WriteLine("--- 深度嵌套测试 ---\n");
        
        Test10LevelNesting();
        Test20LevelNesting();
        
        Console.WriteLine();
    }
    
    private static void Test10LevelNesting()
    {
        Console.WriteLine("1. 10层深度嵌套");
        
        var source = CreateDeepNesting(10);
        
        try
        {
            var sw = System.Diagnostics.Stopwatch.StartNew();
            var target = source.Adapt<DeepLevel>();
            sw.Stop();
            
            var depth = GetDepth(target);
            Console.WriteLine($"  ? 映射成功");
            Console.WriteLine($"  ? 实际深度: {depth}");
            Console.WriteLine($"  ? 耗时: {sw.ElapsedMilliseconds} ms");
            
            if (depth == 10)
            {
                Console.WriteLine($"  ? 深度验证通过");
            }
            else
            {
                Console.WriteLine($"  ? 深度验证失败 (期望: 10, 实际: {depth})");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"  ? 测试失败: {ex.Message}");
        }
        
        Console.WriteLine();
    }
    
    private static void Test20LevelNesting()
    {
        Console.WriteLine("2. 20层深度嵌套（压力测试）");
        
        var source = CreateDeepNesting(20);
        
        try
        {
            var sw = System.Diagnostics.Stopwatch.StartNew();
            var target = source.Adapt<DeepLevel>();
            sw.Stop();
            
            var depth = GetDepth(target);
            Console.WriteLine($"  ? 映射成功");
            Console.WriteLine($"  ? 实际深度: {depth}");
            Console.WriteLine($"  ? 耗时: {sw.ElapsedMilliseconds} ms");
            
            if (sw.ElapsedMilliseconds < 100)
            {
                Console.WriteLine($"  ? 性能测试通过 (< 100ms)");
            }
            else
            {
                Console.WriteLine($"  ? 性能警告: {sw.ElapsedMilliseconds} ms");
            }
        }
        catch (StackOverflowException)
        {
            Console.WriteLine($"  ? StackOverflow：深度嵌套导致堆栈溢出");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"  ? 测试失败: {ex.Message}");
        }
        
        Console.WriteLine();
    }
    
    private static DeepLevel CreateDeepNesting(int depth)
    {
        var root = new DeepLevel { Level = 0, Value = "Level 0" };
        var current = root;
        
        for (int i = 1; i < depth; i++)
        {
            current.Next = new DeepLevel { Level = i, Value = $"Level {i}" };
            current = current.Next;
        }
        
        return root;
    }
    
    private static int GetDepth(DeepLevel? node)
    {
        if (node == null) return 0;
        return 1 + GetDepth(node.Next);
    }
    
    #region Test Models
    
    public class DeepLevel
    {
        public int Level { get; set; }
        public string? Value { get; set; }
        public DeepLevel? Next { get; set; }
    }
    
    #endregion
}
