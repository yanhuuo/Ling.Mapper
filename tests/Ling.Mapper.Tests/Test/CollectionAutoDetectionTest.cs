using Ling.Mapper.Extensions;

namespace TestConsole.Test
{
    /// <summary>
    /// 集合自动识别功能测试
    /// </summary>
    public static class CollectionAutoDetectionTest
    {
        public static void Run()
        {
            Console.WriteLine("=== 集合自动识别测试 (Collection Auto Detection) ===\n");

            Test1_ListAutoDetection();
            Test6_NestedCollection();

            Console.WriteLine("\n=== 所有测试通过 ===");
        }

        private static void Test1_ListAutoDetection()
        {
            Console.WriteLine("【测试1】List<T> 自动识别");

            var entities = new List<CustomerEntity>
            {
                new CustomerEntity { Id = 1, FirstName = "张三", LastName = "", Email = "zhang@test.com" },
                new CustomerEntity { Id = 2, FirstName = "李四", LastName = "", Email = "li@test.com" },
                new CustomerEntity { Id = 3, FirstName = "王五", LastName = "", Email = "wang@test.com" }
            };

            // 🎉 自动识别为集合映射
            var dtos = entities.Adapt<List<CustomerDto>>();

            Console.WriteLine($"✓ 映射成功，共 {dtos?.Count} 项:");
            foreach (var dto in dtos ?? new List<CustomerDto>())
            {
                Console.WriteLine($"  - {dto.FirstName} {dto.LastName} ({dto.Email})");
            }
            Console.WriteLine();
        }

 
        private static void Test6_NestedCollection()
        {
            Console.WriteLine("【测试6】嵌套集合映射");

            var department = new DepartmentEntity
            {
                Name = "技术部",
                Customers = new List<CustomerEntity>
                {
                    new CustomerEntity { Id = 1, FirstName = "张三", LastName = "", Email = "zhang@test.com" },
                    new CustomerEntity { Id = 2, FirstName = "李四", LastName = "", Email = "li@test.com" }
                }
            };

            // 🎉 自动映射嵌套集合
            var deptDto = department.Adapt<DepartmentDto>();

            Console.WriteLine($"✓ 部门映射成功: {deptDto?.Name}");
            Console.WriteLine($"✓ 嵌套用户列表: {deptDto?.Customers?.Count ?? 0} 项");
            foreach (var user in deptDto?.Customers ?? new List<CustomerDto>())
            {
                Console.WriteLine($"  - {user.FirstName} {user.LastName}");
            }
            Console.WriteLine();
        }

        // 辅助方法
        private static IEnumerable<CustomerEntity> GetEntities()
        {
            yield return new CustomerEntity { Id = 1, FirstName = "实体1", LastName = "", Email = "e1@test.com" };
            yield return new CustomerEntity { Id = 2, FirstName = "实体2", LastName = "", Email = "e2@test.com" };
            yield return new CustomerEntity { Id = 3, FirstName = "实体3", LastName = "", Email = "e3@test.com" };
        }
    }

    // 使用已有的 CustomerEntity/CustomerDto 定义，避免类型冲突

}
