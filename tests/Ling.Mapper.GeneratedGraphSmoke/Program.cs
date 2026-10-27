using Ling.Mapper;
using Ling.Mapper.Extensions;
using Ling.Mapper.Provider;

// Child/self/collection pairs are discovered from this root, not enumerated by hand.
[assembly: GeneratedMap(typeof(Graph), typeof(Graph))]
[assembly: GeneratedMap(typeof(DynamicOnly), typeof(DynamicOnly))]
[assembly: GeneratedMap(typeof(DiagnosticRootSource), typeof(DiagnosticRootTarget))]
[assembly: GeneratedMap(typeof(DiagnosticItemSource), typeof(DiagnosticItemTarget))]
[assembly: GeneratedMap(typeof(IdentityFailureSource), typeof(IdentityFailureTarget))]

var mapper = GeneratedMapperRegistry.CreateMapper();
MapperProvider.SetCurrent(mapper);
int assertions = 0;
void Check(bool condition, string label) { assertions++; if (!condition) throw new Exception(label); }
void Rejected(Action action, string path)
{
    assertions++;
    try { action(); }
    catch (MappingException error) when (error.OriginalExceptionType == typeof(NotSupportedException).FullName && error.Path == path) { return; }
    throw new Exception("Expected generated ownership diagnostic at " + path);
}
var node = new Node { Value = 7 }; node.Next = node;
var items = new List<Node> { node, node };
var graph = new Graph
{
    First = node, Second = node, Dynamic = node, Interface = node, Items = items,
    ItemsObject = items, ItemsEnumerable = items, ItemsReadOnly = items,
    Nodes = new[] { node, node }, Bytes = new byte[] { 1, 2 },
    Nested = new List<List<Node>> { items, items },
    Value = new Wrapper { Node = node, Items = items }, Optional = new Wrapper { Node = node, Items = items },
    Values = new[] { new Wrapper { Node = node, Items = items } }, Label = "immutable",
    Small = (Half)1.5, Wide = (Int128)long.MaxValue + 100, UnsignedWide = (UInt128)ulong.MaxValue + 100,
    DynamicItems = new List<object> { node, items, 3, "literal" }, DynamicArray = new object[] { node, items }
};
graph.Self = graph;
for (int iteration = 0; iteration < 3; iteration++)
{
    var copy = iteration == 0 ? mapper.Map<Graph>(graph)! : iteration == 1 ? graph.Adapt<Graph>() : (Graph)mapper.Map(graph, typeof(Graph), typeof(Graph))!;
    Check(!ReferenceEquals(graph, copy), "root is source");
    Check(ReferenceEquals(copy, copy.Self), "root cycle not preserved");
    Check(!ReferenceEquals(node, copy.First), "child is source");
    Check(ReferenceEquals(copy.First, copy.Second), "shared child split");
    Check(ReferenceEquals(copy.First, copy.Dynamic), "object slot split");
    Check(ReferenceEquals(copy.First, copy.Interface), "interface slot split");
    Check(ReferenceEquals(copy.First, copy.First!.Next), "node cycle not preserved");
    Check(!ReferenceEquals(items, copy.Items), "list is source");
    Check(ReferenceEquals(copy.Items, copy.ItemsObject), "object list split");
    Check(ReferenceEquals(copy.Items, copy.ItemsEnumerable), "enumerable list split");
    Check(ReferenceEquals(copy.Items, copy.ItemsReadOnly), "readonly-interface list split");
    Check(copy.Items!.All(item => ReferenceEquals(item, copy.First)), "list element sharing lost");
    Check(!ReferenceEquals(graph.Nodes, copy.Nodes) && copy.Nodes!.All(item => ReferenceEquals(item, copy.First)), "array ownership or identity");
    Check(!ReferenceEquals(graph.Bytes, copy.Bytes) && copy.Bytes!.SequenceEqual(graph.Bytes!), "byte array ownership");
    Check(!ReferenceEquals(graph.Nested, copy.Nested), "nested list is source");
    Check(ReferenceEquals(copy.Nested![0], copy.Items) && ReferenceEquals(copy.Nested[0], copy.Nested[1]), "nested list identity");
    Check(ReferenceEquals(copy.Value.Node, copy.First) && ReferenceEquals(copy.Value.Items, copy.Items), "struct contains source references");
    Check(ReferenceEquals(copy.Optional!.Value.Node, copy.First), "nullable struct contains source reference");
    Check(!ReferenceEquals(graph.Values, copy.Values) && ReferenceEquals(copy.Values![0].Node, copy.First), "struct array ownership");
    Check(ReferenceEquals(graph.Label, copy.Label), "immutable string should be reusable");
    Check(copy.Small == graph.Small && copy.Wide == graph.Wide && copy.UnsignedWide == graph.UnsignedWide, "extended immutable numeric values");
    Check(!ReferenceEquals(copy.DynamicItems, graph.DynamicItems) && ReferenceEquals(copy.DynamicItems![0], copy.First) && ReferenceEquals(copy.DynamicItems[1], copy.Items), "dynamic-list ownership");
    Check(!ReferenceEquals(copy.DynamicArray, graph.DynamicArray) && ReferenceEquals(copy.DynamicArray![0], copy.First) && ReferenceEquals(copy.DynamicArray[1], copy.Items), "dynamic-array ownership");
    copy.First.Value = 99; copy.Bytes![0] = 99; copy.Items!.Add(new Node());
    Check(node.Value == 7 && graph.Bytes![0] == 1 && items.Count == 2, "mutating output changed source");
}
var rootList = mapper.Map<List<Node>>(items)!;
Check(!ReferenceEquals(rootList, items) && !ReferenceEquals(rootList[0], node), "same-list root was shallow");
Check(ReferenceEquals(rootList[0], rootList[1]) && ReferenceEquals(rootList[0], rootList[0].Next), "same-list identity");
var objectRoot = mapper.Map<object>(graph)!;
Check(objectRoot is Graph actual && !ReferenceEquals(actual, graph) && ReferenceEquals(actual.First, actual.Dynamic), "object root not cloned dynamically");
var interfaceRoot = mapper.Map<INode>(node)!;
Check(interfaceRoot is Node fresh && !ReferenceEquals(fresh, node) && ReferenceEquals(fresh, fresh.Next), "interface root not cloned dynamically");
var rootValue = mapper.Map<Wrapper>(graph.Value);
Check(!ReferenceEquals(rootValue.Node, node) && !ReferenceEquals(rootValue.Items, items), "same struct root shallow");
Check(ReferenceEquals(rootValue.Node, rootValue.Items![0]), "struct member shared identity");
Check(mapper.Map<Wrapper?>(null) == null, "nullable null");
Check(!ReferenceEquals(mapper.Map<Wrapper?>(graph.Optional)!.Value.Node, node), "nullable root shallow");
var matrix = mapper.Map<List<List<Node>>>(graph.Nested)!;
Check(!ReferenceEquals(matrix, graph.Nested) && !ReferenceEquals(matrix[0], items) && ReferenceEquals(matrix[0], matrix[1]), "nested collection root");
var dynamicSource = new DynamicOnly { Values = new List<object> { node, node, 4 } };
var dynamicCopy = mapper.Map<DynamicOnly>(dynamicSource)!;
Check(dynamicCopy.Values is List<object> dynamicValues && !ReferenceEquals(dynamicValues, dynamicSource.Values) &&
    !ReferenceEquals(dynamicValues[0], node) && ReferenceEquals(dynamicValues[0], dynamicValues[1]), "dynamic IEnumerable-only slot ownership");
