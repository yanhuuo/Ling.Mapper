using System;
using System.Collections.Generic;
using Ling.Mapper;
using Ling.Mapper.Extensions;

namespace TestConsole.Test;

/// <summary>
/// 嵌套集合测试 - 测试集合中包含集合的复杂场景
/// </summary>
public static class NestedCollectionTest
{
    public static void Run()
    {
        Console.WriteLine("--- 嵌套集合映射测试 ---\n");
        
        TestListOfLists();
        TestCategoryWithProducts();
        TestTreeStructure();
        
        Console.WriteLine();
    }
    
    private static void TestListOfLists()
    {
        Console.WriteLine("1. List<List<T>> 映射");
        
        var source = new MatrixSource
        {
            Name = "Matrix A",
            Data = new List<List<int>>
            {
                new List<int> { 1, 2, 3 },
                new List<int> { 4, 5, 6 },
                new List<int> { 7, 8, 9 }
            }
        };
        
        try
        {
            var target = source.Adapt<MatrixTarget>();
            
            if (target?.Data != null && target.Data.Count == 3)
            {
                Console.WriteLine($"  ? Matrix Name: {target.Name}");
                Console.WriteLine($"  ? Rows: {target.Data.Count}");
                
                for (int i = 0; i < target.Data.Count; i++)
                {
                    Console.WriteLine($"  ? Row {i + 1}: [{string.Join(", ", target.Data[i])}]");
                }
            }
            else
            {
                Console.WriteLine($"  ? List<List<T>> 映射失败");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"  ? 测试失败: {ex.Message}");
        }
        
        Console.WriteLine();
    }
    
    private static void TestCategoryWithProducts()
    {
        Console.WriteLine("2. 类别 -> 产品 (1对多嵌套)");
        
        var source = new List<CategoryWithProductsSource>
        {
            new CategoryWithProductsSource
            {
                CategoryId = 1,
                CategoryName = "Electronics",
                Products = new List<ProductSource>
                {
                    new ProductSource { ProductId = 101, Name = "Laptop", Price = 999.99m },
                    new ProductSource { ProductId = 102, Name = "Mouse", Price = 29.99m },
                    new ProductSource { ProductId = 103, Name = "Keyboard", Price = 79.99m }
                }
            },
            new CategoryWithProductsSource
            {
                CategoryId = 2,
                CategoryName = "Books",
                Products = new List<ProductSource>
                {
                    new ProductSource { ProductId = 201, Name = "C# in Depth", Price = 49.99m },
                    new ProductSource { ProductId = 202, Name = "Clean Code", Price = 39.99m }
                }
            }
        };
        
        try
        {
            var target = source.Adapt<List<CategoryWithProductsTarget>>();
            
            if (target != null && target.Count == 2)
            {
                Console.WriteLine($"  ? 类别数量: {target.Count}");
                
                foreach (var category in target)
                {
                    Console.WriteLine($"  ? {category.CategoryName}: {category.Products?.Count} 个产品");
                    
                    if (category.Products != null)
                    {
                        foreach (var product in category.Products)
                        {
                            Console.WriteLine($"      - {product.Name}: ${product.Price}");
                        }
                    }
                }
            }
            else
            {
                Console.WriteLine($"  ? 嵌套集合映射失败");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"  ? 测试失败: {ex.Message}");
        }
        
        Console.WriteLine();
    }
    
    private static void TestTreeStructure()
    {
        Console.WriteLine("3. 树形结构 (递归集合)");
        
        var source = new TreeNodeSource
        {
            Id = 1,
            Name = "Root",
            Children = new List<TreeNodeSource>
            {
                new TreeNodeSource
                {
                    Id = 2,
                    Name = "Child 1",
                    Children = new List<TreeNodeSource>
                    {
                        new TreeNodeSource { Id = 3, Name = "Grandchild 1", Children = null },
                        new TreeNodeSource { Id = 4, Name = "Grandchild 2", Children = null }
                    }
                },
                new TreeNodeSource
                {
                    Id = 5,
                    Name = "Child 2",
                    Children = new List<TreeNodeSource>
                    {
                        new TreeNodeSource { Id = 6, Name = "Grandchild 3", Children = null }
                    }
                }
            }
        };
        
        try
        {
            var target = source.Adapt<TreeNodeTarget>();
            
            if (target != null)
            {
                Console.WriteLine($"  ? Root: {target.Name}");
                Console.WriteLine($"  ? Children: {target.Children?.Count}");
                
                if (target.Children != null)
                {
                    foreach (var child in target.Children)
                    {
                        Console.WriteLine($"    ? {child.Name}: {child.Children?.Count ?? 0} 个子节点");
                        
                        if (child.Children != null)
                        {
                            foreach (var grandchild in child.Children)
                            {
                                Console.WriteLine($"        - {grandchild.Name}");
                            }
                        }
                    }
                }
            }
            else
            {
                Console.WriteLine($"  ? 树形结构映射失败");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"  ? 测试失败: {ex.Message}");
        }
        
        Console.WriteLine();
    }
    
    #region Test Models
    
    // Matrix Models
    public class MatrixSource
    {
        public string? Name { get; set; }
        public List<List<int>>? Data { get; set; }
    }
    
    public class MatrixTarget
    {
        public string? Name { get; set; }
        public List<List<int>>? Data { get; set; }
    }
    
    // Category-Product Models
    public class CategoryWithProductsSource
    {
        public int CategoryId { get; set; }
        public string? CategoryName { get; set; }
        public List<ProductSource>? Products { get; set; }
    }
    
    public class CategoryWithProductsTarget
    {
        public int CategoryId { get; set; }
        public string? CategoryName { get; set; }
        public List<ProductTarget>? Products { get; set; }
    }
    
    public class ProductSource
    {
        public int ProductId { get; set; }
        public string? Name { get; set; }
        public decimal Price { get; set; }
    }
    
    public class ProductTarget
    {
        public int ProductId { get; set; }
        public string? Name { get; set; }
        public decimal Price { get; set; }
    }
    
    // Tree Structure Models
    public class TreeNodeSource
    {
        public int Id { get; set; }
        public string? Name { get; set; }
        public List<TreeNodeSource>? Children { get; set; }
    }
    
    public class TreeNodeTarget
    {
        public int Id { get; set; }
        public string? Name { get; set; }
        public List<TreeNodeTarget>? Children { get; set; }
    }
    
    #endregion
}
