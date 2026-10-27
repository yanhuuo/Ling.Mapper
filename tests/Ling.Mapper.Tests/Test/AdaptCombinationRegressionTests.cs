using Ling.Mapper;
using Ling.Mapper.Configuration;
using Ling.Mapper.Extensions;
using Ling.Mapper.Mapper;
using Ling.Mapper.Models;
using Ling.Mapper.Provider;

namespace TestConsole.Test;

/// <summary>Executable and compile-time contracts for the Adapt option/ignore/callback matrix.</summary>
public static class AdaptCombinationRegressionTests
{
    public static void Run()
    {
        var failures = new List<Exception>();
        int count = 0;
        void Case(string name, Action action)
        {
            count++;
            try { action(); Console.WriteLine("PASS Adapt combinations: " + name); }
            catch (Exception error) { failures.Add(new Exception(name, error)); }
        }
        var original = MapperProvider.Current;
        try
        {
            MapperProvider.SetCurrent(new MapperConfiguration { DefaultAdaptOptions = AdaptOptions.IgnoreCase }.CreateMapper());
            var source = new Source { value = 7, Label = "mapped", Other = "other" };
            string[] ignores = [nameof(Target.Label)];
            Case("options plus ignore array and expanded arguments", () => {
                Equal(7, source.Adapt<Target>(AdaptOptions.IgnoreCase, ignores).Value);
                Equal("initial", source.Adapt<Target>(ignoreNames: ignores).Label);
                Equal("initial", source.Adapt<Target>(["Label"]).Label);
                Equal("initial", source.Adapt<Target>(AdaptOptions.IgnoreCase, "Label", "Other").Label);
                Equal("other-initial", source.Adapt<Target>(AdaptOptions.IgnoreCase, "Label", "Other").Other);
            });
            Case("result callback sees mapped and ignored values then updates result", () => {
                int calls = 0;
                var mapped = source.Adapt<Target>(d => { calls++; Equal(7, d.Value); Equal("initial", d.Label); d.Label = "callback"; }, ignores);
                Equal(1, calls); Equal("callback", mapped.Label); Equal("mapped", source.Label);
            });
            Case("explicit Strict zero overrides mapper defaults", () => {
                Equal(0, source.Adapt<Target>(AdaptOptions.Strict, d => Equal(0, d.Value), ignores).Value);
                Equal(0, source.Adapt<Target, Source>(AdaptOptions.Strict, (d, s) => Equal(0, d.Value), ignores).Value);
                Equal(0, new[] { source }.Adapt<Target, Source>(AdaptOptions.Strict, (d, s) => Equal(0, d.Value), ignores)[0].Value);
            });
            Case("object callback receives original source after ignore", () => {
                int calls = 0;
                var mapped = source.Adapt<Target, Source>((d, s) => { calls++; Same(source, s); Equal(7, d.Value); Equal("initial", d.Label); d.Other = "callback"; }, "Label");
                Equal(1, calls); Equal("callback", mapped.Other);
            });
            Case("collection callbacks run once per non-null occurrence in order", () => {
                var second = new Source { value = 9, Label = "second" };
                IEnumerable<Source> items = new Source[] { source, null!, second, source };
                var seen = new List<Source>();
                var result = items.Adapt<Target, Source>(AdaptOptions.IgnoreCase, (d, s) => { seen.Add(s); Equal(s.value, d.Value); Equal("initial", d.Label); }, ignores);
                Equal(4, result.Count); Equal(null, result[1]); Equal(3, seen.Count);
                Same(source, seen[0]); Same(second, seen[1]); Same(source, seen[2]); Same(result[0], result[3]);
            });
            Case("collection result callback runs once on completed whole list", () => {
                int calls = 0;
                var result = new[] { source, source }.Adapt<List<Target>>(AdaptOptions.IgnoreCase, list => { calls++; Equal(2, list.Count); Equal("initial", list[0].Label); list.RemoveAt(1); }, ignores);
                Equal(1, calls); Equal(1, result.Count);
            });
            Case("empty ignore arrays preserve normal mapping", () => {
                Equal("mapped", source.Adapt<Target>(AdaptOptions.IgnoreCase, Array.Empty<string>()).Label);
                Equal("mapped", source.Adapt<Target>(d => { }, Array.Empty<string>()).Label);
                Equal("mapped", source.Adapt<Target, Source>((d, s) => { }, Array.Empty<string>()).Label);
                Equal("mapped", new[] { source }.Adapt<Target, Source>((d, s) => { }, Array.Empty<string>())[0].Label);
            });
            Case("unknown and duplicate ignored names are harmless", () => {
                Equal(7, source.Adapt<Target>(d => { }, "Missing", "Missing").Value);
            });
            Case("null result callback runs once and item callbacks do not", () => {
                object? absent = null; int resultCalls = 0, itemCalls = 0;
                Equal(null, absent.Adapt<Target>(AdaptOptions.Strict, d => { resultCalls++; Equal(null, d); }, ignores));
                Source? typed = null;
                Equal(null, typed.Adapt<Target, Source?>((d, s) => itemCalls++, ignores));
                IEnumerable<Source> collection = null!;
                Equal(0, collection.Adapt<Target, Source>(AdaptOptions.Strict, (d, s) => itemCalls++, ignores).Count);
                Equal(0, collection.Adapt<Target, Source>((AdaptOptions)128, (d, s) => itemCalls++, "Child.Value").Count);
                Equal(1, resultCalls); Equal(0, itemCalls);
            });
            Case("nullable and scalar root callbacks retain mapper conversion", () => {
                object? absent = null; int calls = 0;
                Equal(null, absent.Adapt<int?>(AdaptOptions.Strict, d => { calls++; Equal(null, d); }, Array.Empty<string>()));
                Equal(12L, "12".Adapt<long>(AdaptOptions.Strict, d => { calls++; Equal(12L, d); }));
                int? sourceValue = 13;
                Equal(13L, sourceValue.Adapt<long?, int?>(AdaptOptions.Strict, (d, s) => { calls++; Equal(13L, d); Equal(13, s); }));
                Equal(3, calls);
                decimal? noDecimal = null;
                Equal(null, noDecimal.Adapt<int?>(d => Equal(null, d), Array.Empty<string>()));
                Equal(2, 2.5m.Adapt<int>(d => Equal(2, d), Array.Empty<string>()));
                Equal(4, 3.5m.Adapt<int>(d => Equal(4, d), Array.Empty<string>()));
                Equal("$", Throws<MappingException>(() => "invalid".Adapt<int>(d => { }, Array.Empty<string>())).Path);
                Equal("$", Throws<MappingException>(() => long.MaxValue.Adapt<int>(d => { }, Array.Empty<string>())).Path);
            });
            Case("IgnoreNullValues preserves target initializer before callback", () => {
                var nullable = new Source { Label = null };
                nullable.Adapt<Target>(AdaptOptions.IgnoreNullValues, d => Equal("initial", d.Label), "Other");
            });
            Case("same-type ignored projection does not mutate source", () => {
                var s = new Target { Value = 7, Label = "source" };
                var d = s.Adapt<Target>(target => { Equal("initial", target.Label); target.Label = "callback"; }, "Label");
                NotSame(s, d); Equal("source", s.Label); Equal("callback", d.Label);
            });
            Case("nested ignores apply before callback", () => {
                var s = new NestedSource { Child = new Source { value = 4, Label = "source" } };
                s.Adapt<NestedTarget>(d => { Equal(4, d.Child!.Value); Equal("initial", d.Child.Label); }, "Child.Label");
            });
            Case("ignored getter is never read", () => {
                new ThrowingSource().Adapt<Target>(d => Equal("initial", d.Label), "Label");
            });
            Case("ignore array snapshot survives caller mutation in callback", () => {
                string[] names = ["Label"];
                source.Adapt<Target>(d => { names[0] = "Value"; Equal("initial", d.Label); }, names);
                Equal(0, source.Adapt<Target>(d => Equal("mapped", d.Label), names).Value);
                Equal("initial", source.Adapt<Target>(d => { }, "Label").Label);
            });
            Case("null callback delegates are rejected", () => {
                Throws<ArgumentNullException>(() => source.Adapt<Target>((Action<Target>)null!, ignores));
                Throws<ArgumentNullException>(() => source.Adapt<Target>(AdaptOptions.Strict, (Action<Target>)null!));
                Throws<ArgumentNullException>(() => source.Adapt<Target, Source>((Action<Target, Source>)null!, ignores));
                Throws<ArgumentNullException>(() => new[] { source }.Adapt<Target, Source>(AdaptOptions.Strict, (Action<Target, Source>)null!, ignores));
            });
            Case("null ignore arrays are rejected consistently", () => {
                Equal("ignoreNames", Throws<ArgumentNullException>(() => source.Adapt<Target>((string[])null!)).ParamName);
                Equal("ignoreNames", Throws<ArgumentNullException>(() => source.Adapt<Target>(AdaptOptions.Strict, (string[])null!)).ParamName);
                Throws<ArgumentNullException>(() => source.Adapt<Target>(d => { }, (string[])null!));
                Throws<ArgumentNullException>(() => source.Adapt<Target, Source>((d, s) => { }, (string[])null!));
                Throws<ArgumentNullException>(() => new[] { source }.Adapt<Target, Source>((d, s) => { }, (string[])null!));
            });
            foreach (string? invalid in new string?[] { null, "", " ", "\t" })
                Case("invalid ignore name is rejected: " + (invalid ?? "null"), () => {
                    Throws<ArgumentException>(() => source.Adapt<Target>(AdaptOptions.Strict, [invalid!]));
                    Throws<ArgumentException>(() => source.Adapt<Target>(d => { }, [invalid!]));
                    Throws<ArgumentException>(() => source.Adapt<Target, Source>((d, s) => { }, [invalid!]));
                    Throws<ArgumentException>(() => new[] { source }.Adapt<Target, Source>((d, s) => { }, [invalid!]));
                });
            Case("result exception identity and typed exception wrapping remain distinct", () => {
                var marker = new InvalidOperationException("marker");
                Same(marker, Throws<InvalidOperationException>(() => source.Adapt<Target>(d => throw marker, ignores)));
                var error = Throws<MappingException>(() => source.Adapt<Target, Source>((d, s) => throw marker, ignores));
                Equal(typeof(InvalidOperationException).FullName, error.OriginalExceptionType);
                Equal(7, source.Adapt<Target>(d => { }, ignores).Value);
            });
            Case("invalid explicit options reach mapper validation", () => {
                Throws<ArgumentException>(() => source.Adapt<Target>((AdaptOptions)128, ignores));
                Throws<ArgumentException>(() => source.Adapt<Target>((AdaptOptions)128, d => { }, ignores));
                Throws<ArgumentException>(() => source.Adapt<Target, Source>((AdaptOptions)128, (d, s) => { }, ignores));
                Throws<ArgumentException>(() => new[] { source }.Adapt<Target, Source>((AdaptOptions)128, (d, s) => { }, ignores));
            });
            Case("converter null result is preserved with empty ignores", () => {
                TypeConverterRegistry.Register(typeof(NullSource), typeof(NullTarget), new Func<NullSource, NullTarget>(_ => null!));
                int calls = 0;
                Equal(null, new NullSource().Adapt<NullTarget>(d => { calls++; Equal(null, d); }, Array.Empty<string>()));
                Equal(null, new NullSource().Adapt<NullTarget, NullSource>((d, s) => calls++, Array.Empty<string>()));
                Equal(1, calls);
            });
            Case("opaque converter ignore projection is still unsupported", () => {
                Throws<MappingException>(() => new NullSource().Adapt<NullTarget>(d => { }, "Value"));
            });
            Case("new options callbacks with no ignores bind without ambiguity", () => {
                source.Adapt<Target>(AdaptOptions.IgnoreCase, d => Equal(7, d.Value));
                source.Adapt<Target, Source>(AdaptOptions.IgnoreCase, (d, s) => Equal(7, d.Value));
                new[] { source }.Adapt<Target, Source>(AdaptOptions.IgnoreCase, (d, s) => Equal(7, d.Value));
            });
            Case("old and new named/params/lambda call forms compile and run", () => ConsumerCalls(source));
            Case("custom mapper preserves default and null callback contract", () => {
                var fake = new RecordingMapper(); MapperProvider.SetCurrent(fake);
                Source? absent = null;
                absent.Adapt<Target, Source?>((d, s) => { }, ignores);
                Equal(null, fake.Callback); Equal(AdaptOptions.Default, fake.Options); Equal("Label", fake.Ignores![0]);
                source.Adapt<Target>(AdaptOptions.Strict, d => { }, ignores);
                Equal(AdaptOptions.Strict, fake.Options); Equal(null, fake.Callback);
            });
        }
        finally { MapperProvider.SetCurrent(original); }
        Console.WriteLine($"Adapt combinations: {count - failures.Count}/{count} passed.");
        if (failures.Count != 0) throw new AggregateException(failures);
    }

