using System.Collections.Concurrent;
using System.Globalization;
using Ling.Mapper;
using Ling.Mapper.Configuration;
using Ling.Mapper.Mapper;
using Ling.Mapper.Models;

namespace TestConsole.Test;

/// <summary>Deterministic parallel contracts: no sleeps, timing assertions, or shared mutable input.</summary>
public static class ConcurrencyRegressionTests
{
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(60);
    private static long _assertions;

    public static void Run()
    {
        Interlocked.Exchange(ref _assertions, 0);
        var failures = new List<Exception>();
        var count = 0;
        void Case(string name, Action test)
        {
            count++;
            try { test(); Console.WriteLine($"PASS concurrency: {name}"); }
            catch (Exception error) { failures.Add(new Exception(name, error)); Console.WriteLine($"FAIL concurrency: {name}: {error.Message}"); }
        }
        foreach (var degree in new[] { 1, 2, 8 })
        {
            Case($"warm mixed values degree={degree}", () => Warm(degree));
            Case($"barrier first compilation degree={degree}", () => Cold(degree));
            Case($"types options ignores degree={degree}", () => Options(degree));
            Case($"nested collections degree={degree}", () => Collections(degree));
            Case($"cycles shared references isolated roots degree={degree}", () => Graphs(degree));
            Case($"failed calls do not poison context degree={degree}", () => Recovery(degree));
            Case($"mapper configurations isolated degree={degree}", () => Configurations(degree));
            Case($"custom rules refresh warm plans degree={degree}", () => Rules(degree));
            Case($"parallel callbacks isolated degree={degree}", () => Callbacks(degree));
        }
        Console.WriteLine($"Concurrency contracts: {count - failures.Count}/{count} passed; {Interlocked.Read(ref _assertions)} checked assertions.");
        if (failures.Count != 0) throw new AggregateException("Concurrency contract failures", failures);
    }

    private static void Warm(int degree)
    {
        var mapper = new MapperConfiguration().CreateMapper();
        CheckRow(Row(0), mapper.Map<ValueTarget>(Row(0))!);
        Parallel(degree, (worker, iteration) => {
            var source = Row(worker * 1000 + iteration);
            var result = mapper.Map<ValueTarget>(source)!;
            CheckRow(source, result);
            NotSame(source, result, "DTO must be mapped, not assigned");
            var cloned = mapper.Map<ValueSource>(source)!;
            NotSame(source, cloned, "assignable object is fresh");
            Equal(source.Id, cloned.Id, "cloned id"); Equal(source.Name, cloned.Name, "cloned name");
            Equal(source.Amount, cloned.Amount, "cloned amount"); Equal(source.Enabled, cloned.Enabled, "cloned enabled");
            Equal(source.State, cloned.State, "cloned enum text"); Equal(source.Optional, cloned.Optional, "cloned optional text");
            var originalName = source.Name; cloned.Name = "target-only"; Equal(originalName, source.Name, "target mutation isolated");
            var clonedAmount = cloned.Amount; source.Amount = "source-only"; Equal(clonedAmount, cloned.Amount, "source mutation isolated");
            Equal(null, mapper.Map<int?>(null), "nullable root");
            Equal(0, mapper.Map<int>(null), "default root");
            Equal(12.75m, mapper.Map<decimal>("12.75"), "scalar amount");
        });
    }

    private static void Cold(int degree)
    {
        // A fresh instance has no plans for these DTOs. Barrier releases all callers together.
        var mapper = new MapperConfiguration().CreateMapper();
        Parallel(degree, (worker, iteration) => {
            var source = Row(worker * 1000 + iteration);
            CheckRow(source, mapper.Map<ValueTarget>(source)!);
        }, iterations: 1);
    }

    private static void Options(int degree)
    {
        var mapper = new MapperConfiguration().CreateMapper();
        Parallel(degree, (worker, iteration) => {
            var id = worker * 1000 + iteration + 1;
            var source = new NamingSource { Id = id, user_name = "user-" + id, Note = null };
            var mode = iteration % 4;
            var options = mode == 0 ? AdaptOptions.Strict : AdaptOptions.Default;
            if (mode == 3) options |= AdaptOptions.IgnoreNullValues;
            var ignores = mode == 2 ? new[] { "Id", "UserName" } : null;
            var result = mapper.Map<NamingTarget>(source, options, ignoreNames: ignores)!;
            Equal(mode == 2 ? 0 : id, result.Id, "ignored numeric member");
            Equal(mode is 0 or 2 ? null : source.user_name, result.UserName, "name matching");
            Equal(mode == 3 ? "initializer" : null, result.Note, "ignore null cache key");
            var alternate = (NamingTarget2)mapper.Map(source, typeof(NamingSource), typeof(NamingTarget2), AdaptOptions.Default)!;
            Equal(id, alternate.Id, "different destination type");
            Equal(source.user_name, alternate.UserName, "non-generic mapping");
            Equal(id, mapper.Map<int>(id.ToString(CultureInfo.InvariantCulture)), "different source type");
        });
    }

