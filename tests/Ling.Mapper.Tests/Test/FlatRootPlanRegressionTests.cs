using System.Globalization;
using Ling.Mapper;
using Ling.Mapper.Configuration;
using Ling.Mapper.Models;
using TestConsole.Utils;

namespace TestConsole.Test;

public static class FlatRootPlanRegressionTests
{
    public static void Run()
    {
        var config = new MapperConfiguration { IncludeSourceValueInExceptions = true };
        var mapper = config.CreateMapper();
        var source = new Source { Value = 42, Name = "kept" };
        var a = mapper.Map<Target>(source)!;
        var b = (Target)mapper.Map(source, typeof(Source), typeof(Target))!;
        TestHelper.AssertTrue(a.Value == 42 && b.Value == 42 && a.Name == "kept" && !ReferenceEquals(a, b), "typed/untyped root returns fresh correct data");
        var target = new Target { Value = -1 };
        TestHelper.AssertTrue(ReferenceEquals(target, mapper.MapInto(source, target)) && target.Value == 42, "MapInto remains an update");
        TestHelper.AssertTrue(mapper.Map<Target>(null) == null, "root null remains null");
        var callbackCount = 0;
        mapper.Map<Target>(source, AdaptOptions.Strict, (d, s) => { callbackCount++; TestHelper.AssertTrue(ReferenceEquals(s, source), "callback source"); });
        TestHelper.AssertEqual(1, callbackCount, "callback once");
        TestHelper.AssertEqual(-1L, mapper.Map<Target>(source, AdaptOptions.Strict, ignoreNames: new[] { "Value" })!.Value, "ignored member retains default");
        TestHelper.AssertEqual("seed", mapper.Map<Target>(new Source(), AdaptOptions.IgnoreNullValues)!.Name, "ignore null retains constructor value");

        var shared = new Source { Value = 8 };
        var graph = mapper.Map<GraphTarget>(new GraphSource { First = shared, Second = shared })!;
        TestHelper.AssertTrue(ReferenceEquals(graph.First, graph.Second), "flat child in graph preserves aliases");
        TestHelper.AssertTrue(!ReferenceEquals(shared, graph.First), "flat child is not source");
        Error(() => mapper.Map<ByteTarget>(new Source { Value = 1000 }), "$.Value", typeof(OverflowException), typeof(int));
        ThrowingGetter.Reads = 0;
        Error(() => mapper.Map<Target>(new ThrowingGetter()), "$.Value", typeof(InvalidOperationException), typeof(int));
        TestHelper.AssertEqual(1, ThrowingGetter.Reads, "getter not replayed on diagnostic path");
        ThrowingSetter.Writes = 0;
        Error(() => mapper.Map<ThrowingSetter>(source), "$.Value", typeof(InvalidOperationException), typeof(int));
        TestHelper.AssertEqual(1, ThrowingSetter.Writes, "setter not replayed on diagnostic path");
        ThrowingConstructor.Calls = 0;
        Error(() => mapper.Map<ThrowingConstructor>(source), "$", typeof(InvalidOperationException), typeof(Source));
        TestHelper.AssertEqual(1, ThrowingConstructor.Calls, "constructor not replayed on diagnostic path");
        var foreign = Failure(() => mapper.Map<ByteTarget>(new Source { Value = 1000 }));
        Error(() => mapper.Map<Target>(source, AdaptOptions.Strict, (_, _) => throw foreign), "$", typeof(MappingException), typeof(Source));

        var culture = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("fr-FR");
            TestHelper.AssertEqual("1,5", mapper.Map<TextTarget>(new NumberSource { Value = 1.5m })!.Value, "runtime fr culture");
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("en-US");
            TestHelper.AssertEqual("1.5", mapper.Map<TextTarget>(new NumberSource { Value = 1.5m })!.Value, "same plan runtime en culture");
        }
        finally { CultureInfo.CurrentCulture = culture; }