    private static void ConsumerCalls(Source source)
    {
        string[] names = ["Label"];
        source.Adapt<Target>();
        source.Adapt<Target>(options: AdaptOptions.Strict);
        source.Adapt<Target>(firstIgnore: "Label", otherIgnores: ["Other"]);
        source.Adapt<Target>(afterMap: d => { });
        source.Adapt<Target, Source>(afterMapItem: (d, s) => { });
        new[] { source }.Adapt<Target, Source>(afterMapItem: (d, s) => { });
        source.Adapt<Target>(ignoreNames: names);
        source.Adapt<Target>(options: AdaptOptions.Strict, ignoreNames: names);
        source.Adapt<Target>(afterMap: d => { }, ignoreNames: names);
        source.Adapt<Target>(options: AdaptOptions.Strict, afterMap: d => { }, ignoreNames: names);
        source.Adapt<Target, Source>(afterMapItem: (d, s) => { }, ignoreNames: names);
        source.Adapt<Target, Source>(options: AdaptOptions.Strict, afterMapItem: (d, s) => { }, ignoreNames: names);
        new[] { source }.Adapt<Target, Source>(afterMapItem: (d, s) => { }, ignoreNames: names);
        new[] { source }.Adapt<Target, Source>(options: AdaptOptions.Strict, afterMapItem: (d, s) => { }, ignoreNames: names);
        source.Adapt<Target>(AdaptOptions.Strict, d => { }, "Label", "Other");
        source.Adapt<Target, Source>((d, s) => { }, "Label", "Other");
        new[] { source }.Adapt<Target, Source>((d, s) => { }, "Label", "Other");
        AdaptExtensions.Adapt<Target>(source, afterMap: d => { }, ignoreNames: names);
        MapperProvider.Current.Map<Target>(source, options: AdaptOptions.Strict, afterMapItem: null, ignoreNames: names);
        object? absent = null;
        Equal(null, absent.Adapt<Target>(afterMap: d => { }, ignoreNames: names));
        Throws<ArgumentNullException>(() => source.Adapt<Target>((string)null!, "Other"));
    }

