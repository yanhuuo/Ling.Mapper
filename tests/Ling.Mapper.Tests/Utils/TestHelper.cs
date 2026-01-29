using System;

namespace TestConsole.Utils;

/// <summary>
/// 测试辅助工具类
/// 提供统一的断言方法和测试输出格式
/// </summary>
public static class TestHelper
{
    /// <summary>
    /// 断言两个值相等
    /// </summary>
    public static void AssertEqual<T>(T expected, T actual, string message)
    {
        if (!Equals(expected, actual))
        {
            var error = $"断言失败: {message}\n  期望: {expected}\n  实际: {actual}";
            Console.WriteLine($"  ❌ {error}");
            throw new Exception(error);
        }
    }

    /// <summary>
    /// 断言条件为真
    /// </summary>
    public static void AssertTrue(bool condition, string message)
    {
        if (!condition)
        {
            var error = $"断言失败: {message}";
            Console.WriteLine($"  ❌ {error}");
            throw new Exception(error);
        }
    }

    /// <summary>
    /// 断言条件为假
    /// </summary>
    public static void AssertFalse(bool condition, string message)
    {
        if (condition)
        {
            var error = $"断言失败: {message}";
            Console.WriteLine($"  ❌ {error}");
            throw new Exception(error);
        }
    }

    /// <summary>
    /// 断言对象为 null
    /// </summary>
    public static void AssertNull<T>(T? obj, string message) where T : class
    {
        if (obj != null)
        {
            var error = $"断言失败: {message}\n  期望: null\n  实际: {obj}";
            Console.WriteLine($"  ❌ {error}");
            throw new Exception(error);
        }
    }

    /// <summary>
    /// 断言对象不为 null
    /// </summary>
    public static void AssertNotNull<T>(T obj, string message) where T : class
    {
        if (obj == null)
        {
            var error = $"断言失败: {message} - 对象为 null";
            Console.WriteLine($"  ❌ {error}");
            throw new Exception(error);
        }
    }

    /// <summary>
    /// 打印成功消息
    /// </summary>
    public static void PrintSuccess(string message)
    {
        Console.WriteLine($"  ✅ {message}");
    }

    /// <summary>
    /// 帮助打印实际值与期望值对比
    /// </summary>
    public static void PrintActualExpected(string label, object? actual, object? expected)
    {
        Console.WriteLine($"  ? {label}: {actual} (期望: {expected})");
    }

    /// <summary>
    /// 打印错误消息
    /// </summary>
    public static void PrintError(string message)
    {
        Console.WriteLine($"  ❌ {message}");
    }

    /// <summary>
    /// 打印异常消息
    /// </summary>
    public static void PrintException(Exception ex, string context = "")
    {
        var prefix = string.IsNullOrEmpty(context) ? "" : $"{context}: ";
        Console.WriteLine($"  ❌ {prefix}异常 - {ex.Message}");
    }

    /// <summary>
    /// 运行测试并捕获异常
    /// </summary>
    public static bool RunTest(Action testAction, string testName)
    {
        try
        {
            testAction();
            PrintSuccess($"{testName} 通过");
            return true;
        }
        catch (Exception ex)
        {
            PrintError($"{testName} 失败: {ex.Message}");
            return false;
        }
    }

    /// <summary>
    /// 运行测试并在失败时抛出异常
    /// </summary>
    public static void RunTestStrict(Action testAction, string testName)
    {
        try
        {
            testAction();
            PrintSuccess($"{testName} 通过");
        }
        catch (Exception ex)
        {
            PrintError($"{testName} 失败，{ex.Message}");
            throw;
        }
    }
}
