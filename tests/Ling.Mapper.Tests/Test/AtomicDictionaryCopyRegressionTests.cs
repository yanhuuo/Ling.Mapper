using Ling.Mapper;
using Ling.Mapper.Configuration;
using Ling.Mapper.Models;

namespace TestConsole.Test;

public static class AtomicDictionaryCopyRegressionTests
{
    public static void Run()
    {
        var mapper = new MapperConfiguration().CreateMapper();
        void Check(bool condition, string message) => TestConsole.Utils.TestHelper.AssertTrue(condition, message);
        var ints = Enumerable.Range(0, 100).ToDictionary(i => i, i => i + 1);
        var copied = mapper.Map<Dictionary<int, int>>(ints)!;
        Check(!ReferenceEquals(ints, copied) && copied.Count == ints.Count && ints.All(p => copied[p.Key] == p.Value), "typed atomic dictionary copy");
        copied[0] = 500;
        Check(ints[0] == 1, "atomic dictionary mutation isolation");
        var key = Guid.NewGuid();
        var nullable = new Dictionary<Guid, int?> { [key] = null, [Guid.Empty] = 7 };
        var nullableCopy = mapper.Map<Dictionary<Guid, int?>>(nullable)!;
        Check(!ReferenceEquals(nullable, nullableCopy) && nullableCopy[key] == null && nullableCopy[Guid.Empty] == 7, "Guid keys and nullable scalar values");
        var strings = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase) { ["a"] = "value", ["b"] = null };
        var stringsCopy = mapper.Map<Dictionary<string, string?>>(strings)!;
        Check(stringsCopy.ContainsKey("A") && stringsCopy["A"] == "value" && stringsCopy["B"] == null, "comparer retained with atomic keys");
        int callbacks = 0;
        mapper.Map<Dictionary<int, int>>(ints, AdaptOptions.Strict, (destination, source) =>
        {
            Check(destination is int && Equals(destination, source), "dictionary callback value");
            callbacks++;
        });
        Check(callbacks == ints.Count, "dictionary callback per value");
        callbacks = 0;
        mapper.Map<Dictionary<string, string?>>(strings, AdaptOptions.Strict, (_, _) => callbacks++);
        Check(callbacks == 1, "dictionary null value callback semantics");
        var mutable = new Item { Value = 4 };
        var graph = new Dictionary<int, Item> { [1] = mutable, [2] = mutable };
        var graphCopy = mapper.Map<Dictionary<int, Item>>(graph)!;
        Check(!ReferenceEquals(mutable, graphCopy[1]) && ReferenceEquals(graphCopy[1], graphCopy[2]) && graphCopy[1].Value == 4, "typed keys do not shallow-copy mutable values");
        var self = new Dictionary<string, object>(); self["self"] = self;
        var selfCopy = mapper.Map<Dictionary<string, object>>(self)!;
        Check(!ReferenceEquals(self, selfCopy) && ReferenceEquals(selfCopy, selfCopy["self"]), "typed keys retain dictionary cycles");
        var projected = mapper.Map<Dictionary<int, int>>(ints, AdaptOptions.Strict, ignoreNames: ["unused"]);
        Check(projected != null && projected.Count == ints.Count && projected[0] == 1, "scalar dictionary ignores retain identity semantics");
        var profileConfig = new MapperConfiguration(); profileConfig.AddProfile(new AtomicProfile());
        Check(profileConfig.CreateMapper().Map<Dictionary<int, int>>(ints)![0] == 1, "explicit empty element profile preserved");
        var convertedKeys = mapper.Map<Dictionary<int, int>>(new Dictionary<string, int> { ["7"] = 9 })!;
        Check(convertedKeys[7] == 9, "different key types remain on mapping path");
        try
        {
            mapper.Map<Dictionary<int, int>>(new Dictionary<string, int> { ["1"] = 8, ["01"] = 9 });
            throw new Exception("converted key collision not rejected");
        }
        catch (MappingException error) { Check(error.Path.EndsWith(".Key", StringComparison.Ordinal), "collision Key path retained"); }
        Console.WriteLine("PASS atomic dictionary values, nullable, comparer, callbacks, mutable aliases/cycles, profiles and collisions");
    }
    public sealed class AtomicProfile : MapperProfile { public AtomicProfile() { CreateMap<int, int>(); } }
    public sealed class Item { public int Value { get; set; } }
}
