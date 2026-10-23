using System.Collections;
using Ling.Mapper;
using Ling.Mapper.Configuration;
using Ling.Mapper.Extensions;
using Ling.Mapper.Mapper;
using Ling.Mapper.Models;
using Ling.Mapper.Provider;

namespace TestConsole.Test;

/// <summary>
/// Public entry-point and structural boundary contracts, not additional repetitions of
/// the scalar matrix. Unsupported shapes must fail explicitly, never return lost data.
/// Generated plans and NativeAOT are not claimed by this suite.
/// </summary>
public static class PublicContractRegressionTests
{
    public static void Run()
    {
        var failures = new List<Exception>();
        var count = 0;
        void Case(string name, Action test)
        {
            count++;
            try { test(); Console.WriteLine($"PASS public contract: {name}"); }
            catch (Exception error) { failures.Add(new Exception(name, error)); Console.WriteLine($"FAIL public contract: {name}: {error.Message}"); }
        }
        var mapper = new MapperConfiguration().CreateMapper();
        Case("configuration rejects undefined null policy", () => Throws<ArgumentOutOfRangeException>(() => new MapperConfiguration { NullValueHandling = (NullValueHandling)128 }.CreateMapper()));
        Case("configuration rejects negative diagnostic limit", () => Throws<ArgumentOutOfRangeException>(() => new MapperConfiguration { MaxDiagnosticValueLength = -1 }.CreateMapper()));
        Case("configuration accepts zero diagnostic limit", () => {
            var m = new MapperConfiguration { IncludeSourceValueInExceptions = true, MaxDiagnosticValueLength = 0 }.CreateMapper();
            Equal("", Failure(() => m.Map<int>("private"), "$").SourceValue);
        });
        var original = MapperProvider.Current;
        try
        {
            MapperProvider.SetCurrent(mapper);
            Case("IMapper null declared source type", () => Throws<ArgumentNullException>(() => mapper.Map(new Source(), null!, typeof(Target))));
            Case("IMapper null destination type", () => Throws<ArgumentNullException>(() => mapper.Map(new Source(), typeof(Source), null!)));
            Case("IMapper full overload null declared source type", () => Throws<ArgumentNullException>(() => mapper.Map(null, null!, typeof(Target), AdaptOptions.Default)));
            Case("IMapper full overload null destination type", () => Throws<ArgumentNullException>(() => mapper.Map(null, typeof(Source), null!, AdaptOptions.Default)));
            Case("IMapper mismatched concrete declared source rejects", () => Failure(() => mapper.Map(new Source(), typeof(OtherSource), typeof(Target)), "$"));
            Case("IMapper generic and runtime default overload equivalence", () => {
                var s = new Source { Value = 14 }; Equal(14, mapper.Map<Target>(s)!.Value); Equal(14, ((Target)mapper.Map(s, typeof(Source), typeof(Target))!).Value);
            });
            Case("IMapper full runtime callback and ignore", () => {
                var calls = 0; var s = new Source { Value = 14, Label = "keep" };
                var d = (Target)mapper.Map(s, typeof(Source), typeof(Target), AdaptOptions.Strict, (a, b) => { calls++; Same(s, b); Equal(0, ((Target)a).Value); }, ["Value"])!;
                Equal(1, calls); Equal("keep", d.Label);
            });
            Case("Adapt result callback rejects null", () => Throws<ArgumentNullException>(() => new Source().Adapt<Target>((Action<Target>)null!)));
            Case("Adapt object item callback rejects null", () => Throws<ArgumentNullException>(() => new Source().Adapt<Target, Source>((Action<Target, Source>)null!)));
            Case("Adapt collection item callback rejects null", () => Throws<ArgumentNullException>(() => new[] { new Source() }.Adapt<Target, Source>((Action<Target, Source>)null!)));
            Case("Adapt null ignore array rejects clearly", () => Throws<ArgumentNullException>(() => new Source().Adapt<Target>("Value", (string[])null!)));
            Case("Adapt multi-ignore overload preserves other members", () => {
                var d = new Source { Value = 3, Label = "source" }.Adapt<Target>("Value", "Label"); Equal(0, d.Value); Equal("initial", d.Label);
            });
            Case("Adapt result callback receives null result once", () => {
                var calls = 0; object? s = null; var d = s.Adapt<Target>(x => { calls++; Equal(null, x); }); Equal(null, d); Equal(1, calls);
            });
            Case("Adapt result callback exception propagates original", () => {
                var marker = new InvalidOperationException("callback marker"); Same(marker, Throws<InvalidOperationException>(() => new Source().Adapt<Target>(_ => throw marker)));
            });
            Case("IMapper callback exception safely contextualized", () => {
                var e = Failure(() => mapper.Map<Target>(new Source(), AdaptOptions.Default, (_, _) => throw new InvalidOperationException("PRIVATE_CALLBACK")), "$" );
                Equal(typeof(InvalidOperationException).FullName, e.OriginalExceptionType); Private(e, "PRIVATE_CALLBACK");
            });
            Case("provider rejects null and preserves prior mapper", () => { Throws<ArgumentNullException>(() => MapperProvider.SetCurrent(null!)); Same(mapper, MapperProvider.Current); });
            Case("provider clear creates fresh stable default", () => { MapperProvider.Clear(); var a = MapperProvider.Current; Same(a, MapperProvider.Current); NotSame(mapper, a); MapperProvider.SetCurrent(mapper); });
            Case("configuration null profile rejects", () => Throws<ArgumentNullException>(() => new MapperConfiguration().AddProfile(null!)));
            Case("configuration null profile element rejects", () => Throws<ArgumentNullException>(() => new MapperConfiguration().AddProfiles(new MapperProfile[] { null! })));
            Case("configuration null profiles is documented no-op", () => { var c = new MapperConfiguration(); c.AddProfiles(null!); Equal(2, c.CreateMapper().Map<Target>(new Source { Value = 2 })!.Value); });
            Case("configuration null convention is no-op", () => { var c = new MapperConfiguration(); c.ConfigureConventions(null!); Equal(2, c.CreateMapper().Map<Target>(new Source { Value = 2 })!.Value); });
            Case("configuration custom default options apply both IMapper overloads", () => {
                var m = new MapperConfiguration { DefaultAdaptOptions = AdaptOptions.Strict }.CreateMapper();
                var s = new LowerSource { value = 9 }; Equal(0, m.Map<Target>(s)!.Value); Equal(0, ((Target)m.Map(s, typeof(LowerSource), typeof(Target))!).Value);
                Equal(9, m.Map<Target>(s, AdaptOptions.IgnoreCase)!.Value);
            });
            Case("undefined AdaptOptions rejects rather than silently changes matching", () => Reject(() => mapper.Map<Target>(new Source(), (AdaptOptions)128)));
            Case("configuration invalid default options rejects during creation", () => Reject(() => new MapperConfiguration { DefaultAdaptOptions = (AdaptOptions)128 }.CreateMapper()));
            for (var bits = 0; bits <= 7; bits++)
            {
                var option = (AdaptOptions)bits;
                Case($"legal flag combination {bits} preserves exact matches", () => Equal(23, mapper.Map<Target>(new Source { Value = 23 }, option)!.Value));
            }
            Case("negative AdaptOptions rejects rather than enables every flag", () => Reject(() => new Source().Adapt<Target>((AdaptOptions)(-1))));

            Case("registry null source", () => Throws<ArgumentNullException>(() => TypeConverterRegistry.Register(null!, typeof(RuleTarget), new Func<RuleSource, RuleTarget>(_ => new()))));
            Case("registry null target", () => Throws<ArgumentNullException>(() => TypeConverterRegistry.Register(typeof(RuleSource), null!, new Func<RuleSource, RuleTarget>(_ => new()))));
            Case("registry null delegate", () => Throws<ArgumentNullException>(() => TypeConverterRegistry.Register(typeof(RuleSource), typeof(RuleTarget), null!)));
            Case("registry missing lookup clears out value", () => { Equal(false, TypeConverterRegistry.TryGet(typeof(UnregisteredSource), typeof(RuleTarget), out var d)); Equal(null, d); });
            Case("registry lookup null source", () => Throws<ArgumentNullException>(() => TypeConverterRegistry.TryGet(null!, typeof(RuleTarget), out _)));
            Case("registry lookup null target", () => Throws<ArgumentNullException>(() => TypeConverterRegistry.TryGet(typeof(RuleSource), null!, out _)));
            Delegate[] invalidDelegates = [new Action<RuleSource>(_ => { }), new Func<RuleTarget>(() => new()), new Func<RuleSource, RuleSource, RuleTarget>((_, _) => new()), new Func<object, RuleTarget>(_ => new()), new Func<RuleSource, object>(_ => new RuleTarget())];
            string[] signatures = ["void return", "zero parameters", "two parameters", "wrong source", "wrong return"];
            for (var i = 0; i < invalidDelegates.Length; i++)
            {
                var invalid = invalidDelegates[i];
                Case("registry rejects " + signatures[i], () => Throws<ArgumentException>(() => TypeConverterRegistry.Register(typeof(RuleSource), typeof(RuleTarget), invalid)));
            }
            Case("registry retains exact delegate and rejected overwrite retains rule", () => {
                var rule = new Func<RuleSource, RuleTarget>(s => new() { Value = s.Value + 3 }); TypeConverterRegistry.Register(typeof(RuleSource), typeof(RuleTarget), rule);
                Equal(true, TypeConverterRegistry.TryGet(typeof(RuleSource), typeof(RuleTarget), out var found)); Same(rule, found);
                Throws<ArgumentException>(() => TypeConverterRegistry.Register(typeof(RuleSource), typeof(RuleTarget), new Action<RuleSource>(_ => { })));
                Equal(10, mapper.Map<RuleTarget>(new RuleSource { Value = 7 })!.Value);
            });

            Case("getter throw has safe member path and reads once", () => {
                var s = new ThrowingGetter(); var e = Failure(() => mapper.Map<Target>(s), "$.Value"); Equal(1, s.Calls); Equal(typeof(InvalidOperationException).FullName, e.OriginalExceptionType); Private(e, "PRIVATE_GETTER");
            });
            Case("setter throw has safe member path", () => { var e = Failure(() => mapper.Map<ThrowingSetter>(new Source { Value = 12 }), "$.Value"); Private(e, "PRIVATE_SETTER"); Equal(typeof(InvalidOperationException).FullName, e.OriginalExceptionType); });
            Case("ignored getter is never evaluated", () => { var s = new ThrowingGetter(); Equal(0, mapper.Map<Target>(s, AdaptOptions.Default, ignoreNames: ["Value"])!.Value); Equal(0, s.Calls); });
            Case("private setter is not assigned", () => Equal(61, mapper.Map<PrivateSetter>(new Source { Value = 99 })!.Value));
            Case("readonly destination property preserves initializer", () => Equal(62, mapper.Map<ReadonlyTarget>(new Source { Value = 99 })!.Value));
            Case("private source getter is not invoked", () => Equal(0, mapper.Map<Target>(new PrivateGetter())!.Value));
            Case("static properties are neither read nor written", () => { StaticTarget.Value = 63; Equal("ok", mapper.Map<StaticTarget>(new StaticSource())!.Label); Equal(63, StaticTarget.Value); });
            Case("indexers are neither read nor written", () => Equal(8, mapper.Map<IndexedTarget>(new IndexedSource { Value = 8 })!.Value));
            Case("same class preserves object identity", () => { var s = new Source { Value = 4 }; Same(s, mapper.Map<Source>(s)); });
            Case("same struct preserves all fields", () => { var s = new ValueSource { Value = 4, Label = "struct" }; Equal(s, mapper.Map<ValueSource>(s)); });
            Case("different struct maps public setters", () => { var d = mapper.Map<ValueTarget>(new ValueSource { Value = 4, Label = "struct" }); Equal(4L, d.Value); Equal("struct", d.Label); });
            Case("boxed nullable declared source preserves present value", () => { int? s = 17; Equal(17L, mapper.Map(s, typeof(int?), typeof(long?))); });
            Case("boxed nullable declared source preserves absent value", () => { int? s = null; Equal(null, mapper.Map(s, typeof(int?), typeof(long?))); });

            Case("empty array conversion yields exact element shape", () => { var d = mapper.Map<long[]>(Array.Empty<int>())!; Equal(typeof(long[]), d.GetType()); Equal(0, d.Length); });
            Case("empty enumerable constructor is called once", () => { Constructed<long>.Calls = 0; var d = mapper.Map<Constructed<long>>(Array.Empty<int>())!; Equal(0, d.Count()); Equal(1, Constructed<long>.Calls); });
            Case("enumerable constructor preserves converted values and order", () => { var d = mapper.Map<Constructed<long>>(new[] { 8, -3, 0 })!; Sequence(new long[] { 8, -3, 0 }, d); });
            Case("uncounted source array preserves order", () => Sequence(new long[] { 7, 2 }, mapper.Map<long[]>(YieldNumbers())!));
            Case("nested jagged arrays preserve empty rows and order", () => { var d = mapper.Map<long[][]>(new[] { new[] { 1, 2 }, Array.Empty<int>(), new[] { -1 } })!; Equal(3, d.Length); Sequence(new long[] { 1, 2 }, d[0]); Equal(0, d[1].Length); Sequence(new long[] { -1 }, d[2]); });
            Case("multidimensional destination rejects instead of returning wrong rank", () => Failure(() => mapper.Map<long[,]>(new int[,] { { 1, 2 } }), "$"));
            Case("collection without enumerable constructor rejects", () => Failure(() => mapper.Map<NoEnumerableConstructor<long>>(new[] { 1, 2 }), "$"));
            Case("dictionary cross-type conversion preserves entry", () => { var d = mapper.Map<Dictionary<string, long>>(new Dictionary<string, int> { ["key"] = 7 })!; Equal(1, d.Count); Equal(7L, d["key"]); });
            Case("assignable dictionary preserves identity and data", () => { var s = new Dictionary<string, int> { ["key"] = 7 }; Same(s, mapper.Map<IDictionary<string, int>>(s)); Equal(7, mapper.Map<IDictionary<string, int>>(s)!["key"]); });
            Case("non-generic dictionary conversion rejects", () => Failure(() => mapper.Map<Dictionary<string, long>>(new Hashtable { ["key"] = 7 }), "$"));
            Case("dictionary to non-dictionary collection remains unsupported", () => {
                var s = new System.Collections.ObjectModel.ReadOnlyDictionary<string, int>(new Dictionary<string, int> { ["key"] = 7 });
                Failure(() => mapper.Map<List<KeyValuePair<string, long>>>(s), "$");
            });
            Case("explicit dictionary converter overrides automatic mapping", () => {
                TypeConverterRegistry.Register(typeof(Dictionary<string, short>), typeof(Dictionary<string, long>),
                    new Func<Dictionary<string, short>, Dictionary<string, long>>(s => s.ToDictionary(p => p.Key, p => (long)p.Value)));
                var d = mapper.Map<Dictionary<string, long>>(new Dictionary<string, short> { ["key"] = 7 })!; Equal(1, d.Count); Equal(7L, d["key"]);
            });
            Case("readonly keyvalue elements cannot silently become default", () => {
                var d = mapper.Map<KeyValuePair<string, long>[]>(new[] { new KeyValuePair<string, int>("key", 7) })!; Equal(1, d.Length); Equal("key", d[0].Key); Equal(7L, d[0].Value);
            });
            Case("shared array properties retain mapped alias", () => { var values = new[] { 3, 9 }; var d = mapper.Map<SharedArraysTarget>(new SharedArraysSource { First = values, Second = values })!; Same(d.First, d.Second); Sequence(new long[] { 3, 9 }, d.First!); });
            Case("collection constructor exception is safely reported", () => { var e = Failure(() => mapper.Map<ThrowingCollection<long>>(new[] { 1 }), "$" ); Private(e, "PRIVATE_CONSTRUCTOR"); });

            Case("ForMember null destination selector", () => Throws<ArgumentNullException>(() => new MappingExpression<Source, Target>().ForMember<int>(null!, s => s.Value)));
            Case("ForMember null source expression rejects immediately", () => Throws<ArgumentNullException>(() => new MappingExpression<Source, Target>().ForMember(d => d.Value, null!)));
            Case("Ignore null selector", () => Throws<ArgumentNullException>(() => new MappingExpression<Source, Target>().Ignore(null!)));
            Case("Rename null selector", () => Throws<ArgumentNullException>(() => new MappingExpression<Source, Target>().Rename(null!, "Value")));
            Case("Rename null source name rejects immediately", () => Throws<ArgumentNullException>(() => new MappingExpression<Source, Target>().Rename(d => d.Value, null!)));
            Case("Rename empty source name rejects immediately", () => Throws<ArgumentException>(() => new MappingExpression<Source, Target>().Rename(d => d.Value, "")));
            Case("Rename whitespace source name rejects immediately", () => Throws<ArgumentException>(() => new MappingExpression<Source, Target>().Rename(d => d.Value, "  ")));
            Case("ForMember computed destination selector rejects", () => Throws<InvalidOperationException>(() => new MappingExpression<Source, Target>().ForMember(d => d.Value + 1, s => s.Value)));
            Case("Ignore field selector rejects", () => Throws<InvalidOperationException>(() => new MappingExpression<Source, FieldTarget>().Ignore(d => d.Value)));
            Case("ForMember replacing binding uses last explicit expression", () => { var c = new MapperConfiguration(); c.AddProfile(new ReplaceProfile()); Equal(12, c.CreateMapper().Map<Target>(new Source { Value = 3 })!.Value); });
            Case("Ignore takes precedence over explicit member binding", () => { var c = new MapperConfiguration(); c.AddProfile(new IgnoreProfile()); Equal(0, c.CreateMapper().Map<Target>(new Source { Value = 3 })!.Value); });
            Case("nested destination ForMember maps configured leaf", () => NestedContract("member"));
            Case("nested destination Rename maps configured leaf", () => NestedContract("rename"));
            Case("nested destination Ignore maps configured leaf", () => NestedContract("ignore"));
        }
        finally { MapperProvider.SetCurrent(original); }
        Console.WriteLine($"Public contract regression: {count - failures.Count}/{count} passed.");
        if (failures.Count != 0) throw new AggregateException("Public contract regressions failed", failures);
    }

