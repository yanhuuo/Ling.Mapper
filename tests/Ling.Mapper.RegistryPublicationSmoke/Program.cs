using Ling.Mapper;
using Ling.Mapper.Configuration;
using Ling.Mapper.Mapper;
using Ling.Mapper.Models;
using Ling.Mapper.Extensions;

Publication.Run();
internal static class Publication
{
    static int checks;
    static readonly TimeSpan Timeout = TimeSpan.FromSeconds(60);
    static void Check(bool ok, string label)
    {
        Interlocked.Increment(ref checks);
        if (!ok) throw new Exception(label);
    }
    static IMapper New() => new MapperConfiguration().CreateMapper();
    public static void Run()
    {
        // Must precede every runtime registry registration in this fresh process.
        // Hand-written registry delegate tests JIT dispatch guards, NOT Native AOT.
        GeneratedMapperRegistry.Register<S<Generated>, D<Generated>>((s, _, _, _) => new() { Value=s.Value, Stamp=s.Stamp, Text=s.Text });
        var generated = GeneratedMapperRegistry.CreateMapper();
        Coherent(generated.Map<D<Generated>>(Source<Generated>())!, 0, 0, 7);
        var mapper = New();
        Warm<SameThread>(mapper);
        Register<SameThread>(1);
        Shapes<SameThread>(mapper, 1, 1);
        RejectProjection<SameThread>(mapper);
        RejectGenerated(() => GeneratedMapperRegistry.CreateMapper());
        RejectGenerated(() => generated.Map<D<Generated>>(Source<Generated>()));
        EventVisibility();
        ConcurrentVisibility();
        UserCodePublication();
        Console.WriteLine($"RECONSTRUCTED registry publication PASS: {checks} checks; IntPtr.Size={IntPtr.Size}; JIT only, no timing; not historical 6927 fixture.");
    }
    static S<T> Source<T>(int value = 7) => new() { Value=value, Stamp=0, Text="v0" };
    static void Register<T>(int stamp) => TypeConverterRegistry.Register(typeof(S<T>), typeof(D<T>),
        new Func<S<T>, D<T>>(s => new() { Value=s.Value + 100*stamp, Stamp=stamp, Text="v"+stamp }));
    static void Coherent<T>(D<T> value, int low, int high, int original)
        => Check((value.Stamp==low || value.Stamp==high) && value.Value==original+100*value.Stamp && value.Text=="v"+value.Stamp,
            "coherent complete registered result within allowed generations");
    static void Shapes<T>(IMapper mapper, int low, int high)
    {
        var a=Source<T>(); var b=Source<T>(8);
        Coherent(mapper.Map<D<T>>(a)!,low,high,7);
        var list=mapper.Map<List<D<T>>>(new List<S<T>> {a,b})!;
        Check(list.Count==2,"list exact count"); Coherent(list[0],low,high,7); Coherent(list[1],low,high,8);
        var array=mapper.Map<D<T>[]>(new[]{a,b})!;
        Check(array.Length==2,"array exact count"); Coherent(array[0],low,high,7); Coherent(array[1],low,high,8);
        var dictionary=mapper.Map<Dictionary<int,D<T>>>(new Dictionary<int,S<T>> {[1]=a,[2]=b})!;
        Check(dictionary.Count==2 && dictionary.ContainsKey(1) && dictionary.ContainsKey(2),"dictionary exact count/keys");
        Coherent(dictionary[1],low,high,7); Coherent(dictionary[2],low,high,8);
    }
    static void Warm<T>(IMapper mapper)
    {
        Shapes<T>(mapper,0,0);
        Coherent(mapper.GetProjection<S<T>,D<T>>().Compile()(Source<T>()),0,0,7);
        Coherent(mapper.Map<D<T>>(Source<T>())!,0,0,7);
    }
    static void RejectProjection<T>(IMapper mapper)
    {
        try { mapper.GetProjection<S<T>,D<T>>(); }
        catch (ProjectionException) { Check(true,"cached projection invalidated after registry change"); return; }
        throw new Exception("Runtime converter must not reuse stale cached projection");
    }
    static void RejectGenerated(Action action)
    {
        try { action(); }
        catch (NotSupportedException) { Check(true,"generated guard sees nonzero runtime version"); return; }
        catch (MappingException error) when (error.OriginalExceptionType == typeof(NotSupportedException).FullName)
        { Check(true,"existing generated mapper sees nonzero runtime version"); return; }
        throw new Exception("Generated runtime-registry guard did not reject");
    }
    static void EventVisibility()
    {
        var mapper=New(); Warm<EventTag>(mapper);
        using var ready=new Barrier(5);
        using var published=new ManualResetEventSlim();
        var tasks=Enumerable.Range(0,4).Select(_=>Task.Factory.StartNew(()=>
        {
            Warm<EventTag>(mapper);
            if(!ready.SignalAndWait(Timeout)) throw new TimeoutException("event readers ready");
            if(!published.Wait(Timeout)) throw new TimeoutException("published event");
            for(int i=0;i<32;i++) Shapes<EventTag>(mapper,2,2);
            RejectProjection<EventTag>(mapper);
            RejectGenerated(()=>GeneratedMapperRegistry.CreateMapper());
        },CancellationToken.None,TaskCreationOptions.LongRunning,TaskScheduler.Default)).ToArray();
        if(!ready.SignalAndWait(Timeout)) throw new TimeoutException("event writer ready");
        Register<EventTag>(2); // completes before Set; readers Wait before reading.
        published.Set();
        if(!Task.WaitAll(tasks,Timeout)) throw new TimeoutException("event readers completion");
        Shapes<EventTag>(mapper,2,2);
    }
    static void ConcurrentVisibility()
    {
        const int rounds=16;
        var mapper=New(); Register<ConcurrentTag>(0); Shapes<ConcurrentTag>(mapper,0,0);
        using var phases=new Barrier(5);
        using var cancellation=new CancellationTokenSource();
        void Meet() { if(!phases.SignalAndWait(Timeout,cancellation.Token)) throw new TimeoutException("concurrent phase"); }
        var tasks=Enumerable.Range(0,5).Select(worker=>Task.Factory.StartNew(()=>
        {
            try
            {
                for(int round=1;round<=rounds;round++)
                {
                    Meet();
                    if(worker==0) Register<ConcurrentTag>(round);
                    else for(int sample=0;sample<4;sample++) Shapes<ConcurrentTag>(mapper,round-1,round);
                    Meet(); // writer finished; readers must now observe this completed publication.
                    if(worker!=0) Shapes<ConcurrentTag>(mapper,round,round);
                    Meet(); // no next-round update overlaps exact convergence check.
                }
            }
            catch { cancellation.Cancel(); throw; }
        },CancellationToken.None,TaskCreationOptions.LongRunning,TaskScheduler.Default)).ToArray();
        if(!Task.WaitAll(tasks,Timeout)) { cancellation.Cancel(); throw new TimeoutException("concurrent completion"); }
        for(int i=0;i<32;i++) Shapes<ConcurrentTag>(mapper,rounds,rounds);
    }
    static void UserCodePublication()
    {
        var mapper=New();
        var source=new GetterSource();
        Coherent(mapper.Map<GetterTarget>(source)!.Child!,0,0,7);
        source.OnRead=()=>
        {
            Register<GetterTag>(3);
            Coherent(mapper.Map<D<GetterTag>>(Source<GetterTag>(9))!,3,3,9);
        };
        Coherent(mapper.Map<GetterTarget>(source)!.Child!,3,3,7);
        Check(source.Reads==2,"getter not replayed");
        source.OnRead=null;
        Coherent(mapper.Map<GetterTarget>(source)!.Child!,3,3,7);
        var ctorSource=new CtorSource();
        Coherent(mapper.Map<CtorTarget>(ctorSource)!.Child!,0,0,7);
        CtorTarget.OnCreate=()=>
        {
            Register<CtorTag>(4);
            Coherent(mapper.Map<D<CtorTag>>(Source<CtorTag>(9))!,4,4,9);
        };
        try { Coherent(mapper.Map<CtorTarget>(ctorSource)!.Child!,4,4,7); }
        finally { CtorTarget.OnCreate=null; }
        Coherent(mapper.Map<CtorTarget>(ctorSource)!.Child!,4,4,7);
        var items=new[]{Source<CallbackTag>(7),Source<CallbackTag>(8)};
        var warm=mapper.Map<D<CallbackTag>[]>(items)!;
        Check(warm.Length==2,"callback warm count");Coherent(warm[0],0,0,7);Coherent(warm[1],0,0,8);
        int calls=0;
        var output=mapper.Map<D<CallbackTag>[]>(items,AdaptOptions.Strict,(_,_)=>
        {
            if(++calls==1)
            {
                Register<CallbackTag>(5);
                Coherent(mapper.Map<D<CallbackTag>>(Source<CallbackTag>(9))!,5,5,9);
            }
        })!;
        Check(output.Length==2 && calls==2,"callbacks per item, count and order");
        Coherent(output[0],0,0,7); Coherent(output[1],5,5,8);
        Shapes<CallbackTag>(mapper,5,5);
        // Registered user converter updates another previously unregistered pair.
        Coherent(mapper.Map<D<ConverterTargetTag>>(Source<ConverterTargetTag>())!,0,0,7);
        int converterCalls=0;
        TypeConverterRegistry.Register(typeof(Trigger),typeof(TriggerResult),new Func<Trigger,TriggerResult>(s=>
        {
            converterCalls++; Register<ConverterTargetTag>(6);
            Coherent(mapper.Map<D<ConverterTargetTag>>(Source<ConverterTargetTag>(9))!,6,6,9);
            return new(){Value=s.Value};
        }));
        Check(mapper.Map<TriggerResult>(new Trigger{Value=19})!.Value==19 && converterCalls==1,"converter executes once");
        Shapes<ConverterTargetTag>(mapper,6,6);
    }
    public sealed class S<T> { public int Value{get;set;} public int Stamp{get;set;} public string Text{get;set;}="v0"; }
    public sealed class D<T> { public int Value{get;set;} public int Stamp{get;set;} public string Text{get;set;}="v0"; }
    public sealed class GetterSource { public Action? OnRead; public int Reads; public S<GetterTag> Child {get{Reads++;OnRead?.Invoke();return Source<GetterTag>();}} }
    public sealed class GetterTarget { public D<GetterTag>? Child{get;set;} }
    public sealed class CtorSource { public S<CtorTag> Child{get;set;}=Source<CtorTag>(); }
    public sealed class CtorTarget { public static Action? OnCreate; public CtorTarget(){OnCreate?.Invoke();} public D<CtorTag>? Child{get;set;} }
    public sealed class Trigger {public int Value{get;set;}}
    public sealed class TriggerResult {public int Value{get;set;}}
    public sealed class Generated{} public sealed class SameThread{} public sealed class EventTag{} public sealed class ConcurrentTag{} public sealed class GetterTag{} public sealed class CtorTag{} public sealed class CallbackTag{} public sealed class ConverterTargetTag{}
}
