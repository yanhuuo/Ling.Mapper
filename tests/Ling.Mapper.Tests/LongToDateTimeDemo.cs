using Ling.Mapper;
using System;

namespace Ling.Mapper.Tests
{
    internal static class LongToDateTimeDemo
    {
        public static void Run()
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
    }
}
