using System.Runtime.CompilerServices;
using System.Runtime.Serialization;
using System.Globalization;
using Ling.Mapper;
using Ling.Mapper.Configuration;
using Ling.Mapper.Extensions;
using Ling.Mapper.Models;
using Ling.Mapper.Provider;

[assembly: GeneratedMap(typeof(Source), typeof(Target))]
[assembly: GeneratedMap(typeof(Source), typeof(Source))]
[assembly: GeneratedMap(typeof(Node), typeof(Node))]
[assembly: GeneratedMap(typeof(AmbiguousSource), typeof(AmbiguousTarget))]
[assembly: GeneratedMap(typeof(Child), typeof(ChildDto))]
[assembly: GeneratedMap(typeof(Node), typeof(NodeDto))]
[assembly: GeneratedMap(typeof(string), typeof(int))]
[assembly: GeneratedMap(typeof(double), typeof(int))]
[assembly: GeneratedMap(typeof(string), typeof(Status))]
[assembly: GeneratedMap(typeof(SecretSource), typeof(SecretTarget))]
[assembly: GeneratedMap(typeof(ForeignSource), typeof(ForeignTarget))]
[assembly: GeneratedMap(typeof(NullableSource), typeof(NonNullTarget))]

[assembly: GeneratedMap(typeof(string), typeof(WireStatus))]
[assembly: GeneratedMap(typeof(WireStatus), typeof(string))]
[assembly: GeneratedMap(typeof(string), typeof(WireFlags))]
[assembly: GeneratedMap(typeof(WireFlags), typeof(string))]
[assembly: GeneratedMap(typeof(string), typeof(CollidingAlias))]
[assembly: GeneratedMap(typeof(CollidingAlias), typeof(string))]
[assembly: GeneratedMap(typeof(AliasSource), typeof(AliasTarget))]
[assembly: GeneratedMap(typeof(Price), typeof(decimal))]
[assembly: GeneratedMap(typeof(CheckedPrice), typeof(int))]
[assembly: GeneratedMap(typeof(DeclaredNullableValue), typeof(int?))]
[assembly: GeneratedMap(typeof(DeclaredNullableInputSource), typeof(DeclaredNullableInputTarget))]
[assembly: GeneratedMap(typeof(int?), typeof(NullableInputOnly))]
[assembly: GeneratedMap(typeof(decimal), typeof(Price))]
[assembly: GeneratedMap(typeof(Price?), typeof(decimal?))]
[assembly: GeneratedMap(typeof(OperatorSource), typeof(OperatorTarget))]
[assembly: GeneratedMap(typeof(NullableOperatorSource), typeof(NullableOperatorTarget))]
[assembly: GeneratedMap(typeof(NullableOperatorSource), typeof(NonNullableOperatorTarget))]
[assembly: GeneratedMap(typeof(ReferenceOperatorHolderSource), typeof(ReferenceOperatorHolderTarget))]
[assembly: GeneratedMap(typeof(ThrowingOperator), typeof(int))]
[assembly: GeneratedMap(typeof(ThrowingOperatorSource), typeof(ThrowingOperatorTarget))]
[assembly: GeneratedMap(typeof(NullOperatorSource), typeof(NullOperatorTarget))]
[assembly: GeneratedMap(typeof(string), typeof(decimal))]
[assembly: GeneratedMap(typeof(decimal), typeof(string))]

