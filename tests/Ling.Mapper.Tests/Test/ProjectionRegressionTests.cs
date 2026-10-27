using System.Collections;
using System.Linq.Expressions;
using Ling.Mapper;
using Ling.Mapper.Configuration;
using Ling.Mapper.Extensions;
using Ling.Mapper.Mapper;
using Ling.Mapper.Models;

namespace TestConsole.Test;

/// <summary>Expression-only projection contracts; no claim of untested SQL-provider translation.</summary>
public static class ProjectionRegressionTests
{
    public static void Run()
    {
        int count = 0;
        void Equal(object? expected, object? actual) { count++; if (!Equals(expected, actual)) throw new Exception($"Projection: expected {expected ?? "null"}, actual {actual ?? "null"}."); }
        void Reject(Action action, string text, string? path = null)
        {
            count++;
            try { action(); }
            catch (ProjectionException error)
            {
                if (!error.Message.Contains(text, StringComparison.OrdinalIgnoreCase) || path != null && error.MemberPath != path)
                    throw new Exception("Unexpected projection diagnostic: " + error.Message);
                return;
            }
            throw new Exception("Expected explicit projection rejection: " + text);
        }
        var configuration = new MapperConfiguration(); configuration.AddProfile(new Rules());
        var mapper = configuration.CreateMapper();
        var projection = mapper.GetProjection<Input, Output>();
        Equal(true, ReferenceEquals(projection, mapper.GetProjection<Input, Output>()));
        var inspector = new ShapeInspector(); inspector.Visit(projection); Equal(0, inspector.RuntimeNodes);
        var input = new Input { Id = 5, user_name = "Ada", Child = new Child { Value = 7 }, Items = new[] { new Child { Value = 2 }, null! }, Optional = 8 };
        var result = projection.Compile()(input);
        Equal(5L, result.Id); Equal("ADA", result.Name); Equal(10, result.Computed); Equal(7L, result.Child!.Value);
        Equal(7, result.Flat); Equal(2L, result.Items![0].Value); Equal(null, result.Items[1]); Equal(8L, result.Optional); Equal("initial", result.Ignored);
        Equal(null, projection.Compile()(null!));
        var nullResult = projection.Compile()(new Input());
        Equal(null, nullResult.Child); Equal(null, nullResult.Items); Equal(null, nullResult.Optional); Equal(0, nullResult.Flat); Equal("fallback", nullResult.Name);
        var query = new[] { input, new Input { Id = 9, user_name = "Grace" } }.AsQueryable();
        Equal(2, query.ProjectTo<Output>(mapper).Count());
        Equal("ADA", query.ProjectTo<Input, Output>(mapper).First().Name);
        Equal(9L, query.ProjectTo<Output>(AdaptOptions.Strict, mapper).Last().Id);
        var fake = new RecordingProvider(); var source = new RecordingQuery<Input>(fake);
        var deferred = source.ProjectTo<Output>(mapper);
        Equal(1, fake.Created); Equal(0, fake.Executed); Equal(0, fake.Enumerated); Equal(typeof(Output), deferred.ElementType);
        Equal(nameof(Queryable.Select), ((MethodCallExpression)deferred.Expression).Method.Name);
        Equal(ExpressionType.Quote, ((MethodCallExpression)deferred.Expression).Arguments[1].NodeType);
        inspector = new ShapeInspector(); inspector.Visit(deferred.Expression); Equal(0, inspector.RuntimeNodes);
        var immutable = mapper.GetProjection<Input, Immutable>().Compile()(input);
        Equal(5, immutable.Id); Equal("Ada", immutable.Display); Equal("optional", immutable.Tag);
        var immutableStruct = mapper.GetProjection<Input, ImmutableStruct>().Compile()(input);
        Equal(5, immutableStruct.Id);
        var overloaded = mapper.GetProjection<Input, Overloaded>().Compile()(input);
        Equal(5, overloaded.Id); Equal("longest", overloaded.Tag);
        var substitutes = mapper.GetProjection<Input, Substitutes>().Compile()(new Input());
        Equal("substitute", substitutes.Name); Equal(42L, substitutes.Optional);
        Equal(8L, mapper.GetProjection<Input, Substitutes>().Compile()(input).Optional);
        Equal(8L, mapper.GetProjection<Input, NullablePath>().Compile()(input).Value);
        Equal(0L, mapper.GetProjection<Input, NullablePath>().Compile()(new Input()).Value);
        var initialized = mapper.GetProjection<Input, FactoryOutput>().Compile()(input);
        Equal(5, initialized.Id); Equal("factory", initialized.Marker);
        Equal(2L, mapper.GetProjection<int?, long?>().Compile()(2));
        Equal(null, mapper.GetProjection<int?, long?>().Compile()(null));
        Equal(0L, mapper.GetProjection<int?, long>().Compile()(null));
        Equal(2, mapper.GetProjection<decimal, int>().Compile()(2.5m));
        Equal(4, mapper.GetProjection<decimal, int>().Compile()(3.5m));
        Equal(4, new MapperConfiguration().CreateMapper().GetProjection<Input, Plain>().Compile()(new Input { Id = 4 }).Id);
        var flattenConfig = new MapperConfiguration(); flattenConfig.ConfigureConventions(c => c.EnableFlattening = true);
        var flatten = flattenConfig.CreateMapper();
        Equal(7L, flatten.GetProjection<Input, Flattened>().Compile()(input).ChildValue);
        Equal(0L, flatten.GetProjection<Input, Flattened>().Compile()(new Input()).ChildValue);
        var strict = new MapperConfiguration { NullValueHandling = NullValueHandling.Throw }.CreateMapper();
        Reject(() => strict.GetProjection<int?, long>(), "Throw-on-null");
        var strictSubstituteConfiguration = new MapperConfiguration { NullValueHandling = NullValueHandling.Throw };
        strictSubstituteConfiguration.AddProfile(new StrictSubstituteRules());
        var strictSubstitute = strictSubstituteConfiguration.CreateMapper();
        foreach (var optional in new int?[] { null, 8 })
        {
            var strictInput = new Input { Optional = optional };
            var expected = optional ?? 9;
            Equal(expected, strictSubstitute.Map<StrictSubstitute>(strictInput)!.Value);
            Equal(expected, strictSubstitute.GetProjection<Input, StrictSubstitute>().Compile()(strictInput).Value);
            Equal(expected, strictSubstitute.Map<StrictSubstituteConstructor>(strictInput)!.Value);
            Equal(expected, strictSubstitute.GetProjection<Input, StrictSubstituteConstructor>().Compile()(strictInput).Value);
        }
        Reject(() => strictSubstitute.GetProjection<Input, StrictWithoutSubstitute>(), "Throw-on-null", "$.Value");
        Reject(() => mapper.GetProjection<Input, Output>(AdaptOptions.IgnoreNullValues), "IgnoreNullValues", "$");
        Reject(() => mapper.GetProjection<Input, RuntimeMethod>(), "arbitrary runtime", "$.Value");
        Reject(() => mapper.GetProjection<Input, AmbientValue>(), "runtime-only", "$.Value");
        Reject(() => mapper.GetProjection<Input, CapturedServiceValue>(), "Captured runtime", "$.Value");
        Reject(() => mapper.GetProjection<Input, DelegateValue>(), "Delegate invocation", "$.Value");
        Reject(() => mapper.GetProjection<Input, RuntimeFactory>(), "runtime", "$");
        Reject(() => mapper.GetProjection<Input, Conditional>(), "Assignment conditions", "$.Value");
        Reject(() => mapper.GetProjection<Cycle, CycleDto>(), "Recursive", "$.Next");
        Reject(() => mapper.GetProjection<string, int>(), "parsing", "$");
        Reject(() => mapper.GetProjection<OperatorSource, OperatorTarget>(), "User-defined conversion", "$");
        Reject(() => mapper.GetProjection<OperatorHolder, OperatorOutput>(), "User-defined operators", "$.Value");
        Reject(() => mapper.GetProjection<Input, DestinationPath>(), "Destination-path", "$");
        var methodConfig = new MapperConfiguration(); methodConfig.ConfigureConventions(c => c.EnableGetMethods = true);
        Reject(() => methodConfig.CreateMapper().GetProjection<MethodInput, Plain>(), "Get-method", "$.Id");
        var converterMapper = new MapperConfiguration().CreateMapper();
        var before = converterMapper.GetProjection<TokenInput, TokenOutput>();
        Equal(3, before.Compile()(new TokenInput { Value = new Token { Number = 3 } }).Value!.Number);
        TypeConverterRegistry.Register(typeof(Token), typeof(TokenDto), new Func<Token, TokenDto>(s => new TokenDto { Number = s.Number + 100 }));
        Reject(() => converterMapper.GetProjection<TokenInput, TokenOutput>(), "runtime converters", "$.Value");
        Equal(3, before.Compile()(new TokenInput { Value = new Token { Number = 3 } }).Value!.Number);
        // A projection already returned is an immutable snapshot; registering a converter invalidates subsequent cache lookups only.
        var contexts = new MapperConfiguration(); contexts.AddProfile(new ContextRules());
        Reject(() => contexts.CreateMapper().GetProjection<Input, Contextual>(), "Per-call context", "$.Value");
        Equal(0, MethodInput.Calls); Equal(0, RuntimeFactory.Calls);
        Console.WriteLine($"Projection regression: {count} assertions passed (LINQ-to-Objects and recording provider, not SQL translation).");
    }

