using System.Collections;
using System.Globalization;
using Ling.Mapper;
using Ling.Mapper.Configuration;
using Ling.Mapper.Models;

namespace TestConsole.Test;

/// <summary>Public behavioral contracts for safe errors, caller-owned keys and live rule updates.</summary>
public static class DiagnosticsAndCacheRegressionTests
{
    private const string Secret = "UNSAFE_PAYLOAD_7831";
    private static long _assertions;
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(60);

    public static void Run()
    {
        Interlocked.Exchange(ref _assertions, 0);
        var failures = new List<Exception>();
        var cases = 0;
        void Case(string name, Action test)
        {
            cases++;
            try { test(); Console.WriteLine($"PASS diagnostics/cache: {name}"); }
            catch (Exception error) { failures.Add(new Exception(name, error)); Console.WriteLine($"FAIL diagnostics/cache: {name}: {error.Message}"); }
        }
        foreach (var property in typeof(SensitiveSource).GetProperties())
            Case("sensitive member " + property.Name, () => {
                var source = new SensitiveSource(); property.SetValue(source, Secret);
                var mapper = Config(true).CreateMapper();
                var error = Failure(() => mapper.Map<SensitiveTarget>(source));
                Equal("$." + property.Name, error.Path, "sensitive member path");
                Equal("[REDACTED]", error.SourceValue, "case-insensitive sensitivity"); Safe(error);
            });
        Case("sensitive ancestor object and indexed descendant", () => {
            var mapper = Config(true).CreateMapper();
            var error = Failure(() => mapper.Map<OuterTarget>(new OuterSource { AccessToken = new ContainerSource { Items = new[] { new PlainSource { Value = "1" }, new PlainSource { Value = Secret } } } }));
            Equal("$.AccessToken.Items[1].Value", error.Path, "sensitive ancestor path");
            Equal("[REDACTED]", error.SourceValue, "ancestor sensitivity propagation"); Safe(error);
        });
        Case("sensitive ancestor array", () => {
            var mapper = Config(true).CreateMapper();
            var error = Failure(() => mapper.Map<ArrayTarget>(new ArraySource { Credentials = new[] { new PlainSource { Value = Secret } } }));
            Equal("$.Credentials[0].Value", error.Path, "sensitive array path");
            Equal("[REDACTED]", error.SourceValue, "array ancestor sensitivity"); Safe(error);
        });
        Case("ForMember sensitive source with ordinary target", () => {
            var config = Config(true); config.AddProfile(new SensitiveProfile());
            var error = Failure(() => config.CreateMapper().Map<PlainTarget>(new ProfileSource { ApiKey = Secret }));
            Equal("$.Value", error.Path, "custom binding target path");
            Equal("[REDACTED]", error.SourceValue, "custom binding source sensitivity"); Safe(error);
        });
        foreach (var enabled in new[] { false, true })
        {
            Case($"byte array never exposed enabled={enabled}", () => {
                var error = Failure(() => Config(enabled).CreateMapper().Map<int>(System.Text.Encoding.UTF8.GetBytes(Secret)));
                Equal(enabled ? "[REDACTED]" : null, error.SourceValue, "byte array preview"); Safe(error);
            });
            Case($"poison ToString never invoked enabled={enabled}", () => {
                var source = new PoisonSource();
                var mapper = Config(enabled).CreateMapper();
                var error = Failure(() => mapper.Map<int>(source));
                Equal(enabled ? "[REDACTED]" : null, error.SourceValue, "complex preview");
                Equal(0, source.ToStringCalls, "diagnostic must not call arbitrary ToString"); Safe(error);
                Equal(73, mapper.Map<int>("73"), "next call after complex failure");
            });
            Case($"converter exception sanitized enabled={enabled}", () => {
                TypeConverterRegistry.Register(typeof(ThrowingRule), typeof(int), new Func<ThrowingRule, int>(_ => throw UnsafeException()));
                var mapper = Config(enabled).CreateMapper();
                var error = Failure(() => mapper.Map<int>(new ThrowingRule()));
                Equal("$", error.Path, "converter root path"); Safe(error);
                Equal(enabled ? "[REDACTED]" : null, error.SourceValue, "converter source preview");
                Equal(9, mapper.Map<int>("9"), "converter failure recovery");
            });
            Case($"getter exception sanitized enabled={enabled}", () => {
                var mapper = Config(enabled).CreateMapper(); var source = new ThrowingGetter { Fail = true };
                var error = Failure(() => mapper.Map<PlainTarget>(source));
                Equal("$.Value", error.Path, "getter path"); Safe(error);
                if (!enabled) Equal(null, error.SourceValue, "default getter preview hidden");
                source.Fail = false; Equal(42, mapper.Map<PlainTarget>(source)!.Value, "same getter graph after repair");
            });
            Case($"setter exception sanitized enabled={enabled}", () => {
                var mapper = Config(enabled).CreateMapper();
                var error = Failure(() => mapper.Map<ThrowingSetter>(new IntSource { Value = 13 }));
                Equal("$.Value", error.Path, "setter path"); Safe(error);
                Equal(enabled ? "13" : null, error.SourceValue, "ordinary setter value policy");
                Equal(42, mapper.Map<ThrowingSetter>(new IntSource { Value = 42 })!.Value, "setter next call succeeds");
            });
            Case($"constructor exception sanitized enabled={enabled}", () => {
                var mapper = Config(enabled).CreateMapper();
                var error = Failure(() => mapper.Map<ThrowingConstructor>(new IntSource { Value = 42 }));
                Equal("$", error.Path, "constructor root path"); Safe(error);
                Equal(enabled ? "[REDACTED]" : null, error.SourceValue, "constructor source policy");
                Equal(42, mapper.Map<PlainTarget>(new IntSource { Value = 42 })!.Value, "constructor failure recovery");
            });
            Case($"enumerator exception sanitized enabled={enabled}", () => {
                var mapper = Config(enabled).CreateMapper(); var source = new ThrowingEnumerable { Fail = true };
                var error = Failure(() => mapper.Map<List<int>>(source));
                Equal("$", error.Path, "enumerator path after prior item"); Safe(error);
                Equal(enabled ? "[REDACTED]" : null, error.SourceValue, "enumerator preview policy");
                source.Fail = false; var result = mapper.Map<List<int>>(source)!;
                Equal(2, result.Count, "enumerator recovery count"); Equal(7, result[0], "enumerator first value"); Equal(11, result[1], "enumerator final value");
            });
        }
        Case("ordinary default hides source and exception chains", () => {
            var error = Failure(() => new MapperConfiguration().CreateMapper().Map<int>(Secret));
            Equal(null, error.SourceValue, "default preview"); Safe(error);
        });
        foreach (var limit in new[] { 0, 1, 128 })
            Case($"ordinary bounded preview limit={limit}", () => {
                var text = "x\r\n" + new string('q', 300);
                var config = Config(true); config.MaxDiagnosticValueLength = limit;
                var mapper = config.CreateMapper(); var error = Failure(() => mapper.Map<int>(text));
                var expected = text.Substring(0, limit).Replace('\r', ' ').Replace('\n', ' ');
                Equal(expected, error.SourceValue, "exact bounded preview");
                Equal(limit, error.SourceValue!.Length, "preview length");
                True(!error.Message.Contains('\r') && !error.Message.Contains('\n'), "message must not contain raw CRLF");
                Equal("$", error.Path, "bounded preview root"); Safe(error);
                Equal(99, mapper.Map<int>("99"), "recovery after bounded preview");
            });
        Case("ordinary CRLF flattened without truncation", () => {
            var error = Failure(() => Config(true).CreateMapper().Map<int>("x\r\ny"));
            Equal("x  y", error.SourceValue, "CRLF normalization"); Safe(error);
        });
        Case("caller ignore array mutations do not alter cached keys", MutableIgnoreNames);
        Case("ignore permutations duplicates and empty list preserve semantics", IgnorePermutations);
        foreach (var readers in new[] { 1, 7 })
            Case($"registration overlaps {readers} readers", () => LiveRegistration(readers));
        Console.WriteLine($"Diagnostics/cache contracts: {cases - failures.Count}/{cases} passed; {Interlocked.Read(ref _assertions)} checked assertions.");
        if (failures.Count != 0) throw new AggregateException("Diagnostics/cache contract failures", failures);
    }