Console.WriteLine($"NativeAOT smoke: dynamic-code-supported={RuntimeFeature.IsDynamicCodeSupported}");
if (args.Contains("--require-aot") && RuntimeFeature.IsDynamicCodeSupported) throw new Exception("Expected a native AOT executable.");
int assertions = 0;
void Check(bool condition, string label) { assertions++; if (!condition) throw new Exception("FAILED: " + label); }
var configuration = new MapperConfiguration();
var mapper = GeneratedMapperRegistry.CreateMapper(configuration);
MapperProvider.SetCurrent(mapper);
var child = new Child { Value = 7 };
var source = new Source { Id = "42", user_name = "alice", Amount = 2.5, Status = "active", Nullable = null, Child = child, Other = child, Children = new() { child } };
var target = source.Adapt<Target>();
Check(target.Id == 42 && target.UserName == "alice", "Adapt and flexible names");
Check(target.Amount == 2 && target.Status == Status.Active && target.Nullable == null, "Convert rounding, enum and nullable");
Check(target.Child?.Value == 7 && ReferenceEquals(target.Child, target.Other), "nested and shared identity");
Check(target.Children?[0].Value == 7, "nested list");
Check(ReferenceEquals(target.Child, target.Children?[0]), "shared reference through list");
Check(((Target)mapper.Map((object)source, typeof(object), typeof(Target))!).Id == 42, "registered dynamic object");
Check(source.Adapt<Target>(AdaptOptions.Strict).UserName == null, "strict options");
Check(source.Adapt<Target>("Id").Id == 0, "ignore member");
int callbackCount = 0;
source.Adapt<Target>(result => { callbackCount++; Check(result.Id == 42, "result callback"); });
Check(callbackCount == 1, "callback exactly once");
// The complete Adapt option/ignore/callback matrix must stay on the generated backend.
string[] ignoredId = ["Id"];
Check(source.Adapt<Target>(ignoredId).Id == 0, "array-only Adapt ignore");
Check(source.Adapt<Target>(AdaptOptions.Strict, ignoredId).Id == 0, "options plus Adapt ignore array");
callbackCount = 0;
var resultCombination = source.Adapt<Target>(AdaptOptions.Strict, result =>
{
    callbackCount++;
    Check(result.Id == 0 && result.UserName == null, "result callback after explicit options and ignores");
}, ignoredId);
Check(callbackCount == 1 && resultCombination.Id == 0, "combined result callback once");
callbackCount = 0;
source.Adapt<Target, Source>((result, original) =>
{
    callbackCount++;
    Check(result.Id == 0 && ReferenceEquals(source, original), "typed callback after ignores");
}, ignoredId);
source.Adapt<Target, Source>(AdaptOptions.Strict, (result, original) =>
{
    callbackCount++;
    Check(result.Id == 0 && result.UserName == null, "typed callback explicit options");
}, ignoredId);
Check(callbackCount == 2, "combined object callback counts");
callbackCount = 0;
var combinedItems = new[] { child, child }.Adapt<ChildDto, Child>(AdaptOptions.Strict, (result, original) =>
{
    callbackCount++;
    Check(result.Value == 0 && ReferenceEquals(child, original), "collection callback after item ignore");
}, "Value");
Check(callbackCount == 2 && ReferenceEquals(combinedItems[0], combinedItems[1]), "combined collection callback occurrence semantics");
callbackCount = 0;
new[] { child }.Adapt<ChildDto, Child>((result, original) => callbackCount++, ["Value"]);
new[] { child }.Adapt<List<ChildDto>>(list => { callbackCount++; Check(list[0].Value == 0, "whole-list callback after ignore"); }, "Value");
Check(callbackCount == 2, "default-options item and result combinations");
object? combinedNull = null;
combinedNull.Adapt<Target>(result => Check(result == null, "null result callback with ignores"), ignoredId);
try { source.Adapt<Target>(result => { }, "Child.Value"); throw new Exception("Expected dotted ignore rejection"); }
catch (NotSupportedException) { Check(true, "Adapt combination preserves generated dotted-ignore boundary"); }
var array = new[] { new Child { Value = 1 }, new Child { Value = 2 } }.Adapt<List<ChildDto>>();
Check(array.Count == 2 && array[1].Value == 2, "root array to list");
child.Value = 8;
Check(source.Adapt<Target>().Child?.Value == 8 && target.Child?.Value == 7, "fresh root contexts");
var node = new Node { Id = 1 }; node.Next = node;
var mappedNode = node.Adapt<NodeDto>();
Check(ReferenceEquals(mappedNode, mappedNode.Next), "self-cycle");
Check(!ReferenceEquals(mappedNode, node.Adapt<NodeDto>()), "cycle contexts separated");
Check(mapper.Map<int>(2.5) == 2 && mapper.Map<int>(3.5) == 4, "midpoint-to-even");
Check(mapper.Map<int>(null) == 0, "null default");
try { GeneratedMapperRegistry.CreateMapper(new() { NullValueHandling = NullValueHandling.Throw }).Map<int>(null); throw new Exception("Expected null failure"); }
catch (MappingException error) { Check(error.Path == "$", "throw null policy"); }
try { mapper.Map<Target>(new Unknown()); throw new Exception("Expected unregistered failure"); }
catch (MappingException error) { Check(error.OriginalExceptionType == typeof(NotSupportedException).FullName, "unregistered pair clearly rejected"); }
source.Id = "private-invalid";
try { source.Adapt<Target>(); throw new Exception("Expected invalid scalar failure"); }
catch (MappingException error) { Check(error.Path == "$.Id" && !error.ToString().Contains("private-invalid"), "safe scalar path"); }
source.Id = "42";
try { new SecretSource().Adapt<SecretTarget>(); throw new Exception("Expected getter failure"); }
catch (MappingException error) { Check(error.Path == "$.Password" && !error.ToString().Contains("secret-token-value"), "safe getter path"); }
Parallel.For(0, 500, i => { var value = new Child { Value = i }.Adapt<ChildDto>(); if (value.Value != i) throw new Exception("Cross-root context contamination"); });
Check(true, "concurrent generated mapper");
if (RuntimeFeature.IsDynamicCodeSupported)
{
    var runtime = new MapperConfiguration().CreateMapper();
    var expected = runtime.Map<Target>(source)!;
    Check(expected.Id == target.Id && expected.Amount == target.Amount && expected.Status == target.Status, "runtime/generated agreement");
}
else
{
    MapperProvider.Clear();
    Check(new Child { Value = 19 }.Adapt<ChildDto>().Value == 19, "AOT automatic configuration dispatch");
}
var copiedSource = mapper.Map<Source>(source)!;
Check(!ReferenceEquals(source, copiedSource) && !ReferenceEquals(source.Child, copiedSource.Child) &&
    ReferenceEquals(copiedSource.Child, copiedSource.Other), "same-type source independence and internal identity");
