using System.Collections.Concurrent;
using System.Globalization;
using System.Runtime.Serialization;
using Ling.Mapper;
using Ling.Mapper.Configuration;
using Ling.Mapper.Models;

namespace TestConsole.Test;

/// <summary>User-authored conversion intent must not silently replace explicit member rules or existing defaults.</summary>
public static class DeclaredConversionRegressionTests
{
    public static void Run()
    {
        int assertions = 0;
        void Equal(object? expected, object? actual)
        {
            assertions++;
            if (!Equals(expected, actual)) throw new Exception($"Expected {expected ?? "null"}; got {actual ?? "null"}.");
        }
        void Failure(Action action, Type exception, string path)
        {
            assertions++;
            try { action(); }
            catch (MappingException error)
            {
                if (error.OriginalExceptionType == exception.FullName && error.Path == path) return;
                throw;
            }
            throw new Exception($"Expected {exception.Name} at {path}.");
        }
        var mapper = new MapperConfiguration().CreateMapper();
        var meter = new Meter(7);
        Equal(7, mapper.Map<int>(meter));
        Equal(8, mapper.Map<Meter>(8).Value);
        Equal(7, mapper.Map<Box<int>>(new Box<Meter> { Value = meter })!.Value);
        Equal(9, mapper.Map<Box<Meter>>(new Box<int> { Value = 9 })!.Value.Value);
        Equal(7, mapper.Map<ImmutableInt>(new Box<Meter> { Value = meter }).Value);
        Equal(7, mapper.Map<Box<Box<int>>>(new Box<Box<Meter>> { Value = new() { Value = meter } })!.Value!.Value);
        Equal(7, mapper.Map<int[]>(new[] { meter })![0]);
        Equal(7, mapper.Map<List<int>>(new List<Meter> { meter })![0]);
        Equal(8, mapper.Map<List<Meter>>(new[] { 8 })![0].Value);
        Equal(7, mapper.Map<Dictionary<string, int>>(new Dictionary<string, Meter> { ["v"] = meter })!["v"]);
        Equal(8, mapper.Map<Dictionary<int, string>>(new Dictionary<Meter, string> { [new(8)] = "v" })!.Keys.Single());
        Equal(8, mapper.Map<TargetDeclared>(new OperatorInput { Value = 8 })!.Value);
        Equal(17, mapper.Map<OperatorOutput>(new SourceDeclared { Value = 7 })!.Value);
        // Ordinary same-type mapping copies; explicit final registry rules own returned identities.
        var identity = new OperatorOutput { Value = 7 };
        var ordinaryCopy = mapper.Map<OperatorOutput>(identity)!;
        Equal(false, ReferenceEquals(identity, ordinaryCopy)); Equal(7, ordinaryCopy.Value);
        ordinaryCopy.Value = 8; Equal(7, identity.Value);
        int identityCalls = 0;
        TypeConverterRegistry.Register(typeof(OperatorOutput), typeof(OperatorOutput), new Func<OperatorOutput, OperatorOutput>(value => { identityCalls++; return value; }));
        Equal(true, ReferenceEquals(identity, mapper.Map<OperatorOutput>(identity)));
        Equal(true, ReferenceEquals(identity, mapper.Map<Box<OperatorOutput>>(new Box<OperatorOutput> { Value = identity })!.Value));
        Equal(2, identityCalls);

        Equal(7, mapper.Map(meter, typeof(Meter?), typeof(int?)));
        Equal(7, mapper.Map<Box<int?>>(new Box<Meter?> { Value = meter })!.Value);
        Equal(8, mapper.Map<Box<Meter?>>(new Box<int?> { Value = 8 })!.Value!.Value.Value);
        Equal(null, mapper.Map<int?>(null));
        Equal(null, mapper.Map<Meter?>(null));
        Equal(null, mapper.Map<Box<int?>>(new Box<Meter?>())!.Value);
        Equal(null, mapper.Map<Box<Meter?>>(new Box<int?>())!.Value);
        Equal(0, mapper.Map<Box<int>>(new Box<Meter?>())!.Value);
        Equal(0, mapper.Map<int[]>(new Meter?[] { null })![0]);
        Equal(null, mapper.Map<int?[]>(new Meter?[] { null })![0]);
        Equal(null, mapper.Map<int[]>(null));
        Equal(null, mapper.Map<NullOutput>(new NullInput()));
        Equal(null, mapper.Map<Box<NullOutput>>(new Box<NullInput> { Value = new() })!.Value);
        var strict = new MapperConfiguration { NullValueHandling = NullValueHandling.Throw }.CreateMapper();
        Failure(() => strict.Map<Box<int>>(new Box<Meter?>()), typeof(InvalidCastException), "$.Value");
        // Null is handled by mapper policy, not by invoking a potentially null-aware operator.
        Equal(0, mapper.Map<int>((NullAwareInput?)null));
        Equal(null, mapper.Map<Box<int?>>(new Box<NullAwareInput>())!.Value);

        // Preserve explicitly declared nullable operator signatures before ordinary lifting.
        Equal(9, mapper.Map<int?>(new NullableResult()));
        Equal(null, mapper.Map<int?>(new NullableResult { IsNull = true }));
        Equal(9, mapper.Map<Box<int?>>(new Box<NullableResult> { Value = new() })!.Value);
        Equal(9, mapper.Map<int?[]>(new[] { new NullableResult() })![0]);
        Equal(9, mapper.Map<Dictionary<string, int?>>(new Dictionary<string, NullableResult> { ["v"] = new() })!["v"]);
        Failure(() => mapper.Map<int>(new NullableResult()), typeof(InvalidCastException), "$"); // No nullable-result unwrapping.
        Equal(13, mapper.Map<DualInput>(3)!.Value);
        Equal(103, ((DualInput)mapper.Map(3, typeof(int?), typeof(DualInput))!).Value);
        Equal(103, mapper.Map<Box<DualInput>>(new Box<int?> { Value = 3 })!.Value!.Value);
        Equal(103, mapper.Map<DualInput[]>(new int?[] { 3 })![0].Value);
        Equal(103, mapper.Map<Dictionary<string, DualInput>>(new Dictionary<string, int?> { ["v"] = 3 })!["v"].Value);
        Equal(null, mapper.Map<DualInput>(null));
        Equal(null, mapper.Map<DualInput[]>(new int?[] { null })![0]);
        Equal(13, mapper.Map<int>(new DualOutput { Value = 3 }));
        Equal(103, mapper.Map<int?>(new DualOutput { Value = 3 }));
        Equal(103, mapper.Map<Box<int?>>(new Box<DualOutput> { Value = new() { Value = 3 } })!.Value);
        Equal(3, mapper.Map<OnlyNullableInput>(3)!.Value); // Nullable parameter promotion from non-null value.
        Equal(3, ((OnlyNullableInput)mapper.Map(3, typeof(int?), typeof(OnlyNullableInput))!).Value);
        Equal(3, mapper.Map<Box<OnlyNullableInput>>(new Box<int> { Value = 3 })!.Value!.Value);
        Equal(3, mapper.Map<OnlyNullableInput[]>(new[] { 3 })![0].Value);
        Equal(3, mapper.Map<NullableBoth?>(3)!.Value.Value);
        Equal(3, ((NullableBoth?)mapper.Map(3, typeof(int?), typeof(NullableBoth?)))!.Value.Value);
        Equal(3, mapper.Map<Box<NullableBoth?>>(new Box<int?> { Value = 3 })!.Value!.Value.Value);
        Equal(null, mapper.Map<NullableBoth?>(null));
        Equal("ordinary", mapper.Map<string>(new NullableSource()));
        Equal("declared-nullable", mapper.Map(new NullableSource(), typeof(NullableSource?), typeof(string)));
        Equal("declared-nullable", mapper.Map<Box<string>>(new Box<NullableSource?> { Value = new NullableSource() })!.Value);
        Equal("declared-nullable", mapper.Map<string[]>(new NullableSource?[] { new NullableSource() })![0]);
        Equal(null, mapper.Map<string[]>(new NullableSource?[] { null })![0]);
        Failure(() => mapper.Map<Box<OnlyNullableInput>>(new Box<int?> { Value = -1 }), typeof(FormatException), "$.Value");
        Failure(() => mapper.Map<OnlyNullableInput[]>(new int?[] { -1 }), typeof(FormatException), "$[0]");
        Failure(() => mapper.Map<NullableAmbiguity?>(3), typeof(NotSupportedException), "$");

        Failure(() => mapper.Map<long>(meter), typeof(InvalidCastException), "$"); // No operator-to-int then numeric chaining.
        Failure(() => mapper.Map<string>(new PlainObject()), typeof(InvalidCastException), "$"); // No arbitrary ToString.
        Failure(() => mapper.Map<string>(new byte[] { 65 }), typeof(InvalidCastException), "$"); // No assumed UTF-8.
        Failure(() => mapper.Map<AmbiguousOutput>(new AmbiguousInput()), typeof(NotSupportedException), "$");
        Failure(() => mapper.Map<int>(new ErrorInput()), typeof(FormatException), "$");
        Failure(() => mapper.Map<Box<int>>(new Box<ErrorInput> { Value = new() }), typeof(FormatException), "$.Value");
        Failure(() => mapper.Map<Box<Box<int>>>(new Box<Box<ErrorInput>> { Value = new() { Value = new() } }), typeof(FormatException), "$.Value.Value");
        Failure(() => mapper.Map<int[]>(new[] { new ErrorInput() }), typeof(FormatException), "$[0]");
        Failure(() => mapper.Map<Dictionary<string, int>>(new Dictionary<string, ErrorInput> { ["private-key"] = new() }), typeof(FormatException), "$[0].Value");
        Failure(() => mapper.Map<int>(new NullErrorInput()), typeof(ArgumentNullException), "$");
        Failure(() => mapper.Map<Box<int>>(new Box<NullErrorInput> { Value = new() }), typeof(ArgumentNullException), "$.Value");
        Failure(() => mapper.Map<int>(new OverflowInput()), typeof(OverflowException), "$");
        Failure(() => mapper.Map<OperatorOutput>(new SourceDeclared(), AdaptOptions.Default, ignoreNames: ["Value"]), typeof(NotSupportedException), "$");
        Failure(() => mapper.Map<Box<OperatorOutput>>(new Box<SourceDeclared> { Value = new() }, AdaptOptions.Default, ignoreNames: ["Value.Value"]), typeof(NotSupportedException), "$.Value");
        int callbacks = 0;
        Failure(() => mapper.Map<Box<int>>(new Box<ErrorInput> { Value = new() }, AdaptOptions.Default, (_, _) => callbacks++), typeof(FormatException), "$.Value");
        Equal(0, callbacks); // afterMap cannot rescue an automatic conversion that failed earlier.
        Equal(7, mapper.Map<int>(meter, AdaptOptions.Default, (target, source) => { callbacks++; Equal(7, target); Equal(meter, source); }));
        Equal(1, callbacks);

        // Existing Convert semantics stay ahead of decimal's explicit operators.
        Equal(2, mapper.Map<int>(1.5m)); Equal(2, mapper.Map<int>(2.5m));
        Equal(2, mapper.Map<Box<int>>(new Box<decimal> { Value = 2.5m })!.Value);
        Failure(() => mapper.Map<int>(decimal.MaxValue), typeof(OverflowException), "$");
        Equal(1.5m.ToString(CultureInfo.CurrentCulture), mapper.Map<string>(1.5m));

        // Cached plans must give way to explicit registration and replacement.
        var priority = new PriorityInput { Value = 3 };
        Equal(13, mapper.Map<PriorityOutput>(priority)!.Value);
        Equal(13, mapper.Map<Box<PriorityOutput>>(new Box<PriorityInput> { Value = priority })!.Value!.Value);
        TypeConverterRegistry.Register(typeof(PriorityInput), typeof(PriorityOutput), new Func<PriorityInput, PriorityOutput>(source => new() { Value = source.Value + 100 }));
        Equal(103, mapper.Map<PriorityOutput>(priority)!.Value);
        Equal(103, mapper.Map<Box<PriorityOutput>>(new Box<PriorityInput> { Value = priority })!.Value!.Value);
        Equal(103, mapper.Map<PriorityOutput[]>(new[] { priority })![0].Value);
        TypeConverterRegistry.Register(typeof(PriorityInput), typeof(PriorityOutput), new Func<PriorityInput, PriorityOutput>(source => new() { Value = source.Value + 200 }));
        Equal(203, mapper.Map<PriorityOutput>(priority)!.Value);
        Equal(203, mapper.Map<Box<PriorityOutput>>(new Box<PriorityInput> { Value = priority })!.Value!.Value);
        int resolvedCalls = 0;
        TypeConverterRegistry.Register(typeof(PriorityOutput), typeof(PriorityOutput), new Func<PriorityOutput, PriorityOutput>(value => { resolvedCalls++; return new() { Value = value.Value }; }));
        Equal(203, mapper.Map<PriorityOutput>(priority)!.Value); Equal(0, resolvedCalls); // Explicit type converter is final.
        var configuration = new MapperConfiguration(); configuration.AddProfile(new MemberProfile());
        var configured = configuration.CreateMapper();
        Equal(507, configured.Map<OperatorOutput>(new SourceDeclared { Value = 7 })!.Value);
        Equal(507, configured.Map<Box<OperatorOutput>>(new Box<SourceDeclared> { Value = new() { Value = 7 } })!.Value!.Value);
        Equal(507, configured.Map<OperatorOutput[]>(new[] { new SourceDeclared { Value = 7 } })![0].Value);
        Equal(0, configured.Map<OperatorOutput>(new SourceDeclared { Value = 7 }, AdaptOptions.Default, ignoreNames: ["Value"])!.Value);
        var member = new MemberSource { Value = priority };
        Equal(1003, configured.Map<MemberTarget>(member)!.Value!.Value);
        Equal(1, member.Calls); Equal(1, resolvedCalls);
        Equal(1003, configured.Map<NestedTarget>(member)!.Child!.Value!.Value);
        Equal(2, member.Calls); Equal(2, resolvedCalls);
        Equal(1003, configured.Map<ImmutableTarget>(member).Value.Value);
        Equal(3, member.Calls); Equal(3, resolvedCalls);
        Equal(1003, configured.Map<MemberTarget[]>(new[] { member })![0].Value!.Value);
        Equal(4, member.Calls); Equal(4, resolvedCalls);
        Equal(203, configured.Map<RenamedTarget>(member)!.Other!.Value);
        Equal(null, configured.Map<IgnoredTarget>(member)!.Value);
        Equal(4, member.Calls); Equal(4, resolvedCalls);
        Equal(null, configured.Map<MemberTarget>(member, AdaptOptions.Default, ignoreNames: ["Value"])!.Value);
        Equal(4, member.Calls); Equal(4, resolvedCalls);
        Equal(42, configured.Map<Box<int>>(new Box<ErrorInput> { Value = new() })!.Value);
        Equal(null, configured.Map<MemberTarget>(new MemberSource())!.Value);
        Failure(() => configured.Map<AutomaticNullTarget>(new Box<NullErrorInput> { Value = new() }), typeof(ArgumentNullException), "$.Value");

        // Attributes opt individual enum names into aliases; standard names/numbers remain usable.
        Equal(Status.Active, mapper.Map<Status>("in-progress"));
        Equal(Status.Active, mapper.Map<Status>("IN-PROGRESS"));
        Equal(Status.Active, mapper.Map<Status>(" Active "));
        Equal(Status.Active, mapper.Map<Status>("1"));
        Equal("in-progress", mapper.Map<string>(Status.Active));
        Equal("Finished", mapper.Map<string>(Status.Finished));
        Equal("99", mapper.Map<string>((Status)99));
        Equal((Status)99, mapper.Map<Status>("99"));
        Equal(Status.Active, mapper.Map<Box<Status>>(new Box<string> { Value = "in-progress" })!.Value);
        Equal("in-progress", mapper.Map<Box<string>>(new Box<Status> { Value = Status.Active })!.Value);
        Equal(Status.Active, mapper.Map<ImmutableStatus>(new Box<string> { Value = "in-progress" }).Value);
        Equal(Status.Active, mapper.Map<Box<Box<Status>>>(new Box<Box<string>> { Value = new() { Value = "in-progress" } })!.Value!.Value);
        Equal(Status.Active, mapper.Map<Status[]>(new[] { "in-progress" })![0]);
        Equal("in-progress", mapper.Map<string[]>(new[] { Status.Active })![0]);
        Equal(Status.Active, mapper.Map<Dictionary<string, Status>>(new Dictionary<string, string> { ["v"] = "in-progress" })!["v"]);
        Equal("v", mapper.Map<Dictionary<Status, string>>(new Dictionary<string, string> { ["in-progress"] = "v" })![Status.Active]);
        Equal(Status.Active, mapper.Map<Status?>("in-progress"));
        Equal(Status.Active, mapper.Map<Box<Status?>>(new Box<string> { Value = "in-progress" })!.Value);
        Equal(null, mapper.Map<Status?>(null));
        Equal((Status)0, mapper.Map<Status>(null));
        Equal(null, mapper.Map<Box<string>>(new Box<Status?>())!.Value);
        Equal("in-progress", mapper.Map<Box<string>>(new Box<Status?> { Value = Status.Active })!.Value);
        Equal(OtherStatus.NumericallyOne, mapper.Map<OtherStatus>(Status.Active));
        Equal(Status.Finished, mapper.Map<Status>(OtherStatus.Active)); // Different names still map by underlying value.
        Failure(() => mapper.Map<ByteStatus>(300), typeof(OverflowException), "$");
        Failure(() => mapper.Map<ByteStatus>("300"), typeof(OverflowException), "$");
        Failure(() => mapper.Map<ByteStatus>(OtherStatus.Big), typeof(OverflowException), "$");
        Failure(() => mapper.Map<Status>(""), typeof(ArgumentException), "$");
        Failure(() => mapper.Map<Status>("not-a-status"), typeof(ArgumentException), "$");
        Failure(() => mapper.Map<Box<Status>>(new Box<string> { Value = "bad" }), typeof(ArgumentException), "$.Value");
        Failure(() => mapper.Map<Status[]>(new[] { "in-progress", "bad" }), typeof(ArgumentException), "$[1]");
        Failure(() => mapper.Map<Dictionary<string, Status>>(new Dictionary<string, string> { ["v"] = "bad" }), typeof(ArgumentException), "$[0].Value");
        Failure(() => mapper.Map<Dictionary<Status, int>>(new Dictionary<string, int> { ["Active"] = 1, ["in-progress"] = 2 }), typeof(ArgumentException), "$[1].Key");
        Equal(Access.Read | Access.Write, mapper.Map<Access>("read-file, write-file"));
        Equal(Access.Read | Access.Write, mapper.Map<Access>("Read, WRITE-FILE"));
        Equal("read-file, write-file", mapper.Map<string>(Access.Read | Access.Write));
        Equal(Access.High | Access.Read, mapper.Map<Access>("read-file, high-bit"));
        Equal("read-file, high-bit", mapper.Map<string>(Access.Read | Access.High));
        Equal("0", mapper.Map<string>((Access)0));
        Equal("16", mapper.Map<string>((Access)16));
        Equal(Access.Named, mapper.Map<Access>("named-composite"));
        Equal("named-composite", mapper.Map<string>(Access.Named));
        Equal(SignedAccess.All, mapper.Map<SignedAccess>("all-bits"));
        Equal("all-bits", mapper.Map<string>(SignedAccess.All));
        Equal(SameAlias.First, mapper.Map<SameAlias>("shared"));
        Equal("shared", mapper.Map<string>(SameAlias.First));
        Equal(CommaAlias.Value, mapper.Map<CommaAlias>("one,two"));
        Equal("one,two", mapper.Map<string>(CommaAlias.Value));
        foreach (var invalid in new[] { typeof(NameCollision), typeof(CaseNameCollision), typeof(NumericCollision), typeof(NumericOverflowAlias), typeof(AliasCollision), typeof(EmptyAlias), typeof(WhitespaceAlias), typeof(FlagsCommaAlias) })
        {
            Failure(() => mapper.Map("A", typeof(string), invalid), typeof(NotSupportedException), "$");
            Failure(() => mapper.Map<string>(Enum.ToObject(invalid, 1)), typeof(NotSupportedException), "$");
        }
        Equal(CaseNames.A, mapper.Map<CaseNames>("A")); // Unannotated enum remains standard CLR semantics.
        Equal(NullAttribute.A, mapper.Map<NullAttribute>("A"));
        Equal("A", mapper.Map<string>(NullAttribute.A));
        // Registry replacement also invalidates scalar-member plans, not just root enum plans.
        Equal(RuleStatus.A, mapper.Map<Box<RuleStatus>>(new Box<string> { Value = "rule-a" })!.Value);
        TypeConverterRegistry.Register(typeof(string), typeof(RuleStatus), new Func<string, RuleStatus>(_ => RuleStatus.B));
        Equal(RuleStatus.B, mapper.Map<RuleStatus>("rule-a"));
        Equal(RuleStatus.B, mapper.Map<Box<RuleStatus>>(new Box<string> { Value = "rule-a" })!.Value);
        TypeConverterRegistry.Register(typeof(RuleStatus), typeof(string), new Func<RuleStatus, string>(_ => "registered"));
        Equal("registered", mapper.Map<string>(RuleStatus.A));
        Equal("registered", mapper.Map<Box<string>>(new Box<RuleStatus> { Value = RuleStatus.A })!.Value);
        TypeConverterRegistry.Register(typeof(string), typeof(RuleStatus), new Func<string, RuleStatus>(_ => RuleStatus.A));
        Equal(RuleStatus.A, mapper.Map<Box<RuleStatus>>(new Box<string> { Value = "any" })!.Value);

        // Fresh mappers/metadata are exercised concurrently; no cross-call object/result caching.
        var errors = new ConcurrentQueue<Exception>();
        Parallel.For(0, 512, index =>
        {
            try
            {
                if (mapper.Map<int>(new Meter(index)) != index ||
                    mapper.Map<ConcurrentEnum>("concurrent-a") != ConcurrentEnum.A ||
                    mapper.Map<string>(ConcurrentEnum.A) != "concurrent-a" ||
                    mapper.Map<Box<int>>(new Box<Meter> { Value = new(index) })!.Value != index ||
                    mapper.Map<PriorityOutput>(priority)!.Value != 203)
                    throw new Exception("Concurrent cached conversion produced an incorrect result.");
            }
            catch (Exception error) { errors.Enqueue(error); }
        });
        Equal(0, errors.Count);
        assertions += 512 * 5;
        Console.WriteLine($"Declared conversion regression: {assertions}/{assertions} assertions passed.");
    }

