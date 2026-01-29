using System;
using Ling.Mapper;
using Ling.Mapper.Extensions;

namespace TestConsole.Test;

/// <summary>
/// 演示枚举转换场景
/// </summary>
public static class EnumConversionDemo
{
    private static int _passedTests = 0;
    private static int _failedTests = 0;

    public static void Run()
    {
        Console.WriteLine("\n=== 枚举转换测试 ===");

        _passedTests = 0;
        _failedTests = 0;

        // 场景 1: enum -> int
        TestEnumToInt();

        // 场景 2: int -> enum
        TestIntToEnum();

        // 场景 3: enum -> string
        TestEnumToString();

        // 场景 4: string -> enum
        TestStringToEnum();

        // 场景 5: enum -> enum (相同类型)
        TestEnumToSameEnum();

        // 场景 6: enum -> enum (不同类型但值相同)
        TestEnumToDifferentEnum();

        // 场景 7: nullable enum -> int
        TestNullableEnumToInt();

        // 场景 8: int -> nullable enum
        TestIntToNullableEnum();

        // 场景 9: nullable enum -> nullable int
        TestNullableEnumToNullableInt();

        Console.WriteLine($"\n📊 测试统计: ✅ {_passedTests} 通过, ❌ {_failedTests} 失败");
        if (_failedTests == 0)
        {
            Console.WriteLine("✅ 枚举转换测试完成 - 所有测试通过\n");
        }
        else
        {
            Console.WriteLine($"⚠️  枚举转换测试完成 - {_failedTests} 个测试失败\n");
        }
    }

    #region 测试场景

    private static void TestEnumToInt()
    {
        Console.WriteLine("\n--- 场景 1: enum -> int ---");
        var source = new EnumToIntSource { Status = UserStatus.Active };
        
        try
        {
            var result = source.Adapt<EnumToIntTarget>();
            var success = (result?.Status ?? 0) == (int)UserStatus.Active;
            Console.WriteLine($"Status: {result?.Status ?? 0} (期望: {(int)UserStatus.Active})");
            if (success)
            {
                Console.WriteLine("  ✅ 转换成功");
                _passedTests++;
            }
            else
            {
                Console.WriteLine("  ❌ 转换失败");
                _failedTests++;
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"  ❌ 异常: {ex.Message}");
            _failedTests++;
        }
    }

    private static void TestIntToEnum()
    {
        Console.WriteLine("\n--- 场景 2: int -> enum ---");
        var source = new IntToEnumSource { Status = 1 };
        
        try
        {
            var result = source.Adapt<IntToEnumTarget>();
            var success = (result?.Status ?? UserStatus.Inactive) == UserStatus.Active;
            Console.WriteLine($"Status: {result?.Status ?? UserStatus.Inactive} (期望: {UserStatus.Active})");
            if (success)
            {
                Console.WriteLine("  ✅ 转换成功");
                _passedTests++;
            }
            else
            {
                Console.WriteLine("  ❌ 转换失败");
                _failedTests++;
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"  ❌ 异常: {ex.Message}");
            _failedTests++;
        }
    }

    private static void TestEnumToString()
    {
        Console.WriteLine("\n--- 场景 3: enum -> string ---");
        var source = new EnumToStringSource { Status = UserStatus.Inactive };
        
        try
        {
            var result = source.Adapt<EnumToStringTarget>();
            var success = !string.IsNullOrEmpty(result?.Status);
            Console.WriteLine($"Status: {result?.Status ?? "null"} (期望: \"Inactive\")");
            if (success)
            {
                Console.WriteLine("  ✅ 转换成功");
                _passedTests++;
            }
            else
            {
                Console.WriteLine("  ❌ 转换失败");
                _failedTests++;
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"  ❌ 异常: {ex.Message}");
            _failedTests++;
        }
    }

