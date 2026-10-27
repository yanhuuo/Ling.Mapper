using System.Collections.ObjectModel;
using System.Collections.Concurrent;
using Ling.Mapper;
using Ling.Mapper.Configuration;
using Ling.Mapper.Extensions;
using Ling.Mapper.Mapper;
using Ling.Mapper.Models;
using Ling.Mapper.Provider;

namespace TestConsole.Test;

public static class ExistingDestinationRegressionTests
{
    public static void Run()
    {
        int assertions = 0;
        void Equal(object? expected, object? actual) { assertions++; if (!Equals(expected, actual)) throw new Exception($"Expected {expected ?? "null"}; got {actual ?? "null"}."); }
        void Same(object expected, object? actual) => Equal(true, ReferenceEquals(expected, actual));
        void Failure(Action action, Type type, string? path = null)
        {
            assertions++;
            try { action(); }
            catch (MappingException error) when (path != null && error.OriginalExceptionType == type.FullName && error.Path == path) { return; }
            catch (Exception error) when (path == null && error.GetType() == type) { return; }
            throw new Exception($"Expected {type.Name} at {path}.");
        }
        var mapper = new MapperConfiguration().CreateMapper();
        var input = new Input { Value = "42", Name = "new" };
        var target = new Target { Value = 9, Name = "old", Unmatched = "keep" };
        Same(target, mapper.MapInto(input, target)); Equal(42, target.Value); Equal("new", target.Name); Equal("keep", target.Unmatched);
        Equal("42", input.Value);
        target.Value = 8; Same(target, mapper.Map(input, target)); Equal(42, target.Value);
        target.Value = 8; Same(target, mapper.MapInto(input, target, typeof(Input), typeof(Target))); Equal(42, target.Value);
        target.Value = 8; Same(target, mapper.MapInto(input, target, AdaptOptions.Default, ignoreNames: ["Value"])); Equal(8, target.Value);
        Same(target, mapper.MapInto(null, target)); Equal(8, target.Value);
        Same(target, mapper.MapInto(new Input { Name = null, Value = null }, target, AdaptOptions.IgnoreNullValues)); Equal("new", target.Name); Equal(8, target.Value);
        Same(target, mapper.MapInto(new Input(), target)); Equal(null, target.Name); Equal(0, target.Value);
        var noDefault = new NoDefault(1); Same(noDefault, mapper.MapInto(input, noDefault)); Equal(42, noDefault.Value);
        ITarget contract = new InterfaceTarget { Value = 1 }; Same(contract, mapper.MapInto(input, contract)); Equal(42, contract.Value);
        Failure(() => mapper.MapInto(input, (Target)null!), typeof(ArgumentNullException));
        Failure(() => mapper.MapInto(input, "immutable"), typeof(NotSupportedException));
        Failure(() => mapper.MapInto(input, new object(), typeof(Input), typeof(Target)), typeof(ArgumentException));
        Failure(() => mapper.MapInto(input, target, AdaptOptions.Default, ignoreNames: [""]), typeof(ArgumentException));
        Failure(() => mapper.MapInto(input, new ReadOnly(3)), typeof(NotSupportedException), "$");
        Failure(() => mapper.MapInto(input, new InitOnly { Value = 3 }), typeof(NotSupportedException), "$");
        Failure(() => mapper.MapInto(new Input { Value = "bad" }, target), typeof(FormatException), "$.Value");
        var strict = new MapperConfiguration { NullValueHandling = NullValueHandling.Throw }.CreateMapper();
        Same(target, strict.MapInto(null, target));
        Failure(() => strict.MapInto(new Input(), target), typeof(InvalidCastException), "$.Value");
        target.Value = 77; strict.MapInto(new Input(), target, AdaptOptions.IgnoreNullValues); Equal(77, target.Value);

        var config = new MapperConfiguration(); config.AddProfile(new Rules()); var configured = config.CreateMapper();
        var custom = new Target { Value = 3, Name = "old", Unmatched = "keep" };
        configured.MapInto(new CustomInput { Value = "7", Accept = true }, custom); Equal(7, custom.Value); Equal("fallback", custom.Name); Equal("keep", custom.Unmatched);
        var declined = new CustomInput { Accept = false }; configured.MapInto(declined, custom); Equal(0, declined.Reads); Equal(7, custom.Value);
        custom.Value = 99; configured.MapInto(new CustomInput { Value = "2", Accept = true }, custom); Equal(99, custom.Value); // Condition uses current destination.
        var ignored = new ThrowingInput(); configured.MapInto(ignored, custom); Equal(0, ignored.Reads); Equal(99, custom.Value);
        var targetChild = new Child { Value = 1, Keep = "keep" };
        var parent = new ParentTarget { Child = targetChild };
        configured.MapInto(new ParentInput { Child = new ChildInput { Value = "5", Keep = "replace" } }, parent);
        Same(targetChild, parent.Child); Equal(5, targetChild.Value); Equal("keep", targetChild.Keep);
        configured.MapInto(new ParentInput { Child = new ChildInput { Value = "8" } }, parent, AdaptOptions.Default, ignoreNames: ["Child.Value"]);
        Same(targetChild, parent.Child); Equal(5, targetChild.Value);
        configured.MapInto(new ParentInput(), parent, AdaptOptions.IgnoreNullValues); Same(targetChild, parent.Child);
        configured.MapInto(new ParentInput(), parent); Equal(null, parent.Child);
        var sourceNode = new Node { Value = 7 }; sourceNode.Next = sourceNode;
        var destinationNode = new Node { Value = 3 }; destinationNode.Next = destinationNode;
        Same(destinationNode, mapper.MapInto(sourceNode, destinationNode)); Equal(7, destinationNode.Value); Same(destinationNode, destinationNode.Next);
        Equal(7, sourceNode.Value); Same(sourceNode, sourceNode.Next);
        var childInput = new ChildInput { Value = "9" };
        var pairTarget = new PairTarget { First = new Child(), Second = new Child() };
        var first = pairTarget.First;
        configured.MapInto(new PairInput { First = childInput, Second = childInput }, pairTarget);
        Same(first!, pairTarget.First); Same(pairTarget.First!, pairTarget.Second); Equal(9, pairTarget.Second!.Value);
        var final = new Child { Value = 123 };
        var resolvedTarget = new ParentTarget { Child = new Child() };
        configured.MapInto(new ResolvedInput { Value = final }, resolvedTarget); Equal(false, ReferenceEquals(final, resolvedTarget.Child)); Equal(123, resolvedTarget.Child!.Value); Equal(123, final.Value);
        var conditionChild = new Child { Value = 88 };
        var conditionTarget = new ConditionTarget { Child = conditionChild };
        configured.MapInto(new ParentInput { Child = new ChildInput { Value = "5" } }, conditionTarget);
        Same(conditionChild, conditionTarget.Child); Equal(88, conditionChild.Value);
        Rules.FactoryCalls = 0;
        Same(noDefault, configured.MapInto(input, noDefault)); Equal(0, Rules.FactoryCalls); Equal(42, noDefault.Value);

        var numbers = new List<int> { 99 };
        Same(numbers, mapper.MapInto(new[] { "1", "2" }, numbers)); Equal("1,2", string.Join(',', numbers));
        Same(numbers, mapper.MapInto(numbers, numbers)); Equal("1,2", string.Join(',', numbers));
        var array = new[] { 7, 8 };
        Same(array, mapper.MapInto(new[] { "3", "4" }, array)); Equal("3,4", string.Join(',', array));
        Failure(() => mapper.MapInto(new[] { "1" }, array), typeof(InvalidOperationException), "$"); Equal("3,4", string.Join(',', array));
        Failure(() => mapper.MapInto(new[] { "1", "bad" }, array), typeof(FormatException), "$[1]"); Equal("3,4", string.Join(',', array));
        Failure(() => mapper.MapInto(new[] { "1", "bad" }, numbers), typeof(FormatException), "$[1]"); Equal("1,2", string.Join(',', numbers));
        ICollection<int> set = new HashSet<int> { 7 }; Same(set, mapper.MapInto(new[] { "1", "1", "2" }, set)); Equal(2, set.Count);
        Failure(() => mapper.MapInto(new[] { "1" }, new ReadOnlyCollection<int>(numbers)), typeof(NotSupportedException), "$");
        var oldImmutable = new ReadOnly(8);
        var immutableHolder = new ImmutableHolder { Value = oldImmutable };
        mapper.MapInto(new ImmutableInputHolder { Value = new Input { Value = "9" } }, immutableHolder);
        Equal(9, immutableHolder.Value!.Value); Equal(false, ReferenceEquals(oldImmutable, immutableHolder.Value));
        var oldArray = new[] { 7 };
        var arrayHolder = new ArrayHolder { Values = oldArray };
        mapper.MapInto(new ListInput { Values = ["3", "4"] }, arrayHolder);
        Equal("3,4", string.Join(',', arrayHolder.Values!)); Equal(false, ReferenceEquals(oldArray, arrayHolder.Values));
        IReadOnlyList<int> oldReadOnly = new ReadOnlyCollection<int>(new[] { 8 });
        var readOnlyHolder = new ReadOnlyListHolder { Values = oldReadOnly };
        mapper.MapInto(new ListInput { Values = ["3", "4"] }, readOnlyHolder);
        Equal("3,4", string.Join(',', readOnlyHolder.Values!)); Equal(false, ReferenceEquals(oldReadOnly, readOnlyHolder.Values));
        var listHolder = new ListTarget { Values = numbers };
        mapper.MapInto(new ListInput { Values = ["5", "6"] }, listHolder); Same(numbers, listHolder.Values); Equal("5,6", string.Join(',', numbers));
        var mappedChildren = new List<Child> { new() { Value = 99 } };
        configured.MapInto(new[] { childInput, childInput }, mappedChildren); Same(mappedChildren[0], mappedChildren[1]); Equal(9, mappedChildren[0].Value);
        var dictionary = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase) { ["old"] = 99 };
        Same(dictionary, mapper.MapInto(new Dictionary<string, string> { ["a"] = "1", ["b"] = "2" }, dictionary)); Equal(2, dictionary.Count); Equal(1, dictionary["A"]);
        Same(dictionary, mapper.MapInto(dictionary, dictionary)); Equal(2, dictionary.Count); Equal(1, dictionary["A"]);
        Failure(() => mapper.MapInto(new Dictionary<string, string> { ["A"] = "1", ["a"] = "2" }, dictionary), typeof(ArgumentException), "$[1].Key"); Equal(2, dictionary.Count);
        Failure(() => mapper.MapInto(new Dictionary<string, string> { ["x"] = "bad" }, dictionary), typeof(FormatException), "$[0].Value"); Equal(2, dictionary.Count);
        var readOnlyDictionary = new ReadOnlyDictionary<string, int>(dictionary);
        Failure(() => mapper.MapInto(new Dictionary<string, string> { ["a"] = "1" }, readOnlyDictionary), typeof(NotSupportedException), "$");
        var concurrent = new ConcurrentDictionary<string, int>(StringComparer.OrdinalIgnoreCase); concurrent["old"] = 99;
        Failure(() => mapper.MapInto(new Dictionary<string, string> { ["A"] = "1", ["a"] = "2" }, concurrent), typeof(ArgumentException), "$[1].Key");
        Equal(1, concurrent.Count); Equal(99, concurrent["old"]);
        var initPathConfig = new MapperConfiguration(); initPathConfig.AddProfile(new InitPathRules()); var initPathMapper = initPathConfig.CreateMapper();
        var initMiddle = new InitMiddle { Child = new Child { Value = 1 } }; initMiddle.SetterCalls = 0;
        Failure(() => initPathMapper.MapInto(new Input { Value = "7" }, new InitRoot { Middle = initMiddle }), typeof(NotSupportedException), "$");
        Equal(0, initMiddle.SetterCalls); Equal(1, initMiddle.Child.Value);
        var nullOperatorTarget = new NullOperatorTarget { Value = new AmbiguousTarget() };
        mapper.MapInto(new NullOperatorSource(), nullOperatorTarget); Equal(null, nullOperatorTarget.Value);
        var callbacks = 0;
        mapper.MapInto(input, target, AdaptOptions.Default, (destination, source) => { callbacks++; Same(target, destination); Same(input, source); }); Equal(1, callbacks);
        mapper.MapInto(null, target, AdaptOptions.Default, (_, _) => callbacks++); Equal(1, callbacks);
        callbacks = 0; mapper.MapInto(new[] { "3", "4" }, numbers, AdaptOptions.Default, (_, _) => callbacks++); Equal(2, callbacks);
        callbacks = 0;
        Failure(() => mapper.MapInto(new Input { Value = "bad" }, target, AdaptOptions.Default, (_, _) => callbacks++), typeof(FormatException), "$.Value"); Equal(0, callbacks);
        var previous = MapperProvider.Current;
        try
        {
            MapperProvider.SetCurrent(mapper);
            Same(target, input.AdaptInto(target)); Equal(42, target.Value);
            target.Value = 8; input.AdaptInto(target, AdaptOptions.Default, "Value"); Equal(8, target.Value);
            callbacks = 0; input.AdaptInto(target, result => { callbacks++; Same(target, result); }); Equal(1, callbacks);
            ((object?)null).AdaptInto(target, _ => callbacks++); Equal(2, callbacks);
        }
        finally { MapperProvider.SetCurrent(previous); }
        Failure(() => GeneratedMapperRegistry.CreateMapper().MapInto(input, target), typeof(NotSupportedException));
        // Separate update/new-object caches cannot contaminate one another.
        var plainSame = new Node { Value = 4 };
        Equal(false, ReferenceEquals(plainSame, mapper.Map<Node>(plainSame))); Equal(4, mapper.Map<Node>(plainSame)!.Value);
        var otherSame = new Node { Value = 1 }; Same(otherSame, mapper.MapInto(plainSame, otherSame)); Equal(4, otherSame.Value);
        Equal(false, ReferenceEquals(plainSame, mapper.Map<Node>(plainSame))); Equal(4, mapper.Map<Node>(plainSame)!.Value);
        // A new root context has no references. Skipping its guaranteed cache miss
        // must not bypass later plan-generation checks when converters change.
        var opaqueSource = new OpaqueSource();
        var opaqueTarget = new OpaqueTarget { Value = 17 };
        var opaqueCallbacks = 0;
        Same(opaqueTarget, mapper.MapInto(opaqueSource, opaqueTarget, AdaptOptions.Strict, (_, _) => opaqueCallbacks++));
        Equal(17, opaqueTarget.Value); Equal(1, opaqueCallbacks);
        var opaqueCalls = 0;
        TypeConverterRegistry.Register(typeof(OpaqueSource), typeof(OpaqueTarget), new Func<OpaqueSource, OpaqueTarget>(_ => { opaqueCalls++; return new() { Value = 99 }; }));
        Failure(() => mapper.MapInto(opaqueSource, opaqueTarget, AdaptOptions.Strict, (_, _) => opaqueCallbacks++), typeof(NotSupportedException), "$");
        Equal(17, opaqueTarget.Value); Equal(1, opaqueCallbacks); Equal(0, opaqueCalls);
        TypeConverterRegistry.Register(typeof(OpaqueSource), typeof(OpaqueTarget), new Func<OpaqueSource, OpaqueTarget>(_ => { opaqueCalls++; return new() { Value = 101 }; }));
        Failure(() => mapper.MapInto(opaqueSource, opaqueTarget), typeof(NotSupportedException), "$");
        Equal(17, opaqueTarget.Value); Equal(0, opaqueCalls);
        Equal(101, mapper.Map<OpaqueTarget>(opaqueSource)!.Value); Equal(1, opaqueCalls);
        Failure(() => mapper.MapInto(new OperatorSource(), new OpaqueTarget()), typeof(NotSupportedException), "$");
        Parallel.For(0, 256, index =>
        {
            var destination = new Target { Value = -1, Unmatched = "keep" };
            if (!ReferenceEquals(destination, mapper.MapInto(new Input { Value = index.ToString() }, destination)) || destination.Value != index || destination.Unmatched != "keep")
                throw new Exception("Concurrent existing-target mapping used another invocation's target.");
        });
        assertions += 256 * 3;
        Console.WriteLine($"Existing destination regression: {assertions}/{assertions} assertions passed.");
    }
    public sealed class Input { public string? Value { get; set; } public string? Name { get; set; } }
    public sealed class Target { public int Value { get; set; } public string? Name { get; set; } public string? Unmatched { get; set; } }
    public sealed class NoDefault(int value) { public int Value { get; set; } = value; }
    public interface ITarget { int Value { get; set; } }
    public sealed class InterfaceTarget : ITarget { public int Value { get; set; } }
    public sealed record ReadOnly(int Value);
    public sealed class InitOnly { public int Value { get; init; } }
    public sealed class CustomInput { public string? Value { get; set; } public bool Accept { get; set; } public int Reads; public string Read() { Reads++; return Value!; } }
    public sealed class ThrowingInput { public int Reads; public string Value { get { Reads++; throw new FormatException("getter"); } } }
    public sealed class ChildInput { public string? Value { get; set; } public string? Keep { get; set; } }
    public sealed class Child { public int Value { get; set; } public string? Keep { get; set; } }
    public sealed class ParentInput { public ChildInput? Child { get; set; } }
    public sealed class ParentTarget { public Child? Child { get; set; } }
    public sealed class Node { public int Value { get; set; } public Node? Next { get; set; } }
    public sealed class PairInput { public ChildInput? First { get; set; } public ChildInput? Second { get; set; } }
    public sealed class PairTarget { public Child? First { get; set; } public Child? Second { get; set; } }
    public sealed class ResolvedInput { public Child? Value { get; set; } }
    public sealed class ConditionTarget { public Child? Child { get; set; } }
    public sealed class ListInput { public string[]? Values { get; set; } }
    public sealed class ImmutableInputHolder { public Input? Value { get; set; } }
    public sealed class ImmutableHolder { public ReadOnly? Value { get; set; } }
    public sealed class ArrayHolder { public int[]? Values { get; set; } }
    public sealed class ReadOnlyListHolder { public IReadOnlyList<int>? Values { get; set; } }
    public sealed class ListTarget { public List<int>? Values { get; set; } }
    public sealed class OpaqueSource { }
    public sealed class OpaqueTarget { public int Value { get; set; } }
    public sealed class OperatorSource { public static implicit operator OpaqueTarget(OperatorSource source) => new(); }
    public sealed class InitRoot { public InitMiddle Middle { get; set; } = new(); }
    public sealed class InitMiddle { private Child _child = new(); public int SetterCalls; public Child Child { get => _child; init { SetterCalls++; _child = value; } } }
    public sealed class InitPathRules : MapperProfile { public InitPathRules() { CreateMap<Input, InitRoot>().ForMember(destination => destination.Middle.Child.Value, options => options.MapFrom(source => source.Value)); } }
    public sealed class AmbiguousSource { public static implicit operator AmbiguousTarget(AmbiguousSource source) => new(); }
    public sealed class AmbiguousTarget { public int Value { get; set; } public static explicit operator AmbiguousTarget(AmbiguousSource source) => new(); }
    public sealed class NullOperatorSource { public AmbiguousSource? Value { get; set; } }
    public sealed class NullOperatorTarget { public AmbiguousTarget? Value { get; set; } }
    public sealed class Rules : MapperProfile
    {
        public static int FactoryCalls;
        private static NoDefault Make() { FactoryCalls++; return new(0); }
        public Rules()
        {
            CreateMap<CustomInput, Target>()
                .ForMember(destination => destination.Value, options => options.MapFrom(source => source.Read()).PreCondition(source => source.Accept).Condition((source, destination, candidate) => destination.Value < 50))
                .ForMember(destination => destination.Name, options => options.MapFrom(source => (string?)null).NullSubstitute("fallback"))
                .Ignore(destination => destination.Unmatched);
            CreateMap<ThrowingInput, Target>().Ignore(destination => destination.Value);
            CreateMap<ChildInput, Child>().Ignore(destination => destination.Keep);
            CreateMap<ResolvedInput, ParentTarget>().ForMember(destination => destination.Child, source => source.Value);
            CreateMap<ParentInput, ConditionTarget>().ForMember(destination => destination.Child, options => options.MapFrom(source => source.Child).Condition((source, destination, candidate) => false));
            CreateMap<Input, NoDefault>().ConstructUsing(source => Make());
        }
    }
}
