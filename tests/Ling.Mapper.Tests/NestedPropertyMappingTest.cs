using System;
using Ling.Mapper;
using Ling.Mapper.Extensions;

namespace Ling.Mapper.Tests
{
    /// <summary>
    /// 嵌套属性映射测试（支持 A.B.C.D）
    /// </summary>
    public static class NestedPropertyMappingTest
    {
        public static void Run()
        {
            Console.WriteLine("=== 嵌套属性映射测试 (A.B.C.D) ===\n");

            Test1_SimpleNestedProperty();
            Test2_DeepNestedProperty();
            Test3_MultipleNestedProperties();
            Test4_NestedWithRename();

            Console.WriteLine("\n=== 所有测试通过 ===");
        }

        private static void Test1_SimpleNestedProperty()
        {
            Console.WriteLine("【测试1】简单嵌套属性映射 (User.Name)");

            var config = new MapperConfiguration();
            config.AddProfile(new SimpleNestedProfile());
            var mapper = config.CreateMapper();
            MapperProvider.SetCurrent(mapper);

            var source = new OrderSource
            {
                Id = 1,
                User = new UserInfo
                {
                    Name = "张三",
                    Age = 30
                }
            };

            var dest = source.Adapt<OrderDto>();

            Console.WriteLine($"✓ Order ID: {dest?.Id}");
            Console.WriteLine($"✓ User Name (from User.Name): {dest?.UserName}");
            Console.WriteLine($"✓ User Age (from User.Age): {dest?.UserAge}");

            if (dest?.UserName == "张三" && dest?.UserAge == 30)
            {
                Console.WriteLine("✅ 测试通过：简单嵌套属性映射成功\n");
            }
            else
            {
                Console.WriteLine("❌ 测试失败\n");
            }
        }

        private static void Test2_DeepNestedProperty()
        {
            Console.WriteLine("【测试2】深层嵌套属性映射 (Company.Address.City)");

            var config = new MapperConfiguration();
            config.AddProfile(new DeepNestedProfile());
            var mapper = config.CreateMapper();
            MapperProvider.SetCurrent(mapper);

            var source = new EmployeeSource
            {
                Id = 1,
                Name = "李四",
                Company = new CompanyInfo
                {
                    Name = "科技公司",
                    Address = new AddressInfo
                    {
                        Street = "中关村大街1号",
                        City = "北京",
                        ZipCode = "100000"
                    }
                }
            };

            var dest = source.Adapt<EmployeeDto>();

            Console.WriteLine($"✓ Employee: {dest?.Name}");
            Console.WriteLine($"✓ Company: {dest?.CompanyName}");
            Console.WriteLine($"✓ City (from Company.Address.City): {dest?.WorkCity}");
            Console.WriteLine($"✓ ZipCode (from Company.Address.ZipCode): {dest?.WorkZipCode}");

            if (dest?.WorkCity == "北京" && dest?.WorkZipCode == "100000")
            {
                Console.WriteLine("✅ 测试通过：深层嵌套属性映射成功\n");
            }
            else
            {
                Console.WriteLine("❌ 测试失败\n");
            }
        }

        private static void Test3_MultipleNestedProperties()
        {
            Console.WriteLine("【测试3】多个嵌套属性映射");

            var config = new MapperConfiguration();
            config.AddProfile(new MultipleNestedProfile());
            var mapper = config.CreateMapper();
            MapperProvider.SetCurrent(mapper);

            var source = new ProductSource
            {
                Id = 1,
                Name = "笔记本电脑",
                Supplier = new SupplierInfo
                {
                    Name = "供应商A",
                    Contact = new ContactInfo
                    {
                        Name = "王五",
                        Phone = "13800138000",
                        Email = "wangwu@example.com"
                    }
                },
                Price = new PriceInfo
                {
                    Amount = 5999.00m,
                    Currency = "CNY"
                }
            };

            var dest = source.Adapt<ProductDto>();

            Console.WriteLine($"✓ Product: {dest?.Name}");
            Console.WriteLine($"✓ Supplier (from Supplier.Name): {dest?.SupplierName}");
            Console.WriteLine($"✓ Contact (from Supplier.Contact.Name): {dest?.ContactName}");
            Console.WriteLine($"✓ Phone (from Supplier.Contact.Phone): {dest?.ContactPhone}");
            Console.WriteLine($"✓ Price (from Price.Amount): {dest?.PriceAmount}");
            Console.WriteLine($"✓ Currency (from Price.Currency): {dest?.Currency}");

            if (dest?.SupplierName == "供应商A" && 
                dest?.ContactName == "王五" && 
                dest?.PriceAmount == 5999.00m)
            {
                Console.WriteLine("✅ 测试通过：多个嵌套属性映射成功\n");
            }
            else
            {
                Console.WriteLine("❌ 测试失败\n");
            }
        }

