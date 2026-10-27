using System.Reflection;
using Ling.Mapper;
using Ling.Mapper.Configuration;
using Ling.Mapper.Extensions;
using Ling.Mapper.Mapper;
using Ling.Mapper.Models;
using Ling.Mapper.Provider;

var checks = 0;
void Check(bool value, string label) { checks++; if (!value) throw new Exception(label); }
void Throws<T>(Action action, string label) where T : Exception
{
    try { action(); } catch (T) { checks++; return; }
    throw new Exception(label);
}
foreach (var policy in new[] { NullValueHandling.UseDefault, NullValueHandling.Throw })
{
    var mapper = new MapperConfiguration { NullValueHandling = policy }.CreateMapper();
    var calls = 0;
    Action<object, object> callback = (_, _) => calls++;
    Check(mapper.Map<Output>(null, AdaptOptions.Strict, callback) == null, "generic reference null");
    Check(mapper.Map<int?>(null, AdaptOptions.Strict, callback) == null, "generic nullable null");
    Check(mapper.Map<bool?>(null) == null, "nullable bool null");
    Check(mapper.Map<decimal?>(null) == null, "nullable decimal null");
    Check(mapper.Map<Guid?>(null) == null, "nullable Guid null");
    Check(mapper.Map<DateTime?>(null) == null, "nullable DateTime null");
    Check(mapper.Map<SampleEnum?>(null) == null, "nullable enum null");
    Check(mapper.Map<ValueTuple<int, string>?>(null) == null, "nullable generic tuple null");
    Check(mapper.Map<SampleValue<int>?>(null) == null, "nullable custom generic struct null");
    Check(mapper.Map<IReadOnlyList<Output>>(null) == null, "generic interface null");
    Check(mapper.Map<List<int>>(null) == null, "closed generic reference null");
    Check(mapper.Map<string>(null) == null, "string null");
    Check(mapper.Map<List<Output>>(null) == null, "list null");
    Check(mapper.Map<Output[]>(null) == null, "array null");
    Check(mapper.Map(null, typeof(Input), typeof(Output), AdaptOptions.Strict, callback) == null, "explicit reference null");
    Check(mapper.Map(null, typeof(int), typeof(int?), AdaptOptions.Strict, callback) == null, "explicit nullable null");
    Check(mapper.Map(null, new TypeDelegator(typeof(Input)), new TypeDelegator(typeof(Output))) == null, "delegated type null");
    try
    {
        mapper.Map(null, typeof(Input), new ThrowingValueType());
        throw new Exception("custom metadata must throw");
    }
    catch (MappingException error)
    {
        Check(error.Path == "$" && error.OriginalExceptionType == typeof(ApplicationException).FullName,
            "custom Type metadata error remains wrapped");
    }
    try
    {
        mapper.Map(null, typeof(object), new ThrowingGenericMetadata());
        throw new Exception("custom generic metadata must throw");
    }
    catch (MappingException error)
    {
        Check(error.Path == "$" && error.OriginalExceptionType == typeof(ApplicationException).FullName,
            "explicit nullable Type metadata error remains wrapped");
    }
    Check(calls == 0, "null never item callback");
    Throws<ArgumentException>(() => mapper.Map<Output>(null, (AdaptOptions)int.MaxValue), "invalid options before null");
    Throws<ArgumentException>(() => mapper.Map<int?>(null, AdaptOptions.Strict, ignoreNames: new[] { " " }), "invalid ignores before null");
    Throws<ArgumentException>(() => mapper.Map(null, typeof(List<>), typeof(Output)), "invalid declared source before null");
    Throws<ArgumentException>(() => mapper.Map(null, typeof(Input), typeof(void)), "invalid destination before null");
    Throws<ArgumentNullException>(() => mapper.Map(null, null!, typeof(Output)), "missing source type");
    Check(mapper.Map<Output>(null, AdaptOptions.Strict, ignoreNames: Array.Empty<string>()) == null, "empty ignores");
    Check(mapper.Map<Output>(null, AdaptOptions.Strict, ignoreNames: new[] { "Value" }) == null, "valid ignores");
    if (policy == NullValueHandling.UseDefault)
    {
        Check(mapper.Map<int>(null) == 0, "value default");
        Check(mapper.Map<SampleValue<int>>(null).Value == 0, "nonnullable generic struct default");
        Check((int)mapper.Map(null, typeof(object), typeof(int))! == 0, "explicit value default");
    }
    else
    {
        Throws<MappingException>(() => mapper.Map<int>(null), "value throw");
        Throws<MappingException>(() => mapper.Map<SampleValue<int>>(null), "nonnullable generic struct throw");
        Throws<MappingException>(() => mapper.Map(null, typeof(object), typeof(int)), "explicit value throw");
    }
    var target = new Output { Value = 17 };
    Check(ReferenceEquals(target, mapper.MapInto(null, target, AdaptOptions.Strict, callback)) && target.Value == 17, "MapInto null unchanged");
    Check(calls == 0, "MapInto null no callback");
    var source = new Input { Value = 31 };
    var first = mapper.Map<Output>(source, AdaptOptions.Strict, callback)!;
    var second = mapper.Map<Output>(source)!;
    Check(first.Value == 31 && second.Value == 31 && !ReferenceEquals(first, second), "nonnull values fresh");
    Check(calls == 1, "nonnull callback once");
    MapperProvider.SetCurrent(mapper);
    try
    {
        IEnumerable<Input> emptySource = null!;
        var list = emptySource.Adapt<Output, Input>((_, _) => calls++);
        Check(list.Count == 0 && calls == 1, "typed enumerable null remains empty/no callback");
    }
    finally { MapperProvider.Clear(); }
}
// Allocation observations only: run identical source and configuration on both
// versions; no historical byte assumptions and no elapsed-time measurement.
var allocationMapper = new MapperConfiguration().CreateMapper();
var input = new Input { Value = 5 };
Measure("null-reference", () => allocationMapper.Map<Output>(null));
Measure("null-nullable", () => allocationMapper.Map<int?>(null));
Measure("null-nullable-generic-struct", () => allocationMapper.Map<SampleValue<int>?>(null));
Measure("explicit-null-nullable", () => allocationMapper.Map(null, typeof(object), typeof(int?)));
Measure("explicit-null-reference", () => allocationMapper.Map(null, typeof(Input), typeof(Output)));
Measure("nonnull-flat-negative-control", () => allocationMapper.Map<Output>(input));
Console.WriteLine($"Null root smoke PASS: {checks} checks; allocation observations only, no timing.");
void Measure(string label, Func<object?> operation)
{
    for (var i = 0; i < 20000; i++) GC.KeepAlive(operation());
    var before = GC.GetAllocatedBytesForCurrentThread();
    for (var i = 0; i < 10000; i++) GC.KeepAlive(operation());
    var bytes = GC.GetAllocatedBytesForCurrentThread() - before;
    Console.WriteLine($"ALLOCATION {label}: count=10000 bytes={bytes}");
}
public sealed class Input { public int Value { get; set; } }
public sealed class Output { public int Value { get; set; } }

public sealed class ThrowingValueType : TypeDelegator
{
    public ThrowingValueType() : base(typeof(Output)) { }
    protected override bool IsValueTypeImpl() => throw new ApplicationException("metadata");
}

public enum SampleEnum { None, One }
public struct SampleValue<T> { public T Value { get; set; } }
public sealed class ThrowingGenericMetadata : TypeDelegator
{
    public ThrowingGenericMetadata() : base(typeof(int?)) { }
    public override bool ContainsGenericParameters => false;
    public override bool IsGenericType => true;
    public override bool IsGenericTypeDefinition => false;
    public override Type GetGenericTypeDefinition() => throw new ApplicationException("generic metadata");
}
