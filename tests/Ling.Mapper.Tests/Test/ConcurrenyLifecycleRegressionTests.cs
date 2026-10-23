using System.Collections;
using System.Collections.Concurrent;
using System.Reflection;
using System.Runtime.CompilerServices;
using Ling.Mapper;
using Ling.Mapper.Configuration;
using Ling.Mapper.Mapper;
using Ling.Mapper.Provider;

namespace TestConsole.Test;

/// <summary>
/// Lifecycle contracts. Reflection below is limited to generation/plan retention evidence;
/// behavior and publication are checked only through public APIs. GC checks run after
/// no-inline construction frames have returned, without working-set or timing heuristics.
/// </summary>
public static class ConcurrenyLifecycleRegressionTests
{
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(60);
    private static long _assertions;
    public static void Run()
    {
        Interlocked.Exchange(ref _assertions, 0);
        var failures = new List<Exception>(); var cases = 0;
        void Case(string name, Action test)
        {
            cases++;
            try { test(); Console.WriteLine($"PASS lifecycle: {name}"); }
            catch (Exception error) { failures.Add(new Exception(name, error)); Console.WriteLine($"FAIL lifecycle: {name}: {error.Message}"); }
        }
        foreach (var degree in new[] { 1, 2, 8 })
            Case($"provider cold publication degree={degree}", () => ProviderCold(degree));
        foreach (var readers in new[] { 1, 7 })
            Case($"provider Set/Clear/Current readers={readers}", () => ProviderChanges(readers));
        foreach (var writers in new[] { 2, 4 })
            Case($"multiple registry writers={writers}", () => RegistryChanges(writers));
        Case("in-flight converter remains valid across replacement", InFlightReplacement);
        foreach (var replacements in new[] { 64, 256 })
            Case($"retired generation and plan collection replacements={replacements}", () => Retention(replacements));
        Console.WriteLine($"Lifecycle contracts: {cases - failures.Count}/{cases} passed; {Interlocked.Read(ref _assertions)} checked assertions.");
        if (failures.Count != 0) throw new AggregateException("Lifecycle contract failures", failures);
    }

    private static void ProviderCold(int degree)
    {
        var previous = MapperProvider.Current;
        try
        {
            MapperProvider.Clear(); var instances = new ConcurrentBag<IMapper>();
            Workers(degree, (_, meet) => {
                meet();
                var mapper = MapperProvider.Current;
                True(mapper != null, "cold Current must not be null");
                Equal(17, mapper!.Map<ProviderTarget>(new ProviderSource { Number = 17 })!.Number, "published mapper is initialized");
                instances.Add(mapper);
            });
            Equal(degree, instances.Count, "all cold callers returned");
            var current = MapperProvider.Current;
            foreach (var mapper in instances) True(ReferenceEquals(current, mapper), "one cold provider instance published");
            True(!ReferenceEquals(previous, current), "Clear retires previous provider instance");
        }
        finally { MapperProvider.SetCurrent(previous); }
    }

    private static void ProviderChanges(int readers)
    {
        var previous = MapperProvider.Current;
        var configA = new MapperConfiguration(); configA.AddProfile(new ProviderProfile("A"));
        var configB = new MapperConfiguration(); configB.AddProfile(new ProviderProfile("B"));
        var a = configA.CreateMapper(); var b = configB.CreateMapper();
        try
        {
            MapperProvider.SetCurrent(a);
            Workers(readers + 2, (worker, meet) => {
                for (var phase = 0; phase < 48; phase++)
                {
                    meet();
                    if (worker == 0)
                    {
                        for (var i = 0; i < 32; i++) MapperProvider.SetCurrent(i % 2 == 0 ? a : b);
                    }
                    else if (worker == 1)
                    {
                        for (var i = 0; i < 32; i++) MapperProvider.Clear();
                    }
                    else
                    {
                        for (var i = 0; i < 64; i++)
                        {
                            var mapper = MapperProvider.Current; // exactly one public read per operation
                            True(mapper != null, "Current cannot return null during Set/Clear");
                            var value = mapper!.Map<ProviderTarget>(new ProviderSource { Number = phase * 100 + i })!;
                            Equal(phase * 100 + i, value.Number, "provider result number");
                            True(value.Label is "default" or "A" or "B", "provider result belongs to a completely initialized mapper");
                        }
                    }
                    meet();
                }
            });
            // No reader/writer remains. Explicit final Set must be authoritative.
            MapperProvider.SetCurrent(b);
            for (var i = 0; i < 64; i++)
            {
                var current = MapperProvider.Current;
                True(ReferenceEquals(b, current), "final Set converges by identity");
                Equal("B", current.Map<ProviderTarget>(new ProviderSource())!.Label, "final Set converges by configuration");
            }
            MapperProvider.Clear();
            var fresh = MapperProvider.Current;
            True(!ReferenceEquals(a, fresh) && !ReferenceEquals(b, fresh), "final Clear creates a default instance");
            Equal("default", fresh.Map<ProviderTarget>(new ProviderSource())!.Label, "final Clear default configuration");
            True(ReferenceEquals(fresh, MapperProvider.Current), "final default instance remains stable");
        }
        finally { MapperProvider.SetCurrent(previous); }
    }

