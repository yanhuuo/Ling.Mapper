using System;
using System.Collections.Generic;
using System.Linq;
using Ling.Mapper;
using Ling.Mapper.Extensions;

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