    private static void NestedContract(string kind)
    {
        var c = new MapperConfiguration(); c.AddProfile(new NestedProfile(kind));
        var d = c.CreateMapper().Map<NestedTarget>(new NestedSource { Value = 19, Child = new Source { Value = 8 } })!;
        Equal(kind == "ignore" ? 0 : 19, d.Child.Value);
    }
    private static IEnumerable<int> YieldNumbers() { yield return 7; yield return 2; }
    private static T Throws<T>(Action action) where T : Exception { try { action(); } catch (T e) { return e; } throw new Exception($"Expected {typeof(T).Name}."); }
    private static void Reject(Action action) { try { action(); } catch (ArgumentException) { return; } catch (NotSupportedException) { return; } catch (MappingException) { return; } throw new Exception("Expected explicit rejection."); }
    private static MappingException Failure(Action action, string path) { var e = Throws<MappingException>(action); Equal(path, e.Path); return e; }
    private static void Private(Exception e, string secret) { if (e.ToString().Contains(secret, StringComparison.Ordinal)) throw new Exception("Unsafe exception exposed secret."); }
    private static void Equal(object? expected, object? actual) { if (!Equals(expected, actual)) throw new Exception($"Expected '{expected ?? "null"}', actual '{actual ?? "null"}'."); }
    private static void Same(object? expected, object? actual) { if (!ReferenceEquals(expected, actual)) throw new Exception("Reference identity was not preserved."); }
    private static void NotSame(object? expected, object? actual) { if (ReferenceEquals(expected, actual)) throw new Exception("Expected independent instances."); }
    private static void Sequence<T>(IEnumerable<T> expected, IEnumerable<T> actual) { if (!expected.SequenceEqual(actual)) throw new Exception("Sequence values/order differ."); }