    private static void Collections(int degree)
    {
        var mapper = new MapperConfiguration().CreateMapper();
        Parallel(degree, (worker, iteration) => {
            var seed = worker * 1000 + iteration;
            var source = new BatchSource { Name = "batch-" + seed, Detail = Row(seed), Items = new[] { Row(seed + 1), Row(seed + 2) } };
            var result = mapper.Map<BatchTarget>(source)!;
            Equal(source.Name, result.Name, "batch name");
            CheckRow(source.Detail!, result.Detail!);
            Equal(2, result.Items!.Count, "list count");
            CheckRow(source.Items![0], result.Items[0]); CheckRow(source.Items[1], result.Items[1]);
            NotSame(source.Items, result.Items, "converted collection identity");
            NotSame(result.Items[0], result.Items[1], "different input elements");
            var array = mapper.Map<ValueTarget[]>(source.Items)!;
            Equal(2, array.Length, "array count"); CheckRow(source.Items[1], array[1]);
            var direct = new List<int> { seed, seed + 1 };
            var copied = mapper.Map<IEnumerable<int>>(direct)!;
            NotSame(direct, copied, "assignable collection is fresh");
            Equal(2, copied.Count(), "assignable collection count"); Equal(seed, copied.First(), "assignable collection first value"); Equal(seed + 1, copied.Last(), "assignable collection last value");
            direct[0] = seed + 10000; Equal(seed, copied.First(), "source collection mutation isolated");
            var numbers = mapper.Map<List<int?>>(new string?[] { "17", null, "29" })!;
            Equal(17, numbers[0], "nullable element"); Equal(null, numbers[1], "null element"); Equal(29, numbers[2], "last element");
        });
    }

    private static void Graphs(int degree)
    {
        var mapper = new MapperConfiguration().CreateMapper();
        var child = new GraphSource { Id = 23, Name = "shared" };
        var source = new GraphSource { Id = 11, Name = "root", Left = child, Right = child, Items = new[] { child, child } };
        source.Next = source; child.Next = source;
        var roots = new ConcurrentBag<GraphTarget>();
        Parallel(degree, (_, _) => {
            var result = mapper.Map<GraphTarget>(source)!;
            Equal(11, result.Id, "root id"); Equal("root", result.Name, "root name");
            Equal(23, result.Left!.Id, "child id"); Equal("shared", result.Left.Name, "child name");
            Same(result, result.Next, "self cycle"); Same(result.Left, result.Right, "shared child");
            Same(result, result.Left.Next, "two-node back edge");
            Same(result.Left, result.Items![0], "shared reference across property and list");
            Same(result.Items[0], result.Items[1], "repeated list reference");
            NotSame(source, result, "different type root"); NotSame(child, result.Left, "different type child");
            roots.Add(result);
        }, iterations: 12);
        var seen = new HashSet<GraphTarget>(ReferenceEqualityComparer.Instance);
        var children = new HashSet<GraphTarget>(ReferenceEqualityComparer.Instance);
        foreach (var root in roots)
        {
            True(seen.Add(root), "same source repeated maps must have separate root context");
            True(children.Add(root.Left!), "child instances must not leak across calls");
        }
        Equal(degree * 12, seen.Count, "completed graph mappings");
        Equal(11, source.Id, "source unchanged"); Same(child, source.Left, "source graph unchanged");
    }

    private static void Recovery(int degree)
    {
        var mapper = new MapperConfiguration().CreateMapper();
        Parallel(degree, (worker, iteration) => {
            var source = new BatchSource { Items = new[] { Row(1), Row(2) } };
            source.Items[1].Amount = "invalid-amount";
            try { mapper.Map<BatchTarget>(source); throw new InvalidOperationException("Invalid amount unexpectedly succeeded"); }
            catch (MappingException error) { Equal("$.Items[1].Amount", error.Path, "failure path isolated"); }
            // Repair and map the exact same graph: partially cached destinations must not escape.
            source.Items[1].Amount = "2.25";
            var result = mapper.Map<BatchTarget>(source)!;
            Equal(2.25m, result.Items![1].Amount, "retry after partial graph failure");
            CheckRow(source.Items[0], result.Items[0]);
            try { mapper.Map<int>("bad"); throw new InvalidOperationException("Invalid root unexpectedly succeeded"); }
            catch (MappingException error) { Equal("$", error.Path, "next root error path"); }
            Equal(worker + iteration, mapper.Map<int>((worker + iteration).ToString(CultureInfo.InvariantCulture)), "successful next root");
        });
    }