    public sealed class Input { public int Id { get; set; } public string? user_name { get; set; } public Child? Child { get; set; } public Child[]? Items { get; set; } public int? Optional { get; set; } }
    public sealed class Child { public int Value { get; set; } }
    public sealed class ChildDto { public long Value { get; set; } }
    public sealed class Output { public long Id { get; set; } public string? Name { get; set; } public int Computed { get; set; } public ChildDto? Child { get; set; } public List<ChildDto>? Items { get; set; } public long? Optional { get; set; } public int Flat { get; set; } public string Ignored { get; set; } = "initial"; }
    public sealed class Immutable { public int Id { get; } public string? Display { get; } public string Tag { get; } public Immutable(int id, string? display, string tag = "optional") { Id = id; Display = display; Tag = tag; } }
    public readonly struct ImmutableStruct { public int Id { get; } public ImmutableStruct(int id) => Id = id; }
    public sealed class Overloaded { public int Id { get; } public string Tag { get; } public Overloaded(string id) { Id = -1; Tag = "shortest"; } public Overloaded(int id, string tag = "longest") { Id = id; Tag = tag; } }
    public sealed class StrictSubstitute { public int Value { get; set; } }
    public sealed class StrictWithoutSubstitute { public int Value { get; set; } }
    public sealed class StrictSubstituteConstructor { public int Value { get; } public StrictSubstituteConstructor(int value) => Value = value; }
    public sealed class NullablePath { public long Value { get; set; } }
    public sealed class Substitutes { public string? Name { get; set; } public long? Optional { get; set; } }
    public sealed class FactoryOutput { public int Id { get; set; } public string? Marker { get; set; } }
    public sealed class Plain { public int Id { get; set; } }
    public sealed class Flattened { public long ChildValue { get; set; } }
    public sealed class RuntimeMethod { public int Value { get; set; } }
    public sealed class CapturedServiceValue { public int Value { get; set; } }
    public sealed class DelegateValue { public int Value { get; set; } }
    public sealed class ServiceValue { public int Value => 12; }
    public sealed class AmbientValue { public int Value { get; set; } }
    public sealed class Conditional { public int Value { get; set; } }
    public sealed class Contextual { public int Value { get; set; } }
    public sealed class RuntimeFactory { public int Id { get; set; } public static int Calls; public static RuntimeFactory Create(Input source) { Calls++; return new() { Id = source.Id }; } }
    public sealed class Cycle { public Cycle? Next { get; set; } }
    public sealed class CycleDto { public CycleDto? Next { get; set; } }
    public sealed class DestinationPath { public ChildDto? Child { get; set; } }
    public sealed class MethodInput { public static int Calls; public int GetId() { Calls++; return 4; } }
    public readonly struct OperatorSource { public int Number { get; init; } public static implicit operator OperatorTarget(OperatorSource value) => new() { Number = value.Number + 100 }; }
    public struct OperatorTarget { public int Number { get; set; } }
    public sealed class OperatorHolder { public OperatorSource Value { get; set; } }
    public sealed class OperatorOutput { public OperatorTarget Value { get; set; } }
    public sealed class Token { public int Number { get; set; } }
    public sealed class TokenDto { public int Number { get; set; } }
    public sealed class TokenInput { public Token? Value { get; set; } }
    public sealed class TokenOutput { public TokenDto? Value { get; set; } }
    private static int RuntimeOnly(Input source) => source.Id + 1;
    private sealed class Rules : MapperProfile
    {
        public Rules()
        {
            CreateMap<Input, Output>().ForMember(d => d.Name, s => s.user_name == null ? "fallback" : s.user_name.ToUpperInvariant())
                .ForMember(d => d.Computed, s => s.Id * 2).Rename(d => d.Flat, "Child.Value").Ignore(d => d.Ignored);
            CreateMap<Input, Immutable>().Rename(d => d.Display, "user_name");
            CreateMap<Input, NullablePath>().Rename(d => d.Value, "Optional.Value");
            CreateMap<Input, Substitutes>().ForMember(d => d.Name, o => o.MapFrom(s => s.user_name).NullSubstitute("substitute"))
                .ForMember(d => d.Optional, o => o.MapFrom(s => s.Optional).NullSubstitute(42L));
            CreateMap<Input, FactoryOutput>().ConstructUsing(s => new FactoryOutput { Marker = "factory" });
            CreateMap<Input, RuntimeMethod>().ForMember(d => d.Value, s => RuntimeOnly(s));
            var service = new ServiceValue(); Func<int, int> callback = value => value + 1;
            CreateMap<Input, CapturedServiceValue>().ForMember(d => d.Value, s => service.Value);
            CreateMap<Input, DelegateValue>().ForMember(d => d.Value, s => callback(s.Id));
            CreateMap<Input, AmbientValue>().ForMember(d => d.Value, s => MappingCallContext.Current.GetParameter<int>("value"));
            CreateMap<Input, RuntimeFactory>().ConstructUsing(s => RuntimeFactory.Create(s));
            CreateMap<Input, Conditional>().ForMember(d => d.Value, o => o.MapFrom(s => s.Id).PreCondition(s => s.Id > 0));
            CreateMap<Input, DestinationPath>().ForMember(d => d.Child!.Value, s => (long)s.Id);
            CreateMap<OperatorHolder, OperatorOutput>().ForMember(d => d.Value, s => (OperatorTarget)s.Value);
        }
    }
    private sealed class StrictSubstituteRules : MapperProfile
    {
        public StrictSubstituteRules()
        {
            CreateMap<Input, StrictSubstitute>().ForMember(d => d.Value, o => o.MapFrom(s => s.Optional).NullSubstitute(9));
            CreateMap<Input, StrictSubstituteConstructor>().ForMember(d => d.Value, o => o.MapFrom(s => s.Optional).NullSubstitute(9));
            CreateMap<Input, StrictWithoutSubstitute>().ForMember(d => d.Value, o => o.MapFrom(s => s.Optional));
        }
    }
    private sealed class ContextRules : MapperProfile
    { public ContextRules() => CreateMap<Input, Contextual>().ForMember(d => d.Value, o => o.MapFrom((s, c) => s.Id + c.GetParameter<int>("value"))); }
    private sealed class ShapeInspector : ExpressionVisitor
    {
        public int RuntimeNodes;
        public override Expression? Visit(Expression? node)
        { if (node is InvocationExpression or BlockExpression or TryExpression || node?.NodeType == ExpressionType.Assign) RuntimeNodes++; return base.Visit(node); }
        protected override Expression VisitMethodCall(MethodCallExpression node)
        { if (node.Method.DeclaringType?.Assembly == typeof(IMapper).Assembly) RuntimeNodes++; return base.VisitMethodCall(node); }
    }
    private sealed class RecordingProvider : IQueryProvider
    {
        public int Created, Executed, Enumerated;
        public IQueryable<TElement> CreateQuery<TElement>(Expression expression) { Created++; return new RecordingQuery<TElement>(this, expression); }
        public IQueryable CreateQuery(Expression expression) => throw new NotSupportedException();
        public TResult Execute<TResult>(Expression expression) { Executed++; throw new Exception("Projection unexpectedly executed its provider."); }
        public object Execute(Expression expression) { Executed++; throw new Exception("Projection unexpectedly executed its provider."); }
    }
    private sealed class RecordingQuery<T>(RecordingProvider provider, Expression? expression = null) : IQueryable<T>
    {
        public Type ElementType => typeof(T);
        public Expression Expression => expression ?? Expression.Constant(this);
        public IQueryProvider Provider => provider;
        public IEnumerator<T> GetEnumerator() { provider.Enumerated++; throw new Exception("Projection unexpectedly enumerated its source."); }
        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }
}
