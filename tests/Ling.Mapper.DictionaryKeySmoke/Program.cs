using System.Collections;
using System.Diagnostics;
using System.Reflection;
using Ling.Mapper;
using Ling.Mapper.Configuration;
using Ling.Mapper.Mapper;
using Ling.Mapper.Models;

if(args.Length==0)
{
    var total=0;
    foreach(var scenario in new[]{"basic","initial","move","current","ctor","contains","add","comparer","comparer-add","getter","callback","collision","foreign","fallback","profiles"})
    {
        var start=new ProcessStartInfo(Environment.ProcessPath!) { RedirectStandardOutput=true, RedirectStandardError=true, UseShellExecute=false };
        start.ArgumentList.Add(Assembly.GetExecutingAssembly().Location); start.ArgumentList.Add(scenario);
        using var process=Process.Start(start)!;var output=process.StandardOutput.ReadToEnd();var error=process.StandardError.ReadToEnd();process.WaitForExit();
        Console.Write(output);if(process.ExitCode!=0)throw new Exception(scenario+": "+error);
        total+=int.Parse(output.Split('\n').Single(x=>x.StartsWith("CHECKS=")).Substring(7));
    }
    Console.WriteLine($"Dictionary key smoke PASS: {total} checks in 15 fresh processes; no timing.");
}
else Tests.Run(args[0]);
internal static class Tests
{
    static int checks;
    static void Check(bool ok,string label){Interlocked.Increment(ref checks);if(!ok)throw new Exception(label);}
    static IMapper New()=>new MapperConfiguration{IncludeSourceValueInExceptions=true}.CreateMapper();
    static void Key(int offset)=>TypeConverterRegistry.Register(typeof(int),typeof(long),new Func<int,long>(x=>(long)x+offset));
    static void Value(int offset)=>TypeConverterRegistry.Register(typeof(S),typeof(D),new Func<S,D>(s=>new(){Value=s.Value+offset}));
    static Dictionary<int,S> Input()=>new(){[1]=new(){Value=7},[2]=new(){Value=8}};
    public static void Run(string scenario)
    {
        var m=New();
        if(scenario is "move" or "current") m.Map<Dictionary<long,D>>(new SourceDictionary(Input()));
        else if(scenario is "ctor" or "contains" or "add") {m.Map<HookDictionary<D>>(Input());HookDictionary<D>.ContainsCalls=HookDictionary<D>.AddCalls=0;}
        else if(scenario is "comparer" or "comparer-add") {ComparerDictionary<D>.Seed=scenario=="comparer";m.Map<ComparerDictionary<D>>(Input());}
        else if(scenario is "getter" or "callback") m.Map<Dictionary<long,D>>(Input());
        S.Reads=0;
        switch(scenario)
        {
            case "basic":
                var shared=new S{Value=19};var source=new Dictionary<int,S>{{int.MinValue,shared},{0,new(){Value=23}},{int.MaxValue,shared}};
                var o=m.Map<Dictionary<long,D>>(source)!;
                Check(o.Count==3&&o.Keys.SequenceEqual(new[]{(long)int.MinValue,0L,(long)int.MaxValue}),"keys/order/extrema");
                Check(o[int.MinValue].Value==19&&o[0].Value==23&&ReferenceEquals(o[int.MinValue],o[int.MaxValue]),"values/alias");
                shared.Value=31;Check(o[int.MinValue].Value==19,"independent source mutation");o[int.MinValue].Value=37;Check(shared.Value==31,"independent dest mutation");
                var into=new Dictionary<long,D>{{99,new(){Value=1}}};SameInto(m,source,into);
                var again=m.Map<Dictionary<long,D>>(source)!;Check(!ReferenceEquals(o[int.MinValue],again[int.MinValue])&&again[int.MinValue].Value==31,"fresh results");
                Parallel.For(0,128,_=>{var p=m.Map<Dictionary<long,D>>(source)!;Check(p.Count==3&&p[0].Value==23&&p[int.MinValue].Value==31&&ReferenceEquals(p[int.MinValue],p[int.MaxValue]),"parallel map");});break;
            case "initial":Key(100);Value(10);Verify(m.Map<Dictionary<long,D>>(Input())!,101,102,17,18);break;
            case "move":case "current":
                var sequence=new SourceDictionary(Input());sequence.OnMove=scenario=="move"?()=>Key(100):null;sequence.OnCurrent=scenario=="current"?()=>Key(100):null;
                Verify(m.Map<Dictionary<long,D>>(sequence)!,101,102,7,8);Check(sequence.Triggers==1,"iterator hook once");break;
            case "ctor":
                HookDictionary<D>.Construct=()=>{Key(100);Value(100);};Verify(m.Map<HookDictionary<D>>(Input())!,101,102,107,108);break;
            case "contains":
                HookDictionary<D>.Contains=()=>{Key(100);Value(100);};Verify(m.Map<HookDictionary<D>>(Input())!,1,102,107,108);Check(HookDictionary<D>.ContainsCalls==4,"two mapping ContainsKey plus two verification ContainsKey");break;
            case "add":
                HookDictionary<D>.Adding=()=>{Key(100);Value(100);};Verify(m.Map<HookDictionary<D>>(Input())!,1,102,7,108);Check(HookDictionary<D>.AddCalls==2,"add count");break;
            case "comparer":
                ComparerDictionary<D>.Hook=()=>{Key(100);Value(100);};var c=m.Map<ComparerDictionary<D>>(Input())!;
                Verify(c,1,102,107,108,3);Check(c.ContainsKey(-999),"comparer sentinel");break;
            case "comparer-add":
                ComparerDictionary<D>.Hook=()=>{Key(100);Value(100);};Verify(m.Map<ComparerDictionary<D>>(Input())!,1,102,7,108);break;
            case "getter":
                S.Hook=()=>Key(100);Verify(m.Map<Dictionary<long,D>>(Input())!,1,102,7,8);Check(S.Reads==2,"value getter not replayed");break;
            case "callback":
                var calls=0;var output=m.Map<Dictionary<long,D>>(Input(),AdaptOptions.Strict,(_,_)=>{if(++calls==1){Key(100);Value(100);}})!;
                Verify(output,1,102,7,108);Check(calls==2,"value callback once per entry");break;
            case "collision":
                TypeConverterRegistry.Register(typeof(int),typeof(long),new Func<int,long>(_=>1));S.Reads=0;
                var colliding=Input();Error(()=>m.Map<Dictionary<long,D>>(colliding),"$[1].Key",typeof(ArgumentException),true);Check(S.Reads==1&&colliding[2].LocalReads==0,"collision rejects before offending value getter, exactly zero reads");break;
            case "foreign":
                var foreign=Error(()=>m.Map<byte>(1000),"$",typeof(OverflowException),false);int keyCalls=0;
                TypeConverterRegistry.Register(typeof(int),typeof(long),new Func<int,long>(_=>{keyCalls++;throw foreign;}));
                var wrapped=Error(()=>m.Map<Dictionary<long,D>>(Input()),"$[0].Key",typeof(MappingException),true);Check(keyCalls==1&&!ReferenceEquals(foreign,wrapped),"foreign key origin wraps once");break;
            case "fallback":
                Error(()=>m.Map<Dictionary<int,D>>(new Dictionary<long,S>{{long.MaxValue,new(){Value=7}}}),"$[0].Key",typeof(OverflowException),true);
                var text=m.Map<Dictionary<int,D>>(new Dictionary<string,S>{{"7",new(){Value=8}}})!;Check(text.Count==1&&text[7].Value==8,"string fallback");
                var nullable=m.Map<Dictionary<long?,D>>(Input())!;Check(nullable.Count==2&&nullable[1].Value==7&&nullable[2].Value==8,"nullable fallback");
                var enums=m.Map<Dictionary<Number,D>>(Input())!;Check(enums.Count==2&&enums[Number.One].Value==7&&enums[Number.Two].Value==8,"enum fallback");
                var operators=m.Map<Dictionary<long,D>>(new Dictionary<OperatorKey,S>{{new OperatorKey(9),new(){Value=11}}})!;Check(operators.Count==1&&operators[109].Value==11,"user operator fallback");break;
            case "profiles":
                var keyConfig=new MapperConfiguration();keyConfig.AddProfile(new KeyRules());var keyResult=keyConfig.CreateMapper().Map<Dictionary<long,D>>(Input())!;Verify(keyResult,1,2,7,8);
                var a=new MapperConfiguration();a.AddProfile(new RulesA());var b=new MapperConfiguration();b.AddProfile(new RulesB());var ma=a.CreateMapper();var mb=b.CreateMapper();
                Parallel.For(0,128,i=>{var p=(i%2==0?ma:mb).Map<Dictionary<long,D>>(Input())!;Verify(p,1,2,i%2==0?17:27,i%2==0?18:28);});break;
            default:throw new Exception("unknown scenario");
        }
        Console.WriteLine($"SCENARIO {scenario} PASS");Console.WriteLine($"CHECKS={checks}");
    }
    static void SameInto(IMapper m, Dictionary<int,S> source, Dictionary<long,D> destination)
    { var result=m.MapInto(source,destination);Check(ReferenceEquals(result,destination)&&destination.Count==3&&destination[int.MinValue].Value==31,"MapInto unchanged dictionary identity/data");}
    static void Verify(IDictionary<long,D> result,long first,long second,int one,int two,int count=2)
    {Check(result.Count==count&&result.ContainsKey(first)&&result.ContainsKey(second),"count/keys");Check(result[first].Value==one&&result[second].Value==two,"ordered values");}
    static MappingException Error(Action action,string path,Type original,bool sensitive)
    {try{action();}catch(MappingException e){Check(e.Path==path&&e.OriginalExceptionType==original.FullName,"error path/original");if(sensitive)Check(e.SourceValue=="[REDACTED]","key diagnostic sensitive");return e;}throw new Exception("expected failure");}
    public sealed class S{int value;public static Action? Hook;public static int Reads;public int LocalReads;public int Value{get{Reads++;LocalReads++;var h=Hook;Hook=null;h?.Invoke();return value;}set=>this.value=value;}}
    public sealed class D{public int Value{get;set;}}
    public enum Number{One=1,Two=2}
    public readonly record struct OperatorKey(int Value){public static implicit operator long(OperatorKey value)=>value.Value+100L;}
    public sealed class KeyRules:MapperProfile{public KeyRules(){CreateMap<int,long>();}}
    public sealed class RulesA:MapperProfile{public RulesA(){CreateMap<S,D>().ForMember(d=>d.Value,s=>s.Value+10);}}
    public sealed class RulesB:MapperProfile{public RulesB(){CreateMap<S,D>().ForMember(d=>d.Value,s=>s.Value+20);}}
    public sealed class HookDictionary<T>:Dictionary<long,T>,IDictionary<long,T>
    {
        public static Action? Construct,Contains,Adding;public static int ContainsCalls,AddCalls;
        public HookDictionary(){var h=Construct;Construct=null;h?.Invoke();}
        bool IDictionary<long,T>.ContainsKey(long key){ContainsCalls++;var h=Contains;Contains=null;h?.Invoke();return base.ContainsKey(key);}
        void IDictionary<long,T>.Add(long key,T value){AddCalls++;var h=Adding;Adding=null;h?.Invoke();base.Add(key,value);}
    }
    public sealed class ComparerDictionary<T>:Dictionary<long,T>
    {
        public static Action? Hook;public static bool Seed;
        public ComparerDictionary():base(new HookComparer()){HookComparer.Active=false;if(Seed)Add(-999,default!);HookComparer.Active=true;}
        sealed class HookComparer:IEqualityComparer<long>
        {
            public static bool Active;
            public bool Equals(long a,long b)=>a==b;
            public int GetHashCode(long value){if(Active){var h=Hook;Hook=null;h?.Invoke();}return value.GetHashCode();}
        }
    }
    public sealed class SourceDictionary:IReadOnlyDictionary<int,S>
    {
        readonly Dictionary<int,S> values;public Action? OnMove,OnCurrent;public int Triggers;
        public SourceDictionary(Dictionary<int,S> values)=>this.values=values;
        public S this[int key]=>values[key];public IEnumerable<int> Keys=>values.Keys;public IEnumerable<S> Values=>values.Values;public int Count=>values.Count;
        public bool ContainsKey(int key)=>values.ContainsKey(key);public bool TryGetValue(int key,out S value)=>values.TryGetValue(key,out value!);
        public IEnumerator<KeyValuePair<int,S>> GetEnumerator()=>new Cursor(this,values.GetEnumerator());IEnumerator IEnumerable.GetEnumerator()=>GetEnumerator();
        sealed class Cursor:IEnumerator<KeyValuePair<int,S>>
        {
            readonly SourceDictionary owner;Dictionary<int,S>.Enumerator inner;
            public Cursor(SourceDictionary owner,Dictionary<int,S>.Enumerator inner){this.owner=owner;this.inner=inner;}
            public bool MoveNext(){var h=owner.OnMove;owner.OnMove=null;if(h!=null){owner.Triggers++;h();}return inner.MoveNext();}
            public KeyValuePair<int,S> Current{get{var h=owner.OnCurrent;owner.OnCurrent=null;if(h!=null){owner.Triggers++;h();}return inner.Current;}}
            object IEnumerator.Current=>Current;public void Dispose()=>inner.Dispose();public void Reset()=>throw new NotSupportedException();
        }
    }
}