        private static void Test4_NestedWithRename()
        {
            Console.WriteLine("【测试4】嵌套属性 + Rename");

            var config = new MapperConfiguration();
            config.AddProfile(new NestedRenameProfile());
            var mapper = config.CreateMapper();
            MapperProvider.SetCurrent(mapper);

            var source = new OrderSource
            {
                Id = 100,
                User = new UserInfo
                {
                    Name = "测试用户",
                    Age = 25
                }
            };

            var dest = source.Adapt<OrderSummaryDto>();

            Console.WriteLine($"✓ Order ID: {dest?.OrderId}");
            Console.WriteLine($"✓ Customer (renamed from User.Name): {dest?.CustomerName}");

            if (dest?.OrderId == 100 && dest?.CustomerName == "测试用户")
            {
                Console.WriteLine("✅ 测试通过：嵌套属性 + Rename 成功\n");
            }
            else
            {
                Console.WriteLine("❌ 测试失败\n");
            }
        }
    }

    #region 测试数据模型

    // 简单嵌套测试
    public class OrderSource
    {
        public int Id { get; set; }
        public UserInfo User { get; set; }
    }

    public class UserInfo
    {
        public string Name { get; set; }
        public int Age { get; set; }
    }

    public class OrderDto
    {
        public int Id { get; set; }
        public string UserName { get; set; }
        public int UserAge { get; set; }
    }

    public class SimpleNestedProfile : MapperProfile
    {
        public SimpleNestedProfile()
        {
            CreateMap<OrderSource, OrderDto>()
                .Rename(d => d.UserName, "User.Name")
                .Rename(d => d.UserAge, "User.Age");
        }
    }

    // 深层嵌套测试
    public class EmployeeSource
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public CompanyInfo Company { get; set; }
    }

    public class CompanyInfo
    {
        public string Name { get; set; }
        public AddressInfo Address { get; set; }
    }

    public class AddressInfo
    {
        public string Street { get; set; }
        public string City { get; set; }
        public string ZipCode { get; set; }
    }

    public class EmployeeDto
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public string CompanyName { get; set; }
        public string WorkCity { get; set; }
        public string WorkZipCode { get; set; }
    }

    public class DeepNestedProfile : MapperProfile
    {
        public DeepNestedProfile()
        {
            CreateMap<EmployeeSource, EmployeeDto>()
                .Rename(d => d.CompanyName, "Company.Name")
                .Rename(d => d.WorkCity, "Company.Address.City")
                .Rename(d => d.WorkZipCode, "Company.Address.ZipCode");
        }
    }

    // 多个嵌套属性测试
    public class ProductSource
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public SupplierInfo Supplier { get; set; }
        public PriceInfo Price { get; set; }
    }

    public class SupplierInfo
    {
        public string Name { get; set; }
        public ContactInfo Contact { get; set; }
    }

    public class ContactInfo
    {
        public string Name { get; set; }
        public string Phone { get; set; }
        public string Email { get; set; }
    }

    public class PriceInfo
    {
        public decimal Amount { get; set; }
        public string Currency { get; set; }
    }

    public class ProductDto
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public string SupplierName { get; set; }
        public string ContactName { get; set; }
        public string ContactPhone { get; set; }
        public decimal PriceAmount { get; set; }
        public string Currency { get; set; }
    }

    public class MultipleNestedProfile : MapperProfile
    {
        public MultipleNestedProfile()
        {
            CreateMap<ProductSource, ProductDto>()
                .Rename(d => d.SupplierName, "Supplier.Name")
                .Rename(d => d.ContactName, "Supplier.Contact.Name")
                .Rename(d => d.ContactPhone, "Supplier.Contact.Phone")
                .Rename(d => d.PriceAmount, "Price.Amount")
                .Rename(d => d.Currency, "Price.Currency");
        }
    }

    // Rename 测试
    public class OrderSummaryDto
    {
        public int OrderId { get; set; }
        public string CustomerName { get; set; }
    }

    public class NestedRenameProfile : MapperProfile
    {
        public NestedRenameProfile()
        {
            CreateMap<OrderSource, OrderSummaryDto>()
                .Rename(d => d.OrderId, "Id")
                .Rename(d => d.CustomerName, "User.Name");
        }
    }

    #endregion
}
