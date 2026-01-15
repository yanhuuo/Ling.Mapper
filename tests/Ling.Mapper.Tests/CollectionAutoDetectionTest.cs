using System;
using System.Collections.Generic;
using System.Linq;
using Ling.Mapper;

namespace Ling.Mapper.Tests
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
            Test2_IEnumerableAutoDetection();
            Test3_ArrayAutoDetection();
            Test4_WithCallback();
            Test5_CompareWithAdaptList();
            Test6_NestedCollection();

            Console.WriteLine("\n=== 所有测试通过 ===");
        }

        private static void Test1_ListAutoDetection()
        {
            Console.WriteLine("【测试1】List<T> 自动识别");

            var entities = new List<UserEntity>
            {
                new UserEntity { Id = 1, Name = "张三", Email = "zhang@test.com" },
                new UserEntity { Id = 2, Name = "李四", Email = "li@test.com" },
                new UserEntity { Id = 3, Name = "王五", Email = "wang@test.com" }
            };

            // 🎉 自动识别为集合映射
            var dtos = entities.Adapt<List<UserDto>>();

            Console.WriteLine($"✓ 映射成功，共 {dtos?.Count} 项:");
            foreach (var dto in dtos ?? new List<UserDto>())
            {
                Console.WriteLine($"  - {dto.Name} ({dto.Email})");
            }
            Console.WriteLine();
        }

        private static void Test2_IEnumerableAutoDetection()
        {
            Console.WriteLine("【测试2】IEnumerable<T> 自动识别");

            var entities = GetEntities();

            // 🎉 自动识别 IEnumerable<T>
            var dtos = entities.Adapt<IEnumerable<UserDto>>();

            Console.WriteLine($"✓ 映射成功，共 {dtos?.Count()} 项");
            Console.WriteLine();
        }

        private static void Test3_ArrayAutoDetection()
        {
            Console.WriteLine("【测试3】数组 T[] 自动识别");

            var entities = new[]
            {
                new UserEntity { Id = 1, Name = "User1", Email = "u1@test.com" },
                new UserEntity { Id = 2, Name = "User2", Email = "u2@test.com" }
            };

            // 🎉 自动识别数组
            var dtos = entities.Adapt<UserDto[]>();

            Console.WriteLine($"✓ 映射成功，数组长度 {dtos?.Length ?? 0}");
            Console.WriteLine();
        }

        private static void Test4_WithCallback()
        {
            Console.WriteLine("【测试4】带回调的集合映射");

            var entities = GetEntities().ToList();

            // 🎉 带回调处理整个列表
            var dtos = entities.Adapt<List<UserDto>>((result, source) =>
            {
                // 回调接收的是整个映射后的列表
                if (result != null)
                {
                    Console.WriteLine($"  回调被触发，列表有 {result.Count} 项");
                    
                    // 可以对整个列表进行后处理
                    for (int i = 0; i < result.Count; i++)
                    {
                        result[i].DisplayName = $"[{i + 1}] {result[i].Name}";
                    }
                }
            });

            Console.WriteLine($"✓ 映射成功，带后处理:");
            foreach (var dto in dtos ?? new List<UserDto>())
            {
                Console.WriteLine($"  - {dto.DisplayName}");
            }
            Console.WriteLine();
        }

        private static void Test5_CompareWithAdaptList()
        {
            Console.WriteLine("【测试5】对比 Adapt 与 AdaptList");

            var entities = GetEntities().ToList();

            // 方式 1: Adapt 自动识别（回调处理整个列表）
            var dtos1 = entities.Adapt<List<UserDto>>((list, _) =>
            {
                if (list != null)
                {
                    foreach (var dto in list)
                    {
                        dto.DisplayName = $"Adapt: {dto.Name}";
                    }
                }
            });

            // 方式 2: Adapt 自动识别 + 手动处理索引
            var dtos2 = entities.Adapt<List<UserDto>>((list, src) =>
            {
                if (list != null)
                {
                    var sourceList = src as List<UserEntity>;
                    for (int index = 0; index < list.Count; index++)
                    {
                        var entity = sourceList?[index];
                        list[index].DisplayName = $"Adapt[{index}]: {entity?.Name}";
                    }
                }
            });

            Console.WriteLine($"✓ Adapt 方式1: {dtos1?.Count} 项");
            Console.WriteLine($"✓ Adapt 方式2（带索引）: {dtos2?.Count} 项");
            Console.WriteLine("\n  对比结果:");
            Console.WriteLine($"    方式1 第一项: {dtos1?[0].DisplayName}");
            Console.WriteLine($"    方式2 第一项: {dtos2?[0].DisplayName}");
            Console.WriteLine("\n  结论: Adapt 自动识别集合类型，可在回调中灵活处理");
            Console.WriteLine();
        }

        private static void Test6_NestedCollection()
        {
            Console.WriteLine("【测试6】嵌套集合映射");

            var department = new DepartmentEntity
            {
                Name = "技术部",
                Users = new List<UserEntity>
                {
                    new UserEntity { Id = 1, Name = "张三", Email = "zhang@test.com" },
                    new UserEntity { Id = 2, Name = "李四", Email = "li@test.com" }
                }
            };

            // 🎉 自动映射嵌套集合
            var deptDto = department.Adapt<DepartmentDto>();

            Console.WriteLine($"✓ 部门映射成功: {deptDto?.Name}");
            Console.WriteLine($"✓ 嵌套用户列表: {deptDto?.Users?.Count ?? 0} 项");
            foreach (var user in deptDto?.Users ?? new List<UserDto>())
            {
                Console.WriteLine($"  - {user.Name}");
            }
            Console.WriteLine();
        }

        // 辅助方法
        private static IEnumerable<UserEntity> GetEntities()
        {
            yield return new UserEntity { Id = 1, Name = "实体1", Email = "e1@test.com" };
            yield return new UserEntity { Id = 2, Name = "实体2", Email = "e2@test.com" };
            yield return new UserEntity { Id = 3, Name = "实体3", Email = "e3@test.com" };
        }
    }

    // 测试用的实体和 DTO
    public class UserEntity
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
    }

    public class UserDto
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string DisplayName { get; set; } = string.Empty;
    }

    public class DepartmentEntity
    {
        public string Name { get; set; } = string.Empty;
        public List<UserEntity> Users { get; set; } = new List<UserEntity>();
    }

    public class DepartmentDto
    {
        public string Name { get; set; } = string.Empty;
        public List<UserDto> Users { get; set; } = new List<UserDto>();
    }
}
