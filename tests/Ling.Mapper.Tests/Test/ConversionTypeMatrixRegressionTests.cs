using System.Globalization;
using System.Text.Json;
using System.Runtime.Serialization;
using Ling.Mapper.Configuration;
using Ling.Mapper;
using Ling.Mapper.Models;

namespace TestConsole.Test;

/// <summary>Boundary matrix supplements (rather than repeats) the 42-valued pair matrix,
/// enum/operator suites and asynchronous culture suite. BCL is the independent oracle.
/// Set LING_CONVERSION_MATRIX to save every checked case as machine-readable JSON.</summary>
public static class ConversionTypeMatrixRegressionTests
{
    public static void Run()
    {
        var mapper = new MapperConfiguration().CreateMapper();
        var rows = new List<Row>();
        var original = CultureInfo.CurrentCulture;
        void Check(string sample, object? value, Type source, Type target, Func<object?> oracle, string route)
        {
            object? expected = null; Type? rejected = null;
            try { expected = oracle(); }
            catch (Exception ex) when (ex is FormatException or OverflowException or InvalidCastException or ArgumentException) { rejected = ex.GetType(); }
            string path = route == "member" ? "$.Value" : route == "array" ? "$[0]" : "$";
            var row = new Row(CultureInfo.CurrentCulture.Name, source.ToString(), target.ToString(), sample, route,
                rejected == null ? "SUPPORTED" : "REJECTED", rejected?.FullName, path, "NOT_RUN", null);
            try
            {
                object? actual;
                if (route == "member")
                {
                    var st = typeof(Box<>).MakeGenericType(source); var tt = typeof(Box<>).MakeGenericType(target);
                    var input = Activator.CreateInstance(st)!; st.GetProperty("Value")!.SetValue(input, value);
                    actual = tt.GetProperty("Value")!.GetValue(mapper.Map(input, st, tt));
                }
                else if (route == "array")
                {
                    var input = Array.CreateInstance(source, 1); input.SetValue(value, 0);
                    var mapped = (Array)mapper.Map(input, source.MakeArrayType(), target.MakeArrayType())!;
                    if (ReferenceEquals(input, mapped)) throw new Exception("Ordinary array mapping retained source identity");
                    actual = mapped.GetValue(0);
                }
                else actual = mapper.Map(value, route == "object" ? typeof(object) : source, target);
                if (rejected != null) throw new Exception($"Expected {rejected.Name}, returned {actual}");
                if (!Equals(expected, actual) || (expected is DateTimeOffset offset && actual is DateTimeOffset mappedOffset && !offset.EqualsExact(mappedOffset)) || (expected is DateTime date && actual is DateTime mappedDate && date.Kind != mappedDate.Kind)) throw new Exception($"Expected [{expected}] ({expected?.GetType()}), actual [{actual}] ({actual?.GetType()})");
                rows.Add(row with { Result = "PASS" });
            }
            catch (MappingException ex) when (rejected != null && ex.OriginalExceptionType == rejected.FullName && ex.Path == path)
            { rows.Add(row with { Result = "PASS" }); }
            catch (Exception ex) { rows.Add(row with { Result = "FAIL", Detail = ex.ToString() }); }
        }
        try
        {
            var types = new[] { typeof(sbyte), typeof(byte), typeof(short), typeof(ushort), typeof(int), typeof(uint), typeof(long), typeof(ulong), typeof(float), typeof(double), typeof(decimal), typeof(bool), typeof(char) };
            foreach (var culture in new[] { "en-US", "de-DE", "zh-CN" })
            {
                CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo(culture);
                foreach (var source in types)
                {
                    var values = source == typeof(bool) ? new object[] { false, true } : source == typeof(char) ? new object[] { '\0', 'A', char.MaxValue } :
                        new[] { "MinValue", "MaxValue" }.Select(n => source.GetField(n)!.GetValue(null)!).Concat(
                            source == typeof(float) ? new object[] { -1.5f, 0f, float.NaN, float.PositiveInfinity, float.NegativeInfinity } :
                            source == typeof(double) ? new object[] { -1.5d, 0d, double.NaN, double.PositiveInfinity, double.NegativeInfinity } :
                            source == typeof(decimal) ? new object[] { -1.5m, 0m } : new object[] { Convert.ChangeType(0, source) }).ToArray();
                    foreach (var value in values)
                    foreach (var target in types.Append(typeof(string)))
                    foreach (var route in new[] { "root", "member", "object" })
                        Check(Convert.ToString(value, CultureInfo.InvariantCulture)!, value, source, target, () => Convert.ChangeType(value, target, CultureInfo.CurrentCulture), route);
                    var nullable = typeof(Nullable<>).MakeGenericType(source);
                    foreach (var route in new[] { "root", "member" })
                    {
                        Check("nullable-present", values[0], nullable, source, () => values[0], route);
                        Check("nullable-lift", values[0], source, nullable, () => values[0], route);
                        Check("nullable-null", null, nullable, nullable, () => null, route);
                        Check("nullable-default", null, nullable, source, () => Activator.CreateInstance(source), route);
                        Check("nullable-text-null", null, nullable, typeof(string), () => null, route);
                    }
                    foreach (var text in new[] { "", " ", "-1", "0", " 42 ", "invalid", "18446744073709551616", "NaN", "Infinity", "-1,5", "-1.5" })
                    foreach (var route in new[] { "root", "member" })
                        Check("text:" + text, text, typeof(string), source, () => Convert.ChangeType(text, source, CultureInfo.CurrentCulture), route);
                }
                // Existing enum tests use small values; exercise every underlying integral
                // representation at both extremes, including signed-to-unsigned overflow.
                var enums = new[] { typeof(ByteEnum), typeof(SByteEnum), typeof(ShortEnum), typeof(UShortEnum), typeof(IntEnum), typeof(UIntEnum), typeof(LongEnum), typeof(ULongEnum) };
                foreach (var from in enums)
                foreach (var bound in new[] { "MinValue", "MaxValue" })
                {
                    var underlying = Enum.GetUnderlyingType(from);
                    var value = Enum.ToObject(from, underlying.GetField(bound)!.GetValue(null)!);
                    foreach (var to in enums)
                    foreach (var route in new[] { "root", "member", "object" })
                        Check(bound, value, from, to, () => Enum.ToObject(to, Convert.ChangeType(value, Enum.GetUnderlyingType(to), CultureInfo.CurrentCulture)), route);
                }
                foreach (var route in new[] { "root", "member", "object" })
                {
                    var bits = AliasFlags.Low | AliasFlags.High;
                    Check("flags-alias-high-bit", "low-bit, high-bit", typeof(string), typeof(AliasFlags), () => bits, route);
                    Check("flags-alias-format", bits, typeof(AliasFlags), typeof(string), () => "low-bit, high-bit", route);
                    Check("flags-alias-nullable", bits, typeof(AliasFlags?), typeof(string), () => "low-bit, high-bit", route);
                    Check("flags-null", null, typeof(AliasFlags?), typeof(AliasFlags?), () => null, route);
                    Check("flags-whitespace-rejected", " ", typeof(string), typeof(AliasFlags), () => throw new ArgumentException(), route);
                }
                var extendedAtoms = new List<(Type Type, object Value)> { (typeof(Half), (Half)1.5f), (typeof(nint), (nint)7), (typeof(nuint), (nuint)7) };
#if NET7_0_OR_GREATER
                extendedAtoms.Add((typeof(Int128), Int128.MaxValue));
                extendedAtoms.Add((typeof(UInt128), UInt128.MaxValue));
#endif
                foreach (var (type, value) in extendedAtoms)
                foreach (var route in new[] { "root", "member", "object", "array" })
                {
                    var nullable = typeof(Nullable<>).MakeGenericType(type);
                    Check("extended-atomic-self", value, type, type, () => value, route);
                    Check("extended-atomic-object", value, type, typeof(object), () => value, route);
                    Check("extended-nullable-present", value, nullable, type, () => value, route);
                    Check("extended-nullable-lift", value, type, nullable, () => value, route);
                    Check("extended-nullable-null", null, nullable, nullable, () => null, route);
                    Check("extended-nullable-default", null, nullable, type, () => Activator.CreateInstance(type), route);
                }
                // Extended numerics are not part of the legacy IConvertible matrix.
                // Exact declared Half/128-bit operators are supported; native integers have
                // no automatic numeric policy. Do not infer all-pairs support from these probes.
                foreach (var route in new[] { "root", "member", "object" })
                {
                    Check("half-operator", (Half)1.5f, typeof(Half), typeof(float), () => 1.5f, route);
                    Check("half-operator-reverse", 1.5f, typeof(float), typeof(Half), () => (Half)1.5f, route);
                    Check("half-text-not-supported", "1.5", typeof(string), typeof(Half), () => throw new InvalidCastException(), route);
                    Check("native-signed-not-supported", (nint)7, typeof(nint), typeof(long), () => throw new InvalidCastException(), route);
                    Check("native-unsigned-not-supported", (nuint)7, typeof(nuint), typeof(ulong), () => throw new InvalidCastException(), route);
#if NET7_0_OR_GREATER
                    Check("int128-operator", (Int128)7, typeof(Int128), typeof(long), () => 7L, route);
                    Check("uint128-operator", (UInt128)7, typeof(UInt128), typeof(ulong), () => 7UL, route);
                    Check("int128-operator-reverse", 7L, typeof(long), typeof(Int128), () => (Int128)7, route);
                    Check("uint128-operator-reverse", 7UL, typeof(ulong), typeof(UInt128), () => (UInt128)7, route);
                    Check("int128-text-not-supported", "7", typeof(string), typeof(Int128), () => throw new InvalidCastException(), route);
                    Check("uint128-text-not-supported", "7", typeof(string), typeof(UInt128), () => throw new InvalidCastException(), route);
#else
                    rows.Add(new Row(culture, "Int128/UInt128", "N/A", "Not available in net6.0", route, "NOT_AVAILABLE", null, "$", "NOT_RUN", null));
#endif
                }
                var special = new (Type Type, object Value, Func<string, object> Parse)[] {
                    (typeof(Guid), Guid.Parse("ccfb8daf-f51a-4de3-8346-ce482d41a1e3"), s => Guid.Parse(s)),
                    (typeof(DateTime), new DateTime(2026, 10, 27, 13, 45, 12), s => DateTime.Parse(s, CultureInfo.CurrentCulture)),
                    (typeof(DateTimeOffset), new DateTimeOffset(2026, 10, 27, 13, 45, 12, TimeSpan.FromHours(2)), s => DateTimeOffset.Parse(s, CultureInfo.CurrentCulture)),
                    (typeof(TimeSpan), TimeSpan.FromSeconds(-93784), s => TimeSpan.Parse(s, CultureInfo.CurrentCulture)),
                    (typeof(DateOnly), new DateOnly(2026, 10, 27), s => DateOnly.Parse(s, CultureInfo.CurrentCulture)),
                    (typeof(TimeOnly), new TimeOnly(13, 45), s => TimeOnly.Parse(s, CultureInfo.CurrentCulture)) };
                foreach (var (type, value, parse) in special)
                foreach (var route in new[] { "root", "member", "object" })
                {
                    var text = value.ToString()!;
                    Check("format", value, type, typeof(string), () => text, route);
                    foreach (var input in new[] { text, "", " ", "invalid" })
                        Check("parse:" + input, input, typeof(string), type, () => parse(input), route);
                    var nullable = typeof(Nullable<>).MakeGenericType(type);
                    Check("nullable-present", value, nullable, type, () => value, route);
                    Check("nullable-lift", value, type, nullable, () => value, route);
                    Check("nullable-null", null, nullable, nullable, () => null, route);
                    Check("nullable-default", null, nullable, type, () => Activator.CreateInstance(type), route);
                }
            }
        }
        finally { CultureInfo.CurrentCulture = original; }
        var output = Environment.GetEnvironmentVariable("LING_CONVERSION_MATRIX");
        if (!string.IsNullOrEmpty(output)) File.WriteAllText(output, JsonSerializer.Serialize(rows, new JsonSerializerOptions { WriteIndented = true }));
        var failures = rows.Where(r => r.Result == "FAIL").ToArray();
        Console.WriteLine($"Conversion type matrix: {rows.Count} cases; SUPPORTED={rows.Count(r => r.Contract == "SUPPORTED")}; REJECTED={rows.Count(r => r.Contract == "REJECTED")}; NOT_RUN={rows.Count(r => r.Result == "NOT_RUN")}; failures={failures.Length}");
        foreach (var failure in failures.Take(12)) Console.WriteLine(JsonSerializer.Serialize(failure));
        foreach (var row in rows.Where(r => r.Result != "NOT_RUN"))
            TestConsole.Utils.TestHelper.AssertTrue(row.Result == "PASS", $"{row.Culture} {row.Source}->{row.Target} {row.Sample} {row.Route}");
    }
    public enum ByteEnum : byte { Low = byte.MinValue, High = byte.MaxValue }
    public enum SByteEnum : sbyte { Low = sbyte.MinValue, High = sbyte.MaxValue }
    public enum ShortEnum : short { Low = short.MinValue, High = short.MaxValue }
    public enum UShortEnum : ushort { Low = ushort.MinValue, High = ushort.MaxValue }
    public enum IntEnum : int { Low = int.MinValue, High = int.MaxValue }
    public enum UIntEnum : uint { Low = uint.MinValue, High = uint.MaxValue }
    public enum LongEnum : long { Low = long.MinValue, High = long.MaxValue }
    public enum ULongEnum : ulong { Low = ulong.MinValue, High = ulong.MaxValue }
    [Flags] public enum AliasFlags : ulong { [EnumMember(Value = "low-bit")] Low = 1, [EnumMember(Value = "high-bit")] High = 1UL << 63 }
    public sealed class Box<T> { public T? Value { get; set; } }
    private sealed record Row(string Culture, string Source, string Target, string Sample, string Route, string Contract, string? ExceptionType, string Path, string Result, string? Detail);
}

