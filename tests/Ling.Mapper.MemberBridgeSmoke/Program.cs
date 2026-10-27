using System.Diagnostics;
using System.Reflection;
using System.Runtime.ExceptionServices;
using Ling.Mapper;
using Ling.Mapper.Configuration;
using Ling.Mapper.Mapper;
using Ling.Mapper.Models;

if (args.Length == 0)
{
    var total = 0;
    foreach (var scenario in new[] { "cache", "guards", "errors", "registry", "getter", "foreign", "identity", "owners" })
    {
        var start = new ProcessStartInfo(Environment.ProcessPath!) { RedirectStandardOutput = true, RedirectStandardError = true, UseShellExecute = false };
        start.ArgumentList.Add(Assembly.GetExecutingAssembly().Location); start.ArgumentList.Add(scenario);
        using var process = Process.Start(start)!; var output = process.StandardOutput.ReadToEnd(); var error = process.StandardError.ReadToEnd(); process.WaitForExit();
        Console.Write(output); if (process.ExitCode != 0) throw new Exception(scenario + ": " + error);
        total += int.Parse(output.Split('\n').Single(line => line.StartsWith("CHECKS=")).Substring(7));
    }
    Console.WriteLine($"Member bridge smoke PASS: {total} checks in 8 fresh processes; no timing.");
}
else Tests.Run(args[0]);

