using Ling.Mapper;
using System;
using System.Collections.Generic;
using System.Linq;
using Ling.Mapper.Extensions;

namespace TestConsole;

/// <summary>
/// 演示 Adapt 方法的多样化用法，特别是 List 转换
/// </summary>
public static class AdaptListDemo
{
    private static int _passedTests = 0;
    private static int _failedTests = 0;

    public static void Run()
    {
        Console.WriteLine("\n=== Adapt List 转换演示 ===\n");

        _passedTests = 0;
        _failedTests = 0;

        try
        {
            Test1_BasicListConversion();
            Test2_ListConversionWithCallback();
            Test3_PageResultConversion();
            Test4_NestedListConversion();

            Console.WriteLine($"\n📊 测试统计: ✅ {_passedTests} 通过, ❌ {_failedTests} 失败");
            if (_failedTests == 0)
            {
                Console.WriteLine("✅ 所有 Adapt List 测试通过！\n");
            }
            else
            {
                Console.WriteLine($"⚠️  {_failedTests} 个测试失败\n");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"\n❌ 测试执行异常: {ex.Message}\n");
        }
    }

    private static void Test1_BasicListConversion()
    {
        Console.WriteLine("【测试1】基础 List 转换");

        // 准备测试数据
        var sourceCustomers = new List<CustomerEntity>
        {
            new CustomerEntity { Id = 1, FirstName = "Zhang", LastName = "San", Age = 25, Email = "zhangsan@example.com" },
            new CustomerEntity { Id = 2, FirstName = "Li", LastName = "Si", Age = 30, Email = "lisi@example.com" },
            new CustomerEntity { Id = 3, FirstName = "Wang", LastName = "Wu", Age = 28, Email = "wangwu@example.com" }
        };

        // 使用 Adapt<List<T>>() 进行转换
        var customerDtos = sourceCustomers.Adapt<List<CustomerDto>>();

        // 验证结果
        AssertNotNull(customerDtos, "转换结果不应为 null");
        AssertEqual(3, customerDtos.Count, "List 长度");
        
        AssertEqual(1, customerDtos[0].Id, "第1个客户 ID");
        AssertEqual("Zhang", customerDtos[0].FirstName, "第1个客户 FirstName");
        AssertEqual("San", customerDtos[0].LastName, "第1个客户 LastName");
        AssertEqual(25, customerDtos[0].Age, "第1个客户 Age");
        AssertEqual("zhangsan@example.com", customerDtos[0].Email, "第1个客户 Email");

        Console.WriteLine("  ✅ 基础 List 转换成功");
        Console.WriteLine();
    }

    private static void Test2_ListConversionWithCallback()
    {
        Console.WriteLine("【测试2】带回调的 List 转换");

        var sourceCustomers = new List<CustomerEntity>
        {
            new CustomerEntity { Id = 1, FirstName = "Zhang", LastName = "San", Age = 25, Email = "zhangsan@example.com" },
            new CustomerEntity { Id = 2, FirstName = "Li", LastName = "Si", Age = 30, Email = "lisi@example.com" },
            new CustomerEntity { Id = 3, FirstName = "Wang", LastName = "Wu", Age = 28, Email = "wangwu@example.com" }
        };

        // 使用回调进行后处理
        var customerDtos = sourceCustomers.Adapt<List<CustomerDto>>((dtoList, srcList) =>
        {
            if (dtoList == null) return;
            var sources = srcList as List<CustomerEntity>;

            for (int i = 0; i < dtoList.Count; i++)
            {
                var dto = dtoList[i];
                var src = sources?[i];
                
                dto.RowNumber = i + 1;
                dto.DisplayName = $"{src?.FirstName} {src?.LastName}";
                dto.IsFirst = (i == 0);
                dto.IsLast = (i == dtoList.Count - 1);
                dto.AgeGroup = src?.Age < 30 ? "青年" : "中年";
            }
        });

        // 验证结果
        AssertNotNull(customerDtos, "转换结果不应为 null");
        AssertEqual(3, customerDtos.Count, "List 长度");

        // 验证第1个客户
        AssertEqual(1, customerDtos[0].RowNumber, "第1个客户 RowNumber");
        AssertEqual("Zhang San", customerDtos[0].DisplayName, "第1个客户 DisplayName");
        AssertTrue(customerDtos[0].IsFirst, "第1个客户应该是首个");
        AssertFalse(customerDtos[0].IsLast, "第1个客户不应该是最后");
        AssertEqual("青年", customerDtos[0].AgeGroup, "第1个客户年龄段");

        // 验证最后1个客户
        AssertEqual(3, customerDtos[2].RowNumber, "最后客户 RowNumber");
        AssertFalse(customerDtos[2].IsFirst, "最后客户不应该是首个");
        AssertTrue(customerDtos[2].IsLast, "最后客户应该是最后");

        Console.WriteLine("  ✅ 带回调的 List 转换成功");
        PrintCustomers(customerDtos);
        Console.WriteLine();
    }

