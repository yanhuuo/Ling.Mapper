using Ling.Mapper;
using Ling.Mapper.Configuration;
using Ling.Mapper.Extensions;
using Ling.Mapper.Mapper;
using Ling.Mapper.Models;

namespace TestConsole.Test;

public static class IndependentGraphSafetyRegressionTests
{
    public static void Run()
    {
int pass=0,fail=0,assertions=0;
void Check(bool b,string why){assertions++;if(!b)throw new Exception(why);}
void Case(string name,Action a){try{a();pass++;Console.WriteLine("PASS "+name);}catch(Exception e){fail++;Console.WriteLine("FAIL "+name+": "+e);}}
IMapper Make(MapperProfile? p=null){var c=new MapperConfiguration();if(p!=null)c.AddProfile(p);return c.CreateMapper();}
bool Rejected(Action a){try{a();return false;}catch(MappingException ex)when(ex.OriginalExceptionType==typeof(NotSupportedException).FullName){return true;}}
Case("object interface actual type alias and cycle",()=>{
 var x=new Node{Value=7};x.Next=x;var s=new Views{Object=x,Interface=x,Concrete=x};var r=Make().Map<Views>(s)!;
 Check(!ReferenceEquals(r,s)&&r.Object is Node n&&n.Value==7&&!ReferenceEquals(n,x)&&ReferenceEquals(n,r.Interface)&&ReferenceEquals(n,r.Concrete)&&ReferenceEquals(n,n.Next),"runtime shape identity/alias/cycle");r.Concrete!.Value=90;Check(x.Value==7,"source changed");
});
Case("list of object self cycle",()=>{
 var s=new List<object>();s.Add(s);s.Add(new Node{Value=3});var r=Make().Map<List<object>>(s)!;Check(!ReferenceEquals(s,r)&&ReferenceEquals(r,r[0])&&!ReferenceEquals(s[1],r[1])&&((Node)r[1]).Value==3,"list self cycle");
});
Case("mutable struct referenced property cloned",()=>{
 var list=new List<Node>{new(){Value=4}};var s=new RefStruct{Items=list};var r=Make().Map<RefStruct>(s);Check(!ReferenceEquals(r.Items,list)&&!ReferenceEquals(r.Items[0],list[0])&&r.Items[0].Value==4,"struct reference alias");
});
Case("readonly struct constructor referenced property cloned",()=>{
 var list=new List<Node>{new(){Value=4}};var s=new ReadonlyRefStruct(list);var r=Make().Map<ReadonlyRefStruct>(s);Check(!ReferenceEquals(r.Items,list)&&!ReferenceEquals(r.Items[0],list[0])&&r.Items[0].Value==4,"readonly struct alias");
});
Case("repeated boxed struct with references remains populated",()=>{
 var originalList=new List<Node>{new(){Value=42}};object shared=new RefStruct{Items=originalList};var r=Make().Map<Boxes>(new Boxes{A=shared,B=shared})!;
 var a=(RefStruct)r.A!;var b=(RefStruct)r.B!;Check(a.Items!=null&&b.Items!=null&&a.Items.Count==1&&b.Items.Count==1&&a.Items[0].Value==42&&b.Items[0].Value==42&&!ReferenceEquals(a.Items,originalList)&&!ReferenceEquals(b.Items,originalList),"cached boxed struct is an unpopulated snapshot");Check(ReferenceEquals(a.Items,b.Items),"boxed struct child alias lost");Check(ReferenceEquals(r.A,r.B),"same boxed source must return the same completed result box");
});
Case("boxed value self-cycle rejects safely or preserves complete box",()=>{
 object s=new BoxCycle();typeof(BoxCycle).GetProperty(nameof(BoxCycle.Back))!.SetValue(s,s);object? r=null;try{r=Make().Map<object>(s);}catch(MappingException e)when(e.OriginalExceptionType==typeof(InvalidOperationException).FullName){return;}
 Check(r!=null&&!ReferenceEquals(r,s)&&ReferenceEquals(r,((BoxCycle)r).Back),"boxed cycle fake default result");
});
Case("async-only resource rejected before constructing",()=>{
 var s=new AsyncResource{Value=9};AsyncResource.Constructions=0;Check(Rejected(()=>Make().Map<AsyncResource>(s))&&AsyncResource.Constructions==0,"async resource copied");
});
Case("public referenced field rejects rather than alias or drop",()=>{
 var s=new FieldStruct{Items=new(){new(){Value=4}}};Check(Rejected(()=>Make().Map<FieldStruct>(s)),"public fields silently accepted");
});
Case("constructor immutable shell clones mutable contents",()=>{
 var s=new Shell(new(){new(){Value=6}});var r=Make().Map<Shell>(s)!;Check(!ReferenceEquals(r,s)&&!ReferenceEquals(r.Items,s.Items)&&!ReferenceEquals(r.Items[0],s.Items[0])&&r.Items[0].Value==6,"shell mutable alias");
});
Case("getter-only initialized list cannot silently lose data",()=>{
 var s=new GetterOnly();s.Items.Add(new(){Value=8});GetterOnly? r=null;if(Rejected(()=>r=Make().Map<GetterOnly>(s)))return;
 Check(r!=null&&r.Items.Count==1&&r.Items[0].Value==8&&!ReferenceEquals(r.Items,s.Items)&&!ReferenceEquals(r.Items[0],s.Items[0]),"getter-only Items silently lost");
});
Case("getter-only private state cannot silently reset",()=>{
 var s=new PrivateState{Name="kept"};s.SetState(19);PrivateState? r=null;if(Rejected(()=>r=Make().Map<PrivateState>(s)))return;Check(r!=null&&r.State==19&&r.Name=="kept","private getter state silently reset");
});
Case("resource collection rejected before constructing",()=>{
 var s=new ResourceList(new Node[]{new(){Value=1}});ResourceList.Constructions=0;Check(Rejected(()=>Make().Map<ResourceList>(s)),"IDisposable collection clone was accepted");Check(ResourceList.Constructions==0,"resource constructor executed");
});
Case("MapInto preserves target but detaches aliased source child",()=>{
 var child=new Node{Value=9};var s=new One{Child=child};var d=new One{Child=child};var r=Make().MapInto(s,d);Check(ReferenceEquals(d,r)&&!ReferenceEquals(child,r.Child)&&child.Value==9&&r.Child!.Value==9,"update child aliases source");
});
Case("ordinary ForMember target typed selector clones",()=>{
 var child=new Node{Value=2};var m=Make(new Ordinary());var r=m.Map<One>(new One{Child=child})!;Check(!ReferenceEquals(child,r.Child)&&r.Child!.Value==12&&child.Value==2,"ordinary selector treated final");
});
Case("options MapFrom selector clones",()=>{
 var child=new Node{Value=2};var m=Make(new Options());var r=m.Map<One>(new One{Child=child})!;Check(!ReferenceEquals(child,r.Child)&&r.Child!.Value==12&&child.Value==2,"options selector treated final");
});
Case("explicit member final converter identity and single invocation",()=>{
 Counter.Calls=0;var child=new Node{Value=2};var m=Make(new Final());var r=m.Map<One>(new One{Child=child})!;Check(ReferenceEquals(child,r.Child)&&Counter.Calls==1&&child.Value==2,"final converter remapped or repeated");
});
Case("complex null substitute independent across calls and shared within graph",()=>{
 var n=new Node{Value=4};n.Next=n;var m=Make(new NullRules(n));var a=m.Map<NullPair>(new NullPair())!;var b=m.Map<NullPair>(new NullPair())!;
 Check(a.A!=null&&a.B!=null&&!ReferenceEquals(n,a.A)&&ReferenceEquals(a.A,a.B)&&ReferenceEquals(a.A,a.A.Next)&&!ReferenceEquals(a.A,b.A)&&a.A.Value==4,"substitute aliases config or breaks graph");a.A.Value=8;Check(n.Value==4&&b.A!.Value==4,"substitute crosscall mutation");
});
Case("null substitute target value bypasses source final converter",()=>{
 var n=new Node{Value=6};Counter.Calls=0;var m=Make(new NullFinalRules(n));var r=m.Map<One>(new TextInput())!;Check(r.Child!=null&&!ReferenceEquals(n,r.Child)&&r.Child.Value==6&&Counter.Calls==0,"substitute fed to source final converter or aliased");
});
Case("construction factory cannot return source root",()=>{
 var s=new Node{Value=3};var m=Make(new FactoryRules());bool rejected=false;try{m.Map<Node>(s);}catch(MappingException){rejected=true;}Check(rejected&&s.Value==3,"factory returned source or mutated source before rejection");
});
Case("sorted source cross-value interface comparer retained",()=>{
 var cmp=Comparer<string>.Create((a,b)=>StringComparer.OrdinalIgnoreCase.Compare(b,a));var s=new SortedList<string,TextNode>(cmp){{"alpha",new(){Value="7"}},{"zulu",new(){Value="9"}}};
 var r=Make().Map<IDictionary<string,NumberNode>>(s)!;Check(r.Keys.First()=="zulu"&&r.ContainsKey("ALPHA")&&r["ALPHA"].Value==7&&r["ZULU"].Value==9,"sorted cross-value comparer lost");
 var ro=Make().Map<System.Collections.ObjectModel.ReadOnlyDictionary<string,NumberNode>>(s)!;Check(ro.Keys.First()=="zulu"&&ro.ContainsKey("ALPHA")&&ro["ALPHA"].Value==7,"readonly sorted backing lost");
});
Case("dictionary changed key type ignores incompatible comparer",()=>{
 var s=new Dictionary<string,TextNode>(StringComparer.OrdinalIgnoreCase){{"1",new(){Value="7"}}};var r=Make().Map<Dictionary<int,NumberNode>>(s)!;Check(r.Count==1&&r[1].Value==7,"cross-key comparer applied");
});
Case("empty explicit profile preserves declared source scope",()=>{
 var m=Make(new EmptyProfile());var x=new PolyDerived{Value=4,Extra="derived"};var b=(PolyOut)m.Map(x,typeof(PolyBase),typeof(PolyOut))!;var d=m.Map<PolyOut>(x)!;Check(b.Extra==null&&d.Extra=="derived","standalone baseline");var r=m.Map<PolyPairOut>(new PolyPair{Base=x,Derived=x})!;Check(r.Base.Extra==null&&r.Derived.Extra=="derived"&&!ReferenceEquals(r.Base,r.Derived),"empty explicit profile conflated with runtime convention");
});
Case("whole registry converter identity and single invocation",()=>{
 int calls=0;var child=new RegistryNode{Value=2};TypeConverterRegistry.Register(typeof(RegistryNode),typeof(RegistryNode),new Func<RegistryNode,RegistryNode>(x=>{calls++;return x;}));var r=Make().Map<RegistryNode>(child);Check(ReferenceEquals(child,r)&&calls==1,"registry final remapped");
});
Case("readonly collection wrapper can preserve self cycle without source alias",()=>{
 var storage=new List<object>();var source=new System.Collections.ObjectModel.ReadOnlyCollection<object>(storage);storage.Add(source);storage.Add(new Node{Value=8});
 var result=Make().Map<System.Collections.ObjectModel.ReadOnlyCollection<object>>(source)!;
 Check(!ReferenceEquals(source,result)&&ReferenceEquals(result,result[0]),"readonly wrapper cycle");Check(!ReferenceEquals(source[1],result[1])&&((Node)result[1]).Value==8,"readonly wrapper contents");
});
Case("hash set self cycle and data are independent",()=>{
 var source=new HashSet<object>();source.Add(source);source.Add(new Node{Value=8});var result=Make().Map<HashSet<object>>(source)!;
 Check(!ReferenceEquals(source,result)&&result.Contains(result)&&!result.Contains(source),"set cycle");Check(result.OfType<Node>().Single().Value==8&&!ReferenceEquals(source.OfType<Node>().Single(),result.OfType<Node>().Single()),"set values");
});
Case("queue and stack clone retain ordering and self references",()=>{
 var queue=new Queue<object>();queue.Enqueue(queue);queue.Enqueue(new Node{Value=8});var q=Make().Map<Queue<object>>(queue)!;
 Check(!ReferenceEquals(queue,q)&&ReferenceEquals(q,q.Dequeue())&&((Node)q.Dequeue()).Value==8&&queue.Count==2,"queue graph/order");
 var stack=new Stack<object>();stack.Push(stack);stack.Push(new Node{Value=9});var t=Make().Map<Stack<object>>(stack)!;
 Check(!ReferenceEquals(stack,t)&&((Node)t.Pop()).Value==9&&ReferenceEquals(t,t.Pop())&&stack.Count==2,"stack graph/order");
});
Case("collection declared final converters and operators survive runtime derived items",()=>{
 var mapper=Make();var source=new List<ConvertedBase>{new ConvertedDerived{Value=4}};
 var before=mapper.Map<List<ConvertedOut>>(source)!;Check(before[0].Value==4,"convention before registration");
 int calls=0;TypeConverterRegistry.Register(typeof(ConvertedBase),typeof(ConvertedOut),new Func<ConvertedBase,ConvertedOut>(s=>{calls++;return new(){Value=s.Value+100};}));
 Check(mapper.Map<List<ConvertedOut>>(source)![0].Value==104,"declared list converter");
 Check(mapper.Map<ConvertedOut[]>(source)![0].Value==104,"declared array converter");
 Check(mapper.Map<HashSet<ConvertedOut>>(source)!.Single().Value==104,"declared set converter");
 var target=new List<ConvertedOut>();mapper.MapInto(source,target);Check(target[0].Value==104,"declared update converter");
 Check(mapper.Map<Dictionary<int,ConvertedOut>>(new Dictionary<int,ConvertedBase>{{1,source[0]}})![1].Value==104,"declared dictionary converter");
 Check(calls==5,"converter must run once per selected nonnull item");
 TypeConverterRegistry.Register(typeof(ConvertedBase),typeof(ConvertedOut),new Func<ConvertedBase,ConvertedOut>(s=>new(){Value=s.Value+300}));
 Check(mapper.Map<List<ConvertedOut>>(source)![0].Value==304,"hot replacement list converter");
 Check(mapper.Map<HashSet<ConvertedOut>>(source)!.Single().Value==304,"hot replacement set converter");
 var covariant=new List<ConvertedDerived>{new(){Value=8}};
 Check(((List<ConvertedOut>)mapper.Map(covariant,typeof(IEnumerable<ConvertedBase>),typeof(List<ConvertedOut>))!)[0].Value==308,"covariant declared sequence converter");
 Check(mapper.Map<CovariantOut>(new CovariantIn{Items=new ConvertedDerived[]{new(){Value=9}}})!.Items[0].Value==309,"covariant declared array converter");
 var sharedOut=mapper.Map<SharedContainerOut>(new SharedContainerIn{Base=covariant,Derived=covariant})!;
 Check(sharedOut.Base[0].Value==308&&sharedOut.Derived[0].Value==8&&!ReferenceEquals(sharedOut.Base,sharedOut.Derived),"explicit nested conversion scopes must not share different plans");
 mapper.MapInto(source,target);Check(target[0].Value==304,"hot replacement update converter");

 var sameSource=new List<SameConvertedDerived>{new(){Value=6}};
 var sameBefore=(IEnumerable<SameConvertedBase>)mapper.Map(sameSource,typeof(IEnumerable<SameConvertedBase>),typeof(IEnumerable<SameConvertedBase>))!;
 Check(sameBefore.Single().Value==6&&!ReferenceEquals(sameSource,sameBefore),"unconfigured covariant copy");
 TypeConverterRegistry.Register(typeof(SameConvertedBase),typeof(SameConvertedBase),new Func<SameConvertedBase,SameConvertedBase>(s=>new(){Value=s.Value+500}));
 var sameAfter=(IEnumerable<SameConvertedBase>)mapper.Map(sameSource,typeof(IEnumerable<SameConvertedBase>),typeof(IEnumerable<SameConvertedBase>))!;
 Check(sameAfter.Single().Value==506,"destination covariance cannot erase element converter target");
 var operators=new List<OperatorBase>{new OperatorDerived{Value=7}};
 Check(mapper.Map<List<OperatorOut>>(operators)![0].Value==207,"declared list operator");
 Check(mapper.Map<HashSet<OperatorOut>>(operators)!.Single().Value==207,"declared set operator");
 Check(((List<OperatorOut>)mapper.Map(new List<OperatorDerived>{new(){Value=8}},typeof(IEnumerable<OperatorBase>),typeof(List<OperatorOut>))!)[0].Value==208,"covariant declared sequence operator");
 Check(mapper.Map<Dictionary<int,OperatorOut>>(new Dictionary<int,OperatorBase>{{1,operators[0]}})![1].Value==207,"declared dictionary operator");
 var operatorTarget=new List<OperatorOut>();mapper.MapInto(operators,operatorTarget);Check(operatorTarget[0].Value==207,"declared update operator");
});
Console.WriteLine($"Independent graph safety: {pass} cases, {assertions} assertions passed; {fail} failed.");
if(fail!=0)throw new Exception($"Independent graph safety has {fail} failed cases.");


    }
public interface INode{int Value{get;set;}INode? Other{get;set;}}
public class Node:INode{public int Value{get;set;}public Node? Next{get;set;}public INode? Other{get;set;}}
public class Views{public object? Object{get;set;}public INode? Interface{get;set;}public Node? Concrete{get;set;}}
public struct RefStruct{public List<Node> Items{get;set;}}
public readonly record struct ReadonlyRefStruct(List<Node> Items);
public struct FieldStruct{public List<Node> Items;}
public class Shell{public List<Node> Items{get;}public Shell(List<Node> items){Items=items;}}
public class GetterOnly{public List<Node> Items{get;}=new();}
public class PrivateState{private int _state;public string? Name{get;set;}public int State=>_state;public void SetState(int s)=>_state=s;}
public class ResourceList:List<Node>,IDisposable{public static int Constructions;public ResourceList(IEnumerable<Node> source):base(source){Constructions++;}public void Dispose(){}}
public class One{public Node? Child{get;set;}}
public class Ordinary:MapperProfile{public Ordinary(){CreateMap<One,One>().ForMember(d=>d.Child,s=>s.Child);CreateMap<Node,Node>().ForMember(d=>d.Value,s=>s.Value+10);}}
public class Options:MapperProfile{public Options(){CreateMap<One,One>().ForMember(d=>d.Child,o=>o.MapFrom(s=>s.Child));CreateMap<Node,Node>().ForMember(d=>d.Value,s=>s.Value+10);}}
public class Final:MapperProfile{public Final(){CreateMap<One,One>().ForMember(d=>d.Child,o=>o.MapFrom(s=>s.Child).ConvertUsing<Node?>(n=>Counter.Final(n)));CreateMap<Node,Node>().ForMember(d=>d.Value,s=>s.Value+10);}}
public static class Counter{public static int Calls;public static Node? Final(Node? n){Calls++;return n;}public static Node FromText(string? s){Calls++;return new Node{Value=int.Parse(s!)};}}
public class RegistryNode{public int Value{get;set;}}

public class TextNode{public string? Value{get;set;}}
public class NumberNode{public int Value{get;set;}}

public class NullPair{public Node? A{get;set;}public Node? B{get;set;}}
public class NullRules:MapperProfile{public NullRules(Node n){CreateMap<NullPair,NullPair>().ForMember(d=>d.A,o=>o.NullSubstitute(n)).ForMember(d=>d.B,o=>o.NullSubstitute(n));}}
public class TextInput{public string? Raw{get;set;}}
public class NullFinalRules:MapperProfile{public NullFinalRules(Node n){CreateMap<TextInput,One>().ForMember(d=>d.Child,o=>o.MapFrom(s=>s.Raw).NullSubstitute(n).ConvertUsing<string?>(x=>Counter.FromText(x)));}}
public class FactoryRules:MapperProfile{public FactoryRules(){CreateMap<Node,Node>().ConstructUsing(s=>s).ForMember(d=>d.Value,s=>s.Value+1);}}

public class PolyBase{public int Value{get;set;}}
public class PolyDerived:PolyBase{public string? Extra{get;set;}}
public class PolyOut{public int Value{get;set;}public string? Extra{get;set;}}
public class PolyPair{public PolyBase Base{get;set;}=new();public PolyDerived Derived{get;set;}=new();}
public class PolyPairOut{public PolyOut Base{get;set;}=new();public PolyOut Derived{get;set;}=new();}
public class EmptyProfile:MapperProfile{public EmptyProfile()=>CreateMap<PolyBase,PolyOut>();}

public class Boxes{public object? A{get;set;}public object? B{get;set;}}

public struct BoxCycle{public object? Back{get;set;}}
public class AsyncResource:IAsyncDisposable{public static int Constructions;public int Value{get;set;}public AsyncResource(){Constructions++;}public ValueTask DisposeAsync()=>default;}

public class ConvertedBase{public int Value{get;set;}}
public class ConvertedDerived:ConvertedBase{}
public class ConvertedOut{public int Value{get;set;}}
public class OperatorBase{public int Value{get;set;}public static implicit operator OperatorOut(OperatorBase value)=>new(){Value=value.Value+200};}
public class OperatorDerived:OperatorBase{}
public class OperatorOut{public int Value{get;set;}}

public class CovariantIn{public ConvertedBase[] Items{get;set;}=Array.Empty<ConvertedBase>();}
public class CovariantOut{public List<ConvertedOut> Items{get;set;}=new();}
public class SharedContainerIn{public IEnumerable<ConvertedBase> Base{get;set;}=Array.Empty<ConvertedBase>();public List<ConvertedDerived> Derived{get;set;}=new();}
public class SharedContainerOut{public List<ConvertedOut> Base{get;set;}=new();public List<ConvertedOut> Derived{get;set;}=new();}

public class SameConvertedBase{public int Value{get;set;}}
public class SameConvertedDerived:SameConvertedBase{}

}
