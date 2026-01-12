using System;
using Ling.Mapper;

namespace TestConsole;

/// <summary>
/// ª˘¥°”≥…‰≤‚ ‘ - —È÷§∫À–ƒ”≥…‰π¶ƒ‹
/// </summary>
public static class BasicMappingTest
{
    public static void Run()
    {
        Console.WriteLine("--- ª˘¥°”≥…‰≤‚ ‘ ---\n");
        
        TestSimpleMapping();
        TestProfileMapping();
        TestForMemberMapping();
        TestIgnoreMapping();
        TestRenameMapping();
        
        Console.WriteLine();
    }
    
    private static void TestSimpleMapping()
    {
        Console.WriteLine("1. ºÚµ•∂‘œÛ”≥…‰");
        
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
                Console.WriteLine($"  ? Name: {entity.Name}");
                Console.WriteLine($"  ? UserId: {entity.UserId}");
                Console.WriteLine($"  ? ExtraInfo.Level: {entity.ExtraInfo?.Level}");
                Console.WriteLine($"  ? ExtraInfo.Tag: {entity.ExtraInfo?.Tag}");
                Console.WriteLine($"  ? User.NickName: {entity.User?.NickName}");
                Console.WriteLine($"  ? Items Count: {entity.Items?.Count}");
                Console.WriteLine($"  ? InternalCode (ignored): {entity.InternalCode ?? "null"}");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"  ? ≤‚ ‘ ß∞‹: {ex.Message}");
        }
        
        Console.WriteLine();
    }
    
    private static void TestProfileMapping()
    {
        Console.WriteLine("2. Profile ≈‰÷√”≥…‰");
        
        var dto = new ActivityDto
        {
            FirstName = "John",
            LastName = "Doe",
            Uid = 2001
        };
        
        try
        {
            var entity = dto.Adapt<MallActivityEntity>();
            
            // Profile ”¶∏√∫œ≤¢ FirstName + LastName -> Name
            var expectedName = "John Doe";
            if (entity?.Name == expectedName)
            {
                Console.WriteLine($"  ? ForMember ≈‰÷√≥…π¶: Name = '{entity.Name}'");
            }
            else
            {
                Console.WriteLine($"  ? ForMember ≈‰÷√ ß∞‹: ∆⁄Õ˚ '{expectedName}',  µº  '{entity?.Name}'");
            }
            
            // Rename: Uid -> UserId
            if (entity?.UserId == 2001)
            {
                Console.WriteLine($"  ? Rename ≈‰÷√≥…π¶: UserId = {entity.UserId}");
            }
            else
            {
                Console.WriteLine($"  ? Rename ≈‰÷√ ß∞‹");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"  ? ≤‚ ‘ ß∞‹: {ex.Message}");
        }
        
        Console.WriteLine();
    }
    
    private static void TestForMemberMapping()
    {
        Console.WriteLine("3. ForMember ◊‘∂®“Â”≥…‰");
        
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
                    dest.Name = $"{src.FirstName} {src.LastName} - Custom";
                }
            });
            
            if (entity?.Name?.Contains("Custom") == true)
            {
                Console.WriteLine($"  ? ◊‘∂®“Â”≥…‰≥…π¶: Name = '{entity.Name}'");
            }
            else
            {
                Console.WriteLine($"  ? ◊‘∂®“Â”≥…‰ ß∞‹");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"  ? ≤‚ ‘ ß∞‹: {ex.Message}");
        }
        
        Console.WriteLine();
    }
    
    private static void TestIgnoreMapping()
    {
        Console.WriteLine("4. Ignore  Ù–‘”≥…‰");
        
        var dto = new ActivityDto
        {
            FirstName = "Bob",
            LastName = "Wilson",
            Uid = 4001
        };
        
        try
        {
            var entity = dto.Adapt<MallActivityEntity>();
            
            // InternalCode ”¶∏√±ª∫ˆ¬‘£®Profile ÷–≈‰÷√£©
            if (entity?.InternalCode == null)
            {
                Console.WriteLine($"  ? Ignore ≈‰÷√≥…π¶: InternalCode = null");
            }
            else
            {
                Console.WriteLine($"  ? Ignore ≈‰÷√ ß∞‹: InternalCode = '{entity.InternalCode}'");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"  ? ≤‚ ‘ ß∞‹: {ex.Message}");
        }
        
        Console.WriteLine();
    }
    
    private static void TestRenameMapping()
    {
        Console.WriteLine("5. Rename  Ù–‘”≥…‰");
        
        var dto = new ActivityDto
        {
            FirstName = "Charlie",
            LastName = "Brown",
            Uid = 5001
        };
        
        try
        {
            var entity = dto.Adapt<MallActivityEntity>();
            
            // Uid -> UserId (Profile ÷–≈‰÷√)
            if (entity?.UserId == 5001)
            {
                Console.WriteLine($"  ? Rename ”≥…‰≥…π¶: Uid -> UserId = {entity.UserId}");
            }
            else
            {
                Console.WriteLine($"  ? Rename ”≥…‰ ß∞‹");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"  ? ≤‚ ‘ ß∞‹: {ex.Message}");
        }
        
        Console.WriteLine();
    }
}
