using Ling.Mapper;
using Ling.Mapper.Mapper;
using Ling.Mapper.Configuration;
using Ling.Mapper.Models;

Checks.Run();
internal static class Checks
{
    static int checks;
    static void Check(bool ok, string label) { Interlocked.Increment(ref checks); if (!ok) throw new Exception(label); }
    static void Same(object? a, object? b, string label) => Check(ReferenceEquals(a,b), label);
    static IMapper New() => new MapperConfiguration().CreateMapper();
    public static void Run()
    {
        Graphs(); RulesAndRuntime(); Registration(); Failures(); ParallelUse();
        Console.WriteLine($"RECOVERED member-plan smoke PASS: {checks} checks; reconstructed source, not byte-identical historical fixture; no timing.");
    }
    static void Graphs()
    {
        var m = New(); var leaf = new Leaf { Value = 7 }; var input = new Graph { A=leaf, B=leaf, List=new(){leaf,null,leaf}, Array=new[]{leaf,leaf}, Dict=new(){["a"]=leaf,["b"]=leaf} };
        Leaf.Reads=OutLeaf.Writes=OutLeaf.Ctors=0;
        var o=m.Map<OutGraph>(input)!;
        Check(o.List!.Count==3 && o.Array!.Length==2 && o.Dict!.Count==2 && o.List[1]==null, "graph counts/null");
        Same(o.A,o.B,"member alias"); Same(o.A,o.List[0],"list alias"); Same(o.A,o.List[2],"list repeated alias"); Same(o.A,o.Array![1],"array alias"); Same(o.A,o.Dict!["b"],"dict alias");
        Check(o.A!.Value==7 && Leaf.Reads==1 && OutLeaf.Ctors==1 && OutLeaf.Writes==1,"getter/ctor/setter once");
        leaf.Value=8; Check(o.A.Value==7,"source mutation isolation"); o.A.Value=9; Check(leaf.Value==8,"target mutation isolation");
        var again=m.Map<OutGraph>(input)!; Check(!ReferenceEquals(again.A,o.A)&&again.A!.Value==8,"fresh root data");
        var cycle=new Node{Value=12}; cycle.Next=cycle; var mapped=m.Map<OutNode>(cycle)!; Same(mapped,mapped.Next,"cycle early register"); Check(mapped.Value==12,"cycle data");
        var calls=0; Leaf.Reads=0;
        var list=m.Map<OutLeaf?[]>(new Leaf?[]{leaf,null,leaf},AdaptOptions.Strict,(_,_)=>calls++)!;
        Check(list.Length==3&&list[1]==null&&calls==2&&Leaf.Reads==1,"callback per non-null occurrence/getter once"); Same(list[0],list[2],"callback alias");
        var target=new OutLeaf(); Same(target,m.MapInto(leaf,target),"MapInto identity"); Check(target.Value==8,"MapInto value");
        Check(m.Map<StructTarget>(leaf).Value==8,"struct destination");
        Bound.Calls=0; var bound=m.Map<Bound[]>(new[]{leaf,leaf})!; Same(bound[0],bound[1],"constructor binding alias"); Check(bound[0].Value==8&&Bound.Calls==1,"bound constructor once");
        var cfg=new MapperConfiguration(); cfg.AddProfile(new FactoryRules()); Factory.Calls=0;
        var factory=cfg.CreateMapper().Map<Factory[]>(new[]{leaf,leaf})!; Same(factory[0],factory[1],"factory alias"); Check(factory[0].Value==8&&Factory.Calls==1,"factory once");
        var ignored=m.Map<OutLeaf[]>(new[]{leaf,leaf},AdaptOptions.Strict,ignoreNames:new[]{"Value"})!; Check(ignored[0].Value==0,"ignore value"); Same(ignored[0],ignored[1],"ignore alias");
        var privateResult=m.Map<PrivateOut>(new PrivateIn{Value=19})!; Check(privateResult.Value==19,"private nested access");
        Failure(()=>m.Map<PrivateOut>(new PrivateThrow()),"$.Value",typeof(ApplicationException));
        for(int i=0;i<1000;i++) GC.KeepAlive(m.Map<OutLeaf>(leaf));
        var before=GC.GetAllocatedBytesForCurrentThread(); for(int i=0;i<1000;i++) GC.KeepAlive(m.Map<OutLeaf>(leaf)); var allocated=GC.GetAllocatedBytesForCurrentThread()-before;
        before=GC.GetAllocatedBytesForCurrentThread(); for(int i=0;i<1000;i++) GC.KeepAlive(new OutLeaf()); var direct=GC.GetAllocatedBytesForCurrentThread()-before;
        Check(allocated==direct,"flat allocation only result"); Console.WriteLine($"Flat allocation: mapped={allocated}, direct={direct}; count=1000; not timed.");
    }
    static void RulesAndRuntime()
    {
        var cfg=new MapperConfiguration();cfg.AddProfile(new ScopeRules());var m=cfg.CreateMapper(); var s=new Derived{Value=11,Extra=22};
        var src=new ScopeSource{A=s,B=s,Again=s};
        for(int i=0;i<4;i++){var o=m.Map<ScopeTarget>(src)!;Check(o.A!.Value==111&&o.B!.Value==211,"declared profile");Same(o.A,o.Again,"declared same scope");Check(!ReferenceEquals(o.A,o.B),"declared different scope");}
        var plain=New();foreach(Base x in new Base[]{new(){Value=1},new Derived{Value=2},new(){Value=3},new Derived{Value=4}}) Check(plain.Map<RuntimeTarget>(new RuntimeSource{Child=x})!.Child!.Value==x.Value,"runtime alternating source");
        var clone=plain.Map<CloneTarget>(new CloneSource{Child=s})!;Check(clone.Child is Derived{Value:11,Extra:22}&&!ReferenceEquals(clone.Child,s),"assignable runtime shape independent");s.Extra=33;Check(((Derived)clone.Child!).Extra==22,"clone extra isolation");((Derived)clone.Child).Extra=44;Check(s.Extra==33,"reverse clone isolation");
        cfg=new MapperConfiguration();cfg.AddProfile(new CloneRules());var projected=cfg.CreateMapper().Map<CloneTarget>(new CloneSource{Child=s})!;Check(projected.Child!.GetType()==typeof(Base)&&projected.Child.Value==111,"explicit base projection");
        cfg=new MapperConfiguration();cfg.AddProfile(new IgnoreRules());var ignored=cfg.CreateMapper().Map<IgnoreTarget>(new IgnoreSource{A=s,B=s})!;Check(ignored.A!.Value==0&&ignored.B!.Value==11&&!ReferenceEquals(ignored.A,ignored.B),"projection scope separation");
    }
    static void Register<T>(int stamp)=>TypeConverterRegistry.Register(typeof(S<T>),typeof(D<T>),new Func<S<T>,D<T>>(s=>new(){Value=s.Value+stamp*100,Stamp=stamp}));
    static Pair<T> Input<T>()=>new(){First=new(){Value=7},Second=new(){Value=8}};
    static void Verify<T>(Result<T> r,int a,int b){Check(r.First!.Value==7+a*100&&r.First.Stamp==a,"first checksum");Check(r.Second!.Value==8+b*100&&r.Second.Stamp==b,"second checksum");}
    static void Registration()
    {
        var m=New();var g=new GetterSource();Check(m.Map<GetterTarget>(g)!.Child!.Value==7,"getter warm");g.Enabled=true;Check(m.Map<GetterTarget>(g)!.Child is{Value:107,Stamp:1}&&g.Reads==2,"getter first registration current field once");
        var cs=new CtorSource();Check(m.Map<CtorTarget>(cs)!.Child!.Value==7,"ctor warm");CtorTarget.Hook=()=>Register<CtorTag>(2);try{Check(m.Map<CtorTarget>(cs)!.Child is{Value:207,Stamp:2},"constructor registration current field");}finally{CtorTarget.Hook=null;}
        Check(m.Map<CtorTarget>(cs)!.Child!.Value==207,"constructor next root");
        var nested=Input<NestedTag>();Verify(m.Map<Result<NestedTag>>(nested)!,0,0);int reads=0;D<NestedTag>? reentry=null;
        S<NestedTag>.Hook=()=>{if(++reads==1){Register<NestedTag>(3);reentry=m.Map<D<NestedTag>>(new S<NestedTag>{Value=9});}};
        try{Verify(m.Map<Result<NestedTag>>(nested)!,0,3);Check(reentry is{Value:309,Stamp:3},"nested getter reentry");Verify(m.Map<Result<NestedTag>>(nested)!,3,3);}finally{S<NestedTag>.Hook=null;}
        var inline=Input<InlineTag>();Verify(m.Map<Result<InlineTag>>(inline)!,0,0);int calls=0;
        TypeConverterRegistry.Register(typeof(S<InlineTag>),typeof(D<InlineTag>),new Func<S<InlineTag>,D<InlineTag>>(s=>{calls++;Register<InlineTag>(5);var r=m.Map<D<InlineTag>>(new S<InlineTag>{Value=9})!;Check(r is{Value:509,Stamp:5},"inline replacement reentry new");return new(){Value=s.Value+400,Stamp=4};}));
        Verify(m.Map<Result<InlineTag>>(inline)!,4,4);Check(calls==2,"existing pre-registered inline expressions remain captured within parent");Verify(m.Map<Result<InlineTag>>(inline)!,5,5);
        var trigger=new TriggerSource{First=new(){Value=1},Second=new(){Value=8}};Check(m.Map<TriggerTarget>(trigger)!.Second!.Value==8,"trigger warm");
        TypeConverterRegistry.Register(typeof(TriggerIn),typeof(TriggerOut),new Func<TriggerIn,TriggerOut>(s=>{Register<TriggeredTag>(9);Check(m.Map<D<TriggeredTag>>(new S<TriggeredTag>{Value=9}) is{Value:909,Stamp:9},"trigger reentry");return new(){Value=s.Value+1000};}));
        var tr=m.Map<TriggerTarget>(trigger)!;Check(tr.First!.Value==1001&&tr.Second is{Value:908,Stamp:9},"converter first registration applies dynamic sibling");
        var cb=Input<CallbackTag>();Verify(m.Map<Result<CallbackTag>>(cb)!,0,0);calls=0;Verify(m.Map<Result<CallbackTag>>(cb,AdaptOptions.Strict,(_,_)=>{calls++;Register<CallbackTag>(6);})!,0,0);Check(calls==1,"callback root only");Verify(m.Map<Result<CallbackTag>>(cb)!,6,6);
        var items=new[]{Input<ItemTag>(),Input<ItemTag>()};m.Map<Result<ItemTag>[]>(items);calls=0;var output=m.Map<Result<ItemTag>[]>(items,AdaptOptions.Strict,(_,_)=>{if(++calls==1)Register<ItemTag>(7);})!;Check(output.Length==2&&calls==2,"collection callback count");Verify(output[0],0,0);Verify(output[1],7,7);
    }
    static MappingException Failure(Action action,string path,Type original){try{action();}catch(MappingException e){Check(e.Path==path&&e.OriginalExceptionType==original.FullName,"failure path/original");return e;}throw new Exception("expected failure");}
    static void Failures()
    {
        var m=New();var s=new RetryRoot{Child=new(){Value=31,Throw=true}};var first=Failure(()=>m.Map<RetryTarget>(s),"$.Child.Value",typeof(ApplicationException));Check(s.Child.Reads==1,"failed getter once");s.Child.Throw=false;Check(m.Map<RetryTarget>(s)!.Child!.Value==31&&s.Child.Reads==2,"getter retry success");s.Child.Throw=true;var second=Failure(()=>m.Map<RetryTarget>(s),"$.Child.Value",typeof(ApplicationException));Check(!ReferenceEquals(first,second)&&s.Child.Reads==3,"fresh failure after success");
        ThrowCtor.Throw=true;ThrowCtor.Calls=0;var cs=new RetryRoot{Child=new(){Value=41}};Failure(()=>m.Map<CtorRetryTarget>(cs),"$.Child",typeof(ApplicationException));Check(ThrowCtor.Calls==1,"constructor failed once");ThrowCtor.Throw=false;var a=m.Map<CtorRetryTarget>(cs)!;var b=m.Map<CtorRetryTarget>(cs)!;Check(a.Child!.Value==41&&ThrowCtor.Calls==3&&!ReferenceEquals(a.Child,b.Child),"constructor retry no cached data");
        var foreign=Failure(()=>m.Map<byte>(1000),"$",typeof(OverflowException));ForeignSource.Error=foreign;ForeignSource.Reads=0;var wrapped=Failure(()=>m.Map<ForeignTarget>(new ForeignRoot()),"$.Child.Value",typeof(MappingException));Check(!ReferenceEquals(foreign,wrapped)&&ForeignSource.Reads==1,"foreign origin rewrap once");int callbacks=0;Failure(()=>m.Map<OutLeaf[]>(new[]{new Leaf{Value=1}},AdaptOptions.Strict,(_,_)=>{callbacks++;throw foreign;}),"$[0]",typeof(MappingException));Check(callbacks==1,"foreign callback once");
        ThrowSetter.Writes=0;Failure(()=>m.Map<ThrowSetter[]>(new[]{new Leaf{Value=1}}),"$[0].Value",typeof(ApplicationException));Check(ThrowSetter.Writes==1,"setter failure once");
    }
    static void ParallelUse(){var m=New();var s=Input<ParallelTag>();Verify(m.Map<Result<ParallelTag>>(s)!,0,0);Parallel.For(0,256,_=>{var r=m.Map<Result<ParallelTag>>(s)!;Verify(r,0,0);Check(!ReferenceEquals(r.First,r.Second),"parallel fresh fields");});Register<ParallelTag>(8);Parallel.For(0,256,_=>Verify(m.Map<Result<ParallelTag>>(s)!,8,8));}
    public sealed class Leaf{int value;public static int Reads;public int Value{get{Reads++;return value;}set=>this.value=value;}}
    public sealed class OutLeaf{int value;public static int Writes,Ctors;public OutLeaf(){Ctors++;}public int Value{get=>value;set{Writes++;this.value=value;}}}
    public sealed class Graph{public Leaf? A{get;set;}public Leaf? B{get;set;}public List<Leaf?>? List{get;set;}public Leaf[]? Array{get;set;}public Dictionary<string,Leaf>? Dict{get;set;}}
    public sealed class OutGraph{public OutLeaf? A{get;set;}public OutLeaf? B{get;set;}public List<OutLeaf?>? List{get;set;}public OutLeaf[]? Array{get;set;}public Dictionary<string,OutLeaf>? Dict{get;set;}}
    public sealed class Node{public int Value{get;set;}public Node? Next{get;set;}}public sealed class OutNode{public int Value{get;set;}public OutNode? Next{get;set;}}
    public struct StructTarget{public int Value{get;set;}}public sealed class Bound{public static int Calls;public Bound(int value){Calls++;Value=value;}public int Value{get;}}
    public sealed class Factory{public static int Calls;public Factory(){Calls++;}public int Value{get;set;}}public sealed class FactoryRules:MapperProfile{public FactoryRules(){CreateMap<Leaf,Factory>().ConstructUsing(s=>new Factory());}}
    private sealed class PrivateIn{public int Value{get;set;}}private sealed class PrivateOut{public int Value{get;set;}}private sealed class PrivateThrow{public int Value=>throw new ApplicationException();}
    public class Base{public int Value{get;set;}}public sealed class Derived:Base{public int Extra{get;set;}}public sealed class ScopeResult{public int Value{get;set;}}
    public sealed class ScopeSource{public Base? A{get;set;}public Derived? B{get;set;}public Base? Again{get;set;}}public sealed class ScopeTarget{public ScopeResult? A{get;set;}public ScopeResult? B{get;set;}public ScopeResult? Again{get;set;}}
    public sealed class ScopeRules:MapperProfile{public ScopeRules(){CreateMap<Base,ScopeResult>().ForMember(d=>d.Value,s=>s.Value+100);CreateMap<Derived,ScopeResult>().ForMember(d=>d.Value,s=>s.Value+200);}}
    public sealed class RuntimeSource{public Base? Child{get;set;}}public sealed class RuntimeTarget{public ScopeResult? Child{get;set;}}public sealed class CloneSource{public Derived? Child{get;set;}}public sealed class CloneTarget{public Base? Child{get;set;}}
    public sealed class CloneRules:MapperProfile{public CloneRules(){CreateMap<Derived,Base>().ForMember(d=>d.Value,s=>s.Value+100);}}public sealed class IgnoreSource{public Base? A{get;set;}public Base? B{get;set;}}public sealed class IgnoreTarget{public ScopeResult? A{get;set;}public ScopeResult? B{get;set;}}public sealed class IgnoreRules:MapperProfile{public IgnoreRules(){CreateMap<IgnoreSource,IgnoreTarget>().Ignore(d=>d.A!.Value);}}
    public sealed class S<T>{int value;public static Action? Hook;public int Value{get{Hook?.Invoke();return value;}set=>this.value=value;}}public sealed class D<T>{public int Value{get;set;}public int Stamp{get;set;}}public sealed class Pair<T>{public S<T>? First{get;set;}public S<T>? Second{get;set;}}public sealed class Result<T>{public D<T>? First{get;set;}public D<T>? Second{get;set;}}
    public sealed class GetterSource{public bool Enabled;public int Reads;public S<GetterTag> Child{get{Reads++;if(Enabled)Register<GetterTag>(1);return new(){Value=7};}}}public sealed class GetterTarget{public D<GetterTag>? Child{get;set;}}
    public sealed class CtorSource{public S<CtorTag> Child{get;set;}=new(){Value=7};}public sealed class CtorTarget{public static Action? Hook;public CtorTarget(){Hook?.Invoke();}public D<CtorTag>? Child{get;set;}}
    public sealed class TriggerIn{public int Value{get;set;}}public sealed class TriggerOut{public int Value{get;set;}}public sealed class TriggerSource{public TriggerIn? First{get;set;}public S<TriggeredTag>? Second{get;set;}}public sealed class TriggerTarget{public TriggerOut? First{get;set;}public D<TriggeredTag>? Second{get;set;}}
    public sealed class GetterTag{}public sealed class CtorTag{}public sealed class NestedTag{}public sealed class InlineTag{}public sealed class TriggeredTag{}public sealed class CallbackTag{}public sealed class ItemTag{}public sealed class ParallelTag{}
    public sealed class RetrySource{int value;public bool Throw;public int Reads;public int Value{get{Reads++;if(Throw)throw new ApplicationException();return value;}set=>this.value=value;}}public sealed class RetryRoot{public RetrySource? Child{get;set;}}public sealed class RetryTarget{public OutLeaf? Child{get;set;}}
    public sealed class ThrowCtor{public static bool Throw;public static int Calls;public ThrowCtor(){Calls++;if(Throw)throw new ApplicationException();}public int Value{get;set;}}public sealed class CtorRetryTarget{public ThrowCtor? Child{get;set;}}
    public sealed class ForeignSource{public static MappingException Error=null!;public static int Reads;public int Value{get{Reads++;throw Error;}}}public sealed class ForeignRoot{public ForeignSource Child{get;set;}=new();}public sealed class ForeignTarget{public OutLeaf? Child{get;set;}}
    public sealed class ThrowSetter{public static int Writes;public int Value{set{Writes++;throw new ApplicationException();}}}
}