    private static MapperConfiguration Config(bool preview) => new() { IncludeSourceValueInExceptions = preview };
    private static void MutableIgnoreNames()
    {
        var mapper = new MapperConfiguration().CreateMapper();
        var input = new CacheSource { Id = 17, Name = "seventeen" }; var ignores = new[] { "Id" };
        CheckCache(mapper.Map<CacheTarget>(input, AdaptOptions.Default, ignoreNames: ignores)!, 0, "seventeen");
        ignores[0] = "Name";
        CheckCache(mapper.Map<CacheTarget>(input, AdaptOptions.Default, ignoreNames: ignores)!, 17, null);
        CheckCache(mapper.Map<CacheTarget>(input, AdaptOptions.Default, ignoreNames: new[] { "Id" })!, 0, "seventeen");
        ignores[0] = "Unknown";
        CheckCache(mapper.Map<CacheTarget>(input, AdaptOptions.Default, ignoreNames: ignores)!, 17, "seventeen");
        ignores[0] = "Id";
        CheckCache(mapper.Map<CacheTarget>(input, AdaptOptions.Default, ignoreNames: ignores)!, 0, "seventeen");
        CheckCache(mapper.Map<CacheTarget>(input, AdaptOptions.Default, ignoreNames: new[] { "Name" })!, 17, null);
        CheckCache(mapper.Map<CacheTarget>(input)!, 17, "seventeen");
    }
    private static void IgnorePermutations()
    {
        var mapper = new MapperConfiguration().CreateMapper(); var source = new CacheSource { Id = 23, Name = "name" };
        foreach (var ignores in new[] { new[] { "Id", "Name" }, new[] { "Name", "Id" }, new[] { "Id", "Name", "Id" } })
            CheckCache(mapper.Map<CacheTarget>(source, AdaptOptions.Default, ignoreNames: ignores)!, 0, null);
        CheckCache(mapper.Map<CacheTarget>(source, AdaptOptions.Default, ignoreNames: Array.Empty<string>())!, 23, "name");
        CheckCache(mapper.Map<CacheTarget>(source, AdaptOptions.Strict)!, 23, "name");
    }
    private static void CheckCache(CacheTarget result, int id, string? name) { Equal(id, result.Id, "ignored Id"); Equal(name, result.Name, "ignored Name"); }