    public class Box<T> { public T? Value { get; set; } }
    public readonly struct Meter(int value)
    {
        public int Value { get; } = value;
        public static implicit operator int(Meter value) => value.Value;
        public static explicit operator Meter(int value) => new(value);
    }
    public readonly struct ImmutableInt(int value) { public int Value { get; } = value; }
    public readonly struct ImmutableStatus(Status value) { public Status Value { get; } = value; }
    public sealed class OperatorInput { public int Value { get; set; } }
    public sealed class TargetDeclared
    {
        public int Value { get; set; }
        public static implicit operator TargetDeclared(OperatorInput value) => new() { Value = value.Value };
    }
    public sealed class SourceDeclared
    {
        public int Value { get; set; }
        public static explicit operator OperatorOutput(SourceDeclared value) => new() { Value = value.Value + 10 };
    }
    public sealed class OperatorOutput { public int Value { get; set; } }
    public sealed class NullOutput { }
    public sealed class NullInput { public static implicit operator NullOutput?(NullInput value) => null; }
    public sealed class NullAwareInput { public static implicit operator int(NullAwareInput? value) => value == null ? 999 : 1; }
    public sealed class NullableResult { public bool IsNull { get; set; } public static implicit operator int?(NullableResult value) => value.IsNull ? null : 9; }
    public sealed class DualInput
    {
        public int Value { get; set; }
        public static implicit operator DualInput(int value) => new() { Value = value + 10 };
        public static implicit operator DualInput(int? value) => new() { Value = value!.Value + 100 };
    }
    public sealed class DualOutput
    {
        public int Value { get; set; }
        public static implicit operator int(DualOutput value) => value.Value + 10;
        public static implicit operator int?(DualOutput value) => value.Value + 100;
    }
    public sealed class OnlyNullableInput
    {
        public int Value { get; set; }
        public static implicit operator OnlyNullableInput(int? value) => value < 0 ? throw new FormatException("operator") : new() { Value = value!.Value };
    }
    public readonly struct NullableBoth(int value)
    {
        public int Value { get; } = value;
        public static implicit operator NullableBoth?(int? value) => value == null ? null : new NullableBoth(value.Value);
    }
    public readonly struct NullableSource
    {
        public static implicit operator string(NullableSource value) => "ordinary";
        public static implicit operator string(NullableSource? value) => "declared-nullable";
    }
    public readonly struct NullableAmbiguity
    {
        public static implicit operator NullableAmbiguity(int value) => new();
        public static implicit operator NullableAmbiguity(int? value) => new();
    }
    public sealed class ErrorInput { public static implicit operator int(ErrorInput value) => throw new FormatException("private-value"); }
    public sealed class NullErrorInput { public static implicit operator int(NullErrorInput value) => throw new ArgumentNullException("operator"); }
    public sealed class OverflowInput { public static explicit operator int(OverflowInput value) => throw new OverflowException("operator"); }
    public sealed class PlainObject { public override string ToString() => "not-a-conversion"; }
    public sealed class AmbiguousInput { public static implicit operator AmbiguousOutput(AmbiguousInput value) => new(); }
    public sealed class AmbiguousOutput { public static implicit operator AmbiguousOutput(AmbiguousInput value) => new(); }
    public sealed class PriorityInput
    {
        public int Value { get; set; }
        public static implicit operator PriorityOutput(PriorityInput value) => new() { Value = value.Value + 10 };
    }
    public sealed class PriorityOutput { public int Value { get; set; } }
    public sealed class MemberSource
    {
        public PriorityInput? Value { get; set; }
        public int Calls { get; private set; }
        public PriorityOutput? Resolve() { Calls++; return Value == null ? null : new() { Value = Value.Value + 1000 }; }
    }
    public sealed class MemberTarget { public PriorityOutput? Value { get; set; } }
    public sealed class NestedTarget { public MemberTarget? Child { get; set; } }
    public readonly struct ImmutableTarget(PriorityOutput value) { public PriorityOutput Value { get; } = value; }
    public sealed class RenamedTarget { public PriorityOutput? Other { get; set; } }
    public sealed class IgnoredTarget { public PriorityOutput? Value { get; set; } }
    public sealed class AutomaticNullTarget { public int Value { get; set; } }
    public sealed class MemberProfile : MapperProfile
    {
        public MemberProfile()
        {
            CreateMap<SourceDeclared, OperatorOutput>().ForMember(target => target.Value, source => source.Value + 500);
            CreateMap<MemberSource, MemberTarget>().ForMember(target => target.Value, source => source.Resolve());
            CreateMap<MemberSource, NestedTarget>().ForMember(target => target.Child!.Value, source => source.Resolve());
            CreateMap<MemberSource, ImmutableTarget>().ForMember(target => target.Value, source => source.Resolve()!);
            CreateMap<MemberSource, RenamedTarget>().Rename(target => target.Other, nameof(MemberSource.Value));
            CreateMap<MemberSource, IgnoredTarget>().ForMember(target => target.Value, source => source.Resolve()).Ignore(target => target.Value);
            CreateMap<Box<ErrorInput>, Box<int>>().ForMember(target => target.Value, source => 42);
            CreateMap<Box<NullErrorInput>, AutomaticNullTarget>().Rename(target => target.Value, nameof(Box<NullErrorInput>.Value));
        }
    }
    public enum Status { [EnumMember(Value = "in-progress")] Active = 1, Finished = 2 }
    public enum OtherStatus { NumericallyOne = 1, Active = 2, Big = 300 }
    public enum ByteStatus : byte { A = 1 }
    [Flags] public enum Access : ulong
    {
        [EnumMember(Value = "read-file")] Read = 1,
        [EnumMember(Value = "write-file")] Write = 2,
        [EnumMember(Value = "named-composite")] Named = 5,
        [EnumMember(Value = "high-bit")] High = 1UL << 63
    }
    [Flags] public enum SignedAccess : long { [EnumMember(Value = "all-bits")] All = -1 }
    public enum SameAlias { [EnumMember(Value = "shared")] First = 1, [EnumMember(Value = "shared")] Second = 1 }
    public enum CommaAlias { [EnumMember(Value = "one,two")] Value = 1 }
    public enum NameCollision { [EnumMember(Value = "b")] A = 1, B = 2 }
    public enum CaseNameCollision { [EnumMember(Value = "A")] A = 1, a = 2 }
    public enum NumericCollision { [EnumMember(Value = "2")] A = 1, B = 2 }
    public enum NumericOverflowAlias : byte { [EnumMember(Value = "300")] A = 1 }
    public enum AliasCollision { [EnumMember(Value = "same")] A = 1, [EnumMember(Value = "SAME")] B = 2 }
    public enum EmptyAlias { [EnumMember(Value = "")] A = 1 }
    public enum WhitespaceAlias { [EnumMember(Value = " alias ")] A = 1 }
    [Flags] public enum FlagsCommaAlias { [EnumMember(Value = "one,two")] A = 1 }
    public enum CaseNames { A = 1, a = 2 }
    public enum NullAttribute { [EnumMember] A = 1 }
    public enum RuleStatus { [EnumMember(Value = "rule-a")] A = 1, B = 2 }
    public enum ConcurrentEnum { [EnumMember(Value = "concurrent-a")] A = 1 }
}