/// <summary>Ordinary assignment compatibility is not permission to alias mutable source data.</summary>
public static class ConversionAssignableRegressionTests
{
    public static void Run()
    {
        int assertions = 0;
        void Require(bool condition, string message) { assertions++; TestConsole.Utils.TestHelper.AssertTrue(condition, message); }
        var mapper = new MapperConfiguration().CreateMapper();
        var child = new Child { Value = 17 };
        var source = new Derived { Value = 23, Child = child, Extra = 31 };
        foreach (var target in new[] { typeof(Derived), typeof(Base), typeof(IEntity), typeof(object) })
        {
            var mapped = mapper.Map(source, typeof(Derived), target)!;
            Require(!ReferenceEquals(source, mapped), "Assignable root must be fresh: " + target);
            Require(mapped is IEntity { Value: 23, Child.Value: 17 }, "Assignable root values: " + target);
            var entity = (IEntity)mapped;
            Require(!ReferenceEquals(child, entity.Child), "Assignable nested mutable child must be fresh: " + target);
            entity.Child!.Value++;
            Require(child.Value == 17, "Mutating result child changed source: " + target);
        }
        var holder = new Holder { Value = source };
        var copy = mapper.Map<Holder>(holder)!;
        Require(!ReferenceEquals(holder, copy) && !ReferenceEquals(source, copy.Value), "Same-type interface member must be deep copied");
        Require(copy.Value is Derived { Extra: 31, Value: 23, Child.Value: 17 }, "Interface member must preserve polymorphic value");
        var dynamicCopy = mapper.Map<object>(source);
        Require(dynamicCopy is Derived { Extra: 31 }, "Object mapping must retain runtime shape");
        var targetRoot = new Derived { Child = new Child { Value = -1 } };
        var updated = mapper.MapInto(source, targetRoot);
        Require(ReferenceEquals(targetRoot, updated), "MapInto must retain destination root");
        Require(updated.Value == 23 && updated.Child!.Value == 17 && !ReferenceEquals(child, updated.Child), "MapInto cannot borrow source child");
        var inherited = mapper.Map<Derived>(new Base { Value = 9, Child = child })!;
        Require(inherited.Value == 9 && inherited.Child!.Value == 17 && !ReferenceEquals(inherited.Child, child), "Base to derived member values and isolation");

        // A member expression outranks a registered converter, which outranks an operator.
        // Count actual executions (not merely values) and use distinct mutable results.
        CountInput.OperatorCalls = 0;
        int registryCalls = 0;
        var input = new CountInput { Value = 7 };
        Require(mapper.Map<CountOutput>(input)!.Value == 8 && CountInput.OperatorCalls == 1, "Operator must execute exactly once");
        TypeConverterRegistry.Register(typeof(CountInput), typeof(CountOutput), new Func<CountInput, CountOutput>(value => { registryCalls++; return new() { Value = value.Value + 10 }; }));
        Require(mapper.Map<CountOutput>(input)!.Value == 17 && registryCalls == 1 && CountInput.OperatorCalls == 1, "Registry must replace cached operator exactly once");
        var configuration = new MapperConfiguration(); configuration.AddProfile(new CountProfile()); var configured = configuration.CreateMapper();
        var member = new CountSource { Value = input };
        var memberResult = configured.Map<CountTarget>(member)!;
        Require(memberResult.Value!.Value == 107, "Explicit member expression must win");
        Require(!ReferenceEquals(member.Resolved, memberResult.Value), "ForMember is a source selector, not a final converter; cached target must be copied");
        memberResult.Value.Value++;
        Require(member.Resolved.Value == 107, "Mutating a selected target-shaped result cannot mutate the selector cache");
        Require(member.Calls == 1 && registryCalls == 1 && CountInput.OperatorCalls == 1, "Explicit member must execute once without hidden conversion");
        var collection = mapper.Map<CountOutput[]>(new[] { new CountInput { Value = 1 }, new CountInput { Value = 2 } })!;
        Require(collection[0].Value == 11 && collection[1].Value == 12 && registryCalls == 3, "Distinct collection items require one converter invocation each");
        Require(mapper.Map<CountOutput>(null) == null && registryCalls == 3, "Null policy must not invoke user converter");
        int finalCalls = 0;
        TypeConverterRegistry.Register(typeof(FinalValue), typeof(FinalValue), new Func<FinalValue, FinalValue>(value => { finalCalls++; return value; }));
        var finalSource = new FinalValue { Value = 41 };
        var finalResult = mapper.Map<FinalValue>(finalSource)!;
        Require(ReferenceEquals(finalSource, finalResult) && finalCalls == 1, "Explicit same-type final converter owns its result identity");
        finalResult.Value++;
        Require(finalSource.Value == 42, "Deliberate final-converter aliasing must remain observable");
        var cached = new FinalValue { Value = 43 };
        TypeConverterRegistry.Register(typeof(FinalInput), typeof(FinalValue), new Func<FinalInput, FinalValue>(_ => { finalCalls++; return cached; }));
        var nestedFinal = mapper.Map<ConversionTypeMatrixRegressionTests.Box<FinalValue>>(new ConversionTypeMatrixRegressionTests.Box<FinalInput> { Value = new() })!;
        Require(ReferenceEquals(cached, nestedFinal.Value) && cached.Value == 43 && finalCalls == 2, "Final result cannot be remapped or invoke a same-type converter a second time");
        Console.WriteLine($"Assignable/converter matrix: {assertions}/{assertions} assertions passed.");
    }
    public sealed class FinalValue { public int Value { get; set; } }
    public sealed class FinalInput { }
    public interface IEntity { int Value { get; set; } Child? Child { get; set; } }
    public class Base : IEntity { public int Value { get; set; } public Child? Child { get; set; } }
    public sealed class Derived : Base { public int Extra { get; set; } }
    public sealed class Child { public int Value { get; set; } }
    public sealed class Holder { public IEntity? Value { get; set; } }
    public sealed class CountInput
    {
        public static int OperatorCalls;
        public int Value { get; set; }
        public static implicit operator CountOutput(CountInput input) { OperatorCalls++; return new() { Value = input.Value + 1 }; }
    }
    public sealed class CountOutput { public int Value { get; set; } }
    public sealed class CountSource
    {
        public CountInput? Value { get; set; }
        public int Calls { get; private set; }
        public CountOutput Resolved { get; } = new() { Value = 107 };
        public CountOutput Resolve() { Calls++; return Resolved; }
    }
    public sealed class CountTarget { public CountOutput? Value { get; set; } }
    public sealed class CountProfile : MapperProfile
    {
        public CountProfile()
        {
            CreateMap<CountSource, CountTarget>().ForMember(target => target.Value, source => source.Resolve());
        }
    }
}
