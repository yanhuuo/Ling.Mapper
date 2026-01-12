using System;
using System.Collections.Generic;
using Ling.Mapper;

namespace TestConsole;

/// <summary>
/// ∏¥‘”∂‘œÛ”≥…‰≤‚ ‘ - ≤‚ ‘…Ó≤„«∂Ã◊°¢∂‡≤„ºÃ≥–µ»∏¥‘”≥°æ∞
/// </summary>
public static class ComplexObjectMappingTest
{
    public static void Run()
    {
        Console.WriteLine("--- ∏¥‘”∂‘œÛ”≥…‰≤‚ ‘ ---\n");
        
        TestNestedObjectMapping();
        TestMultiLevelNesting();
        TestCollectionInObject();
        TestObjectInCollection();
        
        Console.WriteLine();
    }
    
    private static void TestNestedObjectMapping()
    {
        Console.WriteLine("1. «∂Ã◊∂‘œÛ”≥…‰");
        
        var source = new OrderSource
        {
            OrderId = 1001,
            OrderDate = DateTime.Now,
            Customer = new CustomerSource
            {
                CustomerId = 2001,
                Name = "John Doe",
                Email = "john@example.com",
                Address = new AddressSource
                {
                    Street = "123 Main St",
                    City = "New York",
                    ZipCode = "10001"
                }
            },
            Items = new List<OrderItemSource>
            {
                new OrderItemSource { ProductId = 101, Quantity = 2, Price = 99.99m },
                new OrderItemSource { ProductId = 102, Quantity = 1, Price = 149.99m }
            }
        };
        
        try
        {
            var target = source.Adapt<OrderTarget>();
            
            if (target != null)
            {
                Console.WriteLine($"  ? OrderId: {target.OrderId}");
                Console.WriteLine($"  ? Customer.Name: {target.Customer?.Name}");
                Console.WriteLine($"  ? Customer.Address.City: {target.Customer?.Address?.City}");
                Console.WriteLine($"  ? Items.Count: {target.Items?.Count}");
                
                if (target.Items?.Count == 2)
                {
                    var totalAmount = 0m;
                    foreach (var item in target.Items)
                    {
                        totalAmount += item.Quantity * item.Price;
                    }
                    Console.WriteLine($"  ? ◊‹Ω∂Ó: ${totalAmount}");
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"  ? ≤‚ ‘ ß∞‹: {ex.Message}");
        }
        
        Console.WriteLine();
    }
    
    private static void TestMultiLevelNesting()
    {
        Console.WriteLine("2. ∂‡≤„«∂Ã◊”≥…‰ (5≤„…Ó∂»)");
        
        var source = new Level1
        {
            Id = 1,
            Name = "Level 1",
            Level2 = new Level2
            {
                Id = 2,
                Name = "Level 2",
                Level3 = new Level3
                {
                    Id = 3,
                    Name = "Level 3",
                    Level4 = new Level4
                    {
                        Id = 4,
                        Name = "Level 4",
                        Level5 = new Level5
                        {
                            Id = 5,
                            Name = "Level 5",
                            Value = "Deep Value"
                        }
                    }
                }
            }
        };
        
        try
        {
            var target = source.Adapt<Level1Target>();
            
            if (target?.Level2?.Level3?.Level4?.Level5?.Value == "Deep Value")
            {
                Console.WriteLine($"  ? 5≤„«∂Ã◊”≥…‰≥…π¶");
                Console.WriteLine($"  ? Level 5 Value: {target.Level2.Level3.Level4.Level5.Value}");
            }
            else
            {
                Console.WriteLine($"  ? 5≤„«∂Ã◊”≥…‰ ß∞‹");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"  ? ≤‚ ‘ ß∞‹: {ex.Message}");
        }
        
        Console.WriteLine();
    }
    
    private static void TestCollectionInObject()
    {
        Console.WriteLine("3. ∂‘œÛ÷–∞¸∫¨ºØ∫œ");
        
        var source = new DepartmentSource
        {
            DeptId = 100,
            DeptName = "Engineering",
            Employees = new List<EmployeeSource>
            {
                new EmployeeSource { EmpId = 1, Name = "Alice", Salary = 80000 },
                new EmployeeSource { EmpId = 2, Name = "Bob", Salary = 90000 },
                new EmployeeSource { EmpId = 3, Name = "Charlie", Salary = 85000 }
            },
            Manager = new EmployeeSource
            {
                EmpId = 99,
                Name = "Manager Dave",
                Salary = 120000
            }
        };
        
        try
        {
            var target = source.Adapt<DepartmentTarget>();
            
            if (target != null)
            {
                Console.WriteLine($"  ? Department: {target.DeptName}");
                Console.WriteLine($"  ? Employees: {target.Employees?.Count}");
                Console.WriteLine($"  ? Manager: {target.Manager?.Name}");
                
                if (target.Employees?.Count == 3)
                {
                    var totalSalary = 0m;
                    foreach (var emp in target.Employees)
                    {
                        totalSalary += emp.Salary;
                    }
                    Console.WriteLine($"  ? ◊‹π§◊ : ${totalSalary}");
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"  ? ≤‚ ‘ ß∞‹: {ex.Message}");
        }
        
        Console.WriteLine();
    }
    
    private static void TestObjectInCollection()
    {
        Console.WriteLine("4. ºØ∫œ÷–∞¸∫¨∏¥‘”∂‘œÛ");
        
        var source = new List<ProductWithCategorySource>
        {
            new ProductWithCategorySource
            {
                ProductId = 1,
                ProductName = "Laptop",
                Price = 999.99m,
                Category = new CategorySource { CategoryId = 10, CategoryName = "Electronics" }
            },
            new ProductWithCategorySource
            {
                ProductId = 2,
                ProductName = "Mouse",
                Price = 29.99m,
                Category = new CategorySource { CategoryId = 10, CategoryName = "Electronics" }
            },
            new ProductWithCategorySource
            {
                ProductId = 3,
                ProductName = "Desk",
                Price = 299.99m,
                Category = new CategorySource { CategoryId = 20, CategoryName = "Furniture" }
            }
        };
        
        try
        {
            var target = source.Adapt<List<ProductWithCategoryTarget>>();
            
            if (target != null && target.Count == 3)
            {
                Console.WriteLine($"  ? ≤˙∆∑ ˝¡ø: {target.Count}");
                
                foreach (var product in target)
                {
                    Console.WriteLine($"  ? {product.ProductName} - ${product.Price} ({product.Category?.CategoryName})");
                }
            }
            else
            {
                Console.WriteLine($"  ? ºØ∫œ”≥…‰ ß∞‹");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"  ? ≤‚ ‘ ß∞‹: {ex.Message}");
        }
        
        Console.WriteLine();
    }
    
    #region Test Models
    
    // Order Models
    public class OrderSource
    {
        public int OrderId { get; set; }
        public DateTime OrderDate { get; set; }
        public CustomerSource? Customer { get; set; }
        public List<OrderItemSource>? Items { get; set; }
    }
    
    public class OrderTarget
    {
        public int OrderId { get; set; }
        public DateTime OrderDate { get; set; }
        public CustomerTarget? Customer { get; set; }
        public List<OrderItemTarget>? Items { get; set; }
    }
    
    public class CustomerSource
    {
        public int CustomerId { get; set; }
        public string? Name { get; set; }
        public string? Email { get; set; }
        public AddressSource? Address { get; set; }
    }
    
    public class CustomerTarget
    {
        public int CustomerId { get; set; }
        public string? Name { get; set; }
        public string? Email { get; set; }
        public AddressTarget? Address { get; set; }
    }
    
    public class AddressSource
    {
        public string? Street { get; set; }
        public string? City { get; set; }
        public string? ZipCode { get; set; }
    }
    
    public class AddressTarget
    {
        public string? Street { get; set; }
        public string? City { get; set; }
        public string? ZipCode { get; set; }
    }
    
    public class OrderItemSource
    {
        public int ProductId { get; set; }
        public int Quantity { get; set; }
        public decimal Price { get; set; }
    }
    
    public class OrderItemTarget
    {
        public int ProductId { get; set; }
        public int Quantity { get; set; }
        public decimal Price { get; set; }
    }
    
    // Multi-Level Nesting Models
    public class Level1
    {
        public int Id { get; set; }
        public string? Name { get; set; }
        public Level2? Level2 { get; set; }
    }
    
    public class Level1Target
    {
        public int Id { get; set; }
        public string? Name { get; set; }
        public Level2Target? Level2 { get; set; }
    }
    
    public class Level2
    {
        public int Id { get; set; }
        public string? Name { get; set; }
        public Level3? Level3 { get; set; }
    }
    
    public class Level2Target
    {
        public int Id { get; set; }
        public string? Name { get; set; }
        public Level3Target? Level3 { get; set; }
    }
    
    public class Level3
    {
        public int Id { get; set; }
        public string? Name { get; set; }
        public Level4? Level4 { get; set; }
    }
    
    public class Level3Target
    {
        public int Id { get; set; }
        public string? Name { get; set; }
        public Level4Target? Level4 { get; set; }
    }
    
    public class Level4
    {
        public int Id { get; set; }
        public string? Name { get; set; }
        public Level5? Level5 { get; set; }
    }
    
    public class Level4Target
    {
        public int Id { get; set; }
        public string? Name { get; set; }
        public Level5Target? Level5 { get; set; }
    }
    
    public class Level5
    {
        public int Id { get; set; }
        public string? Name { get; set; }
        public string? Value { get; set; }
    }
    
    public class Level5Target
    {
        public int Id { get; set; }
        public string? Name { get; set; }
        public string? Value { get; set; }
    }
    
    // Department Models
    public class DepartmentSource
    {
        public int DeptId { get; set; }
        public string? DeptName { get; set; }
        public List<EmployeeSource>? Employees { get; set; }
        public EmployeeSource? Manager { get; set; }
    }
    
    public class DepartmentTarget
    {
        public int DeptId { get; set; }
        public string? DeptName { get; set; }
        public List<EmployeeTarget>? Employees { get; set; }
        public EmployeeTarget? Manager { get; set; }
    }
    
    public class EmployeeSource
    {
        public int EmpId { get; set; }
        public string? Name { get; set; }
        public decimal Salary { get; set; }
    }
    
    public class EmployeeTarget
    {
        public int EmpId { get; set; }
        public string? Name { get; set; }
        public decimal Salary { get; set; }
    }
    
    // Product Models
    public class ProductWithCategorySource
    {
        public int ProductId { get; set; }
        public string? ProductName { get; set; }
        public decimal Price { get; set; }
        public CategorySource? Category { get; set; }
    }
    
    public class ProductWithCategoryTarget
    {
        public int ProductId { get; set; }
        public string? ProductName { get; set; }
        public decimal Price { get; set; }
        public CategoryTarget? Category { get; set; }
    }
    
    public class CategorySource
    {
        public int CategoryId { get; set; }
        public string? CategoryName { get; set; }
    }
    
    public class CategoryTarget
    {
        public int CategoryId { get; set; }
        public string? CategoryName { get; set; }
    }
    
    #endregion
}
