using Ling.Mapper;
using Ling.Mapper.Configuration;
using Ling.Mapper.Mapper;
using Ling.Mapper.Models;

namespace TestConsole.Test;

public static class MemberNullShortcutRegressionTests
{
    public static void Run()
    {
        var assertions = 0;
        void Check(bool value, string message) { assertions++; if (!value) throw new Exception(message); }
        MappingException Failure(Action action, string path, Type cause)
        {
            try { action(); } catch (MappingException e) { Check(e.Path == path, "error path: " + e.Path); Check(e.OriginalExceptionType == cause.FullName, "error cause: " + e.InnerException); return e; }
            throw new Exception("Expected mapping failure");
        }
        foreach (var policy in new[] { NullValueHandling.UseDefault, NullValueHandling.Throw })
        {
            var config = new MapperConfiguration { NullValueHandling = policy };
            config.AddProfile(new Rules());
            var mapper = config.CreateMapper();
            var source = new Source(); var callbacks = 0;
            var result = mapper.Map<Target>(source, AdaptOptions.Strict, (d, s) => { callbacks++; Check(ReferenceEquals(s, source), "callback source"); })!;
            Check(result.Child == null && result.Number == null && result.Any == null && result.Items == null, "null members");
            Check(source.Reads == 1 && callbacks == 1, "single source read and root callback");
            Check(Rules.FinalCalls == 0, "final converter not called for null");
            var ctor = mapper.Map<CtorTarget>(source)!;
            Check(ctor.Child == null && ctor.Number == null, "constructor null members");
            Check(source.Reads == 2, "constructor reads once");
            var ignored = mapper.Map<Target>(source, AdaptOptions.Strict | AdaptOptions.IgnoreNullValues)!;
            Check(ignored.Child?.Value == 99 && ignored.Number == 99 && ignored.Any != null && ignored.Items!.Length == 1, "ignore null preserves initial state");
            var concrete = new Child { Value = 12 };
            source.ChildValue = concrete; source.Number = 123; source.Any = concrete; source.Items = new[] { concrete, null };
            result = mapper.Map<Target>(source)!;
            Check(result.Child!.Value == 12 && !ReferenceEquals(result.Child, concrete), "non-null cloned");
            Check(result.Number == 123 && ReferenceEquals(result.Child, result.Any) && ReferenceEquals(result.Child, result.Items![0]) && result.Items[1] == null && result.Items.Length == 2, "numeric/aliases/count/null");
            Check(!ReferenceEquals(result.Items, source.Items), "array cloned");
            source.ChildValue = null;
            Failure(() => mapper.Map<ThrowSetter>(source), "$.Child", typeof(NullReferenceException));
            Failure(() => mapper.Map<ThrowCtor>(source), "$", typeof(ArgumentNullException));
            if (policy == NullValueHandling.Throw)
                Failure(() => mapper.Map<NonNullable>(new Source()), "$.Number", typeof(InvalidCastException));
            else Check(mapper.Map<NonNullable>(new Source())!.Number == 0, "non-nullable default policy");
            var substitute = mapper.Map<SubstituteTarget>(new Source())!;
            Check(substitute.Child!.Value == 42 && !ReferenceEquals(substitute.Child, Rules.Substitute), "mutable substitute clone");
            Check(!ReferenceEquals(substitute.Child, mapper.Map<SubstituteTarget>(new Source())!.Child), "substitute not cached across calls");
            var conditionalSource = new Source();
            Check(mapper.Map<PreTarget>(conditionalSource)!.Child!.Value == 99 && conditionalSource.Reads == 0, "precondition skips resolution");
            Check(mapper.Map<ConditionTarget>(conditionalSource)!.Child!.Value == 99 && conditionalSource.Reads == 1, "condition sees null candidate");
            var opSource = new OperatorSource();
            Check(mapper.Map<OperatorTarget>(opSource)!.Child is null, "null operator member");
            opSource.Child = new OperatorChild { Value = 37 };
            var op = mapper.Map<OperatorTarget>(opSource)!;
            Check(op.Child!.Value == 37 && !ReferenceEquals(op.Child, opSource.Child), "operator member clone");
            Check(OperatorChild.OperatorCalls == 0, "no user equality operator");
        }
        Console.WriteLine($"MemberNullShortcutRegressionTests: {assertions} assertions passed.");
    }
    public sealed class Child { public int Value { get; set; } }
    public sealed class Source
    {
        public Child? ChildValue; public int Reads;
        public Child? Child { get { Reads++; return ChildValue; } }
        public int? Number { get; set; }
        public object? Any { get; set; }
        public Child?[]? Items { get; set; }
    }
    public sealed class Target
    {
        public Child? Child { get; set; } = new() { Value = 99 };
        public long? Number { get; set; } = 99;
        public object? Any { get; set; } = new();
        public Child?[]? Items { get; set; } = new Child?[1];
        public Child? Final { get; set; }
    }
    public sealed class CtorTarget(Child? child, long? number) { public Child? Child { get; } = child; public long? Number { get; } = number; }
    public sealed class ThrowSetter { public Child? Child { get => null; set => throw new NullReferenceException("setter"); } }
    public sealed class ThrowCtor { public Child Child { get; } public ThrowCtor(Child? child) { throw new ArgumentNullException(nameof(child)); } }
    public sealed class NonNullable { public long Number { get; set; } }
    public sealed class SubstituteTarget { public Child? Child { get; set; } }
    public sealed class PreTarget { public Child? Child { get; set; } = new() { Value = 99 }; }
    public sealed class ConditionTarget { public Child? Child { get; set; } = new() { Value = 99 }; }
    public sealed class OperatorSource { public OperatorChild? Child { get; set; } }
    public sealed class OperatorTarget { public OperatorChild? Child { get; set; } }
    public sealed class OperatorChild
    {
        public static int OperatorCalls;
        public int Value { get; set; }
        public static bool operator ==(OperatorChild? x, OperatorChild? y) { OperatorCalls++; throw new Exception("operator == must not run"); }
        public static bool operator !=(OperatorChild? x, OperatorChild? y) { OperatorCalls++; throw new Exception("operator != must not run"); }
        public override bool Equals(object? other) => ReferenceEquals(this, other);
        public override int GetHashCode() => Value;
    }
    private sealed class Rules : MapperProfile
    {
        public static int FinalCalls;
        public static readonly Child Substitute = new() { Value = 42 };
        private static Child? Final(Child? value) { FinalCalls++; return value; }
        public Rules()
        {
            CreateMap<Source, Target>().ForMember(d => d.Final, o => o.MapFrom(s => (Child?)null).ConvertUsing<Child?>(v => Final(v)));
            CreateMap<Source, SubstituteTarget>().ForMember(d => d.Child, o => o.NullSubstitute(Substitute));
            CreateMap<Source, PreTarget>().ForMember(d => d.Child, o => o.MapFrom(s => s.Child).PreCondition(s => false));
            CreateMap<Source, ConditionTarget>().ForMember(d => d.Child, o => o.MapFrom(s => s.Child).Condition((s, d, v) => v != null));
        }
    }
}
