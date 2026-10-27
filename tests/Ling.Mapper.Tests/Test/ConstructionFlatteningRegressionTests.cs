using System.Reflection;
using Ling.Mapper;
using Ling.Mapper.Configuration;
using Ling.Mapper.Mapper;
using Ling.Mapper.Models;
using Ling.Mapper.Provider;

namespace TestConsole.Test;

/// <summary>Constructor and opt-in source-convention contracts; no provider-specific query dependencies.</summary>
public static class ConstructionFlatteningRegressionTests
{
    private static int _assertions;
    public static void Run()
    {
        _assertions = 0;
        var failures = new List<Exception>();
        var cases = 0;
        void Case(string name, Action test)
        {
            cases++;
            try { test(); Console.WriteLine($"PASS construction/flattening: {name}"); }
            catch (Exception error) { failures.Add(new Exception(name, error)); Console.WriteLine($"FAIL construction/flattening: {name}: {error.Message}"); }
        }
        var mapper = new MapperConfiguration().CreateMapper();
        Case("immutable class and positional record", () => {
            var source = new TextInput { Id = "17", Name = "record" };
            Equal(17, mapper.Map<ImmutableId>(source)!.Id);
            Equal(new RecordDto(17, "record"), mapper.Map<RecordDto>(source));
            Equal(17, ((ImmutableId)mapper.Map(source, typeof(TextInput), typeof(ImmutableId))!).Id);
        });
        Case("constructor-bound init member is not assigned twice", () => {
            var source = new CountingInput();
            var result = mapper.Map<MixedDto>(source)!;
            Equal(31, result.Id); Equal("tail", result.Name); Equal(1, source.Reads); Equal(0, result.IdSetterCalls);
        });
        Case("longest fully bound constructor is selected", () => {
            var result = mapper.Map<PreferredDto>(new TextInput { Id = "17", Name = "preferred" })!;
            Equal(2, result.Arity); Equal("preferred", result.Name);
        });
        Case("unbound longer overload does not execute getters", () => {
            var source = new CountingInput(); var result = mapper.Map<FallbackDto>(source)!;
            Equal(31, result.Id); Equal(1, source.Reads); Equal(1, result.Arity);
        });
        Case("parameterless precedence remains compatible", () => {
            var result = mapper.Map<ParameterlessDto>(new TextInput { Id = "17" })!;
            Equal(0, result.SelectedArity); Equal(17, result.Id);
        });
        Case("ambiguous constructor selection is metadata only", () => {
            var source = new CountingInput(); var error = Failure(() => mapper.Map<AmbiguousDto>(source));
            Equal(typeof(AmbiguousMatchException).FullName, error.OriginalExceptionType); Equal(0, source.Reads);
        });
        Case("missing required argument has classified safe failure", () => {
            var source = new CountingInput(); Equal(null, mapper.Map<MissingDto>(source));
            var strict = new MapperConfiguration { StrictMode = true }.CreateMapper();
            var error = Failure(() => strict.Map<MissingDto>(source));
            Equal(typeof(MissingMethodException).FullName, error.OriginalExceptionType); Equal("$", error.Path); Equal(0, source.Reads);
        });
        Case("optional enum decimal nullable and string defaults", () => {
            var result = mapper.Map<OptionalDto>(new TextInput { Id = "17" })!;
            Equal(17, result.Id); Equal("default", result.Label); Equal(Status.Ready, result.Status); Equal(2.5m, result.Amount); Equal(null, result.When);
        });
        Case("ignored required constructor member uses default without reading", () => {
            var source = new CountingInput(); var result = mapper.Map<ImmutableId>(source, AdaptOptions.Default, ignoreNames: new[] { "Id" })!;
            Equal(0, result.Id); Equal(0, source.Reads);
        });
        Case("ignored optional constructor member uses declared default", () => {
            var result = mapper.Map<OptionalLabel>(new LabelInput { Label = "ignored" }, AdaptOptions.Default, ignoreNames: new[] { "Label" })!;
            Equal("default", result.Label);
        });
        Case("null source default and ignore-null constructor fallback", () => {
            Equal(null, mapper.Map<OptionalLabel>(new LabelInput())!.Label);
            Equal("default", mapper.Map<OptionalLabel>(new LabelInput(), AdaptOptions.Default | AdaptOptions.IgnoreNullValues)!.Label);
        });
        Case("constructor precondition skips throwing getter", () => {
            var source = new ThrowingInput(); var result = With(new PreconditionProfile()).Map<ImmutableId>(source)!;
            Equal(0, result.Id); Equal(0, source.Reads);
        });
        Case("constructor null substitute beats ignore-null", () => Equal("substitute", With(new SubstituteProfile()).Map<OptionalLabel>(new LabelInput())!.Label));
        Case("nullable constructor args honor null policy and ignore-null", () => {
            Equal(null, mapper.Map<NullableId>(new NullableIdInput())!.Id);
            var strictNull = new MapperConfiguration { NullValueHandling = NullValueHandling.Throw }.CreateMapper();
            Equal("$.Id", Failure(() => strictNull.Map<ImmutableId>(new NullableIdInput())).Path);
            Equal(0, strictNull.Map<ImmutableId>(new NullableIdInput(), AdaptOptions.Default | AdaptOptions.IgnoreNullValues)!.Id);
        });
        Case("explicit missing optional constructor source is not silently defaulted", () => Throws<NotSupportedException>(() => With(new MissingOptionalRenameProfile())));
        Case("nested configured constructor member protects overridden source", () => {
            var result = With(new NestedConstructorOverrideProfile()).Map<OuterDto>(new OuterInput())!;
            Equal(7, result.Child.Number); Equal("keep", result.Child.Other);
        });
        Case("constructor member ignore-null override", () => Equal(null, With(new AllowNullProfile()).Map<OptionalLabel>(new LabelInput(), AdaptOptions.Default | AdaptOptions.IgnoreNullValues)!.Label));
        Case("constructor condition rejects destination dependency", () => Throws<NotSupportedException>(() => With(new ConstructorConditionProfile())));
        Case("explicit constructor nested rename is null-safe and single-read", () => {
            var source = new NestedInput(); var map = With(new NestedRenameProfile());
            Equal(41, map.Map<ImmutableId>(source)!.Id); Equal(1, source.Reads); Equal(1, source.ChildValue.Reads);
            Equal(0, map.Map<ImmutableId>(new NestedInput { HasChild = false })!.Id);
        });
        Case("constructor source selector continues through explicit final type conversion once", () => {
            var calls = 0;
            TypeConverterRegistry.Register(typeof(ValueDto), typeof(ValueDto), new Func<ValueDto, ValueDto>(value => { calls++; return new() { Id = value.Id + 100 }; }));
            var source = new ValueInput(); var result = With(new ResolvedValueProfile()).Map<ValueHolder>(source)!;
            Equal(false, ReferenceEquals(source.Value, result.Value)); Equal(source.Value.Id + 100, result.Value.Id);
            Equal(1, calls); Equal(1, source.Reads);
        });
        Case("constructor registered member converter runs once", () => {
            var calls = 0;
            TypeConverterRegistry.Register(typeof(EncodedId), typeof(int), new Func<EncodedId, int>(value => { calls++; return value.Value; }));
            Equal(53, mapper.Map<ImmutableId>(new EncodedInput())!.Id); Equal(1, calls);
        });
        Case("constructor failure is safely redacted", () => {
            var error = Failure(() => mapper.Map<ThrowingDto>(new TextInput { Id = "17" }));
            Equal(typeof(InvalidOperationException).FullName, error.OriginalExceptionType); Equal(false, error.ToString().Contains("SECRET_CONSTRUCTOR"));
        });
        Case("constructor conversion failure retains destination path", () => {
            var error = Failure(() => mapper.Map<ImmutableId>(new TextInput { Id = "not-number" }));
            Equal("$.Id", error.Path); Equal(typeof(FormatException).FullName, error.OriginalExceptionType);
        });
        Case("constructor getter failure is not retried", () => {
            var source = new ThrowingInput(); var error = Failure(() => mapper.Map<ImmutableId>(source));
            Equal("$.Id", error.Path); Equal(1, source.Reads); Equal(typeof(InvalidOperationException).FullName, error.OriginalExceptionType);
        });
        Case("inherited property and constructor parameter casing", () => {
            Equal(61, mapper.Map<ImmutableId>(new DerivedInput { Id = 61 }, AdaptOptions.Strict)!.Id);
            Equal(null, mapper.Map<ImmutableId>(new LowerInput { id = 61 }, AdaptOptions.Strict));
            Equal(61, mapper.Map<ImmutableId>(new LowerInput { id = 61 })!.Id);
        });
        Case("cyclic immutable constructor fails without stack overflow", () => {
            var source = new NodeInput(); source.Next = source;
            var error = Failure(() => mapper.Map<NodeDto>(source));
            Equal("$.Next", error.Path); Equal(typeof(InvalidOperationException).FullName, error.OriginalExceptionType);
        });
        Case("finite ignore projections terminate immutable source cycles", () => {
            var source = new NodeInput(); source.Next = source;
            var result = mapper.Map<NodeDto>(source, AdaptOptions.Default, ignoreNames: new[] { "Next.Next" })!;
            Equal(true, result.Next != null); Equal(null, result.Next!.Next);
            var deeper = mapper.Map<NodeDto>(source, AdaptOptions.Default, ignoreNames: new[] { "Next.Next.Next" })!;
            Equal(true, deeper.Next?.Next != null); Equal(null, deeper.Next!.Next!.Next);
        });
        Case("parameterized mutable cycle registers before setter mapping", () => {
            var source = new MutableNodeInput { Id = 67 }; source.Next = source;
            var result = mapper.Map<MutableNodeDto>(source)!; Equal(67, result.Id); Same(result, result.Next!);
        });
        Case("shared immutable child preserves reference identity", () => {
            var source = new TextInput { Id = "71" }; var result = mapper.Map<ImmutableId[]>(new[] { source, source })!;
            Same(result[0], result[1]); Equal(71, result[0].Id);
        });
        Case("explicit construction factory supports inaccessible constructors", () => {
            var result = With(new FactoryProfile()).Map<FactoryDto>(new TextInput { Id = "17", Name = "mapped" })!;
            Equal(101, result.Id); Equal("mapped", result.Name);
        });
        Case("contextual constructor resolver is evaluated per call", () => {
            var map = With(new ContextProfile());
            using (new MappingCallContext(new Dictionary<string, object?> { ["id"] = 5 }).BeginScope()) Equal(5, map.Map<ImmutableId>(new TextInput())!.Id);
            using (new MappingCallContext(new Dictionary<string, object?> { ["id"] = 8 }).BeginScope()) Equal(8, map.Map<ImmutableId>(new TextInput())!.Id);
        });
#if NET8_0_OR_GREATER
        Case("required/init members remaining after construction map normally", () => {
            var result = mapper.Map<RequiredDto>(new TextInput { Id = "17", Name = "required" })!;
            Equal(17, result.Id); Equal("required", result.Name);
        });
#endif
        Case("flattening and methods are disabled by default", () => {
            var source = new FlatInput(); var result = mapper.Map<FlatTarget>(source)!;
            Equal(null, result.CustomerName); Equal(null, result.Title); Equal(0, source.MethodCalls); Equal(0, source.CustomerReads);
        });
        var flattened = Conventions(flattening: true);
        var methods = Conventions(methods: true);
        var combined = Conventions(flattening: true, methods: true);
        Case("opt-in flattening supports mutable and immutable destinations", () => {
            var source = new FlatInput(); Equal("nested", flattened.Map<FlatTarget>(source)!.CustomerName); Equal(1, source.CustomerReads);
            Equal("nested", flattened.Map<FlatRecord>(new FlatInput())!.CustomerName);
        });
        Case("direct source property wins over flattened path", () => {
            var source = new DirectFlatInput(); Equal("direct", combined.Map<FlatTarget>(source)!.CustomerName); Equal(0, source.CustomerReads);
        });
        Case("explicit resolver wins over automatic flattening", () => {
            var source = new FlatInput(); var config = new MapperConfiguration(); config.ConfigureConventions(c => c.EnableFlattening = true); config.AddProfile(new ExplicitFlatProfile());
            Equal("explicit", config.CreateMapper().Map<FlatTarget>(source)!.CustomerName); Equal(0, source.CustomerReads);
        });
        Case("flattened null owner is safe", () => Equal(null, flattened.Map<FlatTarget>(new FlatInput { HasCustomer = false })!.CustomerName));
        Case("flattened ambiguity rejects rather than guessing", () => {
            var source = new AmbiguousFlatInput(); var error = Failure(() => flattened.Map<AmbiguousFlatTarget>(source));
            Equal(typeof(AmbiguousMatchException).FullName, error.OriginalExceptionType); Equal(0, source.Reads);
        });
        Case("Get method convention requires opt-in and runs exactly once", () => {
            var source = new FlatInput(); Equal("method", methods.Map<FlatTarget>(source)!.Title); Equal(1, source.MethodCalls); Equal(0, source.CustomerReads);
        });
        Case("direct property wins over Get method", () => {
            var source = new PropertyAndMethod(); Equal("property", methods.Map<TitleTarget>(source)!.Title); Equal(0, source.Calls);
        });
        Case("flattened segments prefer properties over shadowed Get methods", () => {
            var source = new PropertyAndMethodOwner();
            Equal("property child", combined.Map<FlatTarget>(source)!.CustomerName); Equal(1, source.PropertyCalls); Equal(0, source.MethodCalls);
        });
        Case("flattened segment priority follows name normalization", () => {
            var source = new NormalizedPropertyAndMethodOwner();
            Equal("normalized property", combined.Map<FlatTarget>(source, AdaptOptions.Default)!.CustomerName); Equal(0, source.MethodCalls);
        });
        Case("Get convention excludes parameters generic static and object methods", () => {
            var source = new UnsupportedMethods(); var result = methods.Map<UnsupportedMethodsTarget>(source)!;
            Equal(null, result.Name); Equal(null, result.Generic); Equal(null, result.Static); Equal(null, result.Type); Equal(0, source.Calls);
        });
        Case("Get method exceptions are not swallowed as custom resolver nulls", () => {
            var source = new ThrowingMethod(); var error = Failure(() => methods.Map<TitleTarget>(source));
            Equal(typeof(NullReferenceException).FullName, error.OriginalExceptionType); Equal("$.Title", error.Path); Equal(1, source.Calls);
        });
        Case("flattening can traverse Get method only when both options enabled", () => {
            Equal(null, flattened.Map<FlatTarget>(new MethodOwner())!.CustomerName);
            var source = new MethodOwner(); Equal("method child", combined.Map<FlatTarget>(source)!.CustomerName); Equal(1, source.Calls);
        });
        Case("flattening supports nullable structs and name normalization", () => {
            Equal("nullable", flattened.Map<FlatTarget>(new NullableOwner { Customer = new CustomerValue { Name = "nullable" } })!.CustomerName);
            Equal(null, flattened.Map<FlatTarget>(new NullableOwner())!.CustomerName);
            Equal("underscore", flattened.Map<FlatTarget>(new UnderscoreOwner(), AdaptOptions.Default)!.CustomerName);
            Equal(null, flattened.Map<FlatTarget>(new UnderscoreOwner(), AdaptOptions.Strict)!.CustomerName);
        });
        Case("explicit Rename stays property-only even with method conventions", () => {
            var config = new MapperConfiguration(); config.ConfigureConventions(c => c.EnableGetMethods = true); config.AddProfile(new MissingExplicitMethodProfile());
            Throws<NotSupportedException>(() => config.CreateMapper());
        });
        Console.WriteLine($"Construction/flattening: {cases} cases, {_assertions} assertions, {failures.Count} failures.");
        if (failures.Count != 0) throw new AggregateException(failures);
    }