    private static void Configurations(int degree)
    {
        var flexible = new MapperConfiguration().CreateMapper();
        var strict = new MapperConfiguration { DefaultAdaptOptions = AdaptOptions.Strict, NullValueHandling = NullValueHandling.Throw }.CreateMapper();
        var a = new MapperConfiguration(); a.AddProfile(new LabelProfile("A:"));
        var b = new MapperConfiguration(); b.AddProfile(new LabelProfile("B:"));
        var first = a.CreateMapper(); var second = b.CreateMapper();
        Parallel(degree, (worker, iteration) => {
            var source = new NamingSource { Id = iteration, user_name = "name" };
            Equal("name", flexible.Map<NamingTarget>(source)!.UserName, "flexible mapper");
            Equal(null, strict.Map<NamingTarget>(source)!.UserName, "strict mapper");
            Equal(0, flexible.Map<int>(null), "default null mapper");
            try { strict.Map(null, typeof(string), typeof(int)); throw new InvalidOperationException("Strict null unexpectedly succeeded"); }
            catch (MappingException error) { Equal("$", error.Path, "strict null path"); }
            Equal("A:name", first.Map<NamingTarget>(source)!.UserName, "profile A");
            Equal("B:name", second.Map<NamingTarget>(source)!.UserName, "profile B");
        });
    }

    private static void Rules(int degree)
    {
        var mapper = new MapperConfiguration().CreateMapper();
        // Unique test types avoid altering other suites' converter registrations.
        for (var generation = 1; generation <= 3; generation++)
        {
            var version = generation;
            TypeConverterRegistry.Register(typeof(RuleSource), typeof(RuleTarget),
                new Func<RuleSource, RuleTarget>(source => new RuleTarget { Number = source.Number + version * 100, Text = version + ":" + source.Text }));
            // Registration is complete before workers start. No linearizability claim across updates.
            Parallel(degree, (worker, iteration) => {
                var source = new RuleSource { Number = worker + iteration, Text = "token-" + iteration };
                var root = mapper.Map<RuleTarget>(source)!;
                Equal(source.Number + version * 100, root.Number, "updated root converter");
                Equal(version + ":" + source.Text, root.Text, "updated root text"); NotSame(source, root, "rule result identity");
                var nested = mapper.Map<RuleEnvelopeTarget>(new RuleEnvelopeSource { Value = source, Items = new[] { source } })!;
                Equal(root.Number, nested.Value!.Number, "updated warm nested plan");
                Equal(root.Text, nested.Items![0].Text, "updated warm collection plan");
            });
        }
    }

    private static void Callbacks(int degree)
    {
        var mapper = new MapperConfiguration().CreateMapper();
        Parallel(degree, (worker, iteration) => {
            var input = new[] { Row(worker + iteration), Row(worker + iteration + 1) };
            var calls = 0;
            var result = mapper.Map<List<ValueTarget>>(input, AdaptOptions.Default, (destination, source) => {
                Interlocked.Increment(ref calls);
                var target = (ValueTarget)destination; var original = (ValueSource)source;
                Equal(original.Id, target.Id, "callback sees own source");
                target.Name = original.Name + ":callback";
            })!;
            Equal(2, calls, "per-call callback count");
            Equal(input[0].Name + ":callback", result[0].Name, "first callback value");
            Equal(input[1].Name + ":callback", result[1].Name, "second callback value");
            Equal(input[0].Name, mapper.Map<ValueTarget>(input[0])!.Name, "callback does not leak into cached plan");
        });
    }