    private static void RegistryChanges(int writers)
    {
        const int phases = 24;
        var mapper = new MapperConfiguration().CreateMapper();
        RegisterLive(0);
        var source = new LiveSource { Number = 7, Text = "row" };
        CheckLive(mapper.Map<LiveTarget>(source)!, new HashSet<int> { 0 });
        Workers(writers * 2, (worker, meet) => {
            for (var phase = 1; phase <= phases; phase++)
            {
                var legal = new HashSet<int>();
                if (phase == 1) legal.Add(0);
                for (var writer = 1; writer <= writers; writer++)
                {
                    if (phase > 1) legal.Add((phase - 1) * 100 + writer);
                    legal.Add(phase * 100 + writer);
                }
                meet();
                if (worker < writers) RegisterLive(phase * 100 + worker + 1);
                else
                {
                    for (var call = 0; call < 12; call++)
                    {
                        CheckLive(mapper.Map<LiveTarget>(source)!, legal);
                        var graph = mapper.Map<LiveEnvelopeTarget>(new LiveEnvelopeSource { Value = source, Items = new[] { source, source } })!;
                        // Independent members may observe different complete rules. No graph-wide atomicity is promised.
                        CheckLive(graph.Value!, legal); Equal(2, graph.Items!.Count, "concurrent rule list count");
                        foreach (var item in graph.Items) CheckLive(item, legal);
                    }
                }
                meet();
            }
        });
        const int finalVersion = 99999;
        RegisterLive(finalVersion);
        var final = new HashSet<int> { finalVersion };
        Workers(8, (_, meet) => {
            meet();
            for (var call = 0; call < 32; call++)
            {
                CheckLive(mapper.Map<LiveTarget>(source)!, final);
                var graph = mapper.Map<LiveEnvelopeTarget>(new LiveEnvelopeSource { Value = source, Items = new[] { source } })!;
                CheckLive(graph.Value!, final); CheckLive(graph.Items![0], final);
            }
        });
    }
    private static void RegisterLive(int version) => TypeConverterRegistry.Register(typeof(LiveSource), typeof(LiveTarget),
        new Func<LiveSource, LiveTarget>(source => new LiveTarget { Version = version, Number = source.Number + version * 10, Text = source.Text + ":" + version }));
    private static void CheckLive(LiveTarget value, HashSet<int> legal)
    {
        True(legal.Contains(value.Version), "result version must belong to the previous or current phase");
        Equal(7 + value.Version * 10, value.Number, "one complete delegate numeric result");
        Equal("row:" + value.Version, value.Text, "one complete delegate text result");
    }

