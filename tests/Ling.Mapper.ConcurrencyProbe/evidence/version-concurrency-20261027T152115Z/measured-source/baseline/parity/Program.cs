// NEW RECONSTRUCTION from this agent's retained executed-source conversation.
// This is not a recovered historical file, historical SHA, or historical raw dataset.
using System.Collections;
using System.Globalization;
using System.Reflection;
using System.Runtime.CompilerServices;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Exporters.Json;
using BenchmarkDotNet.Jobs;
using BenchmarkDotNet.Running;
using Perfolizer.Horology;
using Ling.Mapper;
using Ling.Mapper.Models;
using Mapster;
using LM = Ling.Mapper.Mapper.IMapper;
using MM = MapsterMapper.IMapper;
namespace ReconstructedMapperParity;

public static class Program {
    public static void Main(string[] args) {
        CultureInfo.DefaultThreadCurrentCulture=CultureInfo.InvariantCulture;
        CultureInfo.DefaultThreadCurrentUICulture=CultureInfo.InvariantCulture;
        if(args.Contains("--validate")){Validation.Run();return;}
        if(!args.Contains("--list") && Environment.GetEnvironmentVariable("MAPPER_RECONSTRUCTED_TIMING_GO")!="1")
            throw new InvalidOperationException("New reconstruction: timing requires a separate verified CPU/source GO.");
        var job=Job.Default.WithId("ReconstructedNoTiering")
            .WithArguments(new Argument[]{new MsBuildArgument("/m:1")})
            .WithEnvironmentVariable("DOTNET_TieredCompilation","0").WithAffinity((IntPtr)4)
            .WithLaunchCount(2).WithWarmupCount(6).WithIterationCount(12)
            .WithIterationTime(TimeInterval.FromMilliseconds(250));
        BenchmarkSwitcher.FromAssembly(typeof(Program).Assembly).Run(args,
            ManualConfig.Create(DefaultConfig.Instance).AddJob(job).AddExporter(JsonExporter.Full));
    }
}
public sealed class SimpleSource {
    public int Id{get;set;} public string? Name{get;set;} public int Age{get;set;}
    public bool Active{get;set;} public decimal Amount{get;set;} public DateTime Created{get;set;}
    public Guid Key{get;set;} public string? Note{get;set;}
}
public sealed class SimpleDestination {
    public int Id{get;set;} public string? Name{get;set;} public int Age{get;set;}
    public bool Active{get;set;} public decimal Amount{get;set;} public DateTime Created{get;set;}
    public Guid Key{get;set;} public string? Note{get;set;}
}
public sealed class CustomSource { public int Id{get;set;} }
public sealed class CustomDestination { public int Value{get;set;} }
public static class UserWork {
    // Untimed audit only. Benchmark execution leaves Trace null, exactly as the historical design did.
    // Not a cache and never retains business objects. No custom getters/callbacks in these timed cases.
    public static List<int>? Trace;
    [MethodImpl(MethodImplOptions.NoInlining)]
    public static int Transform(int id){Trace?.Add(id);return checked(id*3+1);}
}
public sealed class GraphSource {
    public int Id{get;set;} public byte[] Bytes{get;set;}=Array.Empty<byte>();
    public GraphSource? Next{get;set;} public GraphSource? Alias{get;set;}
    public List<GraphSource?> Items{get;set;}=new();
    public Dictionary<string,GraphSource?> Children{get;set;}=new();
}
public sealed class GraphDestination {
    public int Id{get;set;} public byte[] Bytes{get;set;}=Array.Empty<byte>();
    public GraphDestination? Next{get;set;} public GraphDestination? Alias{get;set;}
    public List<GraphDestination?> Items{get;set;}=new();
    public Dictionary<string,GraphDestination?> Children{get;set;}=new();
}
public sealed class ContainerSource {
    public List<CustomSource>? Left{get;set;} public List<CustomSource>? Right{get;set;}
    public CustomSource?[]? ArrayA{get;set;} public CustomSource?[]? ArrayB{get;set;}
}
public sealed class ContainerDestination {
    public List<CustomDestination>? Left{get;set;} public List<CustomDestination>? Right{get;set;}
    public CustomDestination?[]? ArrayA{get;set;} public CustomDestination?[]? ArrayB{get;set;}
}
public static class Data {
    public static SimpleSource Simple(int id)=>new(){Id=id,Name="Ada",Age=37,Active=true,
        Amount=123.45m,Created=new DateTime(2026,1,2,3,4,5,DateTimeKind.Utc),
        Key=new Guid("12345678-1234-1234-1234-123456789abc"),Note=null};
    public static List<SimpleSource> Batch100()=>Enumerable.Range(0,100).Select(Simple).ToList();
    public static List<CustomSource> Custom100()=>Enumerable.Range(0,100).Select(i=>new CustomSource{Id=i}).ToList();
}
public sealed class Profile:MapperProfile {
    public Profile(){
        CreateMap<SimpleSource,SimpleDestination>();
        CreateMap<CustomSource,CustomDestination>().ForMember(d=>d.Value,s=>UserWork.Transform(s.Id));
        CreateMap<GraphSource,GraphDestination>();CreateMap<ContainerSource,ContainerDestination>();
    }
}
public static class Factory {
    public static (LM Ling,MM Mapster) Create(bool preserve){
        var l=new Ling.Mapper.Configuration.MapperConfiguration{DefaultAdaptOptions=AdaptOptions.Strict};
        l.AddProfile(new Profile());
        var m=new TypeAdapterConfig();
        m.NewConfig<SimpleSource,SimpleDestination>();
        m.NewConfig<CustomSource,CustomDestination>().Map(d=>d.Value,s=>UserWork.Transform(s.Id));
        m.NewConfig<GraphSource,GraphDestination>();m.NewConfig<ContainerSource,ContainerDestination>();
        if(preserve){
            m.Default.PreserveReference(true).ShallowCopyForSameType(false);
            m.ForType<SimpleSource,SimpleDestination>().PreserveReference(true);
            m.ForType<CustomSource,CustomDestination>().PreserveReference(true);
            m.ForType<GraphSource,GraphDestination>().PreserveReference(true);
            m.ForType<ContainerSource,ContainerDestination>().PreserveReference(true);
            m.NewConfig<List<SimpleSource>,List<SimpleDestination>>().PreserveReference(true);
            m.NewConfig<List<CustomSource>,List<CustomDestination>>().PreserveReference(true);
            m.NewConfig<CustomSource?[],CustomDestination?[]>().PreserveReference(true);
            m.NewConfig<List<GraphSource?>,List<GraphDestination?>>().PreserveReference(true);
            m.NewConfig<Dictionary<string,GraphSource?>,Dictionary<string,GraphDestination?>>().PreserveReference(true);
            m.NewConfig<byte[],byte[]>().PreserveReference(true).ShallowCopyForSameType(false);
        }
        return(l.CreateMapper(),new MapsterMapper.Mapper(m));
    }
}
public abstract class BenchBase {
    protected LM ling=null!;protected MM mapster=null!;
    protected abstract bool Preserve{get;}
    [GlobalSetup] public void Setup(){(ling,mapster)=Factory.Create(Preserve);Check();}
    protected abstract void Check();
}
[MemoryDiagnoser]public class List100DefaultBench:BenchBase {
    private readonly List<SimpleSource> source=Data.Batch100();protected override bool Preserve=>false;
    [Benchmark]public List<SimpleDestination> Ling()=>ling.Map<List<SimpleDestination>>(source)!;
    [Benchmark(Baseline=true)]public List<SimpleDestination> Mapster()=>mapster.Map<List<SimpleDestination>>(source);
    protected override void Check()=>Validation.SimpleList(source,Ling,Mapster);
}
[MemoryDiagnoser]public class List100PreserveControl:BenchBase {
    private readonly List<SimpleSource> source=Data.Batch100();protected override bool Preserve=>true;
    [Benchmark]public List<SimpleDestination> Ling()=>ling.Map<List<SimpleDestination>>(source)!;
    [Benchmark(Baseline=true)]public List<SimpleDestination> MapsterPreserve()=>mapster.Map<List<SimpleDestination>>(source);
    protected override void Check(){Validation.SimpleList(source,Ling,MapsterPreserve);Validation.SimpleAliases(ling,mapster);}
}
[MemoryDiagnoser]public class Custom100DefaultBench:BenchBase {
    private readonly List<CustomSource> source=Data.Custom100();protected override bool Preserve=>false;
    [Benchmark]public List<CustomDestination> Ling()=>ling.Map<List<CustomDestination>>(source)!;
    [Benchmark(Baseline=true)]public List<CustomDestination> Mapster()=>mapster.Map<List<CustomDestination>>(source);
    protected override void Check()=>Validation.CustomList(source,Ling,Mapster);
}
[MemoryDiagnoser]public class Custom100PreserveControl:BenchBase {
    private readonly List<CustomSource> source=Data.Custom100();protected override bool Preserve=>true;
    [Benchmark]public List<CustomDestination> Ling()=>ling.Map<List<CustomDestination>>(source)!;
    [Benchmark(Baseline=true)]public List<CustomDestination> MapsterPreserve()=>mapster.Map<List<CustomDestination>>(source);
    protected override void Check(){Validation.CustomList(source,Ling,MapsterPreserve);Validation.CustomAliases(ling,mapster);}
}
public static class Validation {
    public static void Require(bool ok,string text){if(!ok)throw new InvalidOperationException(text);}
    public static void SimpleList(List<SimpleSource> s,Func<List<SimpleDestination>> a,Func<List<SimpleDestination>> b){
        foreach(var f in new[]{a,b}){
            var x=f();var y=f();GraphAssert.SameValuesAndTopology(s,x);GraphAssert.SameValuesAndTopology(s,y);GraphAssert.Disjoint(x,y);
            Require(x.Count==100,"100 unique items");x[0].Id=-1;Require(s[0].Id==0&&y[0].Id==0,"item mutation isolation");
            x.RemoveAt(1);Require(s.Count==100&&y.Count==100,"list mutation isolation");
        }
    }
    public static void CustomList(List<CustomSource> s,Func<List<CustomDestination>> a,Func<List<CustomDestination>> b){
        foreach(var(name,f) in new[]{("Ling",a),("Mapster",b)}){
            var x=TraceMap(f,Enumerable.Range(0,100));var y=TraceMap(f,Enumerable.Range(0,100));
            Require(x.Count==100&&y.Count==100,"custom list count");
            for(int i=0;i<100;i++)Require(x[i].Value==i*3+1&&y[i].Value==i*3+1&&s[i].Id==i,"custom expected values");
            GraphAssert.Disjoint(s,x);GraphAssert.Disjoint(x,y);
            Require(x.Distinct(ReferenceEqualityComparer.Instance).Count()==100,"unique input cannot collapse");
            x[0].Value=-1;Require(s[0].Id==0&&y[0].Value==1,"custom mutation isolation");
            Console.WriteLine($"PASS {name} exact100 custom calls in order0..99, twice; custom getters/callbacks0; trace disabled after audit");
        }
    }
    static T TraceMap<T>(Func<T> map,IEnumerable<int> expected){
        try{UserWork.Trace=new();var d=map();Require(UserWork.Trace.SequenceEqual(expected),"exact user method count/order");return d;}
        finally{UserWork.Trace=null;}
    }
    public static void SimpleAliases(LM l,MM m){var a=Data.Simple(1);var b=Data.Simple(2);var s=new List<SimpleSource>{a,b,a};
        foreach(var f in new Func<List<SimpleDestination>>[]{()=>l.Map<List<SimpleDestination>>(s)!,()=>m.Map<List<SimpleDestination>>(s)}){
            var d=f();GraphAssert.SameValuesAndTopology(s,d);GraphAssert.Disjoint(d,f());d[0].Id=9;Require(d[2].Id==9&&a.Id==1,"simple alias and isolation");}
    }
    public static void CustomAliases(LM l,MM m){var a=new CustomSource{Id=7};var b=new CustomSource{Id=8};var list=new List<CustomSource>{a,b,a};
        foreach(var f in new Func<List<CustomDestination>>[]{()=>l.Map<List<CustomDestination>>(list)!,()=>m.Map<List<CustomDestination>>(list)}){
            var d=TraceMap(f,new[]{7,8});var e=TraceMap(f,new[]{7,8});
            Require(d.Count==3&&d[0].Value==22&&d[1].Value==25&&ReferenceEquals(d[0],d[2]),"custom repeated items preserve and values");GraphAssert.Disjoint(d,e);
        }
        var arr=new CustomSource?[]{a,null,a};var source=new ContainerSource{Left=list,Right=list,ArrayA=arr,ArrayB=arr};
        foreach(var f in new Func<ContainerDestination>[]{()=>l.Map<ContainerDestination>(source)!,()=>m.Map<ContainerDestination>(source)}){
            var d=TraceMap(f,new[]{7,8});var e=TraceMap(f,new[]{7,8});
            Require(ReferenceEquals(d.Left,d.Right)&&ReferenceEquals(d.ArrayA,d.ArrayB),"root-scope list/array container aliases");
            Require(ReferenceEquals(d.Left![0],d.Left[2])&&ReferenceEquals(d.Left[0],d.ArrayA![0])&&ReferenceEquals(d.ArrayA[0],d.ArrayA[2])&&d.ArrayA[1]==null,"shared elements across containers and null");
            GraphAssert.Disjoint(source,d);GraphAssert.Disjoint(d,e);d.ArrayA![0]!.Value=-1;Require(a.Id==7&&d.Left[0].Value==-1,"alias mutation isolation");
        }
        // Root array mapping must also establish a reference scope, not merely wrapper mappings.
        foreach(var f in new Func<CustomDestination?[]>[]{()=>l.Map<CustomDestination?[]>(arr)!,()=>m.Map<CustomDestination?[]>(arr)}){
            var d=TraceMap(f,new[]{7});Require(d.Length==3&&d[1]==null&&ReferenceEquals(d[0],d[2])&&d[0]!.Value==22,"root array aliases/value/null");}
    }
    static void FullGraph(LM l,MM m){var a=new GraphSource{Id=1,Bytes=new byte[]{1,2,3}};var b=new GraphSource{Id=2,Bytes=a.Bytes};
        a.Next=b;a.Alias=b;b.Next=a;b.Alias=a;a.Items=new(){a,b,null};b.Items=a.Items;
        a.Children=new(){{"root",a},{"child",b},{"null",null}};b.Children=a.Children;
        foreach(var f in new Func<GraphDestination>[]{()=>l.Map<GraphDestination>(a)!,()=>m.Map<GraphDestination>(a)}){
            var d=f();var e=f();GraphAssert.SameValuesAndTopology(a,d);GraphAssert.SameValuesAndTopology(a,e);GraphAssert.Disjoint(d,e);
            d.Bytes[0]=99;d.Children["probe"]=null;d.Items.RemoveAt(2);d.Next!.Id=77;
            Require(a.Bytes[0]==1&&!a.Children.ContainsKey("probe")&&a.Items.Count==3&&b.Id==2,"cycle/container/array/node mutation isolation");}
    }
    public static void Run(){foreach(var b in new BenchBase[]{new List100DefaultBench(),new List100PreserveControl(),new Custom100DefaultBench(),new Custom100PreserveControl()}){b.Setup();Console.WriteLine("PASS "+b.GetType().Name);}
        var(l,m)=Factory.Create(true);FullGraph(l,m);
        Require(l.Map<SimpleDestination>(null)==null&&m.Map<SimpleDestination>(null!)==null,"null root");
        var nil=new ContainerSource();GraphAssert.SameValuesAndTopology(nil,l.Map<ContainerDestination>(nil)!);GraphAssert.SameValuesAndTopology(nil,m.Map<ContainerDestination>(nil));
        Require(UserWork.Trace==null,"no instrumentation left enabled");Console.WriteLine("PASS full-cycle/alias/container/value/null/new-graph/cross-call/mutation probes; NEW reconstruction preflight");
    }
}
public static class GraphAssert {
    public static void SameValuesAndTopology(object? source,object? destination){var mapped=new Dictionary<object,object>(ReferenceEqualityComparer.Instance);var reverse=new HashSet<object>(ReferenceEqualityComparer.Instance);
        void Visit(object? s,object? d){if(s==null){Validation.Require(d==null,"null value");return;}Validation.Require(d!=null,"non-null result");var type=s.GetType();
            if(type.IsValueType||s is string){Validation.Require(s.Equals(d),"exact scalar value "+type.Name);return;}
            Validation.Require(!ReferenceEquals(s,d),"new mutable result");
            if(mapped.TryGetValue(s,out var prior)){Validation.Require(ReferenceEquals(prior,d),"alias/cycle identity");return;}
            mapped.Add(s,d!);Validation.Require(reverse.Add(d!),"distinct source objects not collapsed");
            if(s is IDictionary sd && d is IDictionary dd){Validation.Require(sd.Count==dd.Count,"dictionary count");foreach(DictionaryEntry x in sd){Validation.Require(dd.Contains(x.Key),"dictionary key");Visit(x.Value,dd[x.Key]);}return;}
            if(s is IEnumerable se && d is IEnumerable de){var x=se.Cast<object?>().ToArray();var y=de.Cast<object?>().ToArray();Validation.Require(x.Length==y.Length,"sequence length");for(int i=0;i<x.Length;i++)Visit(x[i],y[i]);return;}
            foreach(var p in type.GetProperties(BindingFlags.Public|BindingFlags.Instance).Where(p=>p.CanRead&&p.GetIndexParameters().Length==0)){var q=d!.GetType().GetProperty(p.Name);Validation.Require(q!=null,"destination member "+p.Name);Visit(p.GetValue(s),q!.GetValue(d));}}
        Visit(source,destination);Disjoint(source,destination);
    }
    public static void Disjoint(object? a,object? b){Validation.Require(!Nodes(a).Overlaps(Nodes(b)),"mutable graphs must be disjoint");}
    static HashSet<object> Nodes(object? root){var seen=new HashSet<object>(ReferenceEqualityComparer.Instance);void Visit(object? x){if(x==null||x is string||x.GetType().IsValueType||!seen.Add(x))return;
        if(x is IDictionary d){foreach(DictionaryEntry e in d)Visit(e.Value);}else if(x is IEnumerable e){foreach(var v in e)Visit(v);}else foreach(var p in x.GetType().GetProperties(BindingFlags.Public|BindingFlags.Instance).Where(p=>p.CanRead&&p.GetIndexParameters().Length==0))Visit(p.GetValue(x));}
        Visit(root);return seen;}
}
