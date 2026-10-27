using System.Collections.Concurrent;
using System.Globalization;
using Ling.Mapper;
using Ling.Mapper.Configuration;
using Ling.Mapper.Models;

namespace TestConsole.Test;

/// <summary>Culture is per conversion/async context, never a captured mapping-plan constant.</summary>
public static class CurrentCultureConversionRegressionTests
{
    public static void Run()
    {
        int assertions = 0;
        void Equal(object? expected, object? actual)
        {
            assertions++;
            if (!Equals(expected, actual)) throw new Exception($"Expected {expected ?? "null"}; got {actual ?? "null"}.");
        }
        void Failure(Action action, Type errorType, string path)
        {
            assertions++;
            try { action(); }
            catch (MappingException error) when (error.OriginalExceptionType == errorType.FullName && error.Path == path) { return; }
            throw new Exception($"Expected {errorType.Name} at {path}.");
        }
        var cultures = new[] { "en-US", "de-DE", "fr-FR", "zh-CN" };
        var mapper = new MapperConfiguration().CreateMapper();
        var configuration = new MapperConfiguration(); configuration.AddProfile(new ExplicitCultureProfile());
        var configured = configuration.CreateMapper();
        TypeConverterRegistry.Register(typeof(ProtocolAmount), typeof(string), new Func<ProtocolAmount, string>(value => value.Value.ToString(CultureInfo.InvariantCulture)));
        var original = CultureInfo.CurrentCulture;
        try
        {
            // Repeat the cycle: the very same root/member/collection plans must change cultures.
            foreach (var name in cultures.Concat(Enumerable.Reverse(cultures)))
            {
                var culture = CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo(name);
                var text = 1234.56m.ToString(culture);
                Equal(text, mapper.Map<string>(1234.56m));
                Equal(1234.56m, mapper.Map<decimal>(text));
                Equal(text, ScalarConversion.Convert(1234.56m, typeof(string)));
                Equal(1234.56m, ScalarConversion.Convert(text, typeof(decimal)));
                Equal(text, mapper.Map<Box<string>>(new Box<decimal> { Value = 1234.56m })!.Value);
                Equal(1234.56m, mapper.Map<Box<decimal>>(new Box<string> { Value = text })!.Value);
                Equal(text, mapper.Map<Box<string>>(new Box<decimal?> { Value = 1234.56m })!.Value);
                Equal(1234.56m, mapper.Map<Box<decimal?>>(new Box<string> { Value = text })!.Value);
                Equal(text, mapper.Map<Box<string>>(new Box<object> { Value = 1234.56m })!.Value);
                Equal(1234.56m, mapper.Map<Box<decimal>>(new Box<object> { Value = text })!.Value);
                Equal(1234.56m, mapper.Map<Box<Box<decimal>>>(new Box<Box<string>> { Value = new() { Value = text } })!.Value!.Value);
                Equal(1234.56m, mapper.Map<ImmutableAmount>(new Box<string> { Value = text }).Value);
                Equal(text, mapper.Map<string[]>(new[] { 1234.56m })![0]);
                Equal(1234.56m, mapper.Map<decimal[]>(new[] { text })![0]);
                Equal(1234.56m, mapper.Map<List<decimal>>(new[] { text })![0]);
                Equal(1234.56m, mapper.Map<Dictionary<string, decimal>>(new Dictionary<string, string> { ["v"] = text })!["v"]);
                Equal(1234.56m, mapper.Map<Dictionary<decimal, string>>(new Dictionary<string, string> { [text] = "v" })!.Keys.Single());
                Equal(1234.56m, mapper.Map<decimal>(1234.56m.ToString("N2", culture)));
                Equal(-1234.56d, mapper.Map<double>((-1234.56d).ToString(culture)));
                Equal((-1.25f).ToString(culture), mapper.Map<string>(-1.25f));
                Equal((-1.25d).ToString(culture), mapper.Map<string>(-1.25d));
                Equal(true, mapper.Map<bool>("true"));
                Equal('é', mapper.Map<char>("é"));
                Equal(2, mapper.Map<int>(2.5m));
                Equal(2, mapper.Map<Box<int>>(new Box<decimal> { Value = 1.5m })!.Value);
                // .NET culture-specific parsing can accept grouping; compare exact BCL semantics.
                foreach (var ambiguous in new[] { "1,234", "1.234" })
                {
                    try { Equal(Convert.ToDecimal(ambiguous, culture), mapper.Map<decimal>(ambiguous)); }
                    catch (FormatException) { Failure(() => mapper.Map<decimal>(ambiguous), typeof(FormatException), "$"); }
                }
                Equal(null, mapper.Map<decimal?>(null));
                Equal(0m, mapper.Map<decimal>(null));
                Equal(null, mapper.Map<Box<string>>(new Box<decimal?>())!.Value);
                Equal(null, mapper.Map<Box<decimal?>>(new Box<string>())!.Value);
                Equal(99m, mapper.Map<InitializedAmount>(new Box<string>(), AdaptOptions.IgnoreNullValues)!.Value);
                Failure(() => mapper.Map<decimal>("invalid"), typeof(FormatException), "$");
                Failure(() => mapper.Map<Box<decimal>>(new Box<string> { Value = "invalid" }), typeof(FormatException), "$.Value");
                Failure(() => mapper.Map<decimal[]>(new[] { text, "invalid" }), typeof(FormatException), "$[1]");
                Failure(() => mapper.Map<decimal>(""), typeof(FormatException), "$");
                Failure(() => mapper.Map<int>("2147483648"), typeof(OverflowException), "$");
                Failure(() => mapper.Map<decimal>("79228162514264337593543950336"), typeof(OverflowException), "$");
                var date = new DateTime(2026, 10, 27, 13, 45, 12);
                var dateText = date.ToString(culture);
                Equal(dateText, mapper.Map<string>(date));
                Equal(Convert.ToDateTime(dateText, culture), mapper.Map<DateTime>(dateText));
                Equal(dateText, mapper.Map<Box<string>>(new Box<DateTime> { Value = date })!.Value);
                Equal(Convert.ToDateTime(dateText, culture), mapper.Map<Box<DateTime>>(new Box<string> { Value = dateText })!.Value);
                var offset = new DateTimeOffset(date, TimeSpan.FromHours(2));
                Equal(offset.ToString(culture), mapper.Map<string>(offset));
                Equal(DateTimeOffset.Parse(offset.ToString(culture), culture), mapper.Map<DateTimeOffset>(offset.ToString(culture)));
                var day = DateOnly.FromDateTime(date); var time = TimeOnly.FromDateTime(date);
                Equal(day.ToString(culture), mapper.Map<string>(day));
                Equal(day, mapper.Map<DateOnly>(day.ToString(culture)));
                Equal(time.ToString(culture), mapper.Map<string>(time));
                Equal(TimeOnly.Parse(time.ToString(culture), culture), mapper.Map<TimeOnly>(time.ToString(culture)));
                // Explicit user code retains its explicit culture, despite the new automatic defaults.
                Equal("1234.56", configured.Map<ExplicitText>(new Amount { Value = 1234.56m })!.Value);
                Equal(1234.56m, configured.Map<ExplicitAmount>(new Text { Value = "1234.56" })!.Value);
                Equal("1234.56", mapper.Map<string>(new ProtocolAmount(1234.56m)));
            }
            // CurrentCulture is AsyncLocal. A shared, hot mapper must not cross-contaminate tasks.
            var errors = new ConcurrentQueue<Exception>();
            var tasks = Enumerable.Range(0, 32).Select(async worker =>
            {
                try
                {
                    var culture = CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo(cultures[worker % cultures.Length]);
                    for (int index = 0; index < 64; index++)
                    {
                        await Task.Yield();
                        var amount = worker * 1000m + index + 0.25m;
                        var text = amount.ToString(culture);
                        if (mapper.Map<string>(amount) != text || mapper.Map<decimal>(text) != amount ||
                            mapper.Map<Box<string>>(new Box<decimal> { Value = amount })!.Value != text ||
                            mapper.Map<Box<decimal>>(new Box<string> { Value = text })!.Value != amount ||
                            mapper.Map<decimal[]>(new[] { text })![0] != amount)
                            throw new Exception("Culture escaped its async context or was captured in a plan.");
                    }
                }
                catch (Exception error) { errors.Enqueue(error); }
            }).ToArray();
            Task.WhenAll(tasks).GetAwaiter().GetResult();
            Equal(0, errors.Count);
            assertions += 32 * 64 * 5;
        }
        finally { CultureInfo.CurrentCulture = original; }
        Console.WriteLine($"Current culture conversion regression: {assertions}/{assertions} assertions passed.");
    }
    public class Box<T> { public T? Value { get; set; } }
    public readonly struct ImmutableAmount(decimal value) { public decimal Value { get; } = value; }
    public sealed class InitializedAmount { public decimal Value { get; set; } = 99m; }
    public sealed class Amount { public decimal Value { get; set; } }
    public sealed class Text { public string? Value { get; set; } }
    public sealed class ExplicitText { public string? Value { get; set; } }
    public sealed class ExplicitAmount { public decimal Value { get; set; } }
    public sealed record ProtocolAmount(decimal Value);
    public sealed class ExplicitCultureProfile : MapperProfile
    {
        public ExplicitCultureProfile()
        {
            CreateMap<Amount, ExplicitText>().ForMember(target => target.Value, source => source.Value.ToString(CultureInfo.InvariantCulture));
            CreateMap<Text, ExplicitAmount>().ForMember(target => target.Value, source => decimal.Parse(source.Value!, CultureInfo.InvariantCulture));
        }
    }
}
