using System.Collections.Concurrent;
using System.Collections.ObjectModel;
using Ling.Mapper;
using Ling.Mapper.Configuration;
using Ling.Mapper.Models;
using TestConsole.Utils;

namespace TestConsole.Test;

/// <summary>Bounded graph tests: this is not a promise of unlimited recursion depth.</summary>
public static class DictionaryDeepRegressionTests
{
    public static void Run()
    {
        var mapper = new MapperConfiguration().CreateMapper();
        var failures = new List<string>();
        void Case(string name, Action test)
        {
            try { test(); Console.WriteLine($"CHECKED {name}"); }
            catch (Exception e) { failures.Add(name + ": " + e.Message); Console.WriteLine($"FAILED {name}: {e}"); }
        }
        var source = new Dictionary<string, string> { ["2"] = "17", ["7"] = "31" };
        var sourceTypes = new[] { typeof(Dictionary<string, string>), typeof(IDictionary<string, string>), typeof(IReadOnlyDictionary<string, string>), typeof(SortedDictionary<string, string>), typeof(ConcurrentDictionary<string, string>), typeof(ReadOnlyDictionary<string, string>) };
        var destinationTypes = new[] { typeof(Dictionary<int, long>), typeof(IDictionary<int, long>), typeof(IReadOnlyDictionary<int, long>), typeof(SortedDictionary<int, long>), typeof(ConcurrentDictionary<int, long>), typeof(ReadOnlyDictionary<int, long>) };
        object MakeSource(Type type, bool empty) => type == typeof(SortedDictionary<string, string>) ? new SortedDictionary<string, string>(empty ? new() : source) : type == typeof(ConcurrentDictionary<string, string>) ? new ConcurrentDictionary<string, string>(empty ? new Dictionary<string, string>() : source) : type == typeof(ReadOnlyDictionary<string, string>) ? new ReadOnlyDictionary<string, string>(empty ? new() : source) : empty ? new Dictionary<string, string>() : source;
        foreach (var st in sourceTypes)
        foreach (var dt in destinationTypes)
        foreach (var empty in new[] { false, true })
            Case($"shape {st.Name} -> {dt.Name}; empty={empty}", () => {
                var mapped = (IEnumerable<KeyValuePair<int, long>>)mapper.Map(MakeSource(st, empty), st, dt)!;
                var entries = mapped.ToDictionary(p => p.Key, p => p.Value);
                Equal(empty ? 0 : 2, entries.Count, "entry count");
                if (!empty) { Equal(17L, entries[2], "first value"); Equal(31L, entries[7], "second value"); }
                Check(dt.IsInstanceOfType(mapped), "declared destination shape");
            });
        Case("nullable and enum key/value conversion", () => {
            var result = mapper.Map<Dictionary<Code, long?>>(new Dictionary<string, int?> { ["One"] = 12, ["Two"] = null })!;
            Equal(12L, result[Code.One], "nullable present"); Equal(null, result[Code.Two], "nullable null");
            var reverse = mapper.Map<Dictionary<string, Code?>>(new Dictionary<Code, string?> { [Code.One] = "Two", [Code.Two] = null })!;
            Equal(Code.Two, reverse["One"], "nullable enum"); Equal(null, reverse["Two"], "null enum");
        });
        Case("nested dictionary/list/dictionary complex null and shared values", () => {
            var shared = new Input { Value = "81", Keep = "kept" };
            var inner = new Dictionary<string, Input?> { ["1"] = shared, ["2"] = shared, ["3"] = null };
            var input = new Dictionary<string, List<Dictionary<string, Input?>>> { ["root"] = new() { inner, inner, new() } };
            var output = mapper.Map<Dictionary<string, List<Dictionary<int, Output?>>>>(input)!;
            Equal(1, output.Count, "outer count"); Equal(3, output["root"].Count, "list count");
            Same(output["root"][0], output["root"][1], "shared inner dictionary");
            Same(output["root"][0][1], output["root"][0][2], "shared complex value");
            Equal(81, output["root"][0][1]!.Value, "nested converted value"); Equal("kept", output["root"][0][1]!.Keep, "nested text");
            Equal(null, output["root"][0][3], "null complex value"); Equal(0, output["root"][2].Count, "nested empty dictionary");
            Check(!ReferenceEquals(shared, output["root"][0][1]), "cross-type value distinct"); Equal("81", shared.Value, "source unchanged");
        });
        Case("converted null key explicitly rejected", () => {
            TypeConverterRegistry.Register(typeof(NullKey), typeof(string), new Func<NullKey, string>(_ => null!));
            Failure(() => mapper.Map<Dictionary<string, int>>(new Dictionary<NullKey, int> { [new NullKey()] = 7 }), "$[0].Key", typeof(ArgumentException));
        });
        Case("conversion collision and diagnostics", () => {
            Failure(() => mapper.Map<Dictionary<int, int>>(new Dictionary<string, string> { ["1"] = "2", ["01"] = "3" }), "$[1].Key", typeof(ArgumentException));
            Failure(() => mapper.Map<Dictionary<int, int>>(new Dictionary<string, string> { ["invalid"] = "2" }), "$[0].Key", typeof(FormatException));
            Failure(() => mapper.Map<Dictionary<int, int>>(new Dictionary<string, string> { ["1"] = "invalid" }), "$[0].Value", typeof(FormatException));
        });
        foreach (var kind in new[] { "dictionary", "sorted", "concurrent" })
            Case($"existing {kind} comparer and preflight", () => {
                IDictionary<string, int> target = kind == "sorted" ? new SortedDictionary<string, int>(StringComparer.OrdinalIgnoreCase) : kind == "concurrent" ? new ConcurrentDictionary<string, int>(StringComparer.OrdinalIgnoreCase) : new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
                target["old"] = 99;
                Failure(() => mapper.MapInto(new Dictionary<string, string> { ["A"] = "1", ["a"] = "2" }, target), "$[1].Key", typeof(ArgumentException));
                Equal(1, target.Count, "collision did not clear target"); Equal(99, target["OLD"], "original entry intact");
                Failure(() => mapper.MapInto(new Dictionary<string, string> { ["new"] = "1", ["bad"] = "invalid" }, target), "$[1].Value", typeof(FormatException));
                Equal(1, target.Count, "late value failure did not clear target"); Equal(99, target["OLD"], "original value intact");
                Same(target, mapper.MapInto(new Dictionary<string, string> { ["new"] = "7" }, target), "update identity");
                Equal(1, target.Count, "update replacement count"); Equal(7, target["NEW"], "comparer retained");
            });
        Case("read-only update explicitly rejected without mutation", () => {
            var target = new ReadOnlyDictionary<string, int>(new Dictionary<string, int> { ["old"] = 99 });
            Failure(() => mapper.MapInto(source, target), "$", typeof(NotSupportedException));
            Equal(1, target.Count, "readonly count"); Equal(99, target["old"], "readonly value");
        });
        foreach (int depth in new[] { 16, 64, 128, 256 })
            Case($"bounded acyclic mixed graph depth {depth}", () => {
                var root = Build(depth); var mapped = mapper.Map<TargetNode>(root)!;
                var s = root; var d = mapped;
                for (int i = 0; i < depth; i++) {
                    Equal(i, d.Value, $"level {i} scalar"); Check(!ReferenceEquals(s, d), $"level {i} converted identity");
                    if (i + 1 == depth) { Equal(null, d.Next, "terminal next"); Equal(0, d.Links.Count, "terminal dictionary"); }
                    else { Equal(1, d.Links.Count, "dictionary count"); Equal(2, d.Links[1].Count, "list count"); Same(d.Next, d.Links[1][0], "next/dictionary alias"); Same(d.Next, d.Links[1][1], "duplicate list alias"); s = s.Next!; d = d.Next!; }
                }
            });
        Case("mixed graph cycle closure and shared identity", () => {
            var root = Build(16); var tail = root; while (tail.Next != null) tail = tail.Next;
            tail.Next = root; tail.Links["1"] = new() { root, root };
            var mapped = mapper.Map<TargetNode>(root)!; var cursor = mapped;
            for (int i = 0; i < 16; i++) { Equal(i, cursor.Value, "cycle scalar"); Same(cursor.Next, cursor.Links[1][0], "cycle branch alias"); Same(cursor.Next, cursor.Links[1][1], "cycle duplicate alias"); cursor = cursor.Next!; }
            Same(mapped, cursor, "cycle closes on mapped root");
        });
        Case("deep precise error path then ignored path then clean cache", () => {
            var root = Build(16); var cursor = root; for (int i = 0; i < 15; i++) cursor = cursor.Next!; cursor.Value = "bad";
            string path = string.Concat(Enumerable.Repeat("Next.", 15)) + "Value";
            Failure(() => mapper.Map<TargetNode>(root), "$." + path, typeof(FormatException));
            // Use an unbranched graph to isolate dotted-ignore semantics from alternate paths.
            cursor = root; while (cursor != null) { cursor.Links.Clear(); cursor = cursor.Next; }
            var mapped = mapper.Map<TargetNode>(root, AdaptOptions.Default, ignoreNames: new[] { path })!;
            var d = mapped; for (int i = 0; i < 15; i++) { Equal(i, d.Value, "unignored ancestor"); d = d.Next!; }
            Equal(0, d.Value, "ignored invalid leaf remains default");
            Failure(() => mapper.Map<TargetNode>(root), "$." + path, typeof(FormatException));
        });
        Case("explicit polymorphic dictionary value plans remain isolated", () => {
            var config = new MapperConfiguration(); config.AddProfile(new PolymorphicProfile()); var configured = config.CreateMapper();
            var derived = new Derived { Value = "8", Extra = "derived" };
            var input = new PolymorphicInput { Base = new() { ["1"] = derived }, Derived = new() { ["1"] = derived } };
            var result = configured.Map<PolymorphicOutput>(input)!;
            Equal(8, result.Base[1].Value, "base scalar"); Equal("base", result.Base[1].Extra, "explicit base plan");
            Equal(8, result.Derived[1].Value, "derived scalar"); Equal("derived", result.Derived[1].Extra, "derived member retained");
            Check(!ReferenceEquals(result.Base[1], result.Derived[1]), "different declared source projections isolated");
        });
        foreach (var derivedFirst in new[] { false, true })
            Case($"default polymorphic runtime graph shared full shape; derivedFirst={derivedFirst}", () => {
                var shared = new Derived { Value = "8", Extra = "derived" };
                // Reverse property declaration order, not merely dictionary insertion order.
                object input = derivedFirst
                    ? new ReversePolymorphicInput { Base = new() { ["1"] = shared }, Derived = new() { ["1"] = shared } }
                    : new PolymorphicInput { Base = new() { ["1"] = shared }, Derived = new() { ["1"] = shared } };
                var result = derivedFirst
                    ? mapper.Map(input, typeof(ReversePolymorphicInput), typeof(ReversePolymorphicOutput))!
                    : mapper.Map(input, typeof(PolymorphicInput), typeof(PolymorphicOutput))!;
                var first = result is ReversePolymorphicOutput reverse ? reverse.Base[1] : ((PolymorphicOutput)result).Base[1];
                var second = result is ReversePolymorphicOutput reverse2 ? reverse2.Derived[1] : ((PolymorphicOutput)result).Derived[1];
                Equal(8, first.Value, "default base scalar"); Equal(8, second.Value, "default derived scalar");
                Equal("derived", first.Extra, "runtime derived field via base declaration"); Equal("derived", second.Extra, "runtime derived field via derived declaration");
                Same(first, second, "default projections preserve shared source alias");
            });
        Case("nongeneric dictionary boundaries explicitly unsupported", () => {
            var untyped = new System.Collections.Hashtable { ["1"] = "9" };
            Failure(() => mapper.Map<Dictionary<int, int>>(untyped), "$", typeof(NotSupportedException));
            Failure(() => mapper.Map<System.Collections.Hashtable>(source), "$", typeof(NotSupportedException));
            Failure(() => mapper.Map(untyped, typeof(System.Collections.IDictionary), typeof(Dictionary<int, int>)), "$", typeof(NotSupportedException));
            Equal("9", untyped["1"], "unsupported source unchanged");
        });
        Case("ExpandoObject generic dictionary shape supports typed conversion", () => {
            IDictionary<string, object?> expando = new System.Dynamic.ExpandoObject(); expando["1"] = "9"; expando["2"] = 17;
            var typed = mapper.Map<Dictionary<int, long>>(expando)!;
            Equal(2, typed.Count, "Expando typed count"); Equal(9L, typed[1], "Expando runtime string conversion"); Equal(17L, typed[2], "Expando runtime number conversion");
            var back = mapper.Map<System.Dynamic.ExpandoObject>(new Dictionary<string, int> { ["a"] = 4 })!;
            Equal(4, ((IDictionary<string, object?>)back)["a"], "Expando target entry");
            var child = new Input { Value = "8", Keep = "before" }; expando["child"] = child;
            var clone = mapper.Map<System.Dynamic.ExpandoObject>(expando)!; var clonedChild = (Input)((IDictionary<string, object?>)clone)["child"]!;
            Check(!ReferenceEquals(expando, clone), "fresh Expando root"); Check(!ReferenceEquals(child, clonedChild), "fresh mutable dynamic value");
            Equal("8", clonedChild.Value, "dynamic mutable value data"); clonedChild.Keep = "after"; Equal("before", child.Keep, "dynamic nested mutation isolated");
        });
        if (failures.Count != 0) throw new Exception(string.Join(Environment.NewLine, failures));
    }
    static SourceNode Build(int depth) { var root = new SourceNode { Value = "0" }; var n = root; for (int i = 1; i < depth; i++) { var next = new SourceNode { Value = i.ToString() }; n.Next = next; n.Links["1"] = new() { next, next }; n = next; } return root; }
    static void Check(bool result, string message) => TestHelper.AssertTrue(result, message);
    static void Equal(object? expected, object? actual, string message) => TestHelper.AssertEqual(expected, actual, message);
    static void Same(object? expected, object? actual, string message) => Check(ReferenceEquals(expected, actual), message);
    static void Failure(Action action, string path, Type cause) { MappingException? error = null; try { action(); } catch (MappingException e) { error = e; } Check(error != null, "explicit mapping failure"); Equal(path, error!.Path, "exact diagnostic path"); Equal(cause.FullName, error.OriginalExceptionType, "original exception type"); }
    public sealed class NullKey { }
    public enum Code { One = 1, Two = 2 }
    public class Input { public string? Value { get; set; } public string? Keep { get; set; } }
    public class Output { public int Value { get; set; } public string? Keep { get; set; } }
    public class SourceNode { public string? Value { get; set; } public SourceNode? Next { get; set; } public Dictionary<string, List<SourceNode>> Links { get; set; } = new(); }
    public class TargetNode { public int Value { get; set; } public TargetNode? Next { get; set; } public Dictionary<int, List<TargetNode>> Links { get; set; } = new(); }
    public sealed class PolymorphicProfile : MapperProfile { public PolymorphicProfile() { CreateMap<Base, PolyTarget>().ForMember(d => d.Extra, s => "base"); CreateMap<Derived, PolyTarget>().ForMember(d => d.Extra, s => s.Extra); } }
    public class Base { public string? Value { get; set; } }
    public class Derived : Base { public string? Extra { get; set; } }
    public class PolyTarget { public int Value { get; set; } public string? Extra { get; set; } }
    public class PolymorphicInput { public Dictionary<string, Base> Base { get; set; } = new(); public Dictionary<string, Derived> Derived { get; set; } = new(); }
    public class ReversePolymorphicInput { public Dictionary<string, Derived> Derived { get; set; } = new(); public Dictionary<string, Base> Base { get; set; } = new(); }
    public class ReversePolymorphicOutput { public Dictionary<int, PolyTarget> Derived { get; set; } = new(); public Dictionary<int, PolyTarget> Base { get; set; } = new(); }
    public class PolymorphicOutput { public Dictionary<int, PolyTarget> Base { get; set; } = new(); public Dictionary<int, PolyTarget> Derived { get; set; } = new(); }
}