callbackCount = 0;
mapper.Map<Target>(source, AdaptOptions.Default, (_, _) => callbackCount++);
Check(callbackCount == 1, "root callback does not cascade into nested members");
callbackCount = 0;
var duplicates = mapper.Map<List<ChildDto>>(new List<Child> { child, child }, AdaptOptions.Default, (_, _) => callbackCount++)!;
Check(callbackCount == 2 && ReferenceEquals(duplicates[0], duplicates[1]), "duplicate collection item callbacks and identity");
var ignoredItems = mapper.Map<List<ChildDto>>(new List<Child> { child }, AdaptOptions.Default, ignoreNames: new[] { "Value" })!;
Check(ignoredItems[0].Value == 0, "collection item ignore names");
var strictNull = GeneratedMapperRegistry.CreateMapper(new() { NullValueHandling = NullValueHandling.Throw });
try { strictNull.Map<NonNullTarget>(new NullableSource()); throw new Exception("Expected null member failure"); }
catch (MappingException error) { Check(error.Path == "$.Value", "null member throw path"); }
var verbose = GeneratedMapperRegistry.CreateMapper(new() { IncludeSourceValueInExceptions = true });
try { verbose.Map<int>("external-secret-value"); }
catch (MappingException error) { ForeignSource.Error = error; }
try { mapper.Map<ForeignTarget>(new ForeignSource()); throw new Exception("Expected foreign mapping error"); }
catch (MappingException error) { Check(error.Path == "$.Value" && !error.ToString().Contains("external-secret-value"), "foreign MappingException sanitized"); }
var unsupportedConventions = new MapperConfiguration();
unsupportedConventions.ConfigureConventions(options => options.IgnoreSpecialCharacters = true);
try { GeneratedMapperRegistry.CreateMapper(unsupportedConventions); throw new Exception("Expected conventions rejection"); }
catch (NotSupportedException) { Check(true, "unsupported conventions explicitly rejected"); }
var secondNode = new Node { Id = 2 };
var firstNode = new Node { Id = 1, Next = secondNode };
var projected = mapper.Map<List<NodeDto>>(new[] { firstNode, secondNode }, AdaptOptions.Default, ignoreNames: new[] { "Id" })!;
Check(projected[0].Id == 0 && projected[0].Next?.Id == 2 && projected[1].Id == 0, "ignore projection does not reuse unmasked nested object");
Check(!ReferenceEquals(projected[0].Next, projected[1]), "different masks keep separate cache projections");
callbackCount = 0;
var identityItems = new List<Node> { firstNode, secondNode };
var copiedIdentityItems = mapper.Map<List<Node>>(identityItems, AdaptOptions.Default, (_, _) => callbackCount++)!;
Check(!ReferenceEquals(identityItems, copiedIdentityItems) && !ReferenceEquals(firstNode, copiedIdentityItems[0]) &&
    ReferenceEquals(copiedIdentityItems[0].Next, copiedIdentityItems[1]), "same-type collection independence and internal identity");