    public sealed class Source { public int Value { get; set; } public string? Label { get; set; } }
    public sealed class OtherSource { public int Value { get; set; } }
    public sealed class LowerSource { public int value { get; set; } }
    public sealed class Target { public int Value { get; set; } public string? Label { get; set; } = "initial"; }
    public sealed class RuleSource { public int Value { get; set; } }
    public sealed class RuleTarget { public int Value { get; set; } }
    public sealed class UnregisteredSource { }
    public sealed class ThrowingGetter { public int Calls; public int Value { get { Calls++; throw new InvalidOperationException("PRIVATE_GETTER"); } } }
    public sealed class ThrowingSetter { public int Value { get => 0; set => throw new InvalidOperationException("PRIVATE_SETTER"); } }
    public sealed class PrivateSetter { public int Value { get; private set; } = 61; }
    public sealed class ReadonlyTarget { public int Value { get; } = 62; }
    public sealed class PrivateGetter { public int Value { private get => throw new Exception("must not read"); set { } } }
    public sealed class StaticSource { public static int Value => throw new Exception("must not read"); public string Label => "ok"; }
    public sealed class StaticTarget { public static int Value { get; set; } public string? Label { get; set; } }
    public sealed class IndexedSource { public int Value { get; set; } public int this[int index] => throw new Exception("must not read"); }
    public sealed class IndexedTarget { public int Value { get; set; } public int this[int index] { get => 0; set => throw new Exception("must not write"); } }
    public struct ValueSource { public int Value { get; set; } public string? Label { get; set; } }
    public struct ValueTarget { public long Value { get; set; } public string? Label { get; set; } }
    public sealed class Constructed<T> : IEnumerable<T> { public static int Calls; private readonly T[] _items; public Constructed(IEnumerable<T> items) { Calls++; _items = items.ToArray(); } public IEnumerator<T> GetEnumerator() => ((IEnumerable<T>)_items).GetEnumerator(); IEnumerator IEnumerable.GetEnumerator() => GetEnumerator(); }
    public sealed class NoEnumerableConstructor<T> : List<T> { }
    public sealed class ThrowingCollection<T> : List<T> { public ThrowingCollection(IEnumerable<T> items) => throw new InvalidOperationException("PRIVATE_CONSTRUCTOR"); }
    public sealed class SharedArraysSource { public int[]? First { get; set; } public int[]? Second { get; set; } }
    public sealed class SharedArraysTarget { public long[]? First { get; set; } public long[]? Second { get; set; } }
    public sealed class FieldTarget { public int Value; }
    public sealed class ReplaceProfile : MapperProfile { public ReplaceProfile() => CreateMap<Source, Target>().ForMember(d => d.Value, s => s.Value + 1).ForMember(d => d.Value, s => s.Value * 4); }
    public sealed class IgnoreProfile : MapperProfile { public IgnoreProfile() => CreateMap<Source, Target>().ForMember(d => d.Value, s => s.Value + 1).Ignore(d => d.Value); }
    public sealed class NestedSource { public int Value { get; set; } public Source? Child { get; set; } }
    public sealed class NestedTarget { public Target Child { get; set; } = new(); }
    public sealed class NestedProfile : MapperProfile
    {
        public NestedProfile(string kind)
        {
            var map = CreateMap<NestedSource, NestedTarget>();
            if (kind == "member") map.ForMember(d => d.Child.Value, s => s.Value);
            else if (kind == "rename") map.Rename(d => d.Child.Value, "Value");
            else map.Ignore(d => d.Child.Value);
        }
    }
}
