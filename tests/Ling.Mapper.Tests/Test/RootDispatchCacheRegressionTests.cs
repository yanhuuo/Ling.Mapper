using System.Reflection;
using Ling.Mapper;
using Ling.Mapper.Configuration;
using Ling.Mapper.Models;
using Ling.Mapper.Mapper;
using TestConsole.Utils;

namespace TestConsole.Test;

public static class RootDispatchCacheRegressionTests
{
    public static void Run()
    {
        var mapper = new MapperConfiguration().CreateMapper();
        var a = new SourceA { Value = 7 };
        var b = new SourceB { Value = 11 };
        var first = mapper.Map<TargetA>(a)!;
        var aEntry = Root(mapper);
        var second = mapper.Map<TargetA>(a)!;
        TestHelper.AssertTrue(first.Value == 7 && second.Value == 7 && !ReferenceEquals(first, second), "hot root produces fresh correct data");
        TestHelper.AssertTrue(ReferenceEquals(aEntry, Root(mapper)), "stable request reuses published entry");
        mapper.Map<TargetB>(b);
        var bEntry = Root(mapper);
        TestHelper.AssertTrue(!ReferenceEquals(aEntry, bEntry), "different root shape has distinct plan entry");
        for (var i = 0; i < 100; i++)
        {
            mapper.Map<TargetA>(a);
            TestHelper.AssertTrue(ReferenceEquals(aEntry, Root(mapper)), "alternating A reuses existing plan entry");
            mapper.Map<TargetB>(b);
            TestHelper.AssertTrue(ReferenceEquals(bEntry, Root(mapper)), "alternating B reuses existing plan entry");
        }
        a.Value = 23;
        TestHelper.AssertEqual(23L, mapper.Map<TargetA>(a)!.Value, "inline cache stores plans, not source data");
        var ignore = new[] { "Value" };
        TestHelper.AssertEqual(-1L, mapper.Map<TargetA>(a, AdaptOptions.Strict, ignoreNames: ignore)!.Value, "ignore bypasses ordinary hot root");
        ignore[0] = "Other";
        TestHelper.AssertEqual(23L, mapper.Map<TargetA>(a, AdaptOptions.Strict, ignoreNames: ignore)!.Value, "mutable ignore array remains content keyed");
        TestHelper.AssertEqual(23L, mapper.Map<TargetA>(a, AdaptOptions.Strict, ignoreNames: Array.Empty<string>())!.Value, "empty ignores normalize to default shape");
        var strictEntry = Root(mapper);
        TestHelper.AssertEqual(23L, mapper.Map<TargetA>(a, AdaptOptions.IgnoreCase)!.Value, "option-specific root data");
        TestHelper.AssertTrue(!ReferenceEquals(strictEntry, Root(mapper)), "options are part of inline-cache identity");
        var callbackCount = 0;
        var callbackResult = mapper.Map<TargetA>(a, AdaptOptions.Strict, (_, _) =>
        {
            callbackCount++;
            TestHelper.AssertEqual(11L, mapper.Map<TargetB>(b)!.Value, "callback can reenter another shape");
        });
        TestHelper.AssertTrue(callbackResult!.Value == 23 && callbackCount == 1, "reentry does not duplicate outer callback");
        TestHelper.AssertEqual(23L, mapper.Map<TargetA>(a)!.Value, "outer shape reusable after reentry");
        TestHelper.AssertTrue(mapper.Map<TargetA>(null) == null, "null never invokes cached root");
        var invalid = Failure(() => mapper.Map(a, typeof(SourceB), typeof(TargetA)));
        TestHelper.AssertEqual(typeof(InvalidCastException).FullName, invalid.OriginalExceptionType, "declared mismatch validated before root hint");
        var optionsRejected = false;
        try { mapper.Map<TargetA>(a, (AdaptOptions)128); } catch (ArgumentException) { optionsRejected = true; }
        TestHelper.AssertTrue(optionsRejected, "unsupported options rejected before root hint");

        var configured = new MapperConfiguration(); configured.AddProfile(new DeclaredRules()); var declared = configured.CreateMapper();
        var derived = new DerivedSource { Value = 5 };
        TestHelper.AssertEqual(205, declared.Map<DeclaredTarget>(derived)!.Value, "runtime-derived rule warms root");
        TestHelper.AssertEqual(105, ((DeclaredTarget)declared.Map(derived, typeof(BaseSource), typeof(DeclaredTarget))!).Value, "explicit base rule cannot use derived hint");
        TestHelper.AssertEqual(205, ((DeclaredTarget)declared.Map(derived, typeof(DerivedSource), typeof(DeclaredTarget))!).Value, "explicit exact source uses same rules as generic");

        var concurrent = new MapperConfiguration().CreateMapper();
        Parallel.For(0, 256, i =>
        {
            if (i % 2 == 0 && concurrent.Map<TargetA>(a)!.Value != 23 ||
                i % 2 != 0 && concurrent.Map<TargetB>(b)!.Value != 11) throw new Exception("Concurrent root entry publication mixed shapes");
        });
        TestHelper.AssertTrue(true, "concurrent publication preserves shape/data");

        // Allocation-only probe: alternating warm types must not allocate cache
        // wrappers on each miss. No time or throughput comparison is performed.
        object? retained = null;
        for (var i = 0; i < 1000; i++) { mapper.Map<TargetA>(a); mapper.Map<TargetB>(b); }
        var before = GC.GetAllocatedBytesForCurrentThread();
        for (var i = 0; i < 1000; i++) { retained = new TargetA(); GC.KeepAlive(retained); retained = new TargetB(); GC.KeepAlive(retained); }
        var direct = GC.GetAllocatedBytesForCurrentThread() - before;
        before = GC.GetAllocatedBytesForCurrentThread();
        for (var i = 0; i < 1000; i++) { retained = mapper.Map<TargetA>(a); GC.KeepAlive(retained); retained = mapper.Map<TargetB>(b); GC.KeepAlive(retained); }
        var alternating = GC.GetAllocatedBytesForCurrentThread() - before;
        Console.WriteLine($"Root inline-cache alternating allocation: direct={direct}, mapped={alternating}, count=2000");
        TestHelper.AssertEqual(direct, alternating, "alternating root misses allocate only fresh destinations");
        var oldGeneration = Generation(mapper);
        TypeConverterRegistry.Register(typeof(int), typeof(long), new Func<int, long>(value => value + 100));
        TestHelper.AssertEqual(123L, mapper.Map<TargetA>(a)!.Value, "registry replacement invalidates root hint");
        TestHelper.AssertTrue(!ReferenceEquals(oldGeneration, Generation(mapper)), "hint retires with its generation");
        TestHelper.AssertTrue(!ReferenceEquals(aEntry, Root(mapper)), "new generation cannot retain stale root entry");
        TypeConverterRegistry.Register(typeof(int), typeof(long), new Func<int, long>(value => value + 200));
        TestHelper.AssertEqual(223L, mapper.Map<TargetA>(a)!.Value, "second registry replacement visible");
    }
    private static object Generation(IMapper mapper) => mapper.GetType().GetField("_generation", BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(mapper)!;
    private static object Root(IMapper mapper) { var generation = Generation(mapper); return generation.GetType().GetField("LastRoot", BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(generation)!; }
    private static MappingException Failure(Action action) { try { action(); } catch (MappingException error) { return error; } throw new Exception("Expected MappingException"); }
    public sealed class SourceA { public int Value { get; set; } }
    public sealed class SourceB { public int Value { get; set; } }
    public sealed class TargetA { public long Value { get; set; } = -1; }
    public sealed class TargetB { public long Value { get; set; } = -1; }
    public class BaseSource { public int Value { get; set; } }
    public sealed class DerivedSource : BaseSource { }
    public sealed class DeclaredTarget { public int Value { get; set; } }
    public sealed class DeclaredRules : MapperProfile
    {
        public DeclaredRules()
        {
            CreateMap<BaseSource, DeclaredTarget>().ForMember(d => d.Value, s => s.Value + 100);
            CreateMap<DerivedSource, DeclaredTarget>().ForMember(d => d.Value, s => s.Value + 200);
        }
    }
}