    private static void InFlightReplacement()
    {
        using var entered = new ManualResetEventSlim(); using var release = new ManualResetEventSlim();
        var mapper = new MapperConfiguration().CreateMapper();
        TypeConverterRegistry.Register(typeof(InFlightSource), typeof(InFlightTarget), new Func<InFlightSource, InFlightTarget>(source => {
            entered.Set();
            if (!release.Wait(Timeout)) throw new TimeoutException("In-flight converter release timed out");
            return new InFlightTarget { Number = source.Number + 100, Text = "old" };
        }));
        var running = Task.Factory.StartNew(() => mapper.Map<InFlightTarget>(new InFlightSource { Number = 7 })!, CancellationToken.None, TaskCreationOptions.LongRunning, TaskScheduler.Default);
        try
        {
            True(entered.Wait(Timeout), "old converter has actually entered before replacement");
            TypeConverterRegistry.Register(typeof(InFlightSource), typeof(InFlightTarget), new Func<InFlightSource, InFlightTarget>(source => new InFlightTarget { Number = source.Number + 200, Text = "new" }));
            var current = mapper.Map<InFlightTarget>(new InFlightSource { Number = 7 })!;
            Equal(207, current.Number, "new calls use replacement while old call is active"); Equal("new", current.Text, "replacement complete result");
        }
        finally { release.Set(); }
        True(running.Wait(Timeout), "old in-flight call completes");
        Equal(107, running.Result.Number, "in-flight invocation keeps old delegate"); Equal("old", running.Result.Text, "old complete result");
        Equal("new", mapper.Map<InFlightTarget>(new InFlightSource())!.Text, "replacement remains current");
    }

    private static void Retention(int replacements)
    {
        // Creation and reflection frames return before collecting: JIT-local liveness cannot retain old generations.
        var evidence = BuildHistory(replacements);
        CollectFully();
        Equal(0, CountAlive(evidence.RetiredGenerations), "all retired generations collect after calls finish");
        Equal(0, CountAlive(evidence.RetiredPlans), "all retired root plans collect after calls finish");
        Equal(0, CountAlive(evidence.RetiredOwners), "superseded converter closures collect");
        True(evidence.CurrentGeneration.IsAlive, "current generation must remain retained");
        True(evidence.CurrentPlan.IsAlive, "current plan must remain retained");
        Equal(1, CurrentPlanCount(evidence.Mapper), "fixed type pair retains one current plan after repeated registration");
        var result = evidence.Mapper.Map<RetainedTarget>(new RetainedSource { Number = 7 })!;
        Equal(replacements + 7, result.Number, "surviving mapper remains functional");
        Equal("v" + replacements, result.Text, "surviving final converter");
        // This is a fixed-key replacement bound, not a claim that arbitrary new mapping keys have bounded storage.
        GC.KeepAlive(evidence.Mapper);
    }
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static RetentionEvidence BuildHistory(int replacements)
    {
        var mapper = new MapperConfiguration().CreateMapper();
        var generations = new List<WeakReference>(); var plans = new List<WeakReference>(); var owners = new List<WeakReference>();
        for (var version = 0; version < replacements; version++)
        {
            owners.Add(RegisterRetained(version));
            Equal(version + 7, mapper.Map<RetainedTarget>(new RetainedSource { Number = 7 })!.Number, "historical rule result");
            var capture = CaptureGeneration(mapper); generations.Add(capture.Generation); plans.Add(capture.Plan);
            Equal(1, CurrentPlanCount(mapper), "each fixed-key generation has one plan");
        }
        _ = RegisterRetained(replacements);
        Equal(replacements + 7, mapper.Map<RetainedTarget>(new RetainedSource { Number = 7 })!.Number, "last replacement result");
        var current = CaptureGeneration(mapper);
        return new RetentionEvidence(mapper, generations.ToArray(), plans.ToArray(), owners.ToArray(), current.Generation, current.Plan);
    }
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static WeakReference RegisterRetained(int version)
    {
        var owner = new ConverterOwner(version);
        TypeConverterRegistry.Register(typeof(RetainedSource), typeof(RetainedTarget), new Func<RetainedSource, RetainedTarget>(owner.Convert));
        return new WeakReference(owner);
    }
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static (WeakReference Generation, WeakReference Plan) CaptureGeneration(IMapper mapper)
    {
        var generation = Generation(mapper); var plans = Plans(generation);
        var values = (IEnumerable)(plans.GetType().GetProperty("Values")?.GetValue(plans) ?? throw new InvalidOperationException("Plan Values unavailable"));
        var iterator = values.GetEnumerator();
        try
        {
            True(iterator.MoveNext(), "current generation contains a plan");
            var plan = iterator.Current ?? throw new InvalidOperationException("Null mapping plan");
            True(!iterator.MoveNext(), "retention fixture has exactly one mapping key");
            return (new WeakReference(generation), new WeakReference(plan));
        }
        finally { (iterator as IDisposable)?.Dispose(); }
    }
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static int CurrentPlanCount(IMapper mapper)
    {
        var plans = Plans(Generation(mapper));
        return (int)(plans.GetType().GetProperty("Count")?.GetValue(plans) ?? throw new InvalidOperationException("Plan Count unavailable"));
    }
    private static object Generation(IMapper mapper) => mapper.GetType().GetField("_generation", BindingFlags.Instance | BindingFlags.NonPublic)?.GetValue(mapper)
        ?? throw new InvalidOperationException("Generation layout changed; update the test-only retention probe");
    private static object Plans(object generation) => generation.GetType().GetField("Plans", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public)?.GetValue(generation)
        ?? throw new InvalidOperationException("Generation Plans layout changed; update the test-only retention probe");
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static int CountAlive(WeakReference[] references) { var count = 0; foreach (var reference in references) if (reference.IsAlive) count++; return count; }
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void CollectFully()
    {
        GC.Collect(GC.MaxGeneration, GCCollectionMode.Forced, blocking: true, compacting: true);
        GC.WaitForPendingFinalizers();
        GC.Collect(GC.MaxGeneration, GCCollectionMode.Forced, blocking: true, compacting: true);
    }

