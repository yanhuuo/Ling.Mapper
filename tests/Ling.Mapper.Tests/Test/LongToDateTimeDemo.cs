using Ling.Mapper;
using Ling.Mapper.Configuration;
using static TestConsole.Utils.TestHelper;

namespace TestConsole.Test;

/// <summary>
/// Explicit ticks conversion example. Units and renamed members are configured by
/// this fixture, not inferred by the mapper. The original demo omitted both rules.
/// </summary>
internal static class LongToDateTimeDemo
{
    public static void Run()
    {
        var unconfigured = new MapperConfiguration().CreateMapper();
        bool rejected = false;
        try { unconfigured.Map<DateTime>(1L); }
        catch (MappingException) { rejected = true; }
        AssertTrue(rejected, "long -> DateTime without an explicit unit converter is rejected");
        AssertEqual((DateTime?)null, unconfigured.Map<DateTime?>(null), "null nullable DateTime root");

        TypeConverterRegistry.Register(typeof(long), typeof(DateTime), new Func<long, DateTime>(ticks => new DateTime(ticks)));
        TypeConverterRegistry.Register(typeof(DateTime), typeof(long), new Func<DateTime, long>(date => date.Ticks));
        var configuration = new MapperConfiguration();
        configuration.AddProfile(new TicksProfile());
        var mapper = configuration.CreateMapper();
        var date = new DateTime(2024, 2, 3, 4, 5, 6, DateTimeKind.Unspecified);
        var source = new TicksSource { TimeLong = date.Ticks };
        AssertEqual(date, mapper.Map<DateTarget>(source)!.Time, "explicit ticks and renamed member");
        AssertEqual(date.Ticks, mapper.Map<TicksSource>(new DateTarget { Time = date })!.TimeLong, "reverse explicit ticks");
        AssertEqual((DateTime?)date, mapper.Map<NullableDateTarget>(new NullableTicksSource { TimeLong = date.Ticks })!.Time, "nullable ticks");
        AssertEqual((DateTime?)null, mapper.Map<NullableDateTarget>(new NullableTicksSource())!.Time, "null ticks member");
        AssertEqual((long?)date.Ticks, mapper.Map<NullableTicksSource>(new NullableDateTarget { Time = date })!.TimeLong, "nullable reverse ticks");
        AssertEqual((long?)null, mapper.Map<NullableTicksSource>(new NullableDateTarget())!.TimeLong, "null date member");
        AssertEqual(default(DateTime), mapper.Map<DateTarget>(new { })!.Time, "unmatched member retains default");
        var dates = mapper.Map<DateTime[]>(new[] { date.Ticks, DateTime.MaxValue.Ticks })!;
        AssertEqual(2, dates.Length, "ticks array length");
        AssertEqual(date, dates[0], "ticks array first");
        AssertEqual(DateTime.MaxValue, dates[1], "ticks array upper bound");
        foreach (long invalid in new[] { -1L, long.MaxValue })
        {
            rejected = false;
            try { mapper.Map<DateTime>(invalid); }
            catch (MappingException) { rejected = true; }
            AssertTrue(rejected, "out-of-range ticks must not silently default");
        }
    }
    private sealed class TicksProfile : MapperProfile
    {
        public TicksProfile()
        {
            CreateMap<TicksSource, DateTarget>().Rename(d => d.Time, nameof(TicksSource.TimeLong));
            CreateMap<DateTarget, TicksSource>().Rename(d => d.TimeLong, nameof(DateTarget.Time));
            CreateMap<NullableTicksSource, NullableDateTarget>().Rename(d => d.Time, nameof(NullableTicksSource.TimeLong));
            CreateMap<NullableDateTarget, NullableTicksSource>().Rename(d => d.TimeLong, nameof(NullableDateTarget.Time));
        }
    }
    public sealed class TicksSource { public long TimeLong { get; set; } }
    public sealed class DateTarget { public DateTime Time { get; set; } }
    public sealed class NullableTicksSource { public long? TimeLong { get; set; } }
    public sealed class NullableDateTarget { public DateTime? Time { get; set; } }
}