internal static class Tests
{
    static int checks;
    static void Check(bool ok, string label) { Interlocked.Increment(ref checks); if (!ok) throw new Exception(label); }
    static IMapper New(MapperProfile? profile = null) { var c = new MapperConfiguration { IncludeSourceValueInExceptions = true }; if (profile != null) c.AddProfile(profile); return c.CreateMapper(); }
    static long Version => (long)typeof(TypeConverterRegistry).GetProperty("Version", BindingFlags.Static | BindingFlags.NonPublic)!.GetValue(null)!;
    static object? Invoke(MethodInfo method, object? target, params object?[] args)
    {
        try { return method.Invoke(target, args); }
        catch (TargetInvocationException error) { ExceptionDispatchInfo.Capture(error.InnerException!).Throw(); throw; }
    }
    static MappingException Error(Action operation)
    {
        try { operation(); } catch (MappingException error) { checks++; return error; }
        throw new Exception("expected mapping failure");
    }
    sealed class Bridge
    {
        readonly IMapper mapper; readonly Type implementation; readonly MethodInfo method; readonly FieldInfo plan;
        public readonly object Slot; public readonly object Context;
        public Bridge(IMapper mapper)
        {
            this.mapper = mapper; implementation = mapper.GetType();
            method = implementation.GetMethod("MapKnownMember", BindingFlags.Instance | BindingFlags.NonPublic)!;
            var slotType = implementation.GetNestedType("MemberPlanSlot", BindingFlags.NonPublic)!;
            Slot = Activator.CreateInstance(slotType, true)!; plan = slotType.GetField("Plan", BindingFlags.Instance | BindingFlags.NonPublic)!;
            Context = Activator.CreateInstance(implementation.Assembly.GetType("Ling.Mapper.Mapper.MappingContext")!, true)!;
        }
        public object? Cached => plan.GetValue(Slot);
        public string Path => (string)Context.GetType().GetMethod("GetPath")!.Invoke(Context, null)!;
        public object Origin => Context.GetType().GetProperty("Origin")!.GetValue(Context)!;
        public void Updating(object source, Type sourceType, Type destinationType, object existing)
        {
            Context.GetType().GetProperty("IsUpdating")!.SetValue(Context, true);
            var scope = Invoke(implementation.GetMethod("ReferenceSourceScope", BindingFlags.Instance | BindingFlags.NonPublic)!, mapper, sourceType, destinationType)!;
            Invoke(Context.GetType().GetMethod("Register")!, Context, source, scope, destinationType, existing, null);
        }
        public object? Map(object? source, Type sourceType, Type destinationType, long? version = null, bool sensitive = false)
            => Invoke(method, mapper, source, sourceType, destinationType, AdaptOptions.Strict, "Child", sensitive, Context, version ?? Version, Slot);
    }
    static object? Origin(MappingException error) => typeof(MappingException).GetProperty("Origin", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(error);
    static S Source() { var a = new S { Id = 3 }; var b = new S { Id = 7, Left = a }; a.Left = b; a.Right = b; return a; }
    static void Graph(D d, int offset = 0) { Check(d.Id == 3 + offset && d.Left!.Id == 7 + offset, "values"); Check(ReferenceEquals(d.Left, d.Right) && ReferenceEquals(d.Left!.Left, d), "graph topology"); }
    public static void Run(string scenario)
    {
        var mapper = New(); var source = Source();
        switch (scenario)
        {
            case "cache":
                var bridge = new Bridge(mapper); var first = (D)bridge.Map(source, typeof(S), typeof(D))!; Graph(first);
                var plan = bridge.Cached; Check(plan != null && bridge.Path == "$", "lazy plan and path pop");
                Check(ReferenceEquals(first, bridge.Map(source, typeof(S), typeof(D))) && ReferenceEquals(plan, bridge.Cached), "full plan preserves same-context cache");
                var fresh = (D)new Bridge(mapper).Map(source, typeof(S), typeof(D))!; Graph(fresh); Check(!ReferenceEquals(first, fresh), "contexts isolated");
                var output = mapper.Map<Wrap<D>>(new Wrap<S> { Child = source })!; Graph(output.Child!);
                var target = new Wrap<D> { Child = new D { Id = 99 } }; var previous = target.Child;
                Check(ReferenceEquals(target, mapper.MapInto(new Wrap<S> { Child = source }, target)) && ReferenceEquals(previous, target.Child), "MapInto existing child"); Graph(target.Child!);
                break;
            case "guards":
                var nullBridge = new Bridge(mapper); Check(nullBridge.Map(null, typeof(Unsupported), typeof(Unsupported)) == null && nullBridge.Cached == null && nullBridge.Path == "$", "null defers unsupported plan");
                var runtimeBridge = new Bridge(mapper); var derived = new Derived { Id = 11, Extra = 23 };
                var clone = (Derived)runtimeBridge.Map(derived, typeof(Base), typeof(Base))!;
                Check(clone.Id == 11 && clone.Extra == 23 && !ReferenceEquals(clone, derived) && runtimeBridge.Cached == null, "runtime mismatch fallback");
                Base[] array = new Derived[] { derived, derived }; var covarianceBridge = new Bridge(mapper);
                var clonedArray = (Base[])covarianceBridge.Map(array, typeof(Base[]), typeof(Base[]))!;
                Check(clonedArray is Derived[] && !ReferenceEquals(array, clonedArray) && !ReferenceEquals(array[0], clonedArray[0]) && ReferenceEquals(clonedArray[0], clonedArray[1]), "array covariance and shared element");
                Check(covarianceBridge.Cached == null, "covariance does not freeze declared pair");
                var updateBridge = new Bridge(mapper); var existing = new D { Id = 91 }; updateBridge.Updating(source, typeof(S), typeof(D), existing);
                Check(ReferenceEquals(existing, updateBridge.Map(source, typeof(S), typeof(D))) && existing.Id == 91 && updateBridge.Cached == null, "IsUpdating uses existing registered result");
                var mismatch = new Bridge(mapper); Check(((D)mismatch.Map(source, typeof(S), typeof(D), Version - 1)!).Id == 3 && mismatch.Cached == null, "version mismatch fallback");
                break;
            case "errors":
                foreach (var slow in new[] { false, true })
                {
                    var failure = new Bridge(mapper); object bad = slow ? new BadDerived() : new Bad();
                    var error = Error(() => failure.Map(bad, typeof(Bad), typeof(ValueD)));
                    Check(error.Path == "$.Child.Value" && error.OriginalExceptionType == typeof(ApplicationException).FullName, "fast/slow member error path");
                    Check(ReferenceEquals(Origin(error), failure.Origin) && failure.Path == "$", "same origin and finally pop");
                }
                var sensitiveBridge = new Bridge(mapper); var sensitiveError = Error(() => sensitiveBridge.Map(new Text { Value = "fixture-marker" }, typeof(Text), typeof(ValueD), sensitive: true));
                Check(sensitiveError.Path == "$.Child.Value" && sensitiveError.SourceValue == "[REDACTED]", "sensitive parent preserved");
                Check(sensitiveBridge.Path == "$", "sensitive path unwound");
                break;
            case "registry":
                var warmed = new Bridge(mapper); var oldVersion = Version; Graph((D)warmed.Map(source, typeof(S), typeof(D), oldVersion)!);
                var replacement = new D { Id = 101 }; TypeConverterRegistry.Register(typeof(S), typeof(D), new Func<S, D>(_ => replacement));
                Check(ReferenceEquals(replacement, warmed.Map(source, typeof(S), typeof(D), oldVersion)), "old slot sees registry change and final identity");
                break;
            case "getter":
                var hookMapper = New(); var hooked = new HookRoot { Child = source }; hookMapper.Map<Wrap<D>>(hooked);
                HookRoot.Reads = 0; var later = new D { Id = 202 };
                HookRoot.Hook = () => { HookRoot.Hook = null; TypeConverterRegistry.Register(typeof(S), typeof(D), new Func<S, D>(_ => later)); };
                var fromGetter = hookMapper.Map<Wrap<D>>(hooked)!;
                Check(ReferenceEquals(fromGetter.Child, later) && HookRoot.Reads == 1, "getter registration before conversion and read once");
                var callbackCount = 0; var callbackReplacement = new D { Id = 303 };
                hookMapper.Map<Wrap<D>>(hooked, AdaptOptions.Strict, (_, _) => { callbackCount++; TypeConverterRegistry.Register(typeof(S), typeof(D), new Func<S, D>(_ => callbackReplacement)); });
                Check(callbackCount == 1 && ReferenceEquals(hookMapper.Map<Wrap<D>>(hooked)!.Child, callbackReplacement), "callback registration next call");
                break;
            case "foreign":
                var foreign = Error(() => mapper.Map<ValueD>(new Text { Value = "invalid-value" }));
                TypeConverterRegistry.Register(typeof(S), typeof(D), new Func<S, D>(_ => throw foreign));
                foreach (var stale in new[] { false, true })
                {
                    var guarded = new Bridge(mapper); var wrapped = Error(() => guarded.Map(source, typeof(S), typeof(D), stale ? Version - 1 : Version));
                    Check(wrapped.Path == "$.Child" && wrapped.OriginalExceptionType == typeof(MappingException).FullName, "foreign exception wrapped once fast/slow");
                    Check(!ReferenceEquals(wrapped, foreign) && !ReferenceEquals(Origin(wrapped), Origin(foreign)) && ReferenceEquals(Origin(wrapped), guarded.Origin), "foreign origin replaced");
                    Check(guarded.Path == "$", "foreign failure path unwound");
                }
                break;
            case "identity":
                var finalMapper = New(new FinalProfile()); var input = new Wrap<S> { Child = source }; var result = finalMapper.Map<Wrap<S>>(input)!;
                Check(ReferenceEquals(result.Child, source) && !ReferenceEquals(result, input), "member final converter identity");
                Check(finalMapper.Map<Wrap<S>>(new Wrap<S>())!.Child == null, "null bypasses final converter");
                break;
            case "owners":
                var a = New(new OffsetProfile(10)); var b = New(new OffsetProfile(20));
                Parallel.For(0, 64, _ => { var x = a.Map<Wrap<D>>(new Wrap<S> { Child = source })!; var y = b.Map<Wrap<D>>(new Wrap<S> { Child = source })!; Graph(x.Child!, 10); Graph(y.Child!, 20); Check(!ReferenceEquals(x.Child, y.Child), "parallel owners distinct"); });
                break;
            default: throw new ArgumentException(scenario);
        }
        Console.WriteLine($"SCENARIO {scenario} PASS\nCHECKS={checks}");
    }
    public sealed class OffsetProfile : MapperProfile { public OffsetProfile(int offset) { CreateMap<S, D>().ForMember(d => d.Id, s => s.Id + offset); } }
    public sealed class FinalProfile : MapperProfile { public FinalProfile() { CreateMap<Wrap<S>, Wrap<S>>().ForMember(d => d.Child, o => o.MapFrom(s => s.Child).ConvertUsing<S?>(s => s)); } }
    public sealed class Wrap<T> { public T? Child { get; set; } }
    public sealed class S { public int Id { get; set; } public S? Left { get; set; } public S? Right { get; set; } }
    public sealed class D { public int Id { get; set; } public D? Left { get; set; } public D? Right { get; set; } }
    public class Base { public int Id { get; set; } }
    public sealed class Derived : Base { public int Extra { get; set; } }
    public sealed class Unsupported : IDisposable { public void Dispose() { } }
    public class Bad { public int Value => throw new ApplicationException(); }
    public sealed class BadDerived : Bad { }
    public sealed class ValueD { public int Value { get; set; } }
    public sealed class Text { public string? Value { get; set; } }
    public sealed class HookRoot { public static Action? Hook; public static int Reads; S? child; public S? Child { get { Reads++; Hook?.Invoke(); return child; } set => child = value; } }
}