        var rules = new MapperConfiguration(); rules.AddProfile(new Rules()); var configured = rules.CreateMapper();
        CustomSource.Calls = 0;
        TestHelper.AssertEqual(15, configured.Map<CustomTarget>(new CustomSource { Value = 5 })!.Value, "custom scalar pipeline");
        TestHelper.AssertEqual(1, CustomSource.Calls, "custom selector once");
        TestHelper.AssertEqual(-1, configured.Map<CustomTarget>(new CustomSource { Value = -1 })!.Value, "condition rejects candidate");
        TestHelper.AssertEqual(2, CustomSource.Calls, "selector still once for rejected candidate");
        TypeConverterRegistry.Register(typeof(int), typeof(long), new Func<int, long>(v => v + 100));
        TestHelper.AssertEqual(142L, mapper.Map<Target>(source)!.Value, "registry generation invalidates warmed root plan");
        TypeConverterRegistry.Register(typeof(int), typeof(long), new Func<int, long>(v => v + 200));
        TestHelper.AssertEqual(242L, mapper.Map<Target>(source)!.Value, "registry replacement invalidates root plan again");

        var strictNull = new MapperConfiguration { NullValueHandling = NullValueHandling.Throw }.CreateMapper();
        Error(() => strictNull.Map<AllocationTarget>(new AllocationSource()), "$.Value", typeof(InvalidCastException), typeof(int?));
        TestHelper.AssertEqual(0, mapper.Map<AllocationTarget>(new AllocationSource())!.Value, "nullable scalar default policy");

        // Isolated allocation evidence, not timing. Root plans retain only plan
        // state: the same warmed request allocates exactly its fresh DTO.
        var allocationMapper = new MapperConfiguration().CreateMapper();
        var allocationSource = new AllocationSource { Value = 19 };
        object? retained = null;
        for (var i = 0; i < 1000; i++) retained = allocationMapper.Map<AllocationTarget>(allocationSource);
        var before = GC.GetAllocatedBytesForCurrentThread();
        for (var i = 0; i < 1000; i++) retained = new AllocationTarget { Value = 19 };
        var direct = GC.GetAllocatedBytesForCurrentThread() - before;
        GC.KeepAlive(retained);
        before = GC.GetAllocatedBytesForCurrentThread();
        for (var i = 0; i < 1000; i++) retained = allocationMapper.Map<AllocationTarget>(allocationSource);
        var mapped = GC.GetAllocatedBytesForCurrentThread() - before;
        GC.KeepAlive(retained);
        Console.WriteLine($"Flat root allocation: direct={direct}, mapped={mapped}, count=1000");
        TestHelper.AssertEqual(direct, mapped, "flat root allocates only destination, no MappingContext");
    }
    private static MappingException Failure(Action action) { try { action(); } catch (MappingException error) { return error; } throw new Exception("Expected MappingException"); }
    private static void Error(Action action, string path, Type original, Type source)
    {
        var error = Failure(action);
        TestHelper.AssertEqual(path, error.Path, "failure path");
        TestHelper.AssertEqual(original.FullName, error.OriginalExceptionType, "failure original type");
        TestHelper.AssertEqual(source, error.SourceType, "failure source type");
    }
    public sealed class Source { public int Value { get; set; } public string? Name { get; set; } }
    public sealed class Target { public long Value { get; set; } = -1; public string? Name { get; set; } = "seed"; }
    public sealed class ByteTarget { public byte Value { get; set; } }
    public sealed class GraphSource { public Source? First { get; set; } public Source? Second { get; set; } }
    public sealed class GraphTarget { public Target? First { get; set; } public Target? Second { get; set; } }
    public sealed class ThrowingGetter { public static int Reads; public int Value { get { Reads++; throw new InvalidOperationException(); } } }
    public sealed class ThrowingSetter { public static int Writes; public int Value { set { Writes++; throw new InvalidOperationException(); } } }
    public sealed class ThrowingConstructor { public static int Calls; public ThrowingConstructor() { Calls++; throw new InvalidOperationException(); } public int Value { get; set; } }
    public sealed class NumberSource { public decimal Value { get; set; } }
    public sealed class TextTarget { public string? Value { get; set; } }
    public sealed class CustomSource { public static int Calls; public int Value { get; set; } public int Select() { Calls++; return Value + 10; } }
    public sealed class CustomTarget { public int Value { get; set; } = -1; }
    public sealed class Rules : MapperProfile { public Rules() { CreateMap<CustomSource, CustomTarget>().ForMember(d => d.Value, o => o.MapFrom(s => s.Select()).Condition((s, d, value) => s.Value > 0)); } }
    public sealed class AllocationSource { public int? Value { get; set; } }
    public sealed class AllocationTarget { public int Value { get; set; } }
}