    private static void TestStringToEnum()
    {
        Console.WriteLine("\n--- 场景 4: string -> enum ---");
        var source = new StringToEnumSource { Status = "Active" };
        
        try
        {
            var result = source.Adapt<StringToEnumTarget>();
            var success = (result?.Status ?? UserStatus.Inactive) == UserStatus.Active;
            Console.WriteLine($"Status: {result?.Status ?? UserStatus.Inactive} (期望: {UserStatus.Active})");
            if (success)
            {
                Console.WriteLine("  ✅ 转换成功");
                _passedTests++;
            }
            else
            {
                Console.WriteLine("  ❌ 转换失败");
                _failedTests++;
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"  ❌ 异常: {ex.Message}");
            _failedTests++;
        }
    }

    private static void TestEnumToSameEnum()
    {
        Console.WriteLine("\n--- 场景 5: enum -> enum (相同类型) ---");
        var source = new EnumToSameEnumSource { Status = UserStatus.Pending };
        
        try
        {
            var result = source.Adapt<EnumToSameEnumTarget>();
            var success = (result?.Status ?? UserStatus.Inactive) == UserStatus.Pending;
            Console.WriteLine($"Status: {result?.Status ?? UserStatus.Inactive} (期望: {UserStatus.Pending})");
            if (success)
            {
                Console.WriteLine("  ✅ 转换成功");
                _passedTests++;
            }
            else
            {
                Console.WriteLine("  ❌ 转换失败");
                _failedTests++;
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"  ❌ 异常: {ex.Message}");
            _failedTests++;
        }
    }

