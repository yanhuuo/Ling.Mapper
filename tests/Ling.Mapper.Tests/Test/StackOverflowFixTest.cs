using System;
using System.Collections.Generic;
using Ling.Mapper;
using Ling.Mapper.Extensions;

namespace TestConsole.Test;

/// <summary>
/// StackOverflow 修复验证测试
/// 测试循环引用和自引用场景，确保不会导致 StackOverflowException
/// </summary>
public static class StackOverflowFixTest
{
    public static void Run()
    {
        Console.WriteLine("--- StackOverflow 修复验证测试 ---\n");
        
        TestSelfReferenceType();
        TestCircularReferenceCompilation();
        TestTreeStructureCompilation();
        TestParentChildBidirectional();
        
        Console.WriteLine();
    }
    
    private static void TestSelfReferenceType()
    {
        Console.WriteLine("1. 自引用类型映射（编译期递归保护）");
        
        try
        {
            var node = new SelfRefNode
            {
                Id = 1,
                Name = "Root",
                Next = new SelfRefNode
                {
                    Id = 2,
                    Name = "Child",
                    Next = null
                }
            };
            
            var result = node.Adapt<SelfRefNode>();
            
            if (result != null)
            {
                Console.WriteLine($"  ? 映射成功: {result.Name}");
                Console.WriteLine($"  ? Next 节点: {result.Next?.Name}");
                Console.WriteLine($"  ? 编译期递归保护生效");
            }
            else
            {
                Console.WriteLine($"  ? 映射失败");
            }
        }
        catch (StackOverflowException)
        {
            Console.WriteLine($"  ? StackOverflow：递归保护失效！");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"  ? 异常: {ex.Message}");
        }
        
        Console.WriteLine();
    }
    
    private static void TestCircularReferenceCompilation()
    {
        Console.WriteLine("2. 循环引用类型编译");
        
        try
        {
            // 即使不创建实际的循环，编译映射时也可能导致递归
            var a = new NodeA { Id = 1, Name = "Node A" };
            var b = new NodeB { Id = 2, Name = "Node B" };
            
            a.RelatedB = b;
            b.RelatedA = a;
            
            var resultA = a.Adapt<NodeA>();
            var resultB = b.Adapt<NodeB>();
            
            if (resultA != null && resultB != null)
            {
                Console.WriteLine($"  ? NodeA 映射成功: {resultA.Name}");
                Console.WriteLine($"  ? NodeB 映射成功: {resultB.Name}");
                Console.WriteLine($"  ? 循环引用类型编译成功");
            }
        }
        catch (StackOverflowException)
        {
            Console.WriteLine($"  ? StackOverflow：循环引用编译失败！");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"  ? 异常: {ex.Message}");
        }
        
        Console.WriteLine();
    }
    
    private static void TestTreeStructureCompilation()
    {
        Console.WriteLine("3. 树形结构编译（递归集合）");
        
        try
        {
            var tree = new TreeNode
            {
                Id = 1,
                Name = "Root",
                Children = new List<TreeNode>
                {
                    new TreeNode
                    {
                        Id = 2,
                        Name = "Child 1",
                        Children = new List<TreeNode>
                        {
                            new TreeNode { Id = 3, Name = "Grandchild", Children = null }
                        }
                    },
                    new TreeNode { Id = 4, Name = "Child 2", Children = null }
                }
            };
            
            var result = tree.Adapt<TreeNode>();
            
            if (result != null)
            {
                Console.WriteLine($"  ? Root 映射成功: {result.Name}");
                Console.WriteLine($"  ? Children 数量: {result.Children?.Count}");
                Console.WriteLine($"  ? 树形结构编译成功");
            }
        }
        catch (StackOverflowException)
        {
            Console.WriteLine($"  ? StackOverflow：树形结构编译失败！");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"  ? 异常: {ex.Message}");
        }
        
        Console.WriteLine();
    }
    
    private static void TestParentChildBidirectional()
    {
        Console.WriteLine("4. 父子双向引用");
        
        try
        {
            var parent = new Parent
            {
                Id = 1,
                Name = "Parent",
                Children = new List<Child>()
            };
            
            var child1 = new Child { Id = 2, Name = "Child 1", Parent = parent };
            var child2 = new Child { Id = 3, Name = "Child 2", Parent = parent };
            
            parent.Children.Add(child1);
            parent.Children.Add(child2);
            
            var result = parent.Adapt<Parent>();
            
            if (result != null)
            {
                Console.WriteLine($"  ? Parent 映射成功: {result.Name}");
                Console.WriteLine($"  ? Children 数量: {result.Children?.Count}");
                
                if (result.Children != null && result.Children.Count > 0)
                {
                    Console.WriteLine($"  ? Child[0] 名称: {result.Children[0].Name}");
                    Console.WriteLine($"  ? Child[0].Parent 名称: {result.Children[0].Parent?.Name}");
                }
                
                Console.WriteLine($"  ? 父子双向引用编译成功");
            }
        }
        catch (StackOverflowException)
        {
            Console.WriteLine($"  ? StackOverflow：父子双向引用失败！");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"  ? 异常: {ex.Message}");
        }
        
        Console.WriteLine();
    }
    
    #region Test Models
    
    // 自引用节点
    public class SelfRefNode
    {
        public int Id { get; set; }
        public string? Name { get; set; }
        public SelfRefNode? Next { get; set; }
    }
    
    // 循环引用 A <-> B
    public class NodeA
    {
        public int Id { get; set; }
        public string? Name { get; set; }
        public NodeB? RelatedB { get; set; }
    }
    
    public class NodeB
    {
        public int Id { get; set; }
        public string? Name { get; set; }
        public NodeA? RelatedA { get; set; }
    }
    
    // 树形结构
    public class TreeNode
    {
        public int Id { get; set; }
        public string? Name { get; set; }
        public List<TreeNode>? Children { get; set; }
    }
    
    // 父子双向引用
    public class Parent
    {
        public int Id { get; set; }
        public string? Name { get; set; }
        public List<Child>? Children { get; set; }
    }
    
    public class Child
    {
        public int Id { get; set; }
        public string? Name { get; set; }
        public Parent? Parent { get; set; }
    }
    
    #endregion
}