    private static void LiveRegistration(int readers)
    {
        const int rounds = 16;
        var mapper = new MapperConfiguration().CreateMapper();
        void Register(int version) => TypeConverterRegistry.Register(typeof(LiveSource), typeof(LiveTarget),
            new Func<LiveSource, LiveTarget>(s => new LiveTarget { Version = version, Number = s.Number + version * 100, Text = s.Text + ":" + version.ToString(CultureInfo.InvariantCulture) }));
        var source = new LiveSource { Number = 7, Text = "row" };
        Register(0); CheckLive(mapper.Map<LiveTarget>(source)!, 0, 0);
        using var stop = new CancellationTokenSource();
        using var barrier = new Barrier(readers + 1);
        void Rendezvous() { if (!barrier.SignalAndWait(Timeout, stop.Token)) throw new TimeoutException("Registry rendezvous timed out"); }
        var tasks = Enumerable.Range(0, readers + 1).Select(worker => Task.Factory.StartNew(() => {
            try
            {
                for (var round = 1; round <= rounds; round++)
                {
                    Rendezvous();
                    if (worker == 0) Register(round);
                    else
                    {
                        for (var call = 0; call < 8; call++)
                        {
                            CheckLive(mapper.Map<LiveTarget>(source)!, round - 1, round);
                            var graph = mapper.Map<LiveEnvelopeTarget>(new LiveEnvelopeSource { Left = source, Items = new[] { source, source } })!;
                            // Each conversion may observe either adjacent version. Whole graphs are NOT atomic snapshots.
                            CheckLive(graph.Left!, round - 1, round);
                            Equal(2, graph.Items!.Count, "live list count");
                            foreach (var item in graph.Items) CheckLive(item, round - 1, round);
                        }
                    }
                    Rendezvous(); // writer and readers finish this phase before the next version.
                }
            }
            catch { stop.Cancel(); throw; }
        }, CancellationToken.None, TaskCreationOptions.LongRunning, TaskScheduler.Default)).ToArray();
        if (!Task.WaitAll(tasks, Timeout)) { stop.Cancel(); throw new TimeoutException("Registry concurrency timed out"); }
        // Writer has completed. Every subsequent public mapping must see the final registration.
        for (var i = 0; i < 32; i++)
        {
            CheckLive(mapper.Map<LiveTarget>(source)!, rounds, rounds);
            var result = mapper.Map<LiveEnvelopeTarget>(new LiveEnvelopeSource { Left = source, Items = new[] { source } })!;
            CheckLive(result.Left!, rounds, rounds); CheckLive(result.Items![0], rounds, rounds);
        }
    }
    private static void CheckLive(LiveTarget target, int low, int high)
    {
        True(target.Version == low || target.Version == high, $"rule version must be {low} or {high}, got {target.Version}");
        Equal(7 + target.Version * 100, target.Number, "rule numeric consistency");
        Equal("row:" + target.Version.ToString(CultureInfo.InvariantCulture), target.Text, "rule text consistency");
    }
    private static MappingException Failure(Action action)
    {
        try { action(); }
        catch (MappingException error) { Interlocked.Increment(ref _assertions); return error; }
        throw new InvalidOperationException("Expected MappingException");
    }
    private static InvalidOperationException UnsafeException()
    {
        var error = new InvalidOperationException(Secret + "_message", new Exception(Secret + "_inner"));
        error.Data[Secret + "_key"] = Secret + "_data"; error.InnerException!.Data["nested"] = Secret;
        return error;
    }
    private static void Safe(MappingException error)
    {
        True(!error.ToString().Contains(Secret, StringComparison.Ordinal), "ToString must not leak original secret");
        True(!string.IsNullOrWhiteSpace(error.OriginalExceptionType), "original exception type is retained");
        for (Exception? current = error; current != null; current = current.InnerException)
        {
            True(!current.Message.Contains(Secret, StringComparison.Ordinal), "message chain must be sanitized");
            Equal(0, current.Data.Count, "exception Data must not retain original entries");
        }
        True(error.SourceValue == null || !error.SourceValue.Contains(Secret, StringComparison.Ordinal), "SourceValue must not leak secret");
    }
    private static void Equal<T>(T expected, T actual, string label) { Interlocked.Increment(ref _assertions); if (!EqualityComparer<T>.Default.Equals(expected, actual)) throw new InvalidOperationException($"{label}: expected '{expected}', actual '{actual}'"); }
    private static void True(bool value, string label) { Interlocked.Increment(ref _assertions); if (!value) throw new InvalidOperationException(label); }

