using System.Collections.Concurrent;
using System.Collections.ObjectModel;
using Ling.Mapper.Configuration;
using TestConsole.Utils;
namespace TestConsole.Test;
public static class DictionaryCloneRegressionTests
{
    public static void Run()
    {
        var mapper = new MapperConfiguration().CreateMapper();
        var failures = new List<string>();
        void Case(string name, Action action) { try { action(); Console.WriteLine($"CHECKED {name}"); } catch (Exception e) { failures.Add(name + ": " + e.Message); Console.WriteLine($"FAILED {name}: {e.Message}"); } }
        Case("same-type graph dictionaries lists cycles and mutation independence", () => {
            var root = new Node { Value = "root" }; var leaf = new Node { Value = "leaf", Next = root }; root.Next = leaf;
            root.Links["child"] = new() { leaf, leaf }; leaf.Links["parent"] = new() { root };
            var input = new Dictionary<string, List<Node>> { ["left"] = new() { root, root } }; input["right"] = input["left"];
            var copy = mapper.Map<Dictionary<string, List<Node>>>(input)!;
            Fresh(input, copy, "dictionary root"); Fresh(input["left"], copy["left"], "list");
            Same(copy["left"], copy["right"], "list alias"); Same(copy["left"][0], copy["left"][1], "node alias");
            var r = copy["left"][0]; var l = r.Next!;
            Fresh(root, r, "root node"); Fresh(leaf, l, "leaf node"); Fresh(root.Links, r.Links, "nested dictionary"); Fresh(root.Links["child"], r.Links["child"], "nested list");
            Same(r, l.Next, "next cycle"); Same(l, r.Links["child"][0], "branch alias"); Same(l, r.Links["child"][1], "duplicate alias"); Same(r, l.Links["parent"][0], "dictionary cycle");
            Equal("root", r.Value, "root value"); Equal("leaf", l.Value, "leaf value");
            l.Value = "target-only"; Equal("leaf", leaf.Value, "target mutation isolated"); root.Value = "source-only"; Equal("root", r.Value, "source mutation isolated");
            r.Links["child"].Clear(); Equal(2, root.Links["child"].Count, "nested list mutation isolated");
        });
        Case("dictionary case comparer and mutable values", () => {
            var s = new Dictionary<string, Node>(StringComparer.OrdinalIgnoreCase) { ["Alpha"] = new() { Value = "x" } };
            var d = mapper.Map<Dictionary<string, Node>>(s)!; Fresh(s,d,"dictionary"); Check(d.ContainsKey("ALPHA"),"case insensitive lookup retained"); Fresh(s["Alpha"],d["alpha"],"dictionary value");
        });
        Case("dictionary interface destination preserves comparer with same key type", () => {
            var s = new Dictionary<string, Node>(StringComparer.OrdinalIgnoreCase) { ["Alpha"] = new() { Value = "x" } };
            var d = mapper.Map<IDictionary<string, Node>>(s)!; Fresh(s,d,"interface dictionary"); Check(d.ContainsKey("ALPHA"),"interface comparer retained"); Fresh(s["Alpha"],d["alpha"],"interface value");
            var ro = mapper.Map<IReadOnlyDictionary<string, Node>>(s)!; Fresh(s,ro,"readonly interface dictionary"); Check(ro.ContainsKey("ALPHA"),"readonly interface comparer retained"); Fresh(s["Alpha"],ro["alpha"],"readonly interface value");
        });
        Case("different key type does not reuse incompatible source comparer", () => {
            var s = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase) { ["1"] = "9", ["2"] = "12" };
            var d = mapper.Map<Dictionary<int, long>>(s)!; Equal(2,d.Count,"cross-key count"); Equal(9L,d[1],"cross-key first"); Equal(12L,d[2],"cross-key second");
        });
        Case("concurrent dictionary case comparer", () => {
            var s = new ConcurrentDictionary<string, Node>(StringComparer.OrdinalIgnoreCase); s["Alpha"] = new() { Value = "x" };
            var d = mapper.Map<ConcurrentDictionary<string, Node>>(s)!; Fresh(s,d,"concurrent dictionary"); Check(d.ContainsKey("ALPHA"),"concurrent comparer retained"); Fresh(s["Alpha"],d["alpha"],"concurrent value");
        });
        Case("sorted dictionary reverse custom comparer", () => {
            var comparer = Comparer<string>.Create((a,b) => StringComparer.OrdinalIgnoreCase.Compare(b,a));
            var s = new SortedDictionary<string, Node>(comparer) { ["Alpha"] = new(), ["Zulu"] = new() };
            var d = mapper.Map<SortedDictionary<string, Node>>(s)!; Fresh(s,d,"sorted dictionary"); Equal("Zulu",d.Keys.First(),"reverse order retained"); Check(d.ContainsKey("ALPHA"),"sorted case lookup retained"); Fresh(s["Alpha"],d["alpha"],"sorted value");
        });
        Case("sorted list and sorted interface preserve exposed ordering", () => {
            var comparer = Comparer<string>.Create((a,b) => StringComparer.OrdinalIgnoreCase.Compare(b,a));
            var s = new SortedList<string, Node>(comparer) { ["Alpha"] = new(), ["Zulu"] = new() };
            var d = mapper.Map<SortedList<string, Node>>(s)!; Fresh(s,d,"sorted list"); Equal("Zulu",d.Keys.First(),"sorted list order"); Check(d.ContainsKey("ALPHA"),"sorted list comparer"); Fresh(s["Alpha"],d["alpha"],"sorted list value");
            var contract = mapper.Map<IDictionary<string, Node>>(s)!; Check(contract.ContainsKey("ALPHA"),"sorted interface comparer"); Equal("Zulu",contract.Keys.First(),"sorted interface order");
        });
        Case("sorted source cross-value interface conversion retains order and comparer", () => {
            var comparer = Comparer<string>.Create((a,b) => StringComparer.OrdinalIgnoreCase.Compare(b,a));
            var s = new SortedDictionary<string, TextValue>(comparer) { ["Alpha"] = new() { Value="4" }, ["Zulu"] = new() { Value="9" } };
            var d = mapper.Map<IDictionary<string, NumberValue>>(s)!;
            Equal(2,d.Count,"cross-value sorted count"); Equal("Zulu",d.Keys.First(),"cross-value interface sort order");
            Check(d.ContainsKey("ALPHA"),"cross-value interface ordering comparer retained"); Equal(4,d["alpha"].Value,"cross-value interface numeric conversion"); Equal(9,d["zulu"].Value,"cross-value second numeric conversion");
        });
        Case("exposed comparer retained when constructing readonly wrapper", () => {
            var s = new Dictionary<string, Node>(StringComparer.OrdinalIgnoreCase) { ["Alpha"] = new() { Value="x" } };
            var d = mapper.Map<ReadOnlyDictionary<string, Node>>(s)!; Check(d.ContainsKey("ALPHA"),"readonly target exposed source comparer retained"); Fresh(s["Alpha"],d["alpha"],"readonly target mutable value");
        });
        Case("opaque readonly source wrapper uses documented default target comparer", () => {
            var s = new ReadOnlyDictionary<string, Node>(new Dictionary<string, Node>(StringComparer.OrdinalIgnoreCase) { ["Alpha"] = new() { Value="x" } });
            Check(s.ContainsKey("ALPHA"),"source wrapper has hidden case comparer");
            var d = mapper.Map<ReadOnlyDictionary<string, Node>>(s)!; Fresh(s,d,"opaque wrapper"); Equal("x",d["Alpha"].Value,"opaque wrapper data"); Fresh(s["Alpha"],d["Alpha"],"opaque wrapper mutable value");
            Check(!d.ContainsKey("ALPHA"),"LIMITATION opaque wrapper comparer is not publicly available; target default semantics");
        });
        Case("hash set comparer", () => {
            var s = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "Alpha" };
            var d = mapper.Map<HashSet<string>>(s)!; Fresh(s,d,"hash set"); Check(d.Contains("ALPHA"),"hash set comparer retained"); Check(!d.Add("alpha"),"hash set equality retained"); Equal(1,s.Count,"source set intact");
        });
        Case("readonly dictionary wrapper deep values", () => {
            var node = new Node { Value = "before" }; var s = new ReadOnlyDictionary<string, Node>(new Dictionary<string, Node> { ["a"] = node, ["b"] = node });
            var d = mapper.Map<ReadOnlyDictionary<string, Node>>(s)!; Fresh(s,d,"readonly wrapper"); Fresh(node,d["a"],"readonly value"); Same(d["a"],d["b"],"readonly alias"); d["a"].Value="after"; Equal("before",node.Value,"readonly value mutation isolated");
        });
        Case("mutable reference keys are copied with their alias to value", () => {
            var key = new Node { Value="key" }; var s = new Dictionary<Node, Node> { [key]=key };
            var d=mapper.Map<Dictionary<Node,Node>>(s)!; var pair=d.Single(); Fresh(s,d,"key dictionary"); Fresh(key,pair.Key,"mutable key"); Same(pair.Key,pair.Value,"key value alias"); Equal("key",pair.Key.Value,"key data"); Check(!d.ContainsKey(key),"original reference key deliberately not retained");
        });
        if(failures.Count>0) throw new Exception(string.Join(Environment.NewLine,failures));
    }
    static void Check(bool value,string message)=>TestHelper.AssertTrue(value,message);
    static void Equal(object? a,object? b,string message)=>TestHelper.AssertEqual(a,b,message);
    static void Fresh(object a,object b,string message)=>Check(!ReferenceEquals(a,b),"fresh "+message);
    static void Same(object? a,object? b,string message)=>Check(ReferenceEquals(a,b),message);
    public sealed class TextValue { public string? Value { get; set; } }
    public sealed class NumberValue { public int Value { get; set; } }
    public sealed class Node { public string? Value {get;set;} public Node? Next {get;set;} public Dictionary<string,List<Node>> Links {get;set;}=new(); }
}
