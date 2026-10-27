using System.Collections.Concurrent;
using System.Globalization;
using Ling.Mapper;
using Ling.Mapper.Configuration;
using Ling.Mapper.Models;

namespace TestConsole.Test;

/// <summary>Concurrent public contracts for dictionaries, constructor binding and profile paths.</summary>
public static class AdvancedConcurrencyRegressionTests
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
            try { test(); Console.WriteLine($"PASS advanced concurrency: {name}"); }
            catch (Exception error) { failures.Add(new Exception(name, error)); Console.WriteLine($"FAIL advanced concurrency: {name}: {error.Message}"); }
        }
        foreach (var degree in new[] { 1, 2, 8 })
        {
            Case($"cold dictionary and immutable plans degree={degree}", () => Cold(degree));
            Case($"warm dictionaries and readonly structs degree={degree}", () => Warm(degree));
            Case($"dictionary cycles and shared roots degree={degree}", () => DictionaryGraphs(degree));
            Case($"dictionary and constructor failures recover degree={degree}", () => Recovery(degree));
            Case($"nested destination profile isolation degree={degree}", () => NestedProfiles(degree));
            Case($"reverse mapping profile isolation degree={degree}", () => ReverseProfiles(degree));
            Case($"global convention isolation and explicit options degree={degree}", () => Conventions(degree));
        }
        Console.WriteLine($"Advanced concurrency contracts: {cases - failures.Count}/{cases} passed; {Interlocked.Read(ref _assertions)} checked assertions.");
        if (failures.Count != 0) throw new AggregateException("Advanced concurrency contract failures", failures);
    }
    private static void Cold(int degree)
    {
        var mapper = new MapperConfiguration().CreateMapper();
        Parallel(degree, (worker, _) => {
            var source = new Dictionary<string, TextValue> { ["17"] = Input(worker + 1) };
            var result = mapper.Map<Dictionary<int, ImmutableValue>>(source)!;
            Equal(1, result.Count, "cold dictionary count"); Check(source["17"], result[17]);
            True(!ReferenceEquals(source, result), "cross-type dictionary gets new instance");
        }, 1);
    }
    private static void Warm(int degree)
    {
        var mapper = new MapperConfiguration().CreateMapper();
        _ = mapper.Map<Dictionary<int, ImmutableValue>>(new Dictionary<string, TextValue> { ["0"] = Input(0) });
        Parallel(degree, (worker, iteration) => {
            var n = worker * 100 + iteration;
            var first = Input(n); var second = Input(n + 1);
            var input = new Dictionary<string, TextValue> { ["1"] = first, ["2"] = second };
            var result = mapper.Map<Dictionary<int, ImmutableValue>>(input)!;
            Equal(2, result.Count, "warm count"); Check(first, result[1]); Check(second, result[2]);
            var view = mapper.Map<IReadOnlyDictionary<long, ImmutableValue>>(input)!;
            Equal(2, view.Count, "readonly interface count"); Check(second, view[2]);
            var array = mapper.Map<ImmutableValue[]>(new[] { first, second })!;
            Equal(2, array.Length, "readonly struct array count"); Check(first, array[0]); Check(second, array[1]);
            var direct = mapper.Map<IReadOnlyDictionary<string, TextValue>>(input)!;
            True(!ReferenceEquals(input, direct), "assignable dictionary is an independent root");
            Equal(2, direct.Count, "assignable dictionary count");
            foreach (var key in new[] { "1", "2" })
            {
                True(!ReferenceEquals(input[key], direct[key]), "assignable dictionary mutable value is fresh");
                Equal(input[key].Id, direct[key].Id, "cloned dictionary value id");
                Equal(input[key].Name, direct[key].Name, "cloned dictionary value name");
                Equal(input[key].Amount, direct[key].Amount, "cloned dictionary value amount");
            }
            var originalName = first.Name; direct["1"].Name = "target-only";
            Equal(originalName, first.Name, "target dictionary value mutation isolated");
            var copiedSecondName = direct["2"].Name; second.Name = "source-only";
            Equal(copiedSecondName, direct["2"].Name, "source dictionary value mutation isolated");
            Equal(originalName, input["1"].Name, "source not modified");
        });
    }
    private static void DictionaryGraphs(int degree)
    {
        var mapper = new MapperConfiguration().CreateMapper();
        var source = new Dictionary<string, NodeSource>();
        var child = new NodeSource { Id = 31, Name = "shared", Links = source };
        source["1"] = child; source["2"] = child;
        var roots = new ConcurrentBag<Dictionary<int, NodeTarget>>();
        Parallel(degree, (_, _) => {
            var result = mapper.Map<Dictionary<int, NodeTarget>>(source)!;
            Equal(2, result.Count, "graph dictionary count"); Equal(31, result[1].Id, "graph value id"); Equal("shared", result[1].Name, "graph value name");
            True(ReferenceEquals(result[1], result[2]), "shared dictionary value reference");
            True(ReferenceEquals(result, result[1].Links), "dictionary-to-node-to-dictionary cycle");
            True(!ReferenceEquals(source, result), "mapped dictionary identity");
            True(!ReferenceEquals(child, result[1]), "mapped node identity"); roots.Add(result);
        }, 8);
        var seen = new HashSet<Dictionary<int, NodeTarget>>(ReferenceEqualityComparer.Instance);
        var children = new HashSet<NodeTarget>(ReferenceEqualityComparer.Instance);
        foreach (var root in roots) { True(seen.Add(root), "root context per call"); True(children.Add(root[1]), "shared values cannot leak between calls"); }
        Equal(degree * 8, seen.Count, "all graph mappings completed");
        True(ReferenceEquals(source, child.Links), "source cycle preserved");
    }
    private static void Recovery(int degree)
    {
        var mapper = new MapperConfiguration().CreateMapper();
        Parallel(degree, (worker, iteration) => {
            var value = Input(worker + iteration); value.Amount = "not-an-amount";
            var source = new Dictionary<string, TextValue> { ["1"] = value };
            var error = Failure(() => mapper.Map<Dictionary<int, ImmutableValue>>(source));
            True(error.Path.StartsWith("$", StringComparison.Ordinal), "dictionary error rooted");
            True(error.Path.Contains("Amount", StringComparison.OrdinalIgnoreCase), "constructor error identifies amount");
            value.Amount = "7.25";
            var result = mapper.Map<Dictionary<int, ImmutableValue>>(source)!;
            Equal(7.25m, result[1].Amount, "repaired exact dictionary graph"); Equal(value.Name, result[1].Name, "repaired text");
            var badKey = new Dictionary<string, TextValue> { ["invalid-key"] = Input(3) };
            Failure(() => mapper.Map<Dictionary<int, ImmutableValue>>(badKey));
            badKey.Clear(); badKey["9"] = Input(9);
            Check(badKey["9"], mapper.Map<Dictionary<int, ImmutableValue>>(badKey)![9]);
            var duplicates = new Dictionary<string, int> { ["1"] = 10, ["01"] = 11 };
            Failure(() => mapper.Map<Dictionary<int, int>>(duplicates));
            Equal(12, mapper.Map<Dictionary<int, int>>(new Dictionary<string, int> { ["1"] = 12 })![1], "duplicate-key failure recovery");
        });
    }
    private static void NestedProfiles(int degree)
    {
        var configA = new MapperConfiguration(); configA.AddProfile(new NestedProfile("A:"));
        var configB = new MapperConfiguration(); configB.AddProfile(new NestedProfile("B:"));
        var a = configA.CreateMapper(); var b = configB.CreateMapper();
        Parallel(degree, (worker, iteration) => {
            var input = Input(worker * 100 + iteration);
            var first = a.Map<NestedTarget>(input, AdaptOptions.Strict)!;
            var second = b.Map<NestedTarget>(input, AdaptOptions.Default)!;
            Equal(input.Id, first.Id, "nested root id");
            Equal("A:" + input.Name, first.Detail!.Name, "nested profile A");
            Equal("B:" + input.Name, second.Detail!.Name, "nested profile B");
            Equal(decimal.Parse(input.Amount!, CultureInfo.InvariantCulture), first.Detail.Amount, "nested renamed amount");
            True(!ReferenceEquals(first.Detail, second.Detail), "nested objects separated across mappers");
            var again = a.Map<NestedTarget>(input)!;
            True(!ReferenceEquals(first.Detail, again.Detail), "nested objects separated across calls");
            input.Amount = "bad";
            var error = Failure(() => a.Map<NestedTarget>(input));
            Equal("$.Detail.Amount", error.Path, "nested destination error path");
            input.Amount = "6.5";
            Equal(6.5m, a.Map<NestedTarget>(input)!.Detail!.Amount, "nested destination failure recovery");
        });
    }
    private static void ReverseProfiles(int degree)
    {
        var configA = new MapperConfiguration(); configA.AddProfile(new ReverseProfile(100));
        var configB = new MapperConfiguration(); configB.AddProfile(new ReverseProfile(200));
        var a = configA.CreateMapper(); var b = configB.CreateMapper();
        Parallel(degree, (worker, iteration) => {
            var input = new ReverseSource { Id = worker * 100 + iteration, Name = "name-" + iteration };
            var forward = a.Map<ReverseTarget>(input, AdaptOptions.Strict)!;
            Equal(input.Id, forward.Id, "forward exact member"); Equal(input.Name, forward.Label, "forward renamed member");
            var first = a.Map<ReverseSource>(forward)!; var second = b.Map<ReverseSource>(forward)!;
            Equal(input.Id + 100, first.Id, "reverse profile A custom binding");
            Equal(input.Id + 200, second.Id, "reverse profile B custom binding");
            Equal(input.Name, first.Name, "reverse rename automatically registered");
            Equal(input.Name, second.Name, "reverse rename isolated");
            True(!ReferenceEquals(input, first), "reverse constructs new source");
            Equal(input.Name, a.Map<ReverseTarget>(first)!.Label, "reverse then forward reused mapper");
        });
    }
    private static void Conventions(int degree)
    {
        var flexible = new MapperConfiguration();
        flexible.ConfigureConventions(c => { c.CaseInsensitiveNameMatch = true; c.IgnoreSpecialCharacters = true; });
        var strict = new MapperConfiguration();
        strict.ConfigureConventions(c => { c.CaseInsensitiveNameMatch = false; c.IgnoreSpecialCharacters = false; });
        var a = flexible.CreateMapper(); var b = strict.CreateMapper();
        Parallel(degree, (worker, iteration) => {
            var input = new ConventionSource { user_name = "user-" + worker + ":" + iteration, Id = iteration };
            Equal(input.user_name, a.Map<ConventionTarget>(input)!.UserName, "global flexible conventions");
            Equal(null, b.Map<ConventionTarget>(input)!.UserName, "global strict conventions");
            Equal(input.user_name, b.Map<ConventionTarget>(input, AdaptOptions.Default)!.UserName, "explicit options override strict conventions");
            Equal(null, a.Map<ConventionTarget>(input, AdaptOptions.Strict)!.UserName, "explicit strict overrides flexible conventions");
            Equal(iteration, a.Map<ConventionTarget>(input)!.Id, "numeric exact member unchanged");
        });
    }
    private static TextValue Input(int id) => new() { Id = id, Name = "value-" + id, Amount = (id + 0.25m).ToString(CultureInfo.InvariantCulture) };
    private static void Check(TextValue expected, ImmutableValue actual) { Equal(expected.Id, actual.Id, "immutable Id"); Equal(expected.Name, actual.Name, "immutable Name"); Equal(decimal.Parse(expected.Amount!, CultureInfo.InvariantCulture), actual.Amount, "immutable Amount"); }
    private static MappingException Failure(Action action) { try { action(); } catch (MappingException error) { Interlocked.Increment(ref _assertions); return error; } throw new InvalidOperationException("Expected MappingException"); }
    private static void Parallel(int degree, Action<int, int> action, int iterations = 12)
    {
        using var barrier = new Barrier(degree); using var stop = new CancellationTokenSource();
        var tasks = Enumerable.Range(0, degree).Select(worker => Task.Factory.StartNew(() => {
            try
            {
                if (!barrier.SignalAndWait(Timeout, stop.Token)) throw new TimeoutException("Advanced concurrency start timed out");
                for (var i = 0; i < iterations; i++) { stop.Token.ThrowIfCancellationRequested(); action(worker, i); }
            }
            catch { stop.Cancel(); throw; }
        }, CancellationToken.None, TaskCreationOptions.LongRunning, TaskScheduler.Default)).ToArray();
        if (!Task.WaitAll(tasks, Timeout)) { stop.Cancel(); throw new TimeoutException("Advanced concurrency operations timed out"); }
    }
    private static void Equal<T>(T expected, T actual, string label) { Interlocked.Increment(ref _assertions); if (!EqualityComparer<T>.Default.Equals(expected, actual)) throw new InvalidOperationException($"{label}: expected '{expected}', actual '{actual}'"); }
    private static void True(bool value, string label) { Interlocked.Increment(ref _assertions); if (!value) throw new InvalidOperationException(label); }

    public sealed class TextValue { public int Id { get; set; } public string? Name { get; set; } public string? Amount { get; set; } }
    public readonly struct ImmutableValue
    {
        public ImmutableValue(int id, string? name, decimal amount) { Id = id; Name = name; Amount = amount; }
        public int Id { get; } public string? Name { get; } public decimal Amount { get; }
    }
    public sealed class NodeSource { public int Id { get; set; } public string? Name { get; set; } public Dictionary<string, NodeSource>? Links { get; set; } }
    public sealed class NodeTarget { public int Id { get; set; } public string? Name { get; set; } public Dictionary<int, NodeTarget>? Links { get; set; } }
    public sealed class NestedTarget { public int Id { get; set; } public NestedDetail? Detail { get; set; } }
    public sealed class NestedDetail { public string? Name { get; set; } public decimal Amount { get; set; } }
    public sealed class NestedProfile : MapperProfile
    {
        public NestedProfile(string prefix) { CreateMap<TextValue, NestedTarget>().ForMember(d => d.Detail!.Name, s => prefix + s.Name).Rename(d => d.Detail!.Amount, nameof(TextValue.Amount)); }
    }
    public sealed class ReverseSource { public int Id { get; set; } public string? Name { get; set; } }
    public sealed class ReverseTarget { public int Id { get; set; } public string? Label { get; set; } }
    public sealed class ReverseProfile : MapperProfile
    {
        public ReverseProfile(int offset) { CreateMap<ReverseSource, ReverseTarget>().Rename(d => d.Label!, nameof(ReverseSource.Name)).ReverseMap().ForMember(d => d.Id, s => s.Id + offset); }
    }
    public sealed class ConventionSource { public int Id { get; set; } public string? user_name { get; set; } }
    public sealed class ConventionTarget { public int Id { get; set; } public string? UserName { get; set; } }
}
