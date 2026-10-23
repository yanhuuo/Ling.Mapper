using System.Globalization;
using System.Text.Json;
using Ling.Mapper;
using Ling.Mapper.Configuration;
using Ling.Mapper.Extensions;
using Ling.Mapper.Models;
using Ling.Mapper.Provider;

namespace TestConsole.Test;

/// <summary>Finite value/API boundary contracts, using BCL or explicit rule outcomes as independent oracles.</summary>
public static class ValueAndApiBoundaryRegressionTests
{
    public static void Run()
    {
        var mapper = new MapperConfiguration().CreateMapper();
        var failures = new List<Exception>(); var count = 0;
        void Case(string name, Action action) { count++; try { action(); } catch(Exception e) { failures.Add(new Exception(name,e)); Console.WriteLine($"FAIL value/API: {name}: {e.Message}"); } }
        void Matrix(string label, object value, Type target, Func<object?> oracle)
        {
            object? expected=null; Exception? expectedError=null;
            try { expected=oracle(); } catch(Exception e) { expectedError=e; }
            foreach(var entry in new[]{"R","M","C","N"}) Case($"{label}/{entry}",()=> {
                object? actual;
                try { actual=At(value,value.GetType(),target,entry); }
                catch(MappingException e) {
                    if(expectedError==null) throw;
                    Equal(value.GetType(),e.SourceType); Equal(target,e.DestinationType); Equal(Path(entry),e.Path); Equal(expectedError.GetType().FullName,e.OriginalExceptionType); return;
                }
                if(expectedError!=null) throw new Exception("Expected oracle exception: "+expectedError.GetType().Name);
                Equal(expected,actual);
                if(expected is DateTime d) { Equal(d.Kind,((DateTime)actual!).Kind); Equal(d.Ticks,((DateTime)actual!).Ticks); }
                if(expected is DateTimeOffset dto) { Equal(dto.Offset,((DateTimeOffset)actual!).Offset); Equal(dto.Ticks,((DateTimeOffset)actual!).Ticks); }
            });
        }
        var enumTypes=new[]{typeof(U8),typeof(I8),typeof(U16),typeof(I16),typeof(U32),typeof(I32),typeof(U64),typeof(I64)};
        foreach(var type in enumTypes)
        {
            var underlying=Enum.GetUnderlyingType(type);
            foreach(var text in new[]{"None","One","Alias","One, Two","7","-1","18446744073709551615","18446744073709551616","missing"})
                Matrix($"{type.Name} parse '{text}'",text,type,()=>Enum.Parse(type,text,true));
            foreach(var value in new object[]{0,7,-1,ulong.MaxValue})
                Matrix($"{type.Name} numeric {value}",value,type,()=>Enum.ToObject(type,Convert.ChangeType(value,underlying,CultureInfo.InvariantCulture)));
            var flags=Enum.ToObject(type,3);
            Matrix($"{type.Name} flags formatting",flags,typeof(string),()=>flags.ToString());
            Matrix($"{type.Name} flags numeric",flags,typeof(long),()=>Convert.ChangeType(flags,typeof(long),CultureInfo.InvariantCulture));
        }
        Matrix("negative flags",SignedFlags.All,typeof(string),()=>SignedFlags.All.ToString());
        Matrix("alias formatting",I32.Alias,typeof(string),()=>I32.Alias.ToString());
        foreach(var text in new[]{"true","FALSE"," true ","1","0","","yes"}) Matrix("bool parse "+text,text,typeof(bool),()=>Convert.ChangeType(text,typeof(bool),CultureInfo.InvariantCulture));
        foreach(var text in new[]{"A","\0","中","é","","AB","😀"}) Matrix("char parse "+text,text,typeof(char),()=>Convert.ChangeType(text,typeof(char),CultureInfo.InvariantCulture));
        foreach(var value in new object[]{true,false,'A','中','\0'}) Matrix("bool/char format",value,typeof(string),()=>Convert.ToString(value,CultureInfo.InvariantCulture));
        foreach(var value in new[]{0,1,-1}) Matrix("numeric bool "+value,value,typeof(bool),()=>Convert.ToBoolean(value));
        foreach(var value in new[]{0,65535,-1,65536}) Matrix("numeric char "+value,value,typeof(char),()=>Convert.ToChar(value));
        foreach(var value in new[]{char.MinValue,char.MaxValue}) Matrix("char numeric bound",value,typeof(int),()=>Convert.ToInt32(value));
        foreach(var text in new[]{Guid.Empty.ToString(),"ffffffff-ffff-ffff-ffff-ffffffffffff","{01234567-89ab-cdef-0123-456789abcdef}","0123456789abcdef0123456789abcdef","bad","","01234567-89ab-cdef-0123-456789abcdeg"}) Matrix("Guid parse "+text,text,typeof(Guid),()=>Guid.Parse(text));
        foreach(var guid in new[]{Guid.Empty,Guid.Parse("ffffffff-ffff-ffff-ffff-ffffffffffff")}) Matrix("Guid format",guid,typeof(string),()=>guid.ToString());
        foreach(var value in new object[]{(sbyte)-1,byte.MaxValue,short.MinValue,ushort.MaxValue,int.MinValue,uint.MaxValue,long.MinValue,ulong.MaxValue,decimal.MaxValue,-0.0f,-0.0d,float.NaN,double.NaN,float.PositiveInfinity,double.NegativeInfinity})
            Matrix("numeric string "+value.GetType().Name,value,typeof(string),()=>((IFormattable)value).ToString(null,CultureInfo.InvariantCulture));
        var oldCulture=CultureInfo.CurrentCulture;
        try {
            CultureInfo.CurrentCulture=CultureInfo.InvariantCulture;
            foreach(var date in new object[]{DateTime.MinValue,DateTime.MaxValue,DateTimeOffset.MinValue,DateTimeOffset.MaxValue,DateOnly.MinValue,DateOnly.MaxValue,TimeOnly.MinValue,TimeOnly.MaxValue,TimeSpan.MinValue,TimeSpan.MaxValue})
                Matrix("date bound formatting "+date.GetType().Name,date,typeof(string),()=>date.ToString());
            foreach(var text in new[]{"0001-01-01T00:00:00","9999-12-31T23:59:59.9999999","2024-02-29","2023-02-29","10000-01-01","9999-12-31T23:59:59.99999999"}) {
                Matrix("DateTime bound "+text,text,typeof(DateTime),()=>Convert.ToDateTime(text));
                Matrix("DateOnly bound "+text,text,typeof(DateOnly),()=>DateOnly.Parse(text));
            }
            foreach(var text in new[]{"0001-01-01T00:00:00+00:00","9999-12-31T23:59:59.9999999+00:00","0001-01-01T00:00:00+14:00","9999-12-31T23:59:59.9999999-14:00","2023-02-29T00:00:00+08:00"})
                Matrix("DateTimeOffset bound "+text,text,typeof(DateTimeOffset),()=>DateTimeOffset.Parse(text));
            foreach(var text in new[]{"00:00:00","23:59:59.9999999","12:34:56.1234567","24:00:00","23:59:59.99999999"}) Matrix("TimeOnly precision "+text,text,typeof(TimeOnly),()=>TimeOnly.Parse(text));
            foreach(var text in new[]{"10675199.02:48:05.4775807","-10675199.02:48:05.4775808","10675199.02:48:05.4775808","-10675199.02:48:05.4775809"}) Matrix("TimeSpan tick bound "+text,text,typeof(TimeSpan),()=>TimeSpan.Parse(text));
        } finally { CultureInfo.CurrentCulture=oldCulture; }
        Case("Scalar null value",()=>Throws<ArgumentNullException>(()=>ScalarConversion.Convert(null!,typeof(int))));
        Case("Scalar null destination",()=>Throws<ArgumentNullException>(()=>ScalarConversion.Convert(1,null!)));
        foreach(var type in new[]{typeof(void),typeof(List<>),typeof(int).MakeByRefType(),typeof(object)}) Case("Scalar unsupported destination "+type,()=>Throws<InvalidCastException>(()=>ScalarConversion.Convert(1,type)));
        Case("nullable exact source-only rule",()=> {
            var calls=0; TypeConverterRegistry.Register(typeof(RuleValue?),typeof(RuleTarget),new Func<RuleValue?,RuleTarget>(v=>{calls++;return new(v!.Value.Number+10);}));
            Equal(new RuleTarget(12),mapper.Map(new RuleValue(2),typeof(RuleValue?),typeof(RuleTarget))); Equal(1,calls);
            Equal(new RuleTarget(12),mapper.Map<Box<RuleTarget>>(new Box<RuleValue?>{Value=new RuleValue(2)})!.Value); Equal(2,calls);
            Equal(default(RuleTarget),mapper.Map(null,typeof(RuleValue?),typeof(RuleTarget))); Equal(2,calls);
        });
        Case("nullable exact target-only rule",()=> {
            var calls=0; TypeConverterRegistry.Register(typeof(RuleValue),typeof(RuleTarget?),new Func<RuleValue,RuleTarget?>(v=>{calls++;return new(v.Number+20);}));
            Equal(new RuleTarget(22),mapper.Map(new RuleValue(2),typeof(RuleValue),typeof(RuleTarget?))); Equal(1,calls);
            Equal(new RuleTarget(22),mapper.Map<Box<RuleTarget?>>(new Box<RuleValue>{Value=new RuleValue(2)})!.Value); Equal(2,calls);
        });
        Case("nullable exact both rule and null bypass",()=> {
            var calls=0; TypeConverterRegistry.Register(typeof(RuleValue?),typeof(RuleTarget?),new Func<RuleValue?,RuleTarget?>(v=>{calls++;return new(v!.Value.Number+30);}));
            Equal(new RuleTarget(32),mapper.Map(new RuleValue(2),typeof(RuleValue?),typeof(RuleTarget?))); Equal(1,calls);
            Equal(new RuleTarget(32),mapper.Map<Box<RuleTarget?>>(new Box<RuleValue?>{Value=new RuleValue(2)})!.Value); Equal(2,calls);
            Equal(null,mapper.Map(null,typeof(RuleValue?),typeof(RuleTarget?))); Equal(2,calls);
            var strict=new MapperConfiguration{NullValueHandling=NullValueHandling.Throw}.CreateMapper();
            Throws<MappingException>(()=>strict.Map(null,typeof(RuleValue?),typeof(RuleTarget))); Equal(2,calls);
        });
        Case("nullable converter null result accepted under both null policies",()=> {
            var calls=0; TypeConverterRegistry.Register(typeof(NullRuleValue),typeof(RuleTarget?),new Func<NullRuleValue,RuleTarget?>(_=>{calls++;return null;}));
            foreach(var policy in new[]{NullValueHandling.UseDefault,NullValueHandling.Throw}) {
                var configured=new MapperConfiguration{NullValueHandling=policy}.CreateMapper();
                Equal(null,configured.Map<RuleTarget?>(new NullRuleValue()));
                Equal(null,configured.Map<Box<RuleTarget?>>(new Box<NullRuleValue>{Value=new()})!.Value);
            }
            Equal(4,calls);
        });
        var previous=MapperProvider.Current;
        try {
            MapperProvider.SetCurrent(mapper);
            Case("Adapt result callback throws then recovers",()=> { Throws<InvalidOperationException>(()=>new TextDto{Value="2"}.Adapt<NumberDto>(_=>throw new InvalidOperationException("callback"))); Equal(2,new TextDto{Value="2"}.Adapt<NumberDto>().Value); });
            Case("Adapt typed callback throws then recovers",()=> { Throws<MappingException>(()=>new TextDto{Value="2"}.Adapt<NumberDto,TextDto>((_,_)=>throw new InvalidOperationException("callback"))); Equal(2,new TextDto{Value="2"}.Adapt<NumberDto>().Value); });
            Case("Adapt item callback throws then recovers",()=> { IEnumerable<TextDto> values=new[]{new TextDto{Value="2"}}; Throws<MappingException>(()=>values.Adapt<NumberDto,TextDto>((_,_)=>throw new InvalidOperationException("callback"))); Equal(2,values.Adapt<NumberDto,TextDto>((_,_)=>{})[0].Value); });
            Case("Adapt typed null source has no item callback",()=> { var calls=0; TextDto source=null!; Equal(null,source.Adapt<NumberDto,TextDto>((_,_)=>calls++)); Equal(0,calls); });
            Case("Adapt preserves converter null under both null policies",()=> {
                TypeConverterRegistry.Register(typeof(NullDtoSource),typeof(NumberDto),new Func<NullDtoSource,NumberDto>(_=>null!));
                try {
                    foreach(var policy in new[]{NullValueHandling.UseDefault,NullValueHandling.Throw}) {
                        var configured=new MapperConfiguration{NullValueHandling=policy}.CreateMapper();MapperProvider.SetCurrent(configured);
                        var itemCalls=0;var resultCalls=0;var source=new NullDtoSource();
                        Equal(null,configured.Map<NumberDto>(source));Equal(null,source.Adapt<NumberDto>());
                        Equal(null,source.Adapt<NumberDto,NullDtoSource>((_,_)=>itemCalls++));Equal(0,itemCalls);
                        Equal(null,source.Adapt<NumberDto>(result=>{resultCalls++;Equal(null,result);}));Equal(1,resultCalls);
                    }
                } finally { MapperProvider.SetCurrent(mapper); }
            });
            Case("null collection elements do not trigger callback",()=> {
                var calls=0; IEnumerable<TextDto> values=new TextDto[]{new(){Value="2"},null!,new(){Value="3"}};
                var result=values.Adapt<NumberDto,TextDto>((_,_)=>calls++); Equal(3,result.Count); Equal(null,result[1]); Equal(2,calls);
            });
            Case("whole collection converter result callback once",()=> {
                TypeConverterRegistry.Register(typeof(List<WholeSource>),typeof(List<WholeTarget>),new Func<List<WholeSource>,List<WholeTarget>>(_=>new(){new(){Value=7},new(){Value=8}}));
                var calls=0; var result=new List<WholeSource>{new()}.Adapt<List<WholeTarget>>(items=>{calls++;Equal(2,items.Count);}); Equal(1,calls);Equal(7,result[0].Value);
            });
            Case("whole collection converter skips item callbacks on reordered/expanded output",()=> {
                TypeConverterRegistry.Register(typeof(List<WholeSource>),typeof(List<WholeTarget>),new Func<List<WholeSource>,List<WholeTarget>>(_=>new(){new(){Value=9},new(){Value=3},new(){Value=7}}));
                IEnumerable<WholeSource> source=new List<WholeSource>{new(),new()}; var calls=0;
                var result=source.Adapt<WholeTarget,WholeSource>((_,_)=>calls++);
                Equal(0,calls); Equal(3,result.Count); Equal(9,result[0].Value); Equal(7,result[2].Value);
            });
            Case("whole collection null result has zero item callbacks and one result callback",()=> {
                TypeConverterRegistry.Register(typeof(List<NullWholeSource>),typeof(List<NullWholeTarget>),new Func<List<NullWholeSource>,List<NullWholeTarget>>(_=>null!));
                var source=new List<NullWholeSource>{new()};var itemCalls=0;var resultCalls=0;
                var items=((IEnumerable<NullWholeSource>)source).Adapt<NullWholeTarget,NullWholeSource>((_,_)=>itemCalls++);
                Equal(0,items.Count);Equal(0,itemCalls);
                var result=source.Adapt<List<NullWholeTarget>>(values=>{resultCalls++;Equal(null,values);});
                Equal(null,result);Equal(1,resultCalls);
            });
        } finally { MapperProvider.SetCurrent(previous); }
        TypeConverterRegistry.RegisterJson<JsonPayload>();
        Case("RegisterJson reverse equals serializer",()=> { var value=new JsonPayload{Name="sample",Count=7}; Equal(JsonSerializer.Serialize(value),mapper.Map<string>(value)); });
        Case("RegisterJson forward equals deserializer",()=> { const string json="{\"Name\":\"sample\",\"Count\":7}"; var expected=JsonSerializer.Deserialize<JsonPayload>(json)!; var result=mapper.Map<JsonPayload>(json)!;Equal(expected.Name,result.Name);Equal(expected.Count,result.Count); });
        foreach(var text in new[]{"","   ","null"}) Case("RegisterJson empty/null "+text,()=>Equal(null,mapper.Map<JsonPayload>(text)));
        Case("RegisterJson null source",()=> { Equal(null,mapper.Map<JsonPayload>(null)); Equal(null,mapper.Map(null,typeof(JsonPayload),typeof(string))); });
        foreach(var text in new[]{"{bad}","{\"Count\":\"not-number\"}",string.Concat(Enumerable.Repeat("{\"Child\":",70))+"{}"+new string('}',70)}) Case("RegisterJson malformed/deep input",()=> {
            var expected=Throws<JsonException>(()=>JsonSerializer.Deserialize<JsonPayload>(text)); var error=Throws<MappingException>(()=>mapper.Map<JsonPayload>(text)); Equal(expected.GetType().FullName,error.OriginalExceptionType);Equal("$",error.Path);
        });
        Case("RegisterJson serialization depth overflow",()=> {
            var root=new JsonPayload();var node=root;for(var i=0;i<70;i++){node.Child=new();node=node.Child;}
            var expected=Throws<JsonException>(()=>JsonSerializer.Serialize(root));var error=Throws<MappingException>(()=>mapper.Map<string>(root));Equal(expected.GetType().FullName,error.OriginalExceptionType);
        });
        foreach(var invalid in new string?[]{null,"","   "})
        foreach(var source in new object?[]{null,new NumberDto{Value=2},new TextDto{Value="2"}})
        {
            var label=source==null ? "null-source" : source.GetType()==typeof(NumberDto) ? "same-type" : "different-type";
            Case($"IMapper generic rejects invalid ignore/{label}/{invalid??"<null>"}",()=>
                Throws<ArgumentException>(()=>mapper.Map<NumberDto>(source,AdaptOptions.Default,ignoreNames:new[]{invalid!})));
            Case($"IMapper runtime rejects invalid ignore/{label}/{invalid??"<null>"}",()=>
                Throws<ArgumentException>(()=>mapper.Map(source,source?.GetType()??typeof(TextDto),typeof(NumberDto),AdaptOptions.Default,ignoreNames:new[]{invalid!})));
        }
        var beforeIgnoreTests=MapperProvider.Current;
        try {
            MapperProvider.SetCurrent(mapper);
            foreach(var invalid in new string?[]{null,"   "})
            foreach(var source in new object?[]{null,new NumberDto{Value=2},new TextDto{Value="2"}})
            {
                var label=source==null ? "null-source" : source.GetType()==typeof(NumberDto) ? "same-type" : "different-type";
                Case($"Adapt firstIgnore rejects invalid/{label}/{invalid??"<null>"}",()=>Throws<ArgumentException>(()=>source.Adapt<NumberDto>(invalid!)));
            }
            Case("Adapt unknown nonempty ignore is no-op",()=>Equal(2,new TextDto{Value="2"}.Adapt<NumberDto>("DoesNotExist").Value));
        } finally { MapperProvider.SetCurrent(beforeIgnoreTests); }
        Case("IMapper unknown nonempty ignore is no-op",()=> {
            var source=new TextDto{Value="2"};
            Equal(2,mapper.Map<NumberDto>(source,AdaptOptions.Default,ignoreNames:new[]{"DoesNotExist"})!.Value);
            Equal(2,((NumberDto)mapper.Map(source,typeof(TextDto),typeof(NumberDto),AdaptOptions.Default,ignoreNames:new[]{"DoesNotExist"})!).Value);
        });
        Case("IMapper empty ignores equal null for mapped objects",()=> {
            var source=new TextDto{Value="2"};
            Equal(mapper.Map<NumberDto>(source,AdaptOptions.Default,ignoreNames:null)!.Value,mapper.Map<NumberDto>(source,AdaptOptions.Default,ignoreNames:Array.Empty<string>())!.Value);
            Equal(((NumberDto)mapper.Map(source,typeof(TextDto),typeof(NumberDto),AdaptOptions.Default,ignoreNames:null)!).Value,((NumberDto)mapper.Map(source,typeof(TextDto),typeof(NumberDto),AdaptOptions.Default,ignoreNames:Array.Empty<string>())!).Value);
        });
        Case("IMapper empty ignores preserve null and identity fast paths",()=> {
            var same=new NumberDto{Value=2};
            Equal(null,mapper.Map<NumberDto>(null,AdaptOptions.Default,ignoreNames:Array.Empty<string>()));
            True(ReferenceEquals(same,mapper.Map<NumberDto>(same,AdaptOptions.Default,ignoreNames:Array.Empty<string>())),"Empty ignores changed assignable identity");
            Equal(null,mapper.Map(null,typeof(TextDto),typeof(NumberDto),AdaptOptions.Default,ignoreNames:Array.Empty<string>()));
            True(ReferenceEquals(same,mapper.Map(same,typeof(NumberDto),typeof(NumberDto),AdaptOptions.Default,ignoreNames:Array.Empty<string>())),"Empty ignores changed runtime identity");
        });
        // Phase 1 must finish for all paths before this unique type receives a rule.
        foreach(var entry in new[]{"R","M","C","N"}) Case("complex IConvertible rejected without user code/"+entry,()=> {
            var value=new ConvertibleComplex{Value=17};
            var error=Throws<MappingException>(()=>At(value,typeof(ConvertibleComplex),typeof(int),entry));
            Equal(typeof(ConvertibleComplex),error.SourceType);Equal(typeof(int),error.DestinationType);Equal(Path(entry),error.Path);
            Equal(0,value.Calls);
        });
        // Phase 2 proves explicit rules are allowed without implicitly calling IConvertible.
        TypeConverterRegistry.Register(typeof(ConvertibleComplex),typeof(int),new Func<ConvertibleComplex,int>(value=>value.Value));
        foreach(var entry in new[]{"R","M","C","N"}) Case("complex IConvertible explicit rule succeeds/"+entry,()=> {
            var value=new ConvertibleComplex{Value=17};Equal(17,At(value,typeof(ConvertibleComplex),typeof(int),entry));Equal(0,value.Calls);
        });
        Console.WriteLine($"Value/API boundary regression: {count-failures.Count}/{count} passed (finite boundary set, R/M/C/N paths).");
        if(failures.Count!=0)throw new AggregateException("Value/API boundary failures",failures);

        object? At(object value,Type source,Type target,string entry) {
            if(entry=="R")return mapper.Map(value,source,target);
            if(entry=="C"){var a=Array.CreateInstance(source,1);a.SetValue(value,0);return ((Array)mapper.Map(a,a.GetType(),target.MakeArrayType())!).GetValue(0);}
            var s=typeof(Box<>).MakeGenericType(source);var t=typeof(Box<>).MakeGenericType(target);var box=Activator.CreateInstance(s)!;s.GetProperty("Value")!.SetValue(box,value);
            if(entry=="M")return t.GetProperty("Value")!.GetValue(mapper.Map(box,s,t));
            var os=typeof(Outer<>).MakeGenericType(source);var ot=typeof(Outer<>).MakeGenericType(target);var outer=Activator.CreateInstance(os)!;os.GetProperty("Child")!.SetValue(outer,box);return t.GetProperty("Value")!.GetValue(ot.GetProperty("Child")!.GetValue(mapper.Map(outer,os,ot)));
        }
    }
    private static string Path(string entry)=>entry switch{"R"=>"$","M"=>"$.Value","C"=>"$[0]",_=>"$.Child.Value"};
    private static T Throws<T>(Action action) where T:Exception {try{action();}catch(T e){return e;}throw new Exception("Expected "+typeof(T).Name);}
    private static void Equal<T>(T expected,T actual){if(!EqualityComparer<T>.Default.Equals(expected,actual))throw new Exception($"Expected <{expected}>, actual <{actual}>");}
    private static void True(bool condition,string message){if(!condition)throw new Exception(message);}
    public class Box<T>{public T? Value{get;set;}} public class Outer<T>{public Box<T>? Child{get;set;}}
    [Flags]public enum U8:byte{None=0,One=1,Alias=1,Two=2} [Flags]public enum I8:sbyte{None=0,One=1,Alias=1,Two=2}
    [Flags]public enum U16:ushort{None=0,One=1,Alias=1,Two=2} [Flags]public enum I16:short{None=0,One=1,Alias=1,Two=2}
    [Flags]public enum U32:uint{None=0,One=1,Alias=1,Two=2} [Flags]public enum I32:int{None=0,One=1,Alias=1,Two=2}
    [Flags]public enum U64:ulong{None=0,One=1,Alias=1,Two=2} [Flags]public enum I64:long{None=0,One=1,Alias=1,Two=2}
    [Flags]public enum SignedFlags:int{None=0,Read=1,Write=2,All=-1}
    public readonly record struct RuleValue(int Number); public readonly record struct RuleTarget(int Number); public readonly record struct NullRuleValue;
    public class TextDto{public string? Value{get;set;}}public class NumberDto{public int Value{get;set;}}
    public class NullDtoSource{} public class WholeSource{} public class WholeTarget{public int Value{get;set;}} public class NullWholeSource{} public class NullWholeTarget{}
    public sealed class ConvertibleComplex : IConvertible
    {
        public int Value{get;set;} public int Calls{get;private set;}
        private T Unexpected<T>() { Calls++;throw new InvalidOperationException("Implicit complex conversion must not run"); }
        public override string ToString()=>Unexpected<string>();
        public TypeCode GetTypeCode()=>Unexpected<TypeCode>();
        public bool ToBoolean(IFormatProvider? provider)=>Unexpected<bool>();
        public byte ToByte(IFormatProvider? provider)=>Unexpected<byte>();
        public char ToChar(IFormatProvider? provider)=>Unexpected<char>();
        public DateTime ToDateTime(IFormatProvider? provider)=>Unexpected<DateTime>();
        public decimal ToDecimal(IFormatProvider? provider)=>Unexpected<decimal>();
        public double ToDouble(IFormatProvider? provider)=>Unexpected<double>();
        public short ToInt16(IFormatProvider? provider)=>Unexpected<short>();
        public int ToInt32(IFormatProvider? provider)=>Unexpected<int>();
        public long ToInt64(IFormatProvider? provider)=>Unexpected<long>();
        public sbyte ToSByte(IFormatProvider? provider)=>Unexpected<sbyte>();
        public float ToSingle(IFormatProvider? provider)=>Unexpected<float>();
        public string ToString(IFormatProvider? provider)=>Unexpected<string>();
        public object ToType(Type conversionType,IFormatProvider? provider)=>Unexpected<object>();
        public ushort ToUInt16(IFormatProvider? provider)=>Unexpected<ushort>();
        public uint ToUInt32(IFormatProvider? provider)=>Unexpected<uint>();
        public ulong ToUInt64(IFormatProvider? provider)=>Unexpected<ulong>();
    }
    public class JsonPayload{public string? Name{get;set;}public int Count{get;set;}public JsonPayload? Child{get;set;}}
}