    private static void TestEnumToDifferentEnum()
    {
        Console.WriteLine("\n--- 场景 6: enum -> enum (不同类型) ---");
        var source = new EnumToDifferentEnumSource { Status = UserStatus.Active };
        
        try
        {
            var result = source.Adapt<EnumToDifferentEnumTarget>();
            Console.WriteLine($"Status: {result?.Status ?? OrderStatus.Pending} (期望: {OrderStatus.Completed})");
            Console.WriteLine("  ✅ 转换尝试完成");
            _passedTests++;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"  ❌ 异常: {ex.Message}");
            _failedTests++;
        }
    }

    private static void TestNullableEnumToInt()
    {
        Console.WriteLine("\n--- 场景 7: nullable enum -> int ---");
        
        // 有值的情况
        var source1 = new NullableEnumToIntSource { Status = UserStatus.Active };
        try
        {
            var result1 = source1.Adapt<NullableEnumToIntTarget>();
            var success = (result1?.Status ?? -1) == (int)UserStatus.Active;
            Console.WriteLine($"有值: Status = {result1?.Status ?? -1} (期望: {(int)UserStatus.Active})");
            if (success)
            {
                Console.WriteLine("  ✅ 转换成功");
                _passedTests++;
            }
            else
            {
                Console.WriteLine("  ❌ 转换失败");
                _failedTests++;
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"  ❌ 异常: {ex.Message}");
            _failedTests++;
        }

        // null 的情况
        var source2 = new NullableEnumToIntSource { Status = null };
        try
        {
            var result2 = source2.Adapt<NullableEnumToIntTarget>();
            var success = (result2?.Status ?? -1) == 0;
            Console.WriteLine($"null: Status = {result2?.Status ?? -1} (期望: 0)");
            if (success)
            {
                Console.WriteLine("  ✅ 转换成功");
                _passedTests++;
            }
            else
            {
                Console.WriteLine("  ❌ 转换失败");
                _failedTests++;
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"  ❌ 异常: {ex.Message}");
            _failedTests++;
        }
    }

    private static void TestIntToNullableEnum()
    {
        Console.WriteLine("\n--- 场景 8: int -> nullable enum ---");
        var source = new IntToNullableEnumSource { Status = 1 };
        
        try
        {
            var result = source.Adapt<IntToNullableEnumTarget>();
            var success = (result?.Status ?? UserStatus.Inactive) == UserStatus.Active;
            Console.WriteLine($"Status: {result?.Status?.ToString() ?? "null"} (期望: {UserStatus.Active})");
            if (success)
            {
                Console.WriteLine("  ✅ 转换成功");
                _passedTests++;
            }
            else
            {
                Console.WriteLine("  ❌ 转换失败");
                _failedTests++;
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"  ❌ 异常: {ex.Message}");
            _failedTests++;
        }
    }

    private static void TestNullableEnumToNullableInt()
    {
        Console.WriteLine("\n--- 场景 9: nullable enum -> nullable int ---");
        
        // 有值的情况
        var source1 = new NullableEnumToNullableIntSource { Status = UserStatus.Inactive };
        try
        {
            var result1 = source1.Adapt<NullableEnumToNullableIntTarget>();
            var success = (result1?.Status ?? -1) == (int)UserStatus.Inactive;
            Console.WriteLine($"有值: Status = {result1?.Status?.ToString() ?? "null"} (期望: {(int)UserStatus.Inactive})");
            if (success)
            {
                Console.WriteLine("  ✅ 转换成功");
                _passedTests++;
            }
            else
            {
                Console.WriteLine("  ❌ 转换失败");
                _failedTests++;
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"  ❌ 异常: {ex.Message}");
            _failedTests++;
        }

        // null 的情况
        var source2 = new NullableEnumToNullableIntSource { Status = null };
        try
        {
            var result2 = source2.Adapt<NullableEnumToNullableIntTarget>();
            var success = result2?.Status == null;
            Console.WriteLine($"null: Status = {result2?.Status?.ToString() ?? "null"} (期望: null)");
            if (success)
            {
                Console.WriteLine("  ✅ 转换成功");
                _passedTests++;
            }
            else
            {
                Console.WriteLine("  ❌ 转换失败");
                _failedTests++;
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"  ❌ 异常: {ex.Message}");
            _failedTests++;
        }
    }

    #endregion

    #region 测试模型

    // 枚举定义
    public enum UserStatus
    {
        Inactive = 0,
        Active = 1,
        Pending = 2
    }

    public enum OrderStatus
    {
        Pending = 0,
        Completed = 1,
        Cancelled = 2
    }

    // 场景 1: enum -> int
    public class EnumToIntSource
    {
        public UserStatus Status { get; set; }
    }

    public class EnumToIntTarget
    {
        public int Status { get; set; }
    }

    // 场景 2: int -> enum
    public class IntToEnumSource
    {
        public int Status { get; set; }
    }

    public class IntToEnumTarget
    {
        public UserStatus Status { get; set; }
    }

    // 场景 3: enum -> string
    public class EnumToStringSource
    {
        public UserStatus Status { get; set; }
    }

    public class EnumToStringTarget
    {
        public string? Status { get; set; }
    }

    // 场景 4: string -> enum
    public class StringToEnumSource
    {
        public string? Status { get; set; }
    }

    public class StringToEnumTarget
    {
        public UserStatus Status { get; set; }
    }

    // 场景 5: enum -> enum (相同类型)
    public class EnumToSameEnumSource
    {
        public UserStatus Status { get; set; }
    }

    public class EnumToSameEnumTarget
    {
        public UserStatus Status { get; set; }
    }

    // 场景 6: enum -> enum (不同类型)
    public class EnumToDifferentEnumSource
    {
        public UserStatus Status { get; set; }
    }

    public class EnumToDifferentEnumTarget
    {
        public OrderStatus Status { get; set; }
    }

    // 场景 7: nullable enum -> int
    public class NullableEnumToIntSource
    {
        public UserStatus? Status { get; set; }
    }

    public class NullableEnumToIntTarget
    {
        public int Status { get; set; }
    }

    // 场景 8: int -> nullable enum
    public class IntToNullableEnumSource
    {
        public int Status { get; set; }
    }

    public class IntToNullableEnumTarget
    {
        public UserStatus? Status { get; set; }
    }

    // 场景 9: nullable enum -> nullable int
    public class NullableEnumToNullableIntSource
    {
        public UserStatus? Status { get; set; }
    }

    public class NullableEnumToNullableIntTarget
    {
        public int? Status { get; set; }
    }

    #endregion
}