Rejected(() => mapper.Map<List<object>>(new List<object> { 4, new Dictionary<string, int>() }), "$[1]");
var boxedValue = (object)new Wrapper { Node = node, Items = items };
var copiedBox = mapper.Map<object>(boxedValue)!;
Check(!ReferenceEquals(copiedBox, boxedValue) && copiedBox is Wrapper boxedCopy && !ReferenceEquals(boxedCopy.Node, node), "boxed struct root shares source");
var objectList = mapper.Map<object>(items)!;
Check(objectList is List<Node> objectItems && !ReferenceEquals(objectItems, items) && !ReferenceEquals(objectItems[0], node), "object collection root shares source");
var deep = new Node { Value = 0 }; var tail = deep;
for (int i = 1; i <= 128; i++) { tail.Next = new Node { Value = i }; tail = tail.Next; }
tail.Next = deep;
var deepCopy = mapper.Map<Node>(deep)!;
var currentSource = deep; var currentCopy = deepCopy;
for (int i = 0; i <= 128; i++)
{
    Check(!ReferenceEquals(currentSource, currentCopy) && currentSource.Value == currentCopy.Value, "deep node ownership");
    currentSource = currentSource.Next!; currentCopy = currentCopy.Next!;
}
Check(ReferenceEquals(currentCopy, deepCopy), "deep node backreference");
var cycleNode = new Node(); object cycleBox = new Wrapper { Node = cycleNode }; cycleNode.Payload = cycleBox;
Rejected(() => mapper.Map<object>(cycleBox), "$.Node.Payload");
Rejected(() => mapper.Map<Dictionary<string, Node>>(new Dictionary<string, Node> { ["item"] = node }), "$");
Rejected(() => mapper.Map<object>(new object()), "$");
Rejected(() => mapper.Map<object>(new MemoryStream()), "$");
var bad = new Graph { Dynamic = new Dictionary<string, Node>() };
Rejected(() => mapper.Map<Graph>(bad), "$.Dynamic");
// Inline member scopes retain exact nested paths and sensitivity without closures.
var diagnosticMapper = GeneratedMapperRegistry.CreateMapper(new() { IncludeSourceValueInExceptions = true });
try
{
    diagnosticMapper.Map<DiagnosticRootTarget>(new DiagnosticRootSource
    { Items = new List<DiagnosticItemSource> { new() { AccessToken = "7" }, new() { AccessToken = "private-invalid" } } });
    throw new Exception("Expected nested sensitive conversion failure");
}
catch (MappingException error)
{
    Check(error.Path == "$.Items[1].access_token", "nested inline member/index path");
    Check(error.SourceValue == "[REDACTED]" && !error.ToString().Contains("private-invalid"), "compile-time source/destination sensitivity");
}
foreach (bool getterFailure in new[] { true, false })
{
    try { diagnosticMapper.Map<IdentityFailureTarget>(new IdentityFailureSource { FailGetter = getterFailure }); throw new Exception("Expected identity getter/setter failure"); }
    catch (MappingException error)
    {
        Check(error.Path == "$.Value" && error.OriginalExceptionType == typeof(InvalidOperationException).FullName,
            "identity assignment still wraps getter/setter failures");
    }
}
// A hand-written registration can recover from a member error; siblings must not inherit its path or sensitive state.
GeneratedMapperRegistry.Register<ScopeProbe, ScopeProbe>((source, options, ignores, context) =>
{
    try { context.Member(typeof(string), typeof(int), "Secret", () => context.Scalar<string, int>("bad-secret")); }
    catch (MappingException error) { Check(error.Path == "$.Secret" && error.SourceValue == "[REDACTED]", "legacy Member sensitive error"); }
    try { context.Member(typeof(string), typeof(int), "Ordinary", () => context.Scalar<string, int>("bad-public")); }
    catch (MappingException error) { Check(error.Path == "$.Ordinary" && error.SourceValue == "bad-public", "scope restores sibling path and sensitivity"); }
    // Formal mapper entry validates options; public nested helper retains its prior unknown-bit behavior.
    Check(context.Map<Node, Node>(node, (Ling.Mapper.Models.AdaptOptions)8).Value == 7, "helper unknown positive option");
    Check(context.Map<Node, Node>(node, (Ling.Mapper.Models.AdaptOptions)(-1)).Value == 7, "helper unknown negative option under checked compilation");
    return new ScopeProbe();
});
diagnosticMapper.Map<ScopeProbe>(new ScopeProbe());
// Every call owns a separate reference cache, including concurrent callers.
Parallel.For(0, 32, _ =>
{
    var copy = mapper.Map<Graph>(graph)!;
    if (ReferenceEquals(copy, graph) || ReferenceEquals(copy.First, node) || !ReferenceEquals(copy.First, copy.Dynamic))
        throw new Exception("parallel graph ownership");
});
assertions += 32;
Console.WriteLine($"Generated independent graphs: {assertions} assertions passed; JIT only.");

