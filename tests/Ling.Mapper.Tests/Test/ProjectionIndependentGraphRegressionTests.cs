using Ling.Mapper;
using Ling.Mapper.Configuration;
using Ling.Mapper.Extensions;
using Ling.Mapper.Mapper;

namespace TestConsole.Test;

/// <summary>Query materialization owns supported mutable values; provider reference identity is not a graph-mapping contract.</summary>
public static class ProjectionIndependentGraphRegressionTests
{
    public static void Run()
    {
        int assertions = 0;
        void Check(bool condition, string label) { assertions++; if (!condition) throw new Exception(label); }
        void Reject(Action action, string reason)
        {
            assertions++;
            try { action(); }
            catch (ProjectionException error) when (error.Message.Contains(reason, StringComparison.OrdinalIgnoreCase)) { return; }
            throw new Exception("Expected projection diagnostic: " + reason);
        }
        var mapper = new MapperConfiguration().CreateMapper();
        var leaf = new Leaf { Value = 5 };
        var list = new List<Leaf> { leaf, leaf };
        var source = new Graph { First = leaf, Second = leaf, Items = list, EnumerableItems = list, Array = new[] { leaf }, Bytes = new byte[] { 2, 3 }, Value = new Wrapper { Leaf = leaf }, Optional = new Wrapper { Leaf = leaf } };
        var projection = mapper.GetProjection<Graph, Graph>();
        var copy = projection.Compile()(source);
        Check(!ReferenceEquals(source, copy), "root shares source");
        Check(!ReferenceEquals(leaf, copy.First) && !ReferenceEquals(leaf, copy.Second), "nested property shares source");
        Check(!ReferenceEquals(list, copy.Items) && copy.Items![0].Value == 5, "list shares source");
        Check(copy.Items.All(item => !ReferenceEquals(item, leaf)), "list item shares source");
        Check(copy.EnumerableItems is List<Leaf>, "enumerable projection retained a deferred source iterator");
        Check(!ReferenceEquals(copy.Array, source.Array) && !ReferenceEquals(copy.Array![0], leaf), "array shares source");
        Check(!ReferenceEquals(copy.Bytes, source.Bytes), "byte array shares source");
        Check(!ReferenceEquals(copy.Value.Leaf, leaf) && !ReferenceEquals(copy.Optional!.Value.Leaf, leaf), "struct carries source reference");
        leaf.Value = 11; list.Add(new Leaf()); source.Bytes![0] = 7;
        Check(copy.First!.Value == 5 && copy.Items.Count == 2 && copy.EnumerableItems!.Count() == 2 && copy.Bytes![0] == 2, "projection observes later source mutation");
        copy.First.Value = 20;
        Check(leaf.Value == 11, "output mutation affects source");
        var rootList = mapper.GetProjection<List<Leaf>, List<Leaf>>().Compile()(list);
        Check(!ReferenceEquals(rootList, list) && !ReferenceEquals(rootList[0], leaf), "same-list root shares source");
        var rootEnumerable = mapper.GetProjection<List<Leaf>, IEnumerable<Leaf>>().Compile()(list);
        Check(rootEnumerable is List<Leaf> snapshot && !ReferenceEquals(snapshot, list), "root enumerable retains source");
        var valueCopy = mapper.GetProjection<Wrapper, Wrapper>().Compile()(new Wrapper { Leaf = leaf });
        Check(!ReferenceEquals(valueCopy.Leaf, leaf), "same struct root shares source");
        Check(mapper.GetProjection<string, string>().Compile()("immutable") == "immutable", "atomic value");
        var config = new MapperConfiguration(); config.AddProfile(new Rules());
        var configured = config.CreateMapper();
        var selected = configured.GetProjection<Selected, Selected>().Compile()(new Selected { Leaf = leaf });
        Check(!ReferenceEquals(selected.Leaf, leaf) && selected.Leaf!.Value == 12, "MapFrom result did not continue mapping");
        Reject(() => configured.GetProjection<Selected, Final>(), "Final member converters");
        var nullRows = new[] { new NullInput(), new NullInput() }.AsQueryable();
        Reject(() => nullRows.ProjectTo<NullNodeResult>(configured), "Mutable NullSubstitute");
        Reject(() => nullRows.ProjectTo<NullListResult>(configured), "Mutable NullSubstitute");
        var fallbacks = nullRows.ProjectTo<ExplicitFallback>(configured).ToArray();
        Check(!ReferenceEquals(fallbacks[0].Leaf, fallbacks[1].Leaf) && fallbacks[0].Leaf!.Value == 10, "new-object fallback rows share a mutable node");
        Check(!ReferenceEquals(fallbacks[0].Items, fallbacks[1].Items) && !ReferenceEquals(fallbacks[0].Items![0], fallbacks[1].Items![0]), "new-list fallback rows share mutable values");
        fallbacks[0].Leaf!.Value = 30; fallbacks[0].Items![0].Value = 40;
        Check(fallbacks[1].Leaf!.Value == 10 && fallbacks[1].Items![0].Value == 10, "fallback output mutation leaked across rows");
        Reject(() => mapper.GetProjection<Dictionary<string, Leaf>, Dictionary<string, Leaf>>(), "Dictionary");
        Reject(() => mapper.GetProjection<object, object>(), "polymorphism");
        Reject(() => mapper.GetProjection<DynamicGraph, DynamicGraph>(), "polymorphism");
        Reject(() => mapper.GetProjection<MemoryStream, MemoryStream>(), "Resource/delegate");
        Reject(() => mapper.GetProjection<UserStream, OtherUserStream>(), "Resource/delegate");
        Reject(() => mapper.GetProjection<Leaf, AsyncResource>(), "Resource/delegate");
        Reject(() => mapper.GetProjection<FieldValue, FieldValue>(), "field-only");
        Reject(() => mapper.GetProjection<Opaque, Opaque>(), "opaque private");
        Reject(() => mapper.GetProjection<ReadOnlyChild, ReadOnlyChild>(), "Read-only");
        Reject(() => mapper.GetProjection<Cycle, Cycle>(), "Recursive");
        Reject(() => mapper.GetProjection<GenericGrowth<int>, GenericGrowth<int>>(), "generic recursion");
        Console.WriteLine($"Projection independent graphs: {assertions} assertions passed; LINQ-to-Objects only, no SQL-provider identity claim.");
    }
    public sealed class Leaf { public int Value { get; set; } }
    public struct Wrapper { public Leaf? Leaf { get; set; } }
    public sealed class Graph
    {
        public Leaf? First { get; set; } public Leaf? Second { get; set; }
        public List<Leaf>? Items { get; set; } public IEnumerable<Leaf>? EnumerableItems { get; set; }
        public Leaf[]? Array { get; set; } public byte[]? Bytes { get; set; }
        public Wrapper Value { get; set; } public Wrapper? Optional { get; set; }
    }
    public sealed class Selected { public Leaf? Leaf { get; set; } }
    public sealed class Final { public Leaf? Leaf { get; set; } }
    public sealed class NullInput { public Leaf? Leaf { get; set; } public List<Leaf>? Items { get; set; } }
    public sealed class NullNodeResult { public Leaf? Leaf { get; set; } }
    public sealed class NullListResult { public List<Leaf>? Items { get; set; } }
    public sealed class ExplicitFallback { public Leaf? Leaf { get; set; } public List<Leaf>? Items { get; set; } }
    public sealed class UserStream : MemoryStream { }
    public sealed class OtherUserStream : MemoryStream { }
    public sealed class AsyncResource : IAsyncDisposable { public int Value { get; set; } public ValueTask DisposeAsync() => default; }
    public sealed class DynamicGraph { public object? Value { get; set; } }
    public struct FieldValue { public Leaf? Value; }
    public sealed class Opaque { private int hidden = 3; public int Value { get => hidden; set => hidden = value; } }
    public sealed class ReadOnlyChild { public Leaf Child { get; } = new(); }
    public sealed class GenericGrowth<T> { public GenericGrowth<List<T>>? Child { get; set; } }
    public sealed class Cycle { public Cycle? Next { get; set; } }
    private sealed class Rules : MapperProfile
    {
        public Rules()
        {
            CreateMap<Selected, Selected>().ForMember(d => d.Leaf, s => s.Leaf);
            CreateMap<Leaf, Leaf>().ForMember(d => d.Value, s => s.Value + 1);
            CreateMap<Selected, Final>().ForMember(d => d.Leaf, o => o.MapFrom(s => s.Leaf).ConvertUsing<Leaf?>(value => value));
            CreateMap<NullInput, NullNodeResult>().ForMember(d => d.Leaf, o => o.NullSubstitute(new Leaf { Value = 9 }));
            CreateMap<NullInput, NullListResult>().ForMember(d => d.Items, o => o.NullSubstitute(new List<Leaf> { new Leaf { Value = 9 } }));
            CreateMap<NullInput, ExplicitFallback>().ForMember(d => d.Leaf, s => s.Leaf ?? new Leaf { Value = 9 })
                .ForMember(d => d.Items, s => s.Items ?? new List<Leaf> { new Leaf { Value = 9 } });
        }
    }
}
