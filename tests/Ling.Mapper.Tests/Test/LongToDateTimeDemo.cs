using Ling.Mapper;
using System;

namespace TestConsole.Test
{
    internal static class LongToDateTimeDemo
    {
        public static void Run()
        {
            RunBasicTests();
            RunBoundaryAndCollectionTests();
            RunIncompatibleTests();
        }

        private static void RunBasicTests()
        {
            var mapper = MapperProvider.Current;
            Console.WriteLine("\n--- Long -> DateTime 映射示例 ---");

            var src1 = new { TimeLong = DateTime.Now.Ticks };
            var expected1 = new DateTime(src1.TimeLong);
            var dest1 = mapper.Map<DestA>(src1);
            Console.WriteLine($"源 long (ticks): {src1.TimeLong} => 目标 DateTime: {dest1?.Time} (期望: {expected1})");

            var src2 = new { TimeLong = (long?)DateTime.UtcNow.Ticks };
            var expected2 = src2.TimeLong.HasValue ? new DateTime(src2.TimeLong.Value) : (DateTime?)null;
            var dest2 = mapper.Map<DestB>(src2);
            Console.WriteLine($"源 long? (ticks): {src2.TimeLong} => 目标 DateTime?: {dest2?.Time} (期望: {expected2})");

            var src3 = new { Time = DateTime.Now };
            var expected3 = src3.Time.Ticks;
            var dest3 = mapper.Map<DestC>(src3);
            Console.WriteLine($"源 DateTime: {src3.Time} => 目标 long (ticks): {dest3?.TimeLong} (期望: {expected3})");

            var src4 = new { Time = (DateTime?)DateTime.Now };
            var expected4 = src4.Time.HasValue ? src4.Time.Value.Ticks : (long?)null;
            var dest4 = mapper.Map<DestD>(src4);
            Console.WriteLine($"源 DateTime?: {src4.Time} => 目标 long?: {dest4?.TimeLong} (期望: {expected4})");
        }

        private static void RunBoundaryAndCollectionTests()
        {
            var mapper = MapperProvider.Current;
            Console.WriteLine("\n--- 额外边界与集合测试 ---");

            // 空源对象 -> 目标保持默认
            var srcEmpty = new { };
            var destEmpty = mapper.Map<DestA>(srcEmpty);
            Console.WriteLine($"源 空对象 => 目标 DateTime: {destEmpty?.Time} (期望: {default(DateTime)})");

            // 负 ticks 值 (可能抛出或返回默认，捕获异常并打印)
            var srcNeg = new { TimeLong = -100L };
            try
            {
                var destNeg = mapper.Map<DestA>(srcNeg);
                Console.WriteLine($"源 负 ticks: {srcNeg.TimeLong} => 目标 DateTime: {destNeg?.Time}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"源 负 ticks: {srcNeg.TimeLong} => 映射抛出异常: {ex.GetType().Name} {ex.Message}");
            }

            // 极大 ticks 值 (超出 DateTime 范围) 测试
            var srcBig = new { TimeLong = long.MaxValue };
            try
            {
                var destBig = mapper.Map<DestA>(srcBig);
                Console.WriteLine($"源 大 ticks: {srcBig.TimeLong} => 目标 DateTime: {destBig?.Time}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"源 大 ticks: {srcBig.TimeLong} => 映射抛出异常: {ex.GetType().Name} {ex.Message}");
            }

            // 列表/数组映射测试
            var now = DateTime.Now;
            var srcList = new { Times = new long[] { now.Ticks, DateTime.UtcNow.Ticks } };
            var destList = mapper.Map<DestList>(srcList);
            Console.WriteLine($"源 long[] => 目标 DateTime[]: [{string.Join(',', destList?.Times ?? Array.Empty<DateTime>())}] (期望: ticks -> DateTime)");
        }

        private static void RunIncompatibleTests()
        {
            var mapper = MapperProvider.Current;
            Console.WriteLine("\n--- 不兼容类型映射示例 (应返回 null 或默认值) ---");

            // 1) string -> DateTime? (不兼容，应为 null)
            var src5 = new { TimeStr = "2023-01-01" };
            var dest5 = mapper.Map<DestE>(src5);
            Console.WriteLine($"源 string: {src5.TimeStr} => 目标 DateTime?: {dest5?.Time} (期望: null)");

            // 2) string -> DateTime (非可空，不兼容，应为默认 DateTime)
            var src6 = new { TimeStr = "not-a-date" };
            var dest6 = mapper.Map<DestA>(src6);
            Console.WriteLine($"源 string: {src6.TimeStr} => 目标 DateTime: {dest6?.Time} (期望: {default(DateTime)})");

            // 3) bool -> DateTime? (不兼容，应为 null)
            var src7 = new { Flag = true };
            var dest7 = mapper.Map<DestF>(src7);
            Console.WriteLine($"源 bool: {src7.Flag} => 目标 DateTime?: {dest7?.Time} (期望: null)");

            // 4) double -> DateTime? (不兼容，应为 null)
            var src8 = new { Val = 12345.67 };
            var dest8 = mapper.Map<DestG>(src8);
            Console.WriteLine($"源 double: {src8.Val} => 目标 DateTime?: {dest8?.Time} (期望: null)");

            // 5) string -> long (目标为非可空 long，source string 不兼容，应为默认 0)
            var src9 = new { NumStr = "123456" };
            var dest9 = mapper.Map<DestC>(src9);
            Console.WriteLine($"源 string: {src9.NumStr} => 目标 long: {dest9?.TimeLong} (期望: 0)");

            // 6) string -> DateTime? 空字符串 => 应为 null
            var src10 = new { TimeStr = "" };
            var dest10 = mapper.Map<DestE>(src10);
            Console.WriteLine($"源 空 string => 目标 DateTime?: {dest10?.Time} (期望: null)");
        }

        private class DestA { public DateTime Time { get; set; } }
        private class DestB { public DateTime? Time { get; set; } }
        private class DestC { public long TimeLong { get; set; } }
        private class DestD { public long? TimeLong { get; set; } }
        private class DestE { public DateTime? Time { get; set; } }
        private class DestF { public DateTime? Time { get; set; } }
        private class DestG { public DateTime? Time { get; set; } }
        private class DestList { public DateTime[] Times { get; set; } }
    }
}
