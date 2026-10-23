using System.Runtime.CompilerServices;
using Ling.Mapper;
using Ling.Mapper.Configuration;
using Ling.Mapper.Extensions;
using Ling.Mapper.Models;
using Ling.Mapper.Provider;

[assembly: GeneratedMap(typeof(Source), typeof(Target))]
[assembly: GeneratedMap(typeof(AmbiguousSource), typeof(AmbiguousTarget))]
[assembly: GeneratedMap(typeof(Child), typeof(ChildDto))]
[assembly: GeneratedMap(typeof(Node), typeof(NodeDto))]
[assembly: GeneratedMap(typeof(string), typeof(int))]
[assembly: GeneratedMap(typeof(double), typeof(int))]
[assembly: GeneratedMap(typeof(string), typeof(Status))]
[assembly: GeneratedMap(typeof(SecretSource), typeof(SecretTarget))]
[assembly: GeneratedMap(typeof(ForeignSource), typeof(ForeignTarget))]
[assembly: GeneratedMap(typeof(NullableSource), typeof(NonNullTarget))]

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
Check(ReferenceEquals(source, mapper.Map<Source>(source)), "safe assignable identity");
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
Check(ReferenceEquals(identityItems, mapper.Map<List<Node>>(identityItems, AdaptOptions.Default, (_, _) => callbackCount++)), "collection identity preserved");
Check(callbackCount == 2, "identity collection item callbacks");
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
