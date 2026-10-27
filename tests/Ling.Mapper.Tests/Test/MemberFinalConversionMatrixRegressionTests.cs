using Ling.Mapper;
using Ling.Mapper.Configuration;
using Ling.Mapper.Extensions;
using TestConsole.Utils;

namespace TestConsole.Test;

/// <summary>Selected-source expressions and final converters are separate pipeline stages.</summary>
public static class MemberFinalConversionMatrixRegressionTests
{
    public static void Run()
    {
        var configuration = new MapperConfiguration(); configuration.AddProfile(new Rules()); var mapper = configuration.CreateMapper();
        var source = new Source();
        var selected = mapper.Map<SelectorTarget>(source)!;
        TestHelper.AssertEqual(1, source.SelectorCalls, "Target-shaped selector runs once");
        TestHelper.AssertTrue(!ReferenceEquals(source.Cached, selected.Value), "Selector output is recursively mapped, not assigned");
        TestHelper.AssertEqual(107, selected.Value!.Value, "Selector mapped value");
        selected.Value.Value++;
        TestHelper.AssertEqual(107, source.Cached.Value, "Selector result mutation is isolated");
        TypeConverterRegistry.Register(typeof(Result), typeof(Result), new Func<Result, Result>(_ => throw new Exception("Final output must not re-enter mapping")));
        foreach (var target in new[] { typeof(FinalTarget), typeof(ContextTarget), typeof(ConventionTarget), typeof(ImmutableTarget) })
        {
            source.Reset();
            var mapped = mapper.Map(source, typeof(Source), target)!;
            var value = (Result)target.GetProperty("Value")!.GetValue(mapped)!;
            TestHelper.AssertTrue(ReferenceEquals(source.Cached, value), "Explicit final output identity: " + target.Name);
            TestHelper.AssertEqual(107, value.Value, "Final value: " + target.Name);
            TestHelper.AssertEqual(1, source.SelectorCalls, "Selector once: " + target.Name);
            TestHelper.AssertEqual(1, source.ConverterCalls, "Final converter once: " + target.Name);
        }
        foreach (var context in new[] { new MappingCallContext(), new MappingCallContext() })
        {
            source.Reset();
            var contextResult = mapper.MapWithContext<ContextTarget>(source, context)!;
            TestHelper.AssertTrue(ReferenceEquals(context, source.LastContext) && ReferenceEquals(source.Cached, contextResult.Value), "Final converter uses this invocation context, never a cached one");
            TestHelper.AssertEqual(1, source.SelectorCalls, "Explicit context selector once");
            TestHelper.AssertEqual(1, source.ConverterCalls, "Explicit context converter once");
        }
        source.Reset();
        var nested = mapper.Map<NestedTarget>(source)!;
        TestHelper.AssertTrue(ReferenceEquals(source.Cached, nested.Child!.Value), "Nested final output is not remapped");
        TestHelper.AssertEqual(1, source.SelectorCalls, "Nested selector once");
        TestHelper.AssertEqual(1, source.ConverterCalls, "Nested converter once");
        source.Reset();
        var destination = new FinalTarget();
        TestHelper.AssertTrue(ReferenceEquals(destination, mapper.MapInto(source, destination)), "MapInto root retained");
        TestHelper.AssertTrue(ReferenceEquals(source.Cached, destination.Value), "MapInto respects explicit final result");
        TestHelper.AssertEqual(1, source.SelectorCalls, "MapInto selector once");
        TestHelper.AssertEqual(1, source.ConverterCalls, "MapInto converter once");
        source.Reset();
        TestHelper.AssertTrue(mapper.Map<GuardTarget>(source)!.Value == null, "Precondition skips assignment");
        TestHelper.AssertEqual(0, source.SelectorCalls, "Precondition skips selector");
        TestHelper.AssertEqual(0, source.ConverterCalls, "Precondition skips converter");
        source.Reset();
        TestHelper.AssertTrue(mapper.Map<IgnoredTarget>(source)!.Value == null, "Ignore skips assignment");
        TestHelper.AssertEqual(0, source.SelectorCalls, "Ignore skips selector");
        TestHelper.AssertEqual(0, source.ConverterCalls, "Ignore skips converter");
        source.Reset();
        TestHelper.AssertTrue(mapper.Map<ConditionTarget>(source)!.Value == null, "Condition sees converted candidate and can reject assignment");
        TestHelper.AssertEqual(1, source.SelectorCalls, "Condition selector once");
        TestHelper.AssertEqual(1, source.ConverterCalls, "Condition converter once");
        TestHelper.AssertEqual(1, source.ConditionCalls, "Condition executes once on the final candidate");
        source.Reset();
        try { mapper.Map<ErrorTarget>(source); throw new Exception("Expected final-converter error"); }
        catch (MappingException error)
        {
            TestHelper.AssertEqual(typeof(FormatException).FullName, error.OriginalExceptionType, "Final-converter error type");
            TestHelper.AssertEqual("$.Value", error.Path, "Final-converter error path");
        }
        TestHelper.AssertEqual(1, source.SelectorCalls, "Failing selector once");
        TestHelper.AssertEqual(1, source.ConverterCalls, "Failing converter once");
        source.Reset(); source.IsNull = true;
        TestHelper.AssertTrue(mapper.Map<FinalTarget>(source)!.Value == null, "Selected null follows mapper policy");
        TestHelper.AssertEqual(1, source.SelectorCalls, "Null selector once");
        TestHelper.AssertEqual(0, source.ConverterCalls, "Null does not invoke final converter");
        source.Reset();
        var substituteOne = mapper.Map<SubstituteTarget>(source)!.Value!;
        TestHelper.AssertTrue(!ReferenceEquals(Rules.Substitute, substituteOne) && substituteOne.Value == 51, "Mutable target-typed substitute is independently mapped");
        TestHelper.AssertTrue(!ReferenceEquals(Rules.Substitute.Values, substituteOne.Values) && substituteOne.Values![0] == 61, "Substitute nested container is independent");
        TestHelper.AssertEqual(1, source.SelectorCalls, "Substitute selector once");
        TestHelper.AssertEqual(0, source.ConverterCalls, "Substitute bypasses converter");
        source.Reset();
        var substituteTwo = mapper.Map<SubstituteTarget>(source)!.Value!;
        substituteOne.Value = -1; substituteOne.Values![0] = -2;
        TestHelper.AssertTrue(!ReferenceEquals(substituteOne, substituteTwo) && substituteTwo.Value == 51 && substituteTwo.Values![0] == 61 && Rules.Substitute.Value == 51 && Rules.Substitute.Values![0] == 61, "Mutable substitute isolates configuration and separate calls");
        TestHelper.AssertEqual(1, source.SelectorCalls, "Second substitute selector once");
        TestHelper.AssertEqual(0, source.ConverterCalls, "Second substitute bypasses source-typed converter");
        source.Reset();
        TestHelper.AssertEqual(51, mapper.Map<ScalarSubstituteTarget>(source)!.Value, "Scalar target-typed substitute is not converted twice");
        TestHelper.AssertEqual(1, source.SelectorCalls, "Scalar substitute selector once");
        TestHelper.AssertEqual(0, source.ConverterCalls, "Scalar substitute bypasses final converter");
        source.IsNull = false; source.Reset();
        var list = mapper.Map<ListTarget>(source)!;
        TestHelper.AssertTrue(ReferenceEquals(source.Items, list.Value) && ReferenceEquals(source.Items[0], list.Value![0]), "Explicit list final converter owns container/items identity");
        TestHelper.AssertEqual(1, source.SelectorCalls, "List selector once");
        TestHelper.AssertEqual(1, source.ConverterCalls, "List converter once");
        source.Reset();
        var bytes = mapper.Map<BytesTarget>(source)!;
        TestHelper.AssertTrue(ReferenceEquals(source.Bytes, bytes.Value), "Explicit byte[] final converter owns identity");
        TestHelper.AssertEqual(1, source.SelectorCalls, "Bytes selector once");
        TestHelper.AssertEqual(1, source.ConverterCalls, "Bytes converter once");
        Console.WriteLine($"Member final-conversion matrix: {TestHelper.Assertions} assertions passed.");
    }
    public sealed class Selected(Source owner) { public Source Owner { get; } = owner; public int Value => 7; }
    public sealed class Result { public int Value { get; set; } }
    public sealed class Source
    {
        public int SelectorCalls, ConverterCalls, ConditionCalls;
        public bool IsNull;
        public MappingCallContext? LastContext;
        public Result Cached { get; } = new() { Value = 107 };
        public List<Result> Items { get; } = new() { new() { Value = 107 } };
        public byte[] Bytes { get; } = new byte[] { 7 };
        public Selected? Value { get { SelectorCalls++; return IsNull ? null : new(this); } }
        public Result SelectCached() { SelectorCalls++; return Cached; }
        public List<Result> SelectList() { SelectorCalls++; return Items; }
        public byte[] SelectBytes() { SelectorCalls++; return Bytes; }
        public void Reset() { SelectorCalls = ConverterCalls = ConditionCalls = 0; Active = this; LastContext = null; }
        public static Source? Active;
    }
    public static Result Finish(Selected value) { value.Owner.ConverterCalls++; if (value.Value != 7) throw new Exception("Wrong selected input"); return value.Owner.Cached; }
    public static SubstituteValue FinishSubstitute(Selected value) { value.Owner.ConverterCalls++; return new() { Value = 999 }; }
    public static int FinishScalar(Selected value) { value.Owner.ConverterCalls++; return 999; }
    public static Result Fail(Selected value) { value.Owner.ConverterCalls++; throw new FormatException("final-converter"); }
    public static bool RejectCandidate(Result value) { var owner = Source.Active!; owner.ConditionCalls++; if (!ReferenceEquals(owner.Cached, value)) throw new Exception("Condition did not receive final value"); return false; }
    public static Result FinishContext(Selected value, MappingCallContext context) { ArgumentNullException.ThrowIfNull(context); value.Owner.LastContext = context; return Finish(value); }
    public static List<Result> FinishList(List<Result> value) { Source.Active!.ConverterCalls++; return value; }
    public static byte[] FinishBytes(byte[] value) { Source.Active!.ConverterCalls++; return value; }
    public sealed class SelectorTarget { public Result? Value { get; set; } }
    public sealed class FinalTarget { public Result? Value { get; set; } }
    public sealed class ContextTarget { public Result? Value { get; set; } }
    public sealed class ConventionTarget { public Result? Value { get; set; } }
    public sealed class GuardTarget { public Result? Value { get; set; } }
    public sealed class IgnoredTarget { public Result? Value { get; set; } }
    public sealed class ConditionTarget { public Result? Value { get; set; } }
    public sealed class ErrorTarget { public Result? Value { get; set; } }
    public sealed class SubstituteValue { public int Value { get; set; } public List<int>? Values { get; set; } }
    public sealed class SubstituteTarget { public SubstituteValue? Value { get; set; } }
    public sealed class ScalarSubstituteTarget { public int Value { get; set; } }
    public sealed class NestedTarget { public FinalTarget? Child { get; set; } }
    public sealed class ListTarget { public List<Result>? Value { get; set; } }
    public sealed class BytesTarget { public byte[]? Value { get; set; } }
    public readonly struct ImmutableTarget(Result value) { public Result Value { get; } = value; }
    public sealed class Rules : MapperProfile
    {
        public static SubstituteValue Substitute { get; } = new() { Value = 51, Values = new() { 61 } };
        public Rules()
        {
            CreateMap<Source, SelectorTarget>().ForMember(d => d.Value, s => s.SelectCached());
            CreateMap<Source, FinalTarget>().ForMember(d => d.Value, o => o.MapFrom(s => s.Value).ConvertUsing<Selected>(v => Finish(v)));
            CreateMap<Source, ContextTarget>().ForMember(d => d.Value, o => o.MapFrom(s => s.Value).ConvertUsing<Selected>((v, c) => FinishContext(v, c)));
            CreateMap<Source, ConventionTarget>().ForMember(d => d.Value, o => o.ConvertUsing<Selected>(v => Finish(v)));
            CreateMap<Source, ImmutableTarget>().ForMember(d => d.Value, o => o.MapFrom(s => s.Value).ConvertUsing<Selected>(v => Finish(v)));
            CreateMap<Source, NestedTarget>().ForMember(d => d.Child!.Value, o => o.MapFrom(s => s.Value).ConvertUsing<Selected>(v => Finish(v)));
            CreateMap<Source, GuardTarget>().ForMember(d => d.Value, o => o.MapFrom(s => s.Value).ConvertUsing<Selected>(v => Finish(v)).PreCondition(s => false));
            CreateMap<Source, IgnoredTarget>().ForMember(d => d.Value, o => o.MapFrom(s => s.Value).ConvertUsing<Selected>(v => Finish(v)).Ignore());
            CreateMap<Source, ConditionTarget>().ForMember(d => d.Value, o => o.MapFrom(s => s.Value).ConvertUsing<Selected>(v => Finish(v)).Condition((s, d, v) => RejectCandidate(v!)));
            CreateMap<Source, ErrorTarget>().ForMember(d => d.Value, o => o.MapFrom(s => s.Value).ConvertUsing<Selected>(v => Fail(v)));
            CreateMap<Source, SubstituteTarget>().ForMember(d => d.Value, o => o.MapFrom(s => s.Value).ConvertUsing<Selected>(v => FinishSubstitute(v)).NullSubstitute(Substitute));
            CreateMap<Source, ScalarSubstituteTarget>().ForMember(d => d.Value, o => o.MapFrom(s => s.Value).ConvertUsing<Selected>(v => FinishScalar(v)).NullSubstitute(51));
            CreateMap<Source, ListTarget>().ForMember(d => d.Value, o => o.MapFrom(s => s.SelectList()).ConvertUsing<List<Result>>(v => FinishList(v)));
            CreateMap<Source, BytesTarget>().ForMember(d => d.Value, o => o.MapFrom(s => s.SelectBytes()).ConvertUsing<byte[]>(v => FinishBytes(v)));
        }
    }
}