Check(callbackCount == 2, "copied collection item callbacks");
try { mapper.Map<Target>(source, AdaptOptions.Default, ignoreNames: new[] { "Child.Value" }); throw new Exception("Expected dotted ignore rejection"); }
catch (NotSupportedException) { Check(true, "unsupported dotted ignore explicitly rejected"); }
try { mapper.Map(new int[] { -1 }, typeof(uint[]), typeof(uint[])); throw new Exception("Expected signed array rejection"); }
catch (MappingException error) { Check(error.Path == "$" && error.OriginalExceptionType == typeof(InvalidCastException).FullName, "unsafe declared signed-array alias rejected"); }
// Explicit Type validation must precede null-source handling on both backends.
unsafe
{
    Type[] invalidTypes = { typeof(void), typeof(List<>), typeof(int).MakeByRefType(), typeof(int).MakePointerType(), typeof(Span<int>), typeof(delegate*<void>) };
    foreach (var invalid in invalidTypes)
    {
        foreach (bool sourceSide in new[] { true, false })
        {
            try { mapper.Map(null, sourceSide ? invalid : typeof(string), sourceSide ? typeof(int) : invalid); throw new Exception("Expected invalid Type failure"); }
            catch (ArgumentException error) { Check(error.ParamName == (sourceSide ? "sourceType" : "destType"), "invalid explicit Type before null source"); }
        }
    }
}
try { mapper.Map("private-mismatch", typeof(Child), typeof(ChildDto)); throw new Exception("Expected source mismatch"); }
catch (MappingException error) { Check(error.Path == "$" && error.OriginalExceptionType == typeof(InvalidCastException).FullName && !error.ToString().Contains("private-mismatch"), "declared source mismatch safely wrapped"); }
foreach (string? ignored in new string?[] { null, "", " ", "\t" })
{
    try { mapper.Map<Target>(source, AdaptOptions.Default, ignoreNames: new[] { ignored! }); throw new Exception("Expected invalid ignore"); }
    catch (ArgumentException) { Check(true, "invalid ignore entry rejected"); }
}
foreach (var (input, maximum, expected) in new[] { ("x\0\u001b\u202e\U000E0001z", 30, "x    z"), ("x😀z", 2, "x"), ("x😀z", 3, "x😀"), ("x\ud800z", 5, "x\ufffdz") })
{
    var diagnosticMapper = GeneratedMapperRegistry.CreateMapper(new() { IncludeSourceValueInExceptions = true, MaxDiagnosticValueLength = maximum });
    try { diagnosticMapper.Map<int>(input); throw new Exception("Expected scalar failure"); }
    catch (MappingException error) { Check(error.SourceValue == expected && error.Path == "$", "Unicode preview sanitized and scalar-safe truncation"); }
}
GeneratedMapperRegistry.Register<NullSource, NullTarget>((_, _, _, _) => null!);
MapperProvider.SetCurrent(mapper);
Check(new NullSource().Adapt<NullTarget>() == null, "registered null result is preserved by Adapt");
object? nullSource = null;
Check(nullSource.Adapt<NullTarget>() == null && nullSource.Adapt<int>() == 0, "Adapt null uses mapper policy");
MapperProvider.SetCurrent(strictNull);
try { nullSource.Adapt<int>(); throw new Exception("Expected Adapt null failure"); }
catch (MappingException error) { Check(error.Path == "$", "Adapt null Throw policy"); }
MapperProvider.SetCurrent(mapper);
var ambiguous = new AmbiguousSource();
Check(mapper.Map<AmbiguousTarget>(ambiguous, AdaptOptions.Strict)!.Name == "exact" && ambiguous.GetterCalls == 1, "strict source names disambiguate");
ambiguous.GetterCalls = 0;
try { mapper.Map<AmbiguousTarget>(ambiguous); throw new Exception("Expected ambiguous source failure"); }
catch (MappingException error) { Check(error.Path == "$.Name" && ambiguous.GetterCalls == 0, "ambiguous names rejected before getters"); }
Check(mapper.Map<AmbiguousTarget>(ambiguous, AdaptOptions.Default, ignoreNames: new[] { "Name" })!.Name == null && ambiguous.GetterCalls == 0, "ignored ambiguous member never read");
// Operators are emitted as typed calls, never silently reinterpreted as DTO mappings.
Check(mapper.Map<decimal>(new Price(12.5m)) == 12.5m, "root explicit source operator");
Check(mapper.Map<Price>(7.5m).Value == 7.5m, "root destination-declared operator");
Check(mapper.Map<int>(new CheckedPrice(5)) == 5, "checked consumer does not select op_CheckedExplicit");
Check(mapper.Map<int?>(new DeclaredNullableValue(5)) == 205, "declared nullable return operator beats underlying return operator");
Check(mapper.Map<int?>(new DeclaredNullableValue(-1)) == null, "declared nullable operator null result is preserved");
Check(mapper.Map<DeclaredNullableInputTarget>(new DeclaredNullableInputSource { Value = 5 })!.Value.Value == 205, "member declared nullable parameter operator beats underlying parameter operator");
Check(mapper.Map<DeclaredNullableInputTarget>(new DeclaredNullableInputSource())!.Value.Value == 0, "null skips declared nullable parameter operator");
try { strictNull.Map<DeclaredNullableInputTarget>(new DeclaredNullableInputSource()); throw new Exception("Expected nullable parameter null policy failure"); }
catch (MappingException error) { Check(error.Path == "$.Value", "Throw null policy precedes nullable parameter operator"); }
Check(mapper.Map<NullableInputOnly>((int?)5)!.Value == 405 && mapper.Map<NullableInputOnly>(5)!.Value == 405, "unique nullable parameter root bridge is stable after boxing");
Check(mapper.Map<NullableInputOnly>((int?)null) == null, "nullable parameter root bridge preserves mapper null policy");
Price? nullablePrice = new Price(9m);
Check(mapper.Map<decimal?>(nullablePrice) == 9m && mapper.Map<decimal?>((Price?)null) == null, "lifted nullable root operator");
Check(mapper.Map<OperatorTarget>(new OperatorSource { Value = 3 })!.Value == 103, "operator precedes possible structural DTO mapping");
var nullableOperator = mapper.Map<NullableOperatorTarget>(new NullableOperatorSource { Value = new Price(4m) })!;
Check(nullableOperator.Value == 4m, "lifted nullable member operator");
Check(mapper.Map<NullableOperatorTarget>(new NullableOperatorSource())!.Value == null, "nullable member null does not call operator");
Check(mapper.Map<NullableOperatorTarget>(new NullableOperatorSource(), AdaptOptions.IgnoreNullValues)!.Value == 99m, "ignored null keeps initializer before operator");
Check(mapper.Map<NullOperatorTarget>(new NullOperatorSource()) == null, "operator null result preserved");
Check(mapper.Map<NonNullableOperatorTarget>(new NullableOperatorSource())!.Value == 0m, "nullable-to-nonnullable operator default policy");
try { strictNull.Map<NonNullableOperatorTarget>(new NullableOperatorSource()); throw new Exception("Expected null operator source failure"); }
catch (MappingException error) { Check(error.Path == "$.Value", "nullable-to-nonnullable operator throw policy"); }
var sharedOperatorSource = new ReferenceOperatorSource { Value = 6 };
ReferenceOperatorSource.Calls = 0;
var referenceOperators = mapper.Map<ReferenceOperatorHolderTarget>(new ReferenceOperatorHolderSource { First = sharedOperatorSource, Second = sharedOperatorSource })!;
Check(ReferenceOperatorSource.Calls == 2 && referenceOperators.First!.Value == 106 && referenceOperators.Second!.Value == 106 && !ReferenceEquals(referenceOperators.First, referenceOperators.Second), "reference-source member operators execute per occurrence without structural fallback or caching");
try { mapper.Map<OperatorTarget>(new OperatorSource(), AdaptOptions.Default, ignoreNames: ["Value"]); throw new Exception("Expected opaque operator ignore failure"); }
catch (MappingException error) { Check(error.Path == "$" && error.OriginalExceptionType == typeof(NotSupportedException).FullName, "operator ignore projection explicitly rejected"); }
try { mapper.Map<int>(new ThrowingOperator()); throw new Exception("Expected operator error"); }
catch (MappingException error) { Check(error.Path == "$" && error.OriginalExceptionType == typeof(NullReferenceException).FullName && !error.ToString().Contains("private-operator"), "root operator error keeps exception kind and sanitizes value"); }
try { mapper.Map<ThrowingOperatorTarget>(new ThrowingOperatorSource()); throw new Exception("Expected member operator error"); }
catch (MappingException error) { Check(error.Path == "$.Value" && error.OriginalExceptionType == typeof(NullReferenceException).FullName, "member operator null-like error is not swallowed"); }
Check(mapper.Map<WireStatus>("READY-WIRE") == WireStatus.Ready, "EnumMember alias case-insensitive parse");
Check(mapper.Map<WireStatus>("Ready") == WireStatus.Ready && mapper.Map<WireStatus>("1") == WireStatus.Ready, "EnumMember preserves CLR names and numeric text");
Check(mapper.Map<string>(WireStatus.Ready) == "ready-wire", "EnumMember alias formatting");
Check(mapper.Map<string>((WireStatus)123) == "123", "undefined numeric enum formatting unchanged");
Check(mapper.Map<WireFlags>("r, w") == (WireFlags.Read | WireFlags.Write), "EnumMember flags token parsing");
Check(mapper.Map<string>(WireFlags.Read | WireFlags.Write) == "r, w", "EnumMember flags token formatting");
var aliasTarget = mapper.Map<AliasTarget>(new AliasSource { Status = "ready-wire", Optional = "ready-wire" })!;
Check(aliasTarget.Status == WireStatus.Ready && aliasTarget.Optional == WireStatus.Ready, "EnumMember member and nullable enum conversion");
Check(mapper.Map<AliasTarget>(new AliasSource { Status = "ready-wire" })!.Optional == null, "nullable enum null member");
try { mapper.Map<CollidingAlias>("same"); throw new Exception("Expected alias collision"); }
catch (MappingException error) { Check(error.Path == "$" && error.OriginalExceptionType == typeof(NotSupportedException).FullName, "alias collision rejected during parse"); }
try { mapper.Map<string>(CollidingAlias.One); throw new Exception("Expected alias collision during format"); }
catch (MappingException error) { Check(error.Path == "$" && error.OriginalExceptionType == typeof(NotSupportedException).FullName, "alias collision rejected during format"); }
var previousCulture = CultureInfo.CurrentCulture;
try
{
    CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("fr-FR");
    Check(mapper.Map<decimal>("1,5") == 1.5m && mapper.Map<string>(1.5m) == "1,5", "same generated mapper observes fr-FR at runtime");
    CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("en-US");
    Check(mapper.Map<decimal>("1.5") == 1.5m && mapper.Map<string>(1.5m) == "1.5", "same generated mapper observes culture switch");
}
finally { CultureInfo.CurrentCulture = previousCulture; }
if (RuntimeFeature.IsDynamicCodeSupported)
{
    var runtime = new MapperConfiguration().CreateMapper();
    Check(runtime.Map<decimal>(new Price(12.5m)) == mapper.Map<decimal>(new Price(12.5m)), "runtime/generated operator agreement");
    Check(runtime.Map<int?>(new DeclaredNullableValue(5)) == mapper.Map<int?>(new DeclaredNullableValue(5)), "runtime/generated declared nullable return priority agreement");
    var nullableInput = new DeclaredNullableInputSource { Value = 5 };
    Check(runtime.Map<DeclaredNullableInputTarget>(nullableInput)!.Value == mapper.Map<DeclaredNullableInputTarget>(nullableInput)!.Value, "runtime/generated declared nullable member parameter priority agreement");
    Check(runtime.Map<NullableInputOnly>((int?)5)!.Value == mapper.Map<NullableInputOnly>((int?)5)!.Value, "runtime/generated unique nullable parameter boxing agreement");
    Check(runtime.Map<WireStatus>("ready-wire") == mapper.Map<WireStatus>("ready-wire") && runtime.Map<string>(WireStatus.Ready) == mapper.Map<string>(WireStatus.Ready), "runtime/generated alias agreement");
}
Console.WriteLine($"PASS: {assertions} assertions; generated mappings executed.");