    private static ValueSource Row(int n) => new() { Id = n, Name = "row-" + n, Amount = (n + 0.25m).ToString(CultureInfo.InvariantCulture), Enabled = n % 2 == 0 ? "true" : "false", State = n % 2 == 0 ? "Ready" : "Waiting", Optional = n % 3 == 0 ? null : (n + 7).ToString(CultureInfo.InvariantCulture) };
    private static void CheckRow(ValueSource source, ValueTarget result)
    {
        Equal(source.Id, result.Id, "id"); Equal(source.Name, result.Name, "name");
        Equal(decimal.Parse(source.Amount!, CultureInfo.InvariantCulture), result.Amount, "amount");
        Equal(bool.Parse(source.Enabled!), result.Enabled, "boolean"); Equal(Enum.Parse<State>(source.State!), result.State, "enum");
        Equal(source.Optional == null ? (int?)null : int.Parse(source.Optional, CultureInfo.InvariantCulture), result.Optional, "nullable");
    }

    private static void Parallel(int degree, Action<int, int> action, int iterations = 24)
    {
        using var start = new Barrier(degree);
        using var stop = new CancellationTokenSource();
        var tasks = Enumerable.Range(0, degree).Select(worker => Task.Factory.StartNew(() => {
            if (!start.SignalAndWait(Timeout, stop.Token)) throw new TimeoutException("Concurrent start barrier timed out");
            for (var iteration = 0; iteration < iterations; iteration++)
            {
                stop.Token.ThrowIfCancellationRequested();
                try { action(worker, iteration); }
                catch (Exception error) { throw new InvalidOperationException($"worker={worker}, iteration={iteration}", error); }
            }
        }, CancellationToken.None, TaskCreationOptions.LongRunning, TaskScheduler.Default)).ToArray();
        // Dedicated workers avoid thread-pool starvation while waiting at the barrier.
        if (!Task.WaitAll(tasks, Timeout))
        {
            stop.Cancel();
            throw new TimeoutException($"Concurrent operations exceeded {Timeout.TotalSeconds} seconds (degree={degree})");
        }
    }
    private static void Equal<T>(T expected, T actual, string label) { Interlocked.Increment(ref _assertions); if (!EqualityComparer<T>.Default.Equals(expected, actual)) throw new InvalidOperationException($"{label}: expected '{expected}', actual '{actual}'"); }
    private static void True(bool value, string label) { Interlocked.Increment(ref _assertions); if (!value) throw new InvalidOperationException(label); }
    private static void Same(object? expected, object? actual, string label) => True(ReferenceEquals(expected, actual), label);
    private static void NotSame(object? expected, object? actual, string label) => True(!ReferenceEquals(expected, actual), label);

    public enum State { Waiting = 1, Ready = 2 }
    public sealed class ValueSource { public int Id { get; set; } public string? Name { get; set; } public string? Amount { get; set; } public string? Enabled { get; set; } public string? State { get; set; } public string? Optional { get; set; } }
    public sealed class ValueTarget { public int Id { get; set; } public string? Name { get; set; } public decimal Amount { get; set; } public bool Enabled { get; set; } public State State { get; set; } public int? Optional { get; set; } }
    public sealed class NamingSource { public int Id { get; set; } public string? user_name { get; set; } public string? Note { get; set; } }
    public sealed class NamingTarget { public int Id { get; set; } public string? UserName { get; set; } public string? Note { get; set; } = "initializer"; }
    public sealed class NamingTarget2 { public long Id { get; set; } public string? UserName { get; set; } }
    public sealed class BatchSource { public string? Name { get; set; } public ValueSource? Detail { get; set; } public ValueSource[]? Items { get; set; } }
    public sealed class BatchTarget { public string? Name { get; set; } public ValueTarget? Detail { get; set; } public List<ValueTarget>? Items { get; set; } }
    public sealed class GraphSource { public int Id { get; set; } public string? Name { get; set; } public GraphSource? Next { get; set; } public GraphSource? Left { get; set; } public GraphSource? Right { get; set; } public GraphSource[]? Items { get; set; } }
    public sealed class GraphTarget { public int Id { get; set; } public string? Name { get; set; } public GraphTarget? Next { get; set; } public GraphTarget? Left { get; set; } public GraphTarget? Right { get; set; } public List<GraphTarget>? Items { get; set; } }
    public sealed class RuleSource { public int Number { get; set; } public string? Text { get; set; } }
    public sealed class RuleTarget { public int Number { get; set; } public string? Text { get; set; } }
    public sealed class RuleEnvelopeSource { public RuleSource? Value { get; set; } public RuleSource[]? Items { get; set; } }
    public sealed class RuleEnvelopeTarget { public RuleTarget? Value { get; set; } public List<RuleTarget>? Items { get; set; } }
    public sealed class LabelProfile : MapperProfile
    {
        public LabelProfile(string prefix) { CreateMap<NamingSource, NamingTarget>().ForMember(d => d.UserName, s => prefix + s.user_name); }
    }
}