public interface INode { int Value { get; set; } }
public sealed class Node : INode { public int Value { get; set; } public Node? Next { get; set; } public object? Payload { get; set; } }
public struct Wrapper { public Node? Node { get; set; } public List<Node>? Items { get; set; } }
public sealed class DynamicOnly { public IEnumerable<object>? Values { get; set; } }
public sealed class Graph
{
    public Graph? Self { get; set; }
    public Node? First { get; set; } public Node? Second { get; set; }
    public object? Dynamic { get; set; } public INode? Interface { get; set; }
    public List<Node>? Items { get; set; } public object? ItemsObject { get; set; }
    public IEnumerable<Node>? ItemsEnumerable { get; set; } public IReadOnlyList<Node>? ItemsReadOnly { get; set; }
    public Node[]? Nodes { get; set; } public byte[]? Bytes { get; set; }
    public List<List<Node>>? Nested { get; set; }
    public Wrapper Value { get; set; } public Wrapper? Optional { get; set; } public Wrapper[]? Values { get; set; }
    public string? Label { get; set; }
    public Half Small { get; set; } public Int128 Wide { get; set; } public UInt128 UnsignedWide { get; set; }
    public List<object>? DynamicItems { get; set; } public object[]? DynamicArray { get; set; }
}

public sealed class DiagnosticRootSource { public List<DiagnosticItemSource>? Items { get; set; } }
public sealed class DiagnosticRootTarget { public List<DiagnosticItemTarget>? Items { get; set; } }
public sealed class DiagnosticItemSource { public string? AccessToken { get; set; } }
public sealed class DiagnosticItemTarget { public int access_token { get; set; } }
public sealed class IdentityFailureSource
{
    public bool FailGetter { get; set; }
    public int Value => FailGetter ? throw new InvalidOperationException("private-getter") : 7;
}
public sealed class IdentityFailureTarget { public int Value { get => 0; set => throw new InvalidOperationException("private-setter"); } }
public sealed class ScopeProbe { }