    private static IMapper Conventions(bool flattening = false, bool methods = false) { var config = new MapperConfiguration(); config.ConfigureConventions(c => { c.EnableFlattening = flattening; c.EnableGetMethods = methods; }); return config.CreateMapper(); }
    private static IMapper With(MapperProfile profile) { var config = new MapperConfiguration(); config.AddProfile(profile); return config.CreateMapper(); }
    private static void Equal<T>(T expected, T actual) { _assertions++; if (!EqualityComparer<T>.Default.Equals(expected, actual)) throw new Exception($"Expected {expected}; actual {actual}."); }
    private static void Same(object expected, object actual) { _assertions++; if (!ReferenceEquals(expected, actual)) throw new Exception("Expected the same reference."); }
    private static MappingException Failure(Action action) { _assertions++; try { action(); } catch (MappingException error) { return error; } throw new Exception("Expected MappingException."); }
    private static void Throws<T>(Action action) where T : Exception { _assertions++; try { action(); } catch (T) { return; } throw new Exception($"Expected {typeof(T).Name}."); }

    public class TextInput { public string? Id { get; set; } public string? Name { get; set; } }
    public sealed class ImmutableId(int id) { public int Id { get; } = id; }
    public sealed record RecordDto(int Id, string? Name);
    public sealed class CountingInput { public int Reads; public string Id { get { Reads++; return "31"; } } public string Name => "tail"; }
    public sealed class MixedDto { private int _id; public MixedDto(int id) => _id = id; public int Id { get => _id; init { IdSetterCalls++; _id = value; } } public int IdSetterCalls; public string? Name { get; set; } }
    public sealed class PreferredDto { public PreferredDto(int id) { Id = id; Arity = 1; } public PreferredDto(int id, string? name) { Id = id; Name = name; Arity = 2; } public int Id { get; } public string? Name { get; } public int Arity { get; } }
    public sealed class FallbackDto { public FallbackDto(int id) { Id = id; Arity = 1; } public FallbackDto(int id, int missing) { Id = id; Arity = 2; } public int Id { get; } public int Arity { get; } }
    public sealed class ParameterlessDto { public ParameterlessDto() { } public ParameterlessDto(int id) { Id = id; SelectedArity = 1; } public int Id { get; set; } public int SelectedArity { get; } }
    public sealed class AmbiguousDto { public AmbiguousDto(int id) { } public AmbiguousDto(long id) { } public int Id { get; } }
    public sealed class MissingDto(int missing) { public int Missing { get; } = missing; }
    public enum Status { Missing, Ready }
    public sealed class OptionalDto(int id, string label = "default", Status status = Status.Ready, decimal amount = 2.5m, DateTime? when = null) { public int Id { get; } = id; public string Label { get; } = label; public Status Status { get; } = status; public decimal Amount { get; } = amount; public DateTime? When { get; } = when; }
    public sealed class OptionalLabel(string? label = "default") { public string? Label { get; } = label; }
    public sealed class LabelInput { public string? Label { get; set; } }
    public sealed class NullableIdInput { public int? Id { get; set; } }
    public sealed class NullableId(int? id) { public int? Id { get; } = id; }
    public sealed class MissingOptionalRenameProfile : MapperProfile { public MissingOptionalRenameProfile() => CreateMap<LabelInput, OptionalLabel>().Rename(d => d.Label!, "Missing"); }
    public sealed class InnerInput { public string Number => "bad"; public string Other => "keep"; }
    public sealed class InnerDto { public int Number { get; set; } public string? Other { get; set; } }
    public sealed class OuterInput { public InnerInput Child => new(); public int Override => 7; }
    public sealed class OuterDto(InnerDto child) { public InnerDto Child { get; } = child; }
    public sealed class NestedConstructorOverrideProfile : MapperProfile { public NestedConstructorOverrideProfile() => CreateMap<OuterInput, OuterDto>().ForMember(d => d.Child.Number, s => s.Override); }
    public sealed class ThrowingInput { public int Reads; public string Id { get { Reads++; throw new InvalidOperationException("SECRET_GETTER"); } } }
    public sealed class PreconditionProfile : MapperProfile { public PreconditionProfile() => CreateMap<ThrowingInput, ImmutableId>().ForMember(d => d.Id, o => o.PreCondition(s => false)); }
    public sealed class SubstituteProfile : MapperProfile { public SubstituteProfile() => CreateMap<LabelInput, OptionalLabel>().ForMember(d => d.Label, o => { o.NullSubstitute("substitute"); o.IgnoreNullValues(); }); }
    public sealed class AllowNullProfile : MapperProfile { public AllowNullProfile() => CreateMap<LabelInput, OptionalLabel>().ForMember(d => d.Label, o => o.IgnoreNullValues(false)); }
    public sealed class ConstructorConditionProfile : MapperProfile { public ConstructorConditionProfile() => CreateMap<TextInput, ImmutableId>().ForMember(d => d.Id, o => o.Condition((s, d, v) => true)); }
    public sealed class NestedInput { public int Reads; public bool HasChild = true; public NestedChild ChildValue = new(); public NestedChild? Child { get { Reads++; return HasChild ? ChildValue : null; } } }
    public sealed class NestedChild { public int Reads; public string Id { get { Reads++; return "41"; } } }
    public sealed class NestedRenameProfile : MapperProfile { public NestedRenameProfile() => CreateMap<NestedInput, ImmutableId>().Rename(d => d.Id, "Child.Id"); }
    public sealed class ValueDto { public int Id { get; set; } = 47; }
    public sealed class ValueInput { public ValueDto Value = new(); public int Reads; public ValueDto Resolve() { Reads++; return Value; } }
    public sealed class ValueHolder(ValueDto value) { public ValueDto Value { get; } = value; }
    public sealed class ResolvedValueProfile : MapperProfile { public ResolvedValueProfile() { CreateMap<ValueInput, ValueHolder>().ForMember(d => d.Value, s => s.Resolve()); CreateMap<ValueDto, ValueDto>().ForMember(d => d.Id, s => s.Id + 1000); } }
    public readonly struct EncodedId(int value) { public int Value { get; } = value; }
    public sealed class EncodedInput { public EncodedId Id => new(53); }
    public sealed class ThrowingDto { public ThrowingDto(int id) => throw new InvalidOperationException("SECRET_CONSTRUCTOR"); public int Id { get; } }
    public class BaseInput { public int Id { get; set; } }
    public sealed class DerivedInput : BaseInput { }
    public sealed class LowerInput { public int id { get; set; } }
    public sealed class NodeInput { public NodeInput? Next { get; set; } }
    public sealed class NodeDto(NodeDto? next) { public NodeDto? Next { get; } = next; }
    public sealed class MutableNodeInput { public int Id { get; set; } public MutableNodeInput? Next { get; set; } }
    public sealed class MutableNodeDto(int id) { public int Id { get; } = id; public MutableNodeDto? Next { get; set; } }
    public sealed class FactoryDto { private FactoryDto(int id) => Id = id; public int Id { get; } public string? Name { get; set; } public static FactoryDto Create() => new(101); }
    public sealed class FactoryProfile : MapperProfile { public FactoryProfile() => CreateMap<TextInput, FactoryDto>().ConstructUsing(s => FactoryDto.Create()); }
    public sealed class ContextProfile : MapperProfile { public ContextProfile() => CreateMap<TextInput, ImmutableId>().ForMember(d => d.Id, o => o.MapFrom((s, context) => context.GetParameter<int>("id"))); }
#if NET8_0_OR_GREATER
    public sealed class RequiredDto(int id) { public int Id { get; } = id; public required string Name { get; init; } }
#endif
    public sealed class Customer { public string? Name { get; set; } }
    public class FlatInput { public bool HasCustomer = true; public int CustomerReads; public int MethodCalls; public Customer? Customer { get { CustomerReads++; return HasCustomer ? new() { Name = "nested" } : null; } } public string GetTitle() { MethodCalls++; return "method"; } }
    public sealed class FlatTarget { public string? CustomerName { get; set; } public string? Title { get; set; } }
    public sealed record FlatRecord(string? CustomerName);
    public sealed class DirectFlatInput : FlatInput { public string CustomerName => "direct"; }
    public sealed class ExplicitFlatProfile : MapperProfile { public ExplicitFlatProfile() => CreateMap<FlatInput, FlatTarget>().ForMember(d => d.CustomerName, s => "explicit"); }
    public sealed class AmbiguousFlatInput { public int Reads; public PartA A { get { Reads++; return new(); } } public PartAB AB { get { Reads++; return new(); } } }
    public sealed class PartA { public string BC => "one"; }
    public sealed class PartAB { public string C => "two"; }
    public sealed class AmbiguousFlatTarget { public string? ABC { get; set; } }
    public sealed class TitleTarget { public string? Title { get; set; } }
    public sealed class PropertyAndMethod { public int Calls; public string Title => "property"; public string GetTitle() { Calls++; return "method"; } }
    public sealed class PropertyAndMethodOwner { public int PropertyCalls; public int MethodCalls; public Customer Customer { get { PropertyCalls++; return new() { Name = "property child" }; } } public Customer GetCustomer() { MethodCalls++; return new() { Name = "method child" }; } }
    public sealed class NormalizedPropertyAndMethodOwner { public int MethodCalls; public Customer Custo_mer => new() { Name = "normalized property" }; public Customer GetCustomer() { MethodCalls++; return new() { Name = "method child" }; } }
    public sealed class UnsupportedMethods { public int Calls; public string GetName(string value) { Calls++; return value; } public T? GetGeneric<T>() { Calls++; return default; } public static string GetStatic() => "static"; }
    public sealed class UnsupportedMethodsTarget { public string? Name { get; set; } public string? Generic { get; set; } public string? Static { get; set; } public Type? Type { get; set; } }
    public sealed class ThrowingMethod { public int Calls; public string GetTitle() { Calls++; throw new NullReferenceException("SECRET_METHOD"); } }
    public sealed class MethodOwner { public int Calls; public Customer GetCustomer() { Calls++; return new() { Name = "method child" }; } }
    public struct CustomerValue { public string? Name { get; set; } }
    public sealed class NullableOwner { public CustomerValue? Customer { get; set; } }
    public sealed class UnderscoreOwner { public Customer Custo_mer => new() { Name = "underscore" }; }
    public sealed class MissingExplicitMethodProfile : MapperProfile { public MissingExplicitMethodProfile() => CreateMap<FlatInput, FlatTarget>().Rename(d => d.Title, "Title"); }
}
