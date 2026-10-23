using Ling.Mapper;
using Ling.Mapper.Configuration;
using Ling.Mapper.Extensions;
using Ling.Mapper.Models;
using Ling.Mapper.Provider;
using static TestConsole.Utils.TestHelper;

namespace TestConsole.Test;

/// <summary>Deterministic assertions complement the older interactive demonstrations.</summary>
public static class LegacyContractRegressionTests
{
    public static void Run()
    {
        RunTest(TestProfile, "existing profile/JSON/ignore/nested mapping");
        RunTest(TestOptions, "explicit matching options and ignore cache isolation");
        RunTest(TestCallbacks, "object and list item callbacks");
        RunTest(TestCycles, "cycles and operation isolation");
        RunTest(TestAutoInitialize, "provider auto initialization and collections");
    }
    private static void TestProfile()
    {
        // The runner initializes the same profiles as the original console entry point.
        var source = new ActivityDto { FirstName = "Tom", LastName = "Lee", Uid = 1001,
            ExtraInfoJson = "{\"Level\":3,\"Tag\":\"VIP\"}",
            User = new ActivityUserDto { NickName = "nick" },
            Items = new List<ActivityItemDto> { new() { Key = "A", Price = 99 } } };
        var result = source.Adapt<MallActivityEntity>();
        AssertNotNull(result, "profile result");
        AssertEqual("Tom Lee", result.Name, "ForMember");
        AssertEqual(1001, result.UserId, "Rename");
        // ActivityProfile intentionally does not rename ExtraInfoJson to ExtraInfo.
        var json = new { ExtraInfo = source.ExtraInfoJson }.Adapt<JsonTarget>();
        AssertEqual(3, json.ExtraInfo?.Level, "JSON");
        AssertEqual("VIP", json.ExtraInfo?.Tag, "JSON tag");
        AssertEqual("nick", result.User?.NickName, "nested object");
        AssertEqual(1, result.Items?.Count, "nested collection");
        AssertNull(result.InternalCode, "ignored property");
    }
    private static void TestOptions()
    {
        var source = new { Id = 7, user_name = "Alice" };
        AssertEqual("Alice", source.Adapt<Target>(AdaptOptions.Default).UserName, "default flexible");
        AssertNull(source.Adapt<Target>(AdaptOptions.Strict).UserName, "strict does not fold names");
        AssertNull(source.Adapt<Target>(AdaptOptions.IgnoreUnderscore).UserName, "underscore does not fold case");
        AssertEqual("Alice", source.Adapt<Target>(AdaptOptions.FlexibleOption).UserName, "option cache isolation");
        AssertEqual(0, source.Adapt<Target>("Id").Id, "explicit ignored Id");
        AssertEqual(7, source.Adapt<Target>().Id, "ignore cache isolation");
    }
    private static void TestCallbacks()
    {
        int calls = 0;
        var source = new Source { Id = 3, UserName = "before" };
        var result = source.Adapt<Target, Source>((dest, src) => { calls++; dest.UserName = src.UserName + "!"; });
        AssertEqual(1, calls, "single callback count");
        AssertEqual("before!", result.UserName, "single callback value");
        calls = 0;
        var list = new[] { source, new Source { Id = 4 } }.Adapt<Target, Source>((dest, src) => { calls++; dest.Id = src.Id + 10; });
        AssertEqual(2, calls, "item callback count");
        AssertEqual(13, list[0].Id, "first item");
        AssertEqual(14, list[1].Id, "second item");
    }
    private static void TestCycles()
    {
        var source = new Node { Id = 1 };
        source.Next = source;
        var result = source.Adapt<NodeTarget>();
        AssertTrue(ReferenceEquals(result, result.Next), "self cycle identity");
        var second = source.Adapt<NodeTarget>();
        AssertFalse(ReferenceEquals(result, second), "independent map contexts");
    }
    private static void TestAutoInitialize()
    {
        MapperProvider.Clear();
        AssertEqual(5, new Source { Id = 5 }.Adapt<Target>().Id, "auto initialize object");
        MapperProvider.Clear();
        var list = new[] { new Source { Id = 6 }, new Source { Id = 7 } }.Adapt<List<Target>>();
        AssertEqual(2, list.Count, "auto initialize list");
        AssertEqual(7, list[1].Id, "auto initialize item");
    }
    public sealed class JsonTarget { public ExtraInfoModel? ExtraInfo { get; set; } }
    public sealed class Source { public int Id { get; set; } public string? UserName { get; set; } }
    public sealed class Target { public int Id { get; set; } public string? UserName { get; set; } }
    public sealed class Node { public int Id { get; set; } public Node? Next { get; set; } }
    public sealed class NodeTarget { public int Id { get; set; } public NodeTarget? Next { get; set; } }
}