public enum Status { Inactive, Active }
public sealed class Source
{
    public string Id { get; set; } = "";
    public string? user_name { get; set; }
    public double Amount { get; set; }
    public string Status { get; set; } = "";
    public int? Nullable { get; set; }
    public Child? Child { get; set; }
    public Child? Other { get; set; }
    public List<Child>? Children { get; set; }
}
public sealed class Target
{
    public int Id { get; set; }
    public string? UserName { get; set; }
    public int Amount { get; set; }
    public Status Status { get; set; }
    public int? Nullable { get; set; }
    public ChildDto? Child { get; set; }
    public ChildDto? Other { get; set; }
    public List<ChildDto>? Children { get; set; }
}
public sealed class Child { public int Value { get; set; } }
public sealed class ChildDto { public long Value { get; set; } }
public sealed class Node { public int Id { get; set; } public Node? Next { get; set; } }
public sealed class NodeDto { public int Id { get; set; } public NodeDto? Next { get; set; } }
public sealed class Unknown { public int Id { get; set; } }
public sealed class SecretSource { public string Password => throw new InvalidOperationException("secret-token-value"); }
public sealed class SecretTarget { public string? Password { get; set; } }

public sealed class NullableSource { public int? Value { get; set; } }
public sealed class NonNullTarget { public int Value { get; set; } }
public sealed class ForeignSource { public static Exception Error { get; set; } = new Exception("unset"); public string Value => throw Error; }
public sealed class ForeignTarget { public int Value { get; set; } }

