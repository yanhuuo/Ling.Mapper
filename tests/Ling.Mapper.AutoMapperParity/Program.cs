using System.Globalization;
using System.Text.Json;
using AutoMapper;
Action<IMapperConfigurationExpression> configure = c => { c.CreateMap(typeof(Box<>),typeof(Box<>)); c.CreateMap(typeof(Outer<>),typeof(Outer<>)); };
var ctor = typeof(AutoMapper.MapperConfiguration).GetConstructors().Single(c=>c.GetParameters()[0].ParameterType==typeof(Action<IMapperConfigurationExpression>));
object? logger=null;
if(ctor.GetParameters().Length==2) logger=Type.GetType("Microsoft.Extensions.Logging.Abstractions.NullLoggerFactory, Microsoft.Extensions.Logging.Abstractions")!.GetField("Instance")!.GetValue(null);
var config=(AutoMapper.MapperConfiguration)ctor.Invoke(ctor.GetParameters().Length==1 ? new object?[]{configure} : new object?[]{configure,logger});
var oracle=config.CreateMapper(); var actual=new Ling.Mapper.Configuration.MapperConfiguration().CreateMapper();
var failures=new List<Exception>(); int checks=0, intentional=0;
var dates=new object[]{new DateTime(2026,10,23,7,30,45,DateTimeKind.Utc).AddTicks(1234),new DateTimeOffset(2026,10,23,7,30,45,TimeSpan.FromHours(8)),new DateOnly(2026,10,23),new TimeOnly(7,30,45).Add(TimeSpan.FromTicks(1234)),TimeSpan.FromDays(2)+TimeSpan.FromMinutes(17)};
foreach(var culture in new[]{"en-US","de-DE","fr-FR","zh-CN","tr-TR"}) {
 CultureInfo.CurrentCulture=CultureInfo.GetCultureInfo(culture);
 foreach(var value in dates) { Check(value,value.GetType(),typeof(string)); Check(value,typeof(Nullable<>).MakeGenericType(value.GetType()),typeof(string)); }
 foreach(var type in dates.Select(x=>x.GetType())) {
  foreach(var text in new[]{"2026-10-23T07:30:45","2026-10-23T07:30:45Z","2026-10-23T07:30:45+08:00","2026-10-23T07:30:45-05:00","2026-03-08T02:30:00","2026-11-01T01:30:00","23.10.2026 07:30:45","10/23/2026 07:30:45","2026-10-23","07:30:45.0001234","2.00:17:00","00:00:01,5","invalid",""}) {
   Check(text,typeof(string),type); Check(text,typeof(string),typeof(Nullable<>).MakeGenericType(type));
  }
  Check(null,typeof(string),type);Check(null,typeof(string),typeof(Nullable<>).MakeGenericType(type));
 }
}
Console.WriteLine($"AutoMapper {typeof(AutoMapper.MapperConfiguration).Assembly.GetName().Version}, TZ={TimeZoneInfo.Local.Id}: parity {checks-failures.Count}/{checks}; documented extension/null-policy observations={intentional}");
if(failures.Count!=0)throw new AggregateException(failures);
void Check(object? value,Type source,Type target){ foreach(var entry in new[]{"root","member","collection","nested"}) {
 object? expected=null,got=null;Exception? ee=null,ae=null;
 try{expected=Map(oracle.Map,value,source,target,entry);}catch(Exception e){ee=Root(e);}
 try{got=Map(actual.Map,value,source,target,entry);}catch(Exception e){ae=Root(e);}
 var baseTarget=Nullable.GetUnderlyingType(target)??target;
 if(source==typeof(string)&&(baseTarget==typeof(DateOnly)||baseTarget==typeof(TimeOnly)) || value==null&&entry=="root"&&target.IsValueType&&Nullable.GetUnderlyingType(target)==null) { intentional++; continue; }
 checks++;
 var label=$"{CultureInfo.CurrentCulture.Name}/{entry} {source.Name}->{target.Name} input={value}";
 try{
  if(ee==null&&ae!=null)throw new Exception("Only Ling failed: "+ae.GetType().Name);
  if(ee!=null&&ae==null)throw new Exception("Only AutoMapper failed: "+ee.GetType().Name);
  if(ee!=null) { if(ae is not Ling.Mapper.MappingException m || m.OriginalExceptionType!=ee.GetType().FullName)throw new Exception($"Error type mismatch: {ee.GetType().Name} vs {ae}"); }
  else if(JsonSerializer.Serialize(Describe(expected))!=JsonSerializer.Serialize(Describe(got)))throw new Exception($"Value mismatch: {JsonSerializer.Serialize(Describe(expected))} vs {JsonSerializer.Serialize(Describe(got))}");
 }catch(Exception e){failures.Add(new Exception(label,e));Console.WriteLine("FAIL "+label+": "+e.Message);}
 }}
object? Map(Func<object?,Type,Type,object?> map,object? value,Type source,Type target,string entry){
 if(entry=="root")return map(value,source,target);
 if(entry=="collection"){var a=Array.CreateInstance(source,1);a.SetValue(value,0);return ((Array)map(a,a.GetType(),target.MakeArrayType())!).GetValue(0);}
 var st=typeof(Box<>).MakeGenericType(source);var dt=typeof(Box<>).MakeGenericType(target);var b=Activator.CreateInstance(st)!;st.GetProperty("Value")!.SetValue(b,value);
 if(entry=="member")return dt.GetProperty("Value")!.GetValue(map(b,st,dt));
 var so=typeof(Outer<>).MakeGenericType(source);var to=typeof(Outer<>).MakeGenericType(target);var o=Activator.CreateInstance(so)!;so.GetProperty("Child")!.SetValue(o,b);var mapped=map(o,so,to);return dt.GetProperty("Value")!.GetValue(to.GetProperty("Child")!.GetValue(mapped));
}
Exception Root(Exception e){while(e.InnerException!=null&&e is not Ling.Mapper.MappingException)e=e.InnerException;return e;}
object? Describe(object? o)=>o switch{DateTime d=>new{ticks=d.Ticks,kind=d.Kind.ToString()},DateTimeOffset d=>new{ticks=d.Ticks,offset=d.Offset.Ticks},DateOnly d=>d.DayNumber,TimeOnly t=>t.Ticks,TimeSpan t=>t.Ticks,_=>o};
public class Box<T>{public T? Value{get;set;}}
public class Outer<T>{public Box<T>? Child{get;set;}}