    private static void Workers(int count, Action<int, Action> work)
    {
        using var stop = new CancellationTokenSource(); using var barrier = new Barrier(count);
        void Meet() { if (!barrier.SignalAndWait(Timeout, stop.Token)) throw new TimeoutException("Lifecycle barrier timed out"); }
        var tasks = Enumerable.Range(0, count).Select(worker => Task.Factory.StartNew(() => {
            try { work(worker, Meet); }
            catch { stop.Cancel(); throw; }
        }, CancellationToken.None, TaskCreationOptions.LongRunning, TaskScheduler.Default)).ToArray();
        if (!Task.WaitAll(tasks, Timeout)) { stop.Cancel(); throw new TimeoutException("Lifecycle workers timed out"); }
    }
    private static void Equal<T>(T expected, T actual, string label) { Interlocked.Increment(ref _assertions); if (!EqualityComparer<T>.Default.Equals(expected, actual)) throw new InvalidOperationException($"{label}: expected '{expected}', actual '{actual}'"); }
    private static void True(bool value, string label) { Interlocked.Increment(ref _assertions); if (!value) throw new InvalidOperationException(label); }

    private sealed record RetentionEvidence(IMapper Mapper, WeakReference[] RetiredGenerations, WeakReference[] RetiredPlans, WeakReference[] RetiredOwners, WeakReference CurrentGeneration, WeakReference CurrentPlan);
    private sealed class ConverterOwner(int version)
    {
        public RetainedTarget Convert(RetainedSource source) => new() { Number = source.Number + version, Text = "v" + version };
    }
    public sealed class ProviderSource { public int Number { get; set; } }
    public sealed class ProviderTarget { public int Number { get; set; } public string? Label { get; set; } = "default"; }
    public sealed class ProviderProfile : MapperProfile { public ProviderProfile(string label) { CreateMap<ProviderSource, ProviderTarget>().ForMember(d => d.Label, s => label); } }
    public sealed class LiveSource { public int Number { get; set; } public string? Text { get; set; } }
    public sealed class LiveTarget { public int Version { get; set; } public int Number { get; set; } public string? Text { get; set; } }
    public sealed class LiveEnvelopeSource { public LiveSource? Value { get; set; } public LiveSource[]? Items { get; set; } }
    public sealed class LiveEnvelopeTarget { public LiveTarget? Value { get; set; } public List<LiveTarget>? Items { get; set; } }
    public sealed class InFlightSource { public int Number { get; set; } }
    public sealed class InFlightTarget { public int Number { get; set; } public string? Text { get; set; } }
    public sealed class RetainedSource { public int Number { get; set; } }
    public sealed class RetainedTarget { public int Number { get; set; } public string? Text { get; set; } }
}