public sealed class NullSource { }
public sealed class NullTarget { }

public sealed class AmbiguousSource
{
    public int GetterCalls;
    public string Name { get { GetterCalls++; return "exact"; } }
    public string NAME { get { GetterCalls++; return "other"; } }
}
public sealed class AmbiguousTarget { public string? Name { get; set; } }


public enum WireStatus { None, [EnumMember(Value = "ready-wire")] Ready }
[Flags] public enum WireFlags { None = 0, [EnumMember(Value = "r")] Read = 1, [EnumMember(Value = "w")] Write = 2 }
public enum CollidingAlias { [EnumMember(Value = "same")] One = 1, [EnumMember(Value = "SAME")] Two = 2 }
public sealed class AliasSource { public string Status { get; set; } = ""; public string? Optional { get; set; } }
public sealed class AliasTarget { public WireStatus Status { get; set; } public WireStatus? Optional { get; set; } }
public readonly record struct Price(decimal Value)
{
    public static explicit operator decimal(Price value) => value.Value;
    public static explicit operator Price(decimal value) => new(value);
}
public struct OperatorSource
{
    public int Value { get; set; }
    public static implicit operator OperatorTarget(OperatorSource value) => new() { Value = value.Value + 100 };
}
public sealed class OperatorTarget { public int Value { get; set; } }
public sealed class NullableOperatorSource { public Price? Value { get; set; } }
public sealed class NullableOperatorTarget { public decimal? Value { get; set; } = 99m; }
public struct ThrowingOperator
{
    public static explicit operator int(ThrowingOperator value) => throw new NullReferenceException("private-operator");
}
public sealed class ThrowingOperatorSource { public ThrowingOperator Value { get; set; } = new(); }
public sealed class ThrowingOperatorTarget { public int Value { get; set; } }
public struct NullOperatorSource { public static implicit operator NullOperatorTarget(NullOperatorSource source) => null!; }
public sealed class NullOperatorTarget { }

