using System;
using Ling.Mapper;
using Ling.Mapper.Extensions;

namespace TestConsole.Test;

/// <summary>
/// 基础映射测试 - 验证核心映射功能
/// </summary>
public static class BasicMappingTest
{
    public static void Run()
    {
        Console.WriteLine("--- 基础映射测试 ---\n");
        
        TestSimpleMapping();
        TestProfileMapping();
        TestForMemberMapping();
        TestIgnoreMapping();
        TestRenameMapping();
        
        Console.WriteLine();
    }
    
    private static void TestSimpleMapping()
    {
        Console.WriteLine("1. 简单对象映射");
        
        var dto = new ActivityDto
        {
            FirstName = "Tom",
            LastName = "Lee",
            Uid = 1001,
            ExtraInfoJson = "{\"Level\":3,\"Tag\":\"VIP\"}",
            Items = new List<ActivityItemDto>
            {
                new ActivityItemDto { Key = "A", Price = 99 },
                new ActivityItemDto { Key = "B", Price = 199 }
            },
            User = new ActivityUserDto { NickName = "SuperTom" }
        };
        
        try
        {
            var entity = dto.Adapt<MallActivityEntity>();
            
            if (entity != null)
            {
                var expectedName = dto.FirstName + " " + dto.LastName;
                var expectedUserId = dto.Uid;
                var expectedExtraLevel = 3;
                var expectedExtraTag = "VIP";
                var expectedUserNick = dto.User?.NickName;
                var expectedItemsCount = dto.Items?.Count ?? 0;
                var expectedInternalCode = "null";

                Console.WriteLine($"  ? Name: {entity.Name} (期望: {expectedName})");
                Console.WriteLine($"  ? UserId: {entity.UserId} (期望: {expectedUserId})");
                Console.WriteLine($"  ? ExtraInfo.Level: {entity.ExtraInfo?.Level} (期望: {expectedExtraLevel})");
                Console.WriteLine($"  ? ExtraInfo.Tag: {entity.ExtraInfo?.Tag} (期望: {expectedExtraTag})");
                Console.WriteLine($"  ? User.NickName: {entity.User?.NickName} (期望: {expectedUserNick})");
                Console.WriteLine($"  ? Items Count: {entity.Items?.Count} (期望: {expectedItemsCount})");
                Console.WriteLine($"  ? InternalCode (ignored): {entity.InternalCode ?? "null"} (期望: {expectedInternalCode})");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"  ? 测试失败: {ex.Message}");
        }
        
        Console.WriteLine();
    }
    
    private static void TestProfileMapping()
    {
        Console.WriteLine("2. Profile 配置映射");
        
        var dto = new ActivityDto
        {
            FirstName = "John",
            LastName = "Doe",
            Uid = 2001
        };
        
        try
        {
            var entity = dto.Adapt<MallActivityEntity>();
            
            // Profile 应该合并 FirstName + LastName -> Name
            var expectedName = "John Doe";
            if (entity?.Name == expectedName)
            {
                Console.WriteLine($"  ? ForMember 配置成功: Name = '{entity.Name}'");
            }
            else
            {
                Console.WriteLine($"  ? ForMember 配置失败: 期望 '{expectedName}', 实际 '{entity?.Name}'");
            }
            
            // Rename: Uid -> UserId
            if (entity?.UserId == 2001)
            {
                Console.WriteLine($"  ? Rename 配置成功: UserId = {entity.UserId}");
            }
            else
            {
                Console.WriteLine($"  ? Rename 配置失败");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"  ? 测试失败: {ex.Message}");
        }
        
        Console.WriteLine();
    }
    
    private static void TestForMemberMapping()
    {
        Console.WriteLine("3. ForMember 自定义映射");
        
        var dto = new ActivityDto
        {
            FirstName = "Alice",
            LastName = "Smith",
            Uid = 3001
        };
        
        try
        {
            var entity = dto.Adapt<MallActivityEntity, ActivityDto>((src, dest) =>
            {
                if (dest != null)
                {
                    src.Name = $"{dest.FirstName} {dest.LastName} - Custom";
                }
            });
            
            if (entity?.Name?.Contains("Custom") == true)
            {
                Console.WriteLine($"  ? 自定义映射成功: Name = '{entity.Name}'");
            }
            else
            {
                Console.WriteLine($"  ? 自定义映射失败");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"  ? 测试失败: {ex.Message}");
        }
        
        Console.WriteLine();
    }
    
    private static void TestIgnoreMapping()
    {
        Console.WriteLine("4. Ignore 属性映射");
        
        var dto = new ActivityDto
        {
            FirstName = "Bob",
            LastName = "Wilson",
            Uid = 4001
        };
        
        try
        {
            var entity = dto.Adapt<MallActivityEntity>();
            
            // InternalCode 应该被忽略（Profile 中配置）
            if (entity?.InternalCode == null)
            {
                Console.WriteLine($"  ? Ignore 配置成功: InternalCode = null");
            }
            else
            {
                Console.WriteLine($"  ? Ignore 配置失败: InternalCode = '{entity.InternalCode}'");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"  ? 测试失败: {ex.Message}");
        }
        
        Console.WriteLine();
    }
    
    private static void TestRenameMapping()
    {
        Console.WriteLine("5. Rename 属性映射");
        
        var dto = new ActivityDto
        {
            FirstName = "Charlie",
            LastName = "Brown",
            Uid = 5001
        };
        
        try
        {
            var entity = dto.Adapt<MallActivityEntity>();
            
            // Uid -> UserId (Profile 中配置)
            if (entity?.UserId == 5001)
            {
                Console.WriteLine($"  ? Rename 映射成功: Uid -> UserId = {entity.UserId}");
            }
            else
            {
                Console.WriteLine($"  ? Rename 映射失败");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"  ? 测试失败: {ex.Message}");
        }
        
        Console.WriteLine();
    }
}
