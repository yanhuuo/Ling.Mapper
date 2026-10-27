using System.Collections;
using Ling.Mapper;
using Ling.Mapper.Configuration;
using Ling.Mapper.Models;
using TestConsole.Utils;

namespace TestConsole.Test;

public static class TypedCollectionDispatchRegressionTests
{
    public static void Run()
    {
        static void Check(bool value, string message) => TestHelper.AssertTrue(value, message);
        var mapper = new MapperConfiguration().CreateMapper();
        var node = new Node { Value = 4 }; node.Next = node;
        var source = new List<Node?> { node, null, node };
        for (var i = 0; i < 2; i++)
        {
            var list = mapper.Map<List<Node?>>(source)!;
            var array = mapper.Map<Node?[]>(source)!;
            var arrayCopy = mapper.Map<Node?[]>(source.ToArray())!;
            var arrayList = mapper.Map<List<Node?>>(source.ToArray())!;
            foreach (var result in new IEnumerable<Node?>[] { list, array, arrayCopy, arrayList })
            {
                var values = result.ToArray();
                Check(values[0]!.Value == 4 && values[1] == null, "typed containers preserve each value and null");
                Check(!ReferenceEquals(node, values[0]) && ReferenceEquals(values[0], values[2]) && ReferenceEquals(values[0], values[0]!.Next), "typed containers preserve independent graph aliases/cycles");
            }
        }
        var isolatedSource = new List<Node> { new() { Value = 10 } };
        var isolatedFirst = mapper.Map<Node[]>(isolatedSource)!;
        var isolatedSecond = mapper.Map<Node[]>(isolatedSource)!;
        Check(!ReferenceEquals(isolatedFirst, isolatedSecond) && !ReferenceEquals(isolatedFirst[0], isolatedSecond[0]), "separate mappings do not share target containers or nodes");
        isolatedSource[0].Value = 20;
        isolatedSource.Add(new() { Value = 30 });
        Check(isolatedFirst.Length == 1 && isolatedFirst[0].Value == 10, "source mutation does not change mapped container or nodes");
        isolatedFirst[0].Value = 40;
        Check(isolatedSource[0].Value == 20 && isolatedSecond[0].Value == 10, "target mutation does not change source or another mapping result");
        var callbacks = 0;
        mapper.Map<Node?[]>(source, AdaptOptions.Strict, (_, _) => callbacks++);
        Check(callbacks == 2, "callback fires for each nonnull occurrence, including alias");
        var ignored = mapper.Map<List<Node?>>(source, AdaptOptions.Strict, ignoreNames: ["Value"])!;
        Check(ignored[0]!.Value == 0 && ReferenceEquals(ignored[0], ignored[2]), "ignore-scoped plans retain aliases");
        var projected = mapper.Map<List<Output>>(new List<Input> { new() { Value = 1 }, new() { Value = 2 } })!;
        Check(projected[0].Value == 1 && projected[1].Value == 2, "cached plan consumes each source value");
        var derived = mapper.Map<Base[]>(new List<Base> { new() { Value = 1 }, new Child { Value = 2, Extra = 3 } })!;
        Check(derived[1] is Child { Extra: 3 }, "polymorphic later item is not projected by exact-base plan");
        var assignable = mapper.Map<List<Base>>(new List<Child> { new() { Value = 2, Extra = 3 } })!;
        Check(assignable[0] is Child { Extra: 3 }, "assignable destination retains concrete shape");
        var strange = new StrangeList { new() { Value = 1 } };
        Check(mapper.Map<Output[]>(strange)![0].Value == 99 && strange.Enumerated, "List subclass custom non-generic enumeration retained");
        var dictionary = new StrangeDictionary { [1] = new() { Value = 1 } };
        Check(mapper.Map<Dictionary<int, Output>>(dictionary)![9].Value == 99 && dictionary.Enumerated, "Dictionary subclass custom generic enumeration retained");
        try { mapper.Map<Dictionary<int, Output>>(new NullEnumeratorDictionary()); throw new Exception("Expected invalid enumerator failure"); }
        catch (MappingException error) { Check(error.OriginalExceptionType == typeof(NullReferenceException).FullName, "null custom dictionary enumerator still fails rather than silently producing empty data"); }
        var disposable = new DisposingDictionary();
        Check(mapper.Map<Dictionary<int, Output>>(disposable)![1].Value == 7 && disposable.Disposed, "custom dictionary enumerator is disposed");
        Check(mapper.Map<int[]>(new List<OperatorInput> { new(1), new(2) })!.SequenceEqual(new[] { 11, 12 }), "typed element dispatch preserves user operator intent");
        var attempts = 0;
        TypeConverterRegistry.Register(typeof(Flaky), typeof(FlakyResult), new Func<Flaky, FlakyResult>(value => ++attempts == 1 ? throw new InvalidOperationException("first") : new() { Value = value.Value }));
        var flaky = new[] { new Flaky { Value = 8 } };
        try { mapper.Map<FlakyResult[]>(flaky); throw new Exception("Expected first-use failure"); }
        catch (MappingException error) { Check(error.Path == "$[0]", "first-use failure retains element path"); }
        Check(mapper.Map<FlakyResult[]>(flaky)![0].Value == 8 && attempts == 2, "failed first use does not poison child-plan retry");
        var inputs = new[] { new Live { Value = 1 }, new Live { Value = 2 } };
        _ = mapper.Map<LiveResult[]>(inputs);
        TypeConverterRegistry.Register(typeof(Live), typeof(LiveResult), new Func<Live, LiveResult>(value =>
        {
            TypeConverterRegistry.Register(typeof(Live), typeof(LiveResult), new Func<Live, LiveResult>(next => new() { Value = next.Value + 200 }));
            return new() { Value = value.Value + 100 };
        }));
        var changed = mapper.Map<LiveResult[]>(inputs)!;
        Check(changed[0].Value == 101 && changed[1].Value == 202, "registry replacement within an array takes effect on next item");
        var getterInputs = new List<GetterInput> { new() { Value = 1 }, new() { Value = 2 } };
        _ = mapper.Map<List<GetterOutput>>(getterInputs);
        getterInputs[0].OnGet = () => TypeConverterRegistry.Register(typeof(GetterInput), typeof(GetterOutput), new Func<GetterInput, GetterOutput>(value => new() { Value = value.Value + 20 }));
        var getterChanged = mapper.Map<List<GetterOutput>>(getterInputs)!;
        Check(getterChanged[0].Value == 1 && getterChanged[1].Value == 22, "getter registry addition within list invalidates cached child plan");
        TypeConverterRegistry.Register(typeof(Input), typeof(Failure), new Func<Input, Failure>(value => value.Value == 1 ? new() : throw new InvalidOperationException("bad")));
        try { mapper.Map<Failure[]>(new List<Input> { new() { Value = 1 }, new() { Value = 2 } }); throw new Exception("Expected failure"); }
        catch (MappingException error) { Check(error.Path == "$[1]" && error.OriginalExceptionType == typeof(InvalidOperationException).FullName, "typed element error retains index and cause"); }
        Check(mapper.Map<long?[]>(new int?[] { 1, null, 3 })!.SequenceEqual(new long?[] { 1, null, 3 }), "nullable source and destination conversion");
        Check(mapper.Map<int[]>(new List<int> { 1, 2, 3 })!.SequenceEqual(new[] { 1, 2, 3 }), "typed atomic list copy");
        var mutating = new List<MutatingInput>();
        mutating.Add(new() { OnGet = () => mutating.Add(new()) });
        try { mapper.Map<MutatingOutput[]>(mutating); throw new Exception("Expected mutation failure"); }
        catch (MappingException error) { Check(error.OriginalExceptionType == typeof(InvalidOperationException).FullName, "list enumerator mutation check retained"); }
        var concurrent = new MapperConfiguration().CreateMapper();
        Parallel.For(0, 32, _ =>
        {
            var result = concurrent.Map<Node?[]>(source)!;
            if (ReferenceEquals(result[0], node) || !ReferenceEquals(result[0], result[2])) throw new Exception("Lost concurrent graph identity");
        });
        Check(true, "concurrent typed lazy-plan publication");
    }
    public sealed class Node { public int Value { get; set; } public Node? Next { get; set; } }
    public sealed class Input { public int Value { get; set; } }
    public sealed class Output { public int Value { get; set; } }
    public class Base { public int Value { get; set; } }
    public sealed class Child : Base { public int Extra { get; set; } }
    public sealed class Live { public int Value { get; set; } }
    public sealed class LiveResult { public int Value { get; set; } }
    public sealed class Failure { }
    public sealed class GetterInput
    {
        private int _value;
        public Action? OnGet;
        public int Value { get { var action = OnGet; OnGet = null; action?.Invoke(); return _value; } set => _value = value; }
    }
    public sealed class GetterOutput { public int Value { get; set; } }
    public sealed class MutatingInput { public Action? OnGet; public int Value { get { OnGet?.Invoke(); return 1; } } }
    public sealed class MutatingOutput { public int Value { get; set; } }
    public sealed class StrangeList : List<Input>, IEnumerable
    {
        public bool Enumerated;
        IEnumerator IEnumerable.GetEnumerator() { Enumerated = true; return new[] { new Input { Value = 99 } }.GetEnumerator(); }
    }
    public readonly record struct OperatorInput(int Value) { public static implicit operator int(OperatorInput value) => value.Value + 10; }
    public sealed class Flaky { public int Value { get; set; } }
    public sealed class FlakyResult { public int Value { get; set; } }
    public sealed class DisposingDictionary : Dictionary<int, Input>, IEnumerable<KeyValuePair<int, Input>>
    {
        public bool Disposed;
        IEnumerator<KeyValuePair<int, Input>> IEnumerable<KeyValuePair<int, Input>>.GetEnumerator()
        { try { yield return new(1, new() { Value = 7 }); } finally { Disposed = true; } }
    }
    public sealed class NullEnumeratorDictionary : Dictionary<int, Input>, IEnumerable<KeyValuePair<int, Input>>
    {
        IEnumerator<KeyValuePair<int, Input>> IEnumerable<KeyValuePair<int, Input>>.GetEnumerator() => null!;
    }
    public sealed class StrangeDictionary : Dictionary<int, Input>, IEnumerable<KeyValuePair<int, Input>>
    {
        public bool Enumerated;
        IEnumerator<KeyValuePair<int, Input>> IEnumerable<KeyValuePair<int, Input>>.GetEnumerator()
        { Enumerated = true; return ((IEnumerable<KeyValuePair<int, Input>>)new[] { new KeyValuePair<int, Input>(9, new() { Value = 99 }) }).GetEnumerator(); }
    }
}