public sealed class NonNullableOperatorTarget { public decimal Value { get; set; } }
public sealed class ReferenceOperatorSource
{
    public static int Calls;
    public int Value { get; set; }
    public static explicit operator ReferenceOperatorTarget(ReferenceOperatorSource source) { Calls++; return new() { Value = source.Value + 100 }; }
}
public sealed class ReferenceOperatorTarget { public int Value { get; set; } }
public sealed class ReferenceOperatorHolderSource { public ReferenceOperatorSource? First { get; set; } public ReferenceOperatorSource? Second { get; set; } }
public sealed class ReferenceOperatorHolderTarget { public ReferenceOperatorTarget? First { get; set; } public ReferenceOperatorTarget? Second { get; set; } }

public readonly record struct CheckedPrice(int Value)
{
    public static explicit operator int(CheckedPrice source) => source.Value;
    public static explicit operator checked int(CheckedPrice source) => source.Value + 1000;
}

public readonly record struct DeclaredNullableValue(int Value)
{
    public static explicit operator int(DeclaredNullableValue source) => source.Value + 100;
    public static explicit operator int?(DeclaredNullableValue source) => source.Value < 0 ? null : source.Value + 200;
    public static explicit operator DeclaredNullableValue(int source) => new(source + 100);
    public static explicit operator DeclaredNullableValue(int? source) => new(source.GetValueOrDefault() + 200);
}
public sealed class DeclaredNullableInputSource { public int? Value { get; set; } }
public sealed class DeclaredNullableInputTarget { public DeclaredNullableValue Value { get; set; } }
public sealed class NullableInputOnly
{
    public int Value { get; }
    private NullableInputOnly(int value) => Value = value;
    public static explicit operator NullableInputOnly(int? source) => new(source.GetValueOrDefault() + 400);
}