    private static void Test3_PageResultConversion()
    {
        Console.WriteLine("【测试3】分页结果转换");

        var sourceCustomers = new List<CustomerEntity>
        {
            new CustomerEntity { Id = 1, FirstName = "Zhang", LastName = "San", Age = 25, Email = "zhangsan@example.com" },
            new CustomerEntity { Id = 2, FirstName = "Li", LastName = "Si", Age = 30, Email = "lisi@example.com" }
        };

        var pageResult = new PageResult<CustomerEntity>
        {
            Page = 1,
            Size = 10,
            Total = 2,
            Data = sourceCustomers
        };

        // 转换分页数据
        var dtoPageResult = new PageResult<CustomerDto>
        {
            Page = pageResult.Page,
            Size = pageResult.Size,
            Total = pageResult.Total,
            Data = pageResult.Data.Adapt<List<CustomerDto>>()
        };

        // 验证结果
        AssertNotNull(dtoPageResult.Data, "分页数据不应为 null");
        AssertEqual(2, dtoPageResult.Data.Count, "分页数据长度");
        AssertEqual(1, dtoPageResult.Page, "页码");
        AssertEqual(10, dtoPageResult.Size, "页大小");
        AssertEqual(2, dtoPageResult.Total, "总数");

        Console.WriteLine($"  ✅ 分页结果转换成功 (Page {dtoPageResult.Page}, Total {dtoPageResult.Total})");
        Console.WriteLine();
    }

    private static void Test4_NestedListConversion()
    {
        Console.WriteLine("【测试4】嵌套对象中的 List 转换");

        var department = new DepartmentEntity
        {
            Id = 1,
            Name = "技术部",
            Customers = new List<CustomerEntity>
            {
                new CustomerEntity { Id = 1, FirstName = "Zhang", LastName = "San", Age = 25, Email = "zhangsan@example.com" },
                new CustomerEntity { Id = 2, FirstName = "Li", LastName = "Si", Age = 30, Email = "lisi@example.com" }
            }
        };

        var deptDto = department.Adapt<DepartmentDto>();

        // 验证结果
        AssertNotNull(deptDto, "部门 DTO 不应为 null");
        AssertEqual("技术部", deptDto.Name, "部门名称");
        AssertNotNull(deptDto.Customers, "客户列表不应为 null");
        AssertEqual(2, deptDto.Customers.Count, "客户列表长度");
        AssertEqual("Zhang", deptDto.Customers[0].FirstName, "第1个客户名称");

        Console.WriteLine($"  ✅ 嵌套 List 转换成功 (部门: {deptDto.Name}, 客户数: {deptDto.Customers.Count})");
        Console.WriteLine();
    }

    // ========== 断言方法 ==========

    private static void AssertNotNull<T>(T obj, string message) where T : class
    {
        if (obj == null)
        {
            Console.WriteLine($"  ❌ {message} - 对象为 null");
            _failedTests++;
        }
        else
        {
            _passedTests++;
        }
    }

    private static void AssertEqual<T>(T expected, T actual, string message)
    {
        if (!Equals(expected, actual))
        {
            Console.WriteLine($"  ❌ {message}: 期望 {expected}, 实际 {actual}");
            _failedTests++;
        }
        else
        {
            _passedTests++;
        }
    }

    private static void AssertTrue(bool condition, string message)
    {
        if (!condition)
        {
            Console.WriteLine($"  ❌ {message}");
            _failedTests++;
        }
        else
        {
            _passedTests++;
        }
    }

    private static void AssertFalse(bool condition, string message)
    {
        if (condition)
        {
            Console.WriteLine($"  ❌ {message}");
            _failedTests++;
        }
        else
        {
            _passedTests++;
        }
    }

    // ========== 辅助方法 ==========

    private static void PrintCustomers(List<CustomerDto>? customers)
    {
        if (customers == null || customers.Count == 0)
        {
            Console.WriteLine("    (无数据)");
            return;
        }

        foreach (var customer in customers)
        {
            Console.Write($"    [{customer.RowNumber}] {customer.DisplayName}");
            if (customer.IsFirst) Console.Write(" [首个]");
            if (customer.IsLast) Console.Write(" [最后]");
            if (!string.IsNullOrEmpty(customer.AgeGroup)) Console.Write($" [{customer.AgeGroup}]");
            Console.WriteLine();
        }
    }
}

// ========== 实体类 ==========

public class CustomerEntity
{
    public int Id { get; set; }
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public int Age { get; set; }
    public string Email { get; set; } = string.Empty;
}

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

public class DepartmentEntity
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public List<CustomerEntity> Customers { get; set; } = new List<CustomerEntity>();
}

public class DepartmentDto
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public List<CustomerDto> Customers { get; set; } = new List<CustomerDto>();
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
        CreateMap<DepartmentEntity, DepartmentDto>();
    }
}
