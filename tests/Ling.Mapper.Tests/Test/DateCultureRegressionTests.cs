using System.Globalization;
using Ling.Mapper;
using Ling.Mapper.Configuration;

namespace TestConsole.Test;

/// <summary>
/// Default date conversion contracts verified against official AutoMapper packages
/// by the separate .work/automapper-date-parity harness. DateOnly/TimeOnly parsing
/// is a Ling extension, not a claim of native AutoMapper support. Numeric conversion
/// also follows CurrentCulture; explicit user-supplied format providers remain authoritative.
/// </summary>
public static class DateCultureRegressionTests
{
    public static void Run()
    {
        var mapper = new MapperConfiguration().CreateMapper();
        var failures = new List<Exception>(); var count = 0;
        void Case(string name, Action action) { count++; try { action(); } catch(Exception e) { failures.Add(new Exception(name,e)); Console.WriteLine($"FAIL date culture: {name}: {e.Message}"); } }
        var previousCulture = CultureInfo.CurrentCulture;
        try
        {
            foreach(var culture in new[] { "en-US", "de-DE", "fr-FR", "zh-CN", "tr-TR" })
            {
                CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo(culture);
                var values = new object[] {
                    new DateTime(2026,10,23,7,30,45,DateTimeKind.Utc).AddTicks(1234),
                    new DateTimeOffset(2026,10,23,7,30,45,TimeSpan.FromHours(8)).AddTicks(1234),
                    new DateOnly(2026,10,23), new TimeOnly(7,30,45).Add(TimeSpan.FromTicks(1234)),
                    TimeSpan.FromDays(2)+TimeSpan.FromMinutes(17)+TimeSpan.FromTicks(1234)
                };
                foreach(var value in values)
                foreach(var entry in new[] { "scalar", "root", "member", "collection", "nested" })
                    Case($"{culture}/{entry}/{value.GetType().Name} default ToString", () => Equal(value.ToString(), ConvertAt(value,value.GetType(),typeof(string),entry)));

                foreach(var text in new[] {
                    "2026-10-23T07:30:45", "2026-10-23T07:30:45Z", "2026-10-23T07:30:45+08:00", "2026-10-23T07:30:45-05:00",
                    "2026-03-08T02:30:00", "2026-11-01T01:30:00", "23.10.2026 07:30:45", "10/23/2026 07:30:45", "2026-10-23",
                    "07:30:45.0001234", "2.00:17:00", "00:00:01,5", "invalid", ""
                })
                foreach(var type in values.Select(v=>v.GetType()))
                {
                    object? expected=null; Exception? expectedError=null;
                    try { expected=Parse(text,type); } catch(Exception e) when(e is FormatException or OverflowException) { expectedError=e; }
                    foreach(var entry in new[] { "scalar", "root", "member", "collection", "nested" })
                        Case($"{culture}/{entry}/parse {type.Name}/{text}", () => {
                            object? actual;
                            try { actual=ConvertAt(text,typeof(string),type,entry); }
                            catch(Exception error) {
                                if(expectedError==null) throw;
                                if(entry=="scalar") Equal(expectedError.GetType(),error.GetType());
                                else {
                                    if(error is not MappingException mapped) throw new Exception("Expected contextual MappingException",error);
                                    Equal(expectedError.GetType().FullName,mapped.OriginalExceptionType);
                                    Equal(entry switch { "root"=>"$", "member"=>"$.Value", "collection"=>"$[0]", _=>"$.Child.Value" },mapped.Path);
                                }
                                return;
                            }
                            if(expectedError!=null) throw new Exception("Expected parsing failure, but conversion succeeded");
                            Equal(expected,actual);
                            if(expected is DateTime date) { Equal(date.Kind,((DateTime)actual!).Kind); Equal(date.Ticks,((DateTime)actual!).Ticks); }
                            if(expected is DateTimeOffset offset) { Equal(offset.Offset,((DateTimeOffset)actual!).Offset); Equal(offset.Ticks,((DateTimeOffset)actual!).Ticks); }
                        });
                }
                Case($"{culture}/amount follows current culture", () => { var text = 1234.56m.ToString(CultureInfo.CurrentCulture); Equal(text,mapper.Map<string>(1234.56m)); Equal(1234.56m,mapper.Map<decimal>(text)); Equal(text,ScalarConversion.Convert(1234.56m,typeof(string))); });
            }
        }
        finally { CultureInfo.CurrentCulture=previousCulture; }
        Console.WriteLine($"Date culture regression: {count-failures.Count}/{count} passed; timezone={TimeZoneInfo.Local.Id}.");
        if(failures.Count!=0) throw new AggregateException("Date culture conversion failures",failures);

        object? ConvertAt(object value,Type source,Type target,string entry)
        {
            if(entry=="scalar") return ScalarConversion.Convert(value,target);
            if(entry=="root") return mapper.Map(value,source,target);
            if(entry=="collection") { var array=Array.CreateInstance(source,1); array.SetValue(value,0); return ((Array)mapper.Map(array,array.GetType(),target.MakeArrayType())!).GetValue(0); }
            var sourceBox=typeof(Box<>).MakeGenericType(source); var targetBox=typeof(Box<>).MakeGenericType(target);
            var box=Activator.CreateInstance(sourceBox)!; sourceBox.GetProperty("Value")!.SetValue(box,value);
            if(entry=="member") return targetBox.GetProperty("Value")!.GetValue(mapper.Map(box,sourceBox,targetBox));
            var sourceOuter=typeof(Outer<>).MakeGenericType(source); var targetOuter=typeof(Outer<>).MakeGenericType(target);
            var outer=Activator.CreateInstance(sourceOuter)!; sourceOuter.GetProperty("Child")!.SetValue(outer,box);
            var mapped=mapper.Map(outer,sourceOuter,targetOuter);
            return targetBox.GetProperty("Value")!.GetValue(targetOuter.GetProperty("Child")!.GetValue(mapped));
        }
    }
    private static object Parse(string value,Type type) => type==typeof(DateTime) ? Convert.ToDateTime(value)
        : type==typeof(DateTimeOffset) ? DateTimeOffset.Parse(value)
        : type==typeof(DateOnly) ? DateOnly.Parse(value)
        : type==typeof(TimeOnly) ? TimeOnly.Parse(value)
        : type==typeof(TimeSpan) ? TimeSpan.Parse(value) : throw new InvalidOperationException();
    private static void Equal<T>(T expected,T actual) { if(!EqualityComparer<T>.Default.Equals(expected,actual)) throw new Exception($"Expected <{expected}>, actual <{actual}>"); }
    public class Box<T> { public T? Value {get;set;} }
    public class Outer<T> { public Box<T>? Child {get;set;} }
}