    public sealed class PlainSource { public string? Value { get; set; } }
    public sealed class PlainTarget { public int Value { get; set; } }
    public sealed class IntSource { public int Value { get; set; } }
    public sealed class ContainerSource { public PlainSource[]? Items { get; set; } }
    public sealed class ContainerTarget { public PlainTarget[]? Items { get; set; } }
    public sealed class OuterSource { public ContainerSource? AccessToken { get; set; } }
    public sealed class OuterTarget { public ContainerTarget? AccessToken { get; set; } }
    public sealed class ArraySource { public PlainSource[]? Credentials { get; set; } }
    public sealed class ArrayTarget { public PlainTarget[]? Credentials { get; set; } }
    public sealed class ProfileSource { public string? ApiKey { get; set; } }
    public sealed class SensitiveProfile : MapperProfile { public SensitiveProfile() { CreateMap<ProfileSource, PlainTarget>().ForMember<object>(d => d.Value, s => s.ApiKey!); } }
    public sealed class PoisonSource { public int ToStringCalls; public override string ToString() { ToStringCalls++; throw UnsafeException(); } }
    public sealed class ThrowingRule { }
    public sealed class ThrowingGetter { public bool Fail { get; set; } public int Value => Fail ? throw UnsafeException() : 42; }
    public sealed class ThrowingSetter { private int _value; public int Value { get => _value; set { if (value == 13) throw UnsafeException(); _value = value; } } }
    public sealed class ThrowingConstructor { public ThrowingConstructor() => throw UnsafeException(); public int Value { get; set; } }
    public sealed class ThrowingEnumerable : IEnumerable<string>
    {
        public bool Fail { get; set; }
        public IEnumerator<string> GetEnumerator() { yield return "7"; if (Fail) throw UnsafeException(); yield return "11"; }
        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }
    public sealed class CacheSource { public int Id { get; set; } public string? Name { get; set; } }
    public sealed class CacheTarget { public int Id { get; set; } public string? Name { get; set; } }
    public sealed class LiveSource { public int Number { get; set; } public string? Text { get; set; } }
    public sealed class LiveTarget { public int Number { get; set; } public string? Text { get; set; } public int Version { get; set; } }
    public sealed class LiveEnvelopeSource { public LiveSource? Left { get; set; } public LiveSource[]? Items { get; set; } }
    public sealed class LiveEnvelopeTarget { public LiveTarget? Left { get; set; } public List<LiveTarget>? Items { get; set; } }
    public sealed class SensitiveSource
    {
        public string? password { get; set; }
        public string? PASSWORDMixed { get; set; }
        public string? passwd { get; set; }
        public string? PASSWDMixed { get; set; }
        public string? pwd { get; set; }
        public string? PWDMixed { get; set; }
        public string? token { get; set; }
        public string? TOKENMixed { get; set; }
        public string? secret { get; set; }
        public string? SECRETMixed { get; set; }
        public string? credential { get; set; }
        public string? CREDENTIALMixed { get; set; }
        public string? encrypt { get; set; }
        public string? ENCRYPTMixed { get; set; }
        public string? cipher { get; set; }
        public string? CIPHERMixed { get; set; }
        public string? privatekey { get; set; }
        public string? PRIVATEKEYMixed { get; set; }
        public string? apikey { get; set; }
        public string? APIKEYMixed { get; set; }
    }
    public sealed class SensitiveTarget
    {
        public int password { get; set; }
        public int PASSWORDMixed { get; set; }
        public int passwd { get; set; }
        public int PASSWDMixed { get; set; }
        public int pwd { get; set; }
        public int PWDMixed { get; set; }
        public int token { get; set; }
        public int TOKENMixed { get; set; }
        public int secret { get; set; }
        public int SECRETMixed { get; set; }
        public int credential { get; set; }
        public int CREDENTIALMixed { get; set; }
        public int encrypt { get; set; }
        public int ENCRYPTMixed { get; set; }
        public int cipher { get; set; }
        public int CIPHERMixed { get; set; }
        public int privatekey { get; set; }
        public int PRIVATEKEYMixed { get; set; }
        public int apikey { get; set; }
        public int APIKEYMixed { get; set; }
    }
}
