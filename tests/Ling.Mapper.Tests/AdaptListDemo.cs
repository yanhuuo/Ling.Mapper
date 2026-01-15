using Ling.Mapper;
using System;
using System.Collections.Generic;
using Ling.Mapper.Extensions;

namespace TestConsole;

/// <summary>
/// 演示 Adapt 方法的多样化用法，特别是 List 转换
/// </summary>
public static class AdaptListDemo
{
    public static void Run()
    {
        Console.WriteLine("\n=== Adapt List 转换演示 ===\n");

        // 准备测试数据
        var sourceCustomers = new List<CustomerEntity>
        {
            new CustomerEntity { Id = 1, FirstName = "Zhang", LastName = "San", Age = 25, Email = "zhangsan@example.com" },
            new CustomerEntity { Id = 2, FirstName = "Li", LastName = "Si", Age = 30, Email = "lisi@example.com" },
            new CustomerEntity { Id = 3, FirstName = "Wang", LastName = "Wu", Age = 28, Email = "wangwu@example.com" }
        };

        

        // 方式 3：使用 Adapt<List<T>>() 对每个元素单独处理
        Console.WriteLine("\n方式 3: 使用 Adapt<List<T>>() 逐个处理元素");
        var customerDtos3 = sourceCustomers.Adapt<List<CustomerDto>>();
        PrintCustomers(customerDtos3);

        // 方式 4：模拟分页场景 - page.Data.AdaptToList<TDto, TEntity>()
        Console.WriteLine("\n方式 4: 模拟分页场景 - page.Data.AdaptToList<TDto, TEntity>()");
        var pageResult = new PageResult<CustomerEntity>
        {
            Page = 1,
            Size = 10,
            Total = 3,
            Data = sourceCustomers
        };

        

        //// 方式 5：简化写法（使用 Adapt 自动识别集合）
        //Console.WriteLine("\n方式 5: 使用 Adapt<List<T>>() 自动识别集合");
        //var customerDtos5 = sourceCustomers.Adapt<List<CustomerDto>>((dtoList, srcList) =>
        //{
        //    if (dtoList == null) return;
            
        //    for (int index = 0; index < dtoList.Count; index++)
        //    {
        //        dtoList[index].RowNumber = index + 1;
        //        dtoList[index].DisplayName = $"Customer #{index + 1}";
        //    }
        //});
        //PrintCustomers(customerDtos5);
    }

    private static string FormatCustomerName(CustomerDto customer)
    {
        return $"{customer.FirstName} {customer.LastName} ({customer.Age}岁)";
    }

    private static void PrintCustomers(List<CustomerDto>? customers)
    {
        if (customers == null || customers.Count == 0)
        {
            Console.WriteLine("  (无数据)");
            return;
        }

        foreach (var customer in customers)
        {
            Console.WriteLine($"  [{customer.RowNumber}] {customer.DisplayName} - Email: {customer.Email}" +
                            (customer.IsFirst ? " [首个]" : "") +
                            (customer.IsLast ? " [最后]" : "") +
                            (customer.AgeGroup != null ? $" - 年龄段: {customer.AgeGroup}" : ""));
        }
    }
}

// 实体类
public class CustomerEntity
{
    public int Id { get; set; }
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public int Age { get; set; }
    public string Email { get; set; } = string.Empty;
}

// DTO 类
public class CustomerDto
{
    public int Id { get; set; }
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public int Age { get; set; }
    public string Email { get; set; } = string.Empty;
    public int RowNumber { get; set; }
    public string DisplayName { get; set; } = string.Empty;
    public bool IsFirst { get; set; }
    public bool IsLast { get; set; }
    public string? AgeGroup { get; set; }
}

// 分页结果类
public class PageResult<T>
{
    public int Page { get; set; }
    public int Size { get; set; }
    public int Total { get; set; }
    public List<T> Data { get; set; } = new List<T>();
}

// Mapper 配置
public class CustomerDemoProfile : MapperProfile
{
    public CustomerDemoProfile()
    {
        CreateMap<CustomerEntity, CustomerDto>();
    }
}
