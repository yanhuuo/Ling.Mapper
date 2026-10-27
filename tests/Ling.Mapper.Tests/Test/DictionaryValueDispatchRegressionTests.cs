using Ling.Mapper;
using Ling.Mapper.Configuration;
using Ling.Mapper.Models;
using TestConsole.Utils;

namespace TestConsole.Test;

public static class DictionaryValueDispatchRegressionTests
{
    public static void Run()
    {
        static void Check(bool condition, string message) => TestHelper.AssertTrue(condition, message);
        var mapper = new MapperConfiguration().CreateMapper();
        var node = new Node { Value = 7 }; node.Next = node;
        var source = new Dictionary<string, Node?> { ["first"] = node, ["null"] = null, ["second"] = node };
        for (var iteration = 0; iteration < 2; iteration++)
        {
            var copy = mapper.Map<Dictionary<string, Node?>>(source)!;
            Check(!ReferenceEquals(copy["first"], node), "exact values remain independently cloned");
            Check(ReferenceEquals(copy["first"], copy["second"]) && ReferenceEquals(copy["first"], copy["first"]!.Next), "exact values preserve sharing and cycles");
            Check(copy["null"] == null && copy["first"]!.Value == 7, "null and scalar contents");
        }
        var callbacks = 0;
        mapper.Map<Dictionary<string, Node?>>(source, AdaptOptions.Strict, (_, _) => callbacks++);
        Check(callbacks == 2, "callback remains once per non-null dictionary value, including aliases");
        var child = new Child { Value = 3, Extra = 9 };
        var redirected = mapper.Map<Dictionary<int, Base>>(new Dictionary<int, Child> { [1] = child, [2] = child })!;
        Check(redirected[1] is Child { Extra: 9 } && !ReferenceEquals(redirected[1], child) && ReferenceEquals(redirected[1], redirected[2]), "assignable destination keeps actual concrete shape and aliases");
        var polymorphic = mapper.Map<Dictionary<int, Base>>(new Dictionary<int, Base> { [1] = new Base { Value = 2 }, [2] = child })!;
        Check(polymorphic[2] is Child { Extra: 9 }, "later derived value does not reuse exact-base plan");
        var ignored = mapper.Map<Dictionary<string, Node?>>(source, AdaptOptions.Strict, ignoreNames: ["Value"])!;
        Check(ignored["first"]!.Value == 0 && ReferenceEquals(ignored["first"], ignored["second"]), "ignore-scoped value plans preserve sharing");
        var configured = new MapperConfiguration(); configured.AddProfile(new ValueProfile());
        var projected = configured.CreateMapper().Map<Dictionary<int, Output>>(new Dictionary<int, Input> { [1] = new() { Value = 4 }, [2] = new() { Value = 6 } })!;
        Check(projected[1].Value == 14 && projected[2].Value == 16, "explicit profile honored on lazy and cached dispatch");
        var hot = new Dictionary<int, Live> { [1] = new() { Value = 1 }, [2] = new() { Value = 2 } };
        _ = mapper.Map<Dictionary<int, LiveResult>>(hot); // Cache the unconfigured value plan first.
        TypeConverterRegistry.Register(typeof(Live), typeof(LiveResult), new Func<Live, LiveResult>(value =>
        {
            TypeConverterRegistry.Register(typeof(Live), typeof(LiveResult), new Func<Live, LiveResult>(next => new() { Value = next.Value + 200 }));
            return new() { Value = value.Value + 100 };
        }));
        var changed = mapper.Map<Dictionary<int, LiveResult>>(hot)!;
        Check(changed[1].Value == 101 && changed[2].Value == 202, "registry addition and replacement within a batch invalidate cached dispatch");
        Check(mapper.Map<Dictionary<int, LiveResult>>(hot)![1].Value == 201, "later mapping uses replaced registry generation");
        var bad = new Dictionary<int, Input> { [1] = new() { Value = 1 }, [2] = new() { Value = 2 } };
        TypeConverterRegistry.Register(typeof(Input), typeof(Failure), new Func<Input, Failure>(value => value.Value == 1 ? new() : throw new InvalidOperationException("failure")));
        try { mapper.Map<Dictionary<int, Failure>>(bad); throw new Exception("Expected conversion failure"); }
        catch (MappingException error) { Check(error.Path == "$[1].Value" && error.OriginalExceptionType == typeof(InvalidOperationException).FullName, "cached value failure keeps indexed Value path and original cause"); }
        var attempts = 0;
        TypeConverterRegistry.Register(typeof(Flaky), typeof(FlakyResult), new Func<Flaky, FlakyResult>(value => ++attempts == 1 ? throw new InvalidOperationException("first") : new() { Value = value.Value }));
        var flaky = new Dictionary<int, Flaky> { [1] = new() { Value = 5 } };
        try { mapper.Map<Dictionary<int, FlakyResult>>(flaky); throw new Exception("Expected initial conversion failure"); }
        catch (MappingException error) { Check(error.Path == "$[0].Value", "first-use failure retains Value context"); }
        Check(mapper.Map<Dictionary<int, FlakyResult>>(flaky)![1].Value == 5 && attempts == 2, "first-use failure does not poison retry");
        var comparer = new RegisteringComparer();
        var compared = new Dictionary<int, Compared>(comparer) { [1] = new() { Value = 1 }, [2] = new() { Value = 2 } };
        _ = mapper.Map<Dictionary<int, ComparedResult>>(compared);
        comparer.OnSecondKey = () => TypeConverterRegistry.Register(typeof(Compared), typeof(ComparedResult), new Func<Compared, ComparedResult>(value => new() { Value = value.Value + 100 }));
        var comparedResult = mapper.Map<Dictionary<int, ComparedResult>>(compared)!;
        Check(comparedResult[1].Value == 1 && comparedResult[2].Value == 102, "key preflight registry change invalidates value plan immediately");
        var into = new Dictionary<string, Node?> { ["old"] = new() };
        Check(ReferenceEquals(into, mapper.MapInto(source, into)) && ReferenceEquals(into["first"], into["second"]) && !ReferenceEquals(into["first"], node), "MapInto preserves update and independent graph behavior");
        var concurrentMapper = new MapperConfiguration().CreateMapper();
        Parallel.For(0, 32, _ =>
        {
            var result = concurrentMapper.Map<Dictionary<string, Node?>>(source)!;
            if (ReferenceEquals(result["first"], node) || !ReferenceEquals(result["first"], result["second"])) throw new Exception("Concurrent lazy plan publication lost graph identity");
        });
        Check(true, "concurrent first-use publication");
    }
    public sealed class Node { public int Value { get; set; } public Node? Next { get; set; } }
    public class Base { public int Value { get; set; } }
    public sealed class Child : Base { public int Extra { get; set; } }
    public sealed class Input { public int Value { get; set; } }
    public sealed class Output { public int Value { get; set; } }
    public sealed class Live { public int Value { get; set; } }
    public sealed class LiveResult { public int Value { get; set; } }
    public sealed class Failure { }
    public sealed class Flaky { public int Value { get; set; } }
    public sealed class FlakyResult { public int Value { get; set; } }
    public sealed class Compared { public int Value { get; set; } }
    public sealed class ComparedResult { public int Value { get; set; } }
    public sealed class RegisteringComparer : IEqualityComparer<int>
    {
        public Action? OnSecondKey { get; set; }
        public bool Equals(int left, int right) => left == right;
        public int GetHashCode(int key)
        {
            if (key == 2 && OnSecondKey is { } action) { OnSecondKey = null; action(); }
            return key;
        }
    }
    public sealed class ValueProfile : MapperProfile { public ValueProfile() { CreateMap<Input, Output>().ForMember(value => value.Value, source => source.Value + 10); } }
}