    private static T Throws<T>(Action action) where T : Exception { try { action(); } catch (T error) { return error; } throw new Exception("Expected " + typeof(T).Name); }
    private static void Equal(object? expected, object? actual) { if (!Equals(expected, actual)) throw new Exception($"Expected {expected ?? "null"}, got {actual ?? "null"}."); }
    private static void Same(object? expected, object? actual) { if (!ReferenceEquals(expected, actual)) throw new Exception("Expected same reference."); }
    private static void NotSame(object? expected, object? actual) { if (ReferenceEquals(expected, actual)) throw new Exception("Expected distinct references."); }
    public sealed class Source { public int value { get; set; } public string? Label { get; set; } public string? Other { get; set; } }
    public sealed class Target { public int Value { get; set; } public string? Label { get; set; } = "initial"; public string? Other { get; set; } = "other-initial"; }
    public sealed class NestedSource { public Source? Child { get; set; } }
    public sealed class NestedTarget { public Target? Child { get; set; } }
    public sealed class ThrowingSource { public string Label => throw new Exception("Ignored getter ran."); }
    public sealed class NullSource { }
    public sealed class NullTarget { public int Value { get; set; } }
    private sealed class RecordingMapper : IMapper
    {
        public AdaptOptions Options; public Action<object, object>? Callback; public string[]? Ignores;
        public TDestination? Map<TDestination>(object? source) => Map<TDestination>(source, AdaptOptions.Default);
        public TDestination? Map<TDestination>(object? source, AdaptOptions options, Action<object, object>? afterMapItem = null, string[]? ignoreNames = null)
        { Options = options; Callback = afterMapItem; Ignores = ignoreNames; return default; }
        public object? Map(object? source, Type sourceType, Type destType) => throw new NotSupportedException();
        public object? Map(object? source, Type sourceType, Type destType, AdaptOptions options, Action<object, object>? afterMapItem = null, string[]? ignoreNames = null) => throw new NotSupportedException();
    }
}
