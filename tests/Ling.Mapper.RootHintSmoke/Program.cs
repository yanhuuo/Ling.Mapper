using System.Diagnostics;
using System.Reflection;
using Ling.Mapper;
using Ling.Mapper.Configuration;
using Ling.Mapper.Mapper;
using Ling.Mapper.Models;

// Every registry scenario owns a fresh process, so one scenario cannot disable
// another scenario's warm-path qualification. This executable does no timing.
if (args.Length == 0)
{
    var total = 0;
    foreach (var scenario in new[] { "graphs", "owners", "options", "runtime", "metadata", "errors", "registry", "reentrant" })
    {
        var start = new ProcessStartInfo(Environment.ProcessPath!) { RedirectStandardOutput = true, RedirectStandardError = true, UseShellExecute = false };
        start.ArgumentList.Add(Assembly.GetExecutingAssembly().Location);
        start.ArgumentList.Add(scenario);
        using var process = Process.Start(start)!;
        var output = process.StandardOutput.ReadToEnd();
        var error = process.StandardError.ReadToEnd();
        process.WaitForExit();
        Console.Write(output);
        if (process.ExitCode != 0) throw new Exception(scenario + ": " + error);
        total += int.Parse(output.Split('\n').Single(line => line.StartsWith("CHECKS=")).Substring(7));
    }
    Console.WriteLine($"Root hint smoke PASS: {total} checks in 8 fresh processes; no timing.");
}
else Tests.Run(args[0]);

internal static class Tests
{
    static int checks;
    static void Check(bool condition, string label) { Interlocked.Increment(ref checks); if (!condition) throw new Exception(label); }
    static IMapper New(MapperProfile? profile = null)
    {
        var configuration = new MapperConfiguration();
        if (profile != null) configuration.AddProfile(profile);
        return configuration.CreateMapper();
    }
    static S Input() { var child = new S { Id = 7 }; var root = new S { Id = 3, Left = child, Right = child }; child.Left = root; return root; }
    static void Graph(D output, int offset = 0)
    {
        Check(output.Id == 3 + offset && output.Left!.Id == 7 + offset, "graph values");
        Check(ReferenceEquals(output.Left, output.Right) && ReferenceEquals(output, output.Left!.Left), "aliases and cycle");
    }
    static MappingException Failure(Action action)
    {
        try { action(); } catch (MappingException error) { checks++; return error; }
        throw new Exception("expected mapping failure");
    }
    public static void Run(string scenario)
    {
        var mapper = New();
        var source = Input();
        switch (scenario)
        {
            case "graphs":
                for (var i = 0; i < 20; i++)
                {
                    // Alternating flat/nested shapes exercises the shared hint slot.
                    Check(mapper.Map<FlatD>(new FlatS { Id = i })!.Id == i, "alternating flat");
                    var first = mapper.Map<D>(source)!; var second = mapper.Map<D>(source)!;
                    Graph(first); Graph(second);
                    Check(!ReferenceEquals(first, second) && !ReferenceEquals(first.Left, second.Left), "independent graphs");
                    first.Left!.Id = -1; Check(second.Left!.Id == 7 && source.Left!.Id == 7, "mutation isolation");
                }
                Parallel.For(0, 64, i => { var result = mapper.Map<D>(source)!; Graph(result); Check(mapper.Map<FlatD>(new FlatS { Id = i })!.Id == i, "parallel alternating"); });
                break;
            case "owners":
                var a = New(new OffsetProfile(10)); var b = New(new OffsetProfile(20));
                for (var i = 0; i < 20; i++) { Graph(a.Map<D>(source)!, 10); Graph(b.Map<D>(source)!, 20); Graph(mapper.Map<D>(source)!); }
                break;
            case "options":
                Graph(mapper.Map<D>(source)!);
                var ignored = mapper.Map<D>(source, AdaptOptions.Strict, ignoreNames: new[] { "Left", "Right" })!;
                Check(ignored.Id == 3 && ignored.Left == null && ignored.Right == null, "filtered root not reused");
                Graph(mapper.Map<D>(source)!);
                var optionSource = new LowerS { id = 19, Child = new FlatS { Id = 5 } };
                for (var i = 0; i < 4; i++)
                {
                    Check(mapper.Map<LowerD>(optionSource, AdaptOptions.Strict)!.Id == 0, "strict options");
                    Check(mapper.Map<LowerD>(optionSource, AdaptOptions.IgnoreCase)!.Id == 19, "case options");
                }
                var target = new D { Id = -1 };
                Check(ReferenceEquals(target, mapper.MapInto(source, target)), "MapInto keeps root"); Graph(target);
                Check(ReferenceEquals(target, mapper.MapInto(null, target)), "MapInto null");
                Check(mapper.Map<D>(null) == null, "null root after warm");
                var callbacks = 0;
                Graph(mapper.Map<D>(source, AdaptOptions.Strict, (destination, original) => { callbacks++; Check(ReferenceEquals(original, source), "root callback input"); Check(destination is D, "root callback output"); })!);
                Check(callbacks == 1, "one root callback");
                var listCalls = 0;
                var list = mapper.Map<List<D>>(new List<S> { source, source }, AdaptOptions.Strict, (_, _) => listCalls++)!;
                Check(listCalls == 2 && ReferenceEquals(list[0], list[1]), "collection item callbacks and shared references");
                break;
            case "runtime":
                var derived = new Derived { Id = 11, Extra = 23, Child = new FlatS { Id = 9 } };
                for (var i = 0; i < 4; i++)
                {
                    var actual = mapper.Map<Base>(derived)!;
                    Check(actual is Derived d && d.Extra == 23 && d.Child!.Id == 9 && !ReferenceEquals(d.Child, derived.Child), "assignable runtime clone");
                    var explicitActual = (Base)mapper.Map(derived, typeof(Base), typeof(Base))!;
                    Check(explicitActual is Derived e && e.Extra == 23, "explicit runtime clone");
                    var configured = New(new BaseProfile());
                    var declared = (BaseD)configured.Map(derived, typeof(Base), typeof(BaseD))!;
                    Check(declared.Id == 111 && declared.Child!.Id == 9, "explicit declared profile");
                    var objectView = mapper.Map<object>(derived)!;
                    Check(objectView is Derived o && o.Extra == 23 && !ReferenceEquals(o, derived), "object view clone");
                    var interfaceView = mapper.Map<IBase>(derived)!;
                    Check(interfaceView is Derived j && j.Extra == 23, "interface view clone");
                }
                Graph((D)mapper.Map(source, typeof(S), typeof(D))!);
                break;
            case "metadata":
                var dictionary = new Dictionary<int, S> { [1] = source };
                var wrapped = new WrappedType(typeof(Dictionary<long, D>));
                var initial = (Dictionary<long, D>)mapper.Map(dictionary, dictionary.GetType(), wrapped)!;
                Graph(initial[1]);
                wrapped.Throw = true; wrapped.AssignCalls = 0;
                try { mapper.Map(dictionary, dictionary.GetType(), wrapped); throw new Exception("expected raw metadata error"); }
                catch (ApplicationException) { Check(wrapped.AssignCalls == 1, "explicit warm metadata remains raw on first guard"); }
                var genericWarmMapper = New();
                Graph(genericWarmMapper.Map<Dictionary<long, D>>(dictionary)![1]);
                wrapped.AssignCalls = 0;
                try { genericWarmMapper.Map(dictionary, dictionary.GetType(), wrapped); throw new Exception("expected raw metadata error"); }
                catch (ApplicationException) { Check(wrapped.AssignCalls == 1, "explicit cannot consume generic complex hint"); }
                Graph((D)genericWarmMapper.Map(source, typeof(S), typeof(D))!);
                var mismatch = Failure(() => genericWarmMapper.Map(new FlatS(), typeof(S), typeof(D)));
                Check(mismatch.Path == "$" && mismatch.OriginalExceptionType == typeof(InvalidCastException).FullName, "explicit source mismatch still validated after warm");
                break;
            case "errors":
                var bad = new BadRoot { Child = new BadChild() };
                for (var i = 0; i < 2; i++)
                {
                    var error = Failure(() => mapper.Map<BadRootD>(bad));
                    Check(error.Path == "$.Child.Value" && error.OriginalExceptionType == typeof(InvalidOperationException).FullName, "warm failure path/type");
                    var callbackError = Failure(() => mapper.Map<D>(source, AdaptOptions.Strict, (_, _) => throw new ApplicationException()));
                    Check(callbackError.Path == "$" && callbackError.OriginalExceptionType == typeof(ApplicationException).FullName, "callback error boundary");
                }
                break;
            case "registry":
                for (var i = 0; i < 4; i++) Graph(mapper.Map<D>(source)!);
                TypeConverterRegistry.Register(typeof(S), typeof(D), new Func<S, D>(s => new D { Id = s.Id + 100 }));
                Check(mapper.Map<D>(source)!.Id == 103, "registry invalidates warm root");
                TypeConverterRegistry.Register(typeof(S), typeof(D), new Func<S, D>(s => new D { Id = s.Id + 200 }));
                Check(mapper.Map<D>(source)!.Id == 203, "second generation");
                break;
            case "reentrant":
                var hooked = new HookS { Id = 9, Child = new FlatS { Id = 4 } };
                mapper.Map<HookD>(hooked);
                HookS.Reads = 0; HookS.Action = () =>
                {
                    HookS.Action = null;
                    TypeConverterRegistry.Register(typeof(HookS), typeof(HookD), new Func<HookS, HookD>(s => new HookD { Id = 99 }));
                    Check(mapper.Map<HookD>(hooked)!.Id == 99, "reentrant new generation");
                };
                var inFlight = mapper.Map<HookD>(hooked)!;
                Check(inFlight.Id == 9 && inFlight.Child!.Id == 4, "in-flight plan continues");
                Check(HookS.Reads == 1 && mapper.Map<HookD>(hooked)!.Id == 99, "getter once and next call new generation");
                break;
            default: throw new ArgumentException(scenario);
        }
        Console.WriteLine($"SCENARIO {scenario} PASS\nCHECKS={checks}");
    }
    public sealed class OffsetProfile : MapperProfile { public OffsetProfile(int offset) { CreateMap<S, D>().ForMember(d => d.Id, s => s.Id + offset); } }
    public sealed class BaseProfile : MapperProfile { public BaseProfile() { CreateMap<Base, BaseD>().ForMember(d => d.Id, s => s.Id + 100); } }
    public sealed class S { public int Id { get; set; } public S? Left { get; set; } public S? Right { get; set; } }
    public sealed class D { public int Id { get; set; } public D? Left { get; set; } public D? Right { get; set; } }
    public sealed class FlatS { public int Id { get; set; } }
    public sealed class FlatD { public int Id { get; set; } }
    public sealed class LowerS { public int id { get; set; } public FlatS? Child { get; set; } }
    public sealed class LowerD { public int Id { get; set; } public FlatD? Child { get; set; } }
    public interface IBase { int Id { get; set; } }
    public class Base : IBase { public int Id { get; set; } public FlatS? Child { get; set; } }
    public sealed class Derived : Base { public int Extra { get; set; } }
    public sealed class BaseD { public int Id { get; set; } public FlatD? Child { get; set; } }
    public sealed class BadRoot { public BadChild? Child { get; set; } }
    public sealed class BadChild { public int Value => throw new InvalidOperationException(); }
    public sealed class BadRootD { public BadChildD? Child { get; set; } }
    public sealed class BadChildD { public int Value { get; set; } }
    public sealed class HookS { public static Action? Action; public static int Reads; int id; public int Id { get { Reads++; Action?.Invoke(); return id; } set => id = value; } public FlatS? Child { get; set; } }
    public sealed class HookD { public int Id { get; set; } public FlatD? Child { get; set; } }
}

public sealed class WrappedType : TypeDelegator
{
    public bool Throw;
    public int AssignCalls;
    public WrappedType(Type type) : base(type) { }
    public override bool IsGenericType => typeImpl.IsGenericType;
    public override bool ContainsGenericParameters => false;
    public override Type GetGenericTypeDefinition() => typeImpl.GetGenericTypeDefinition();
    public override Type[] GetGenericArguments() => typeImpl.GetGenericArguments();
    public override bool IsAssignableFrom(Type? candidate)
    {
        AssignCalls++;
        if (Throw) throw new ApplicationException("metadata");
        return base.IsAssignableFrom(candidate);
    }
}
