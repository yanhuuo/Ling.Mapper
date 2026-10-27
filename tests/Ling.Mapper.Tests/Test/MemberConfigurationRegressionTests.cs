using Ling.Mapper;
using Ling.Mapper.Configuration;
using Ling.Mapper.Mapper;
using Ling.Mapper.Models;
using System.Linq.Expressions;

namespace TestConsole.Test;

/// <summary>Member-rule ordering, source/destination type independence and opt-in configuration validation.</summary>
public static class MemberConfigurationRegressionTests
{
    public static void Run()
    {
        var assertions = 0;
        void Equal(object? expected, object? actual)
        {
            assertions++;
            if (!Equals(expected, actual)) throw new Exception($"Expected '{expected ?? "null"}', got '{actual ?? "null"}'.");
        }
        T Throws<T>(Action action) where T : Exception
        {
            assertions++;
            try { action(); }
            catch (T error) { return error; }
            throw new Exception($"Expected {typeof(T).Name}.");
        }
        IMapper Mapper(MapperProfile profile)
        {
            var configuration = new MapperConfiguration();
            configuration.AddProfile(profile);
            return configuration.CreateMapper();
        }

        var mapper = Mapper(new Rules());
        Throws<ArgumentNullException>(() => new MappingExpression<Source, Target>().ForMember(d => d.Value, (Expression<Func<Source, int>>)null!));
        Equal("srcExpr", Throws<ArgumentNullException>(() => new MappingExpression<Source, Target>().ForMember(d => d.Value, null!)).ParamName);
        Throws<ArgumentNullException>(() => new MappingExpression<Source, Target>().ForMember(d => d.Value, (Action<MemberOptions<Source, Target, int>>)null!));
        Equal(41, Mapper(new MethodNameCollisionRules()).Map<Target>(new MethodNameCollisionSource())!.Value);
        var source = new Source { Enabled = false, ThrowOnRead = true };
        Equal(17, mapper.Map<Target>(source)!.Value);
        Equal(0, source.Reads); // PreCondition does not resolve the throwing getter.
        source.Enabled = true;
        var readError = Throws<MappingException>(() => mapper.Map<Target>(source));
        Equal(true, readError.Path.Contains("Value"));
        Equal(1, source.Reads);
        source.ThrowOnRead = false;
        source.Raw = "42";
        Equal(42, mapper.Map<Target>(source)!.Value);
        Equal(2, source.Reads); // One read, independently typed string -> int.
        source.Raw = "-8";
        Equal(17, mapper.Map<Target>(source)!.Value); // Condition receives converted candidate.
        Equal(3, source.Reads); // Condition is not a getter guard.
        source.Raw = null;
        Equal(23, mapper.Map<Target>(source)!.Value);
        Equal(23, mapper.Map<Target>(source, AdaptOptions.Default | AdaptOptions.IgnoreNullValues)!.Value);
        source.Raw = "invalid";
        Throws<MappingException>(() => mapper.Map<Target>(source)); // Condition cannot hide conversion failure.
        Equal("$.Value", Throws<MappingException>(() => Mapper(new ThrowingPreconditionRules()).Map<Target>(new Source())).Path);
        Equal("$.Value", Throws<MappingException>(() => Mapper(new ThrowingConditionRules()).Map<Target>(new Source { Raw = "1" })).Path);
        source.ThrowOnRead = true;
        Equal(17, mapper.Map<Target>(source, AdaptOptions.Default, ignoreNames: ["Value"])!.Value);

        var ignored = Mapper(new IgnoredRules());
        Equal(17, ignored.Map<Target>(new Source { Enabled = true, ThrowOnRead = true })!.Value);
        var precedence = Mapper(new ExplicitRules());
        Equal(31, precedence.Map<Target>(new Source { ThrowOnRead = true })!.Value);

        var nullMapper = Mapper(new NullRules());
        var nullResult = nullMapper.Map<NullTarget>(new NullSource(), AdaptOptions.Default | AdaptOptions.IgnoreNullValues)!;
        Equal(null, nullResult.AssignNull);
        Equal("initial", nullResult.KeepNull);
        Equal("substitute", nullResult.Substitute);
        var ordinary = nullMapper.Map<NullTarget>(new NullSource())!;
        Equal(null, ordinary.AssignNull);
        Equal("initial", ordinary.KeepNull);
        Equal("substitute", ordinary.Substitute);
        Equal((int?)29, Mapper(new NullableSubstituteRules()).Map<NullableTarget>(new Source())!.Value);

        var substitute = new Child { Value = 7 };
        var finalMapper = Mapper(new SubstituteRules(substitute));
        var childResult = finalMapper.Map<ChildTarget>(new ChildSource())!;
        Equal(false, ReferenceEquals(substitute, childResult.Child));
        Equal(107, childResult.Child!.Value); // Mutable substitute maps independently and bypasses the source-typed final converter.
        Equal(7, substitute.Value); Equal(false, ReferenceEquals(childResult.Child, finalMapper.Map<ChildTarget>(new ChildSource())!.Child));
        Equal(107, finalMapper.Map<Child>(substitute)!.Value);

        var same = Mapper(new SameRules());
        var sameSource = new Same { Value = 6 };
        var sameResult = same.Map<Same>(sameSource)!;
        Equal(false, ReferenceEquals(sameSource, sameResult));
        Equal(0, sameResult.Value);
        Equal(6, sameSource.Value);

        var configuration = new MapperConfiguration();
        configuration.AddProfile(new MissingRules());
        Equal(null, configuration.CreateMapper().Map<MissingTarget>(new MissingSource())!.Unmapped); // Default remains permissive.
        var validation = Throws<MappingConfigurationException>(configuration.AssertConfigurationIsValid);
        Equal(1, validation.Errors.Count);
        Equal("Unmapped", validation.Errors[0].MemberPath);
        Equal(typeof(MissingSource), validation.Errors[0].SourceType);
        Equal(typeof(MissingTarget), validation.Errors[0].DestinationType);
        Equal(true, validation.Message.Contains("MapFrom/Rename"));
        configuration.RequireDestinationMemberSource = true;
        Throws<MappingConfigurationException>(() => configuration.CreateMapper());
        configuration = new MapperConfiguration { RequireDestinationMemberSource = true };
        configuration.AddProfile(new IgnoredMissingRules());
        configuration.AssertConfigurationIsValid(); assertions++;
        Equal(null, configuration.CreateMapper().Map<MissingTarget>(new MissingSource())!.Unmapped);

        configuration = new MapperConfiguration();
        configuration.AddProfile(new BadRenameRules());
        validation = Throws<MappingConfigurationException>(configuration.AssertConfigurationIsValid);
        Equal("Value", validation.Errors.Single().MemberPath);
        Equal(true, validation.Message.Contains("Missing.Path"));
        configuration = new MapperConfiguration(); configuration.AddProfile(new ExplicitRules());
        configuration.AssertConfigurationIsValid(); assertions++; // Explicit selector suppresses invalid lower-priority rename.
        configuration = new MapperConfiguration(); configuration.AddProfile(new IgnoredRules());
        configuration.AssertConfigurationIsValid(); assertions++; // Ignore suppresses getter and invalid rename.

        configuration = new MapperConfiguration(); configuration.AddProfiles(new MissingRules(), new MissingRules());
        validation = Throws<MappingConfigurationException>(configuration.AssertConfigurationIsValid);
        Equal(true, validation.Errors.Any(error => error.MemberPath == "$" && error.Message.Contains("Multiple explicit")));
        Equal(true, validation.Errors.Any(error => error.MemberPath == "Unmapped"));

        configuration = new MapperConfiguration(); configuration.AddProfile(new AmbiguousRules());
        validation = Throws<MappingConfigurationException>(configuration.AssertConfigurationIsValid);
        Equal("Id", validation.Errors.Single().MemberPath);
        Equal(true, validation.Errors.Single().Message.Contains("ambiguous"));
        configuration = new MapperConfiguration(); configuration.AddProfile(new ExplicitAmbiguousRules());
        configuration.AssertConfigurationIsValid();
        Equal(7, configuration.CreateMapper().Map<AmbiguousTarget>(new AmbiguousSource { ID = 7, I_D = 99 })!.Id);
        configuration = new MapperConfiguration(); configuration.AddProfile(new IgnoredReadonlyRules());
        configuration.AssertConfigurationIsValid();
        Equal(41, configuration.CreateMapper().Map<ReadonlyTarget>(new Source())!.Value);
        configuration = new MapperConfiguration(); configuration.AddProfile(new AmbiguousConstructorRules());
        validation = Throws<MappingConfigurationException>(configuration.AssertConfigurationIsValid);
        Equal("$", validation.Errors.Single().MemberPath);
        Equal(true, validation.Errors.Single().Message.Contains("multiple equally preferred"));
        configuration = new MapperConfiguration(); configuration.AddProfile(new MissingConstructorRules());
        validation = Throws<MappingConfigurationException>(configuration.AssertConfigurationIsValid);
        Equal("$", validation.Errors.Single().MemberPath);
        Equal(true, validation.Errors.Single().Message.Contains("constructor"));
        configuration = new MapperConfiguration(); configuration.AddProfile(new InterfaceRules());
        validation = Throws<MappingConfigurationException>(configuration.AssertConfigurationIsValid);
        Equal("$", validation.Errors.Single().MemberPath);
        Equal(true, validation.Errors.Single().Message.Contains("ConstructUsing"));

        Source.TotalReads = 0;
        configuration = new MapperConfiguration(); configuration.AddProfile(new Rules());
        configuration.AssertConfigurationIsValid();
        Equal(0, Source.TotalReads); // Metadata/plan validation never invokes source getters.
        var conventionCalls = 0;
        configuration.ConfigureConventions(options => { conventionCalls++; options.IgnoreSpecialCharacters = true; });
        configuration.AssertConfigurationIsValid(); configuration.CreateMapper();
        Equal(1, conventionCalls);

        var converterCalls = 0;
        TypeConverterRegistry.Register(typeof(ConverterSource), typeof(ConverterTarget), new Func<ConverterSource, ConverterTarget>(_ =>
        {
            converterCalls++;
            return new ConverterTarget { Value = 88 };
        }));
        configuration = new MapperConfiguration { RequireDestinationMemberSource = true };
        configuration.AddProfiles(new ConverterRules(), new ConverterRules());
        configuration.AssertConfigurationIsValid();
        Equal(0, converterCalls);
        Equal(88, configuration.CreateMapper().Map<ConverterTarget>(new ConverterSource())!.Value);
        Equal(1, converterCalls);

        Throws<NotSupportedException>(() => new ComputedReverseRules());
        Throws<NotSupportedException>(() => new ConditionalReverseRules());
        Throws<ArgumentException>(() => new MappingExpression<Source, Target>().ForMember<object>(d => d.Value, options => options.NullSubstitute("wrong type")));
        configuration = new MapperConfiguration(); configuration.AddProfile(new NestedRules());
        configuration.AssertConfigurationIsValid(); assertions++;
        var nested = Mapper(new NestedRules());
        Equal(12, nested.Map<NestedTarget>(new Source { Raw = "12", Enabled = true })!.Child!.Value);
        Equal(null, nested.Map<NestedTarget>(new Source { ThrowOnRead = true, Enabled = false })!.Child);
        Console.WriteLine($"MemberConfigurationRegressionTests: {assertions} assertions passed.");
    }

    public sealed class Source
    {
        public static int TotalReads;
        public bool Enabled { get; set; }
        public bool ThrowOnRead { get; set; }
        public string? Raw { get; set; }
        public int Reads { get; private set; }
        public string? Text
        {
            get { Reads++; TotalReads++; if (ThrowOnRead) throw new InvalidOperationException("Getter must not run."); return Raw; }
        }
    }
    public sealed class Target { public int Value { get; set; } = 17; }
    public sealed class NullableTarget { public int? Value { get; set; } }
    private sealed class NullableSubstituteRules : MapperProfile
    {
        public NullableSubstituteRules() => CreateMap<Source, NullableTarget>().ForMember(d => d.Value, options => options.MapFrom(s => s.Text).NullSubstitute(29));
    }
    private static bool FailCondition() => throw new NullReferenceException("Condition errors must not be swallowed.");
    private sealed class ThrowingPreconditionRules : MapperProfile
    {
        public ThrowingPreconditionRules() => CreateMap<Source, Target>().ForMember(d => d.Value, options => options.MapFrom(s => s.Text).PreCondition(s => FailCondition()));
    }
    private sealed class ThrowingConditionRules : MapperProfile
    {
        public ThrowingConditionRules() => CreateMap<Source, Target>().ForMember(d => d.Value, options => options.MapFrom(s => s.Text).Condition((s, d, v) => FailCondition()));
    }
    public sealed class MethodNameCollisionSource { public int Ignore() => 41; }
    private sealed class MethodNameCollisionRules : MapperProfile
    {
        public MethodNameCollisionRules() => CreateMap<MethodNameCollisionSource, Target>().ForMember(d => d.Value, s => s.Ignore());
    }
    private sealed class Rules : MapperProfile
    {
        public Rules() => CreateMap<Source, Target>().ForMember(d => d.Value, options => options
            .MapFrom(s => s.Text).PreCondition(s => s.Enabled).NullSubstitute(23)
            .Condition((s, d, value) => value >= 0 && d.Value == 17));
    }
    private sealed class IgnoredRules : MapperProfile
    {
        public IgnoredRules() => CreateMap<Source, Target>().ForMember(d => d.Value, options => options.MapFrom(s => s.Text).Ignore())
            .Rename(d => d.Value, "Missing.Path");
    }
    private sealed class ExplicitRules : MapperProfile
    {
        public ExplicitRules() => CreateMap<Source, Target>().Rename(d => d.Value, "Missing.Path").ForMember(d => d.Value, s => 31);
    }
    private sealed class BadRenameRules : MapperProfile
    {
        public BadRenameRules() => CreateMap<Source, Target>().Rename(d => d.Value, "Missing.Path");
    }
    public sealed class NullSource { public string? AssignNull { get; set; } public string? KeepNull { get; set; } public string? Substitute { get; set; } }
    public sealed class NullTarget
    {
        public string? AssignNull { get; set; } = "initial";
        public string? KeepNull { get; set; } = "initial";
        public string? Substitute { get; set; } = "initial";
    }
    private sealed class NullRules : MapperProfile
    {
        public NullRules() => CreateMap<NullSource, NullTarget>()
            .ForMember(d => d.AssignNull, options => options.IgnoreNullValues(false))
            .ForMember(d => d.KeepNull, options => options.IgnoreNullValues())
            .ForMember(d => d.Substitute, options => options.NullSubstitute("substitute"));
    }
    public sealed class Child { public int Value { get; set; } }
    public sealed class ChildSource { public Child? Child { get; set; } }
    public sealed class ChildTarget { public Child? Child { get; set; } }
    private sealed class SubstituteRules : MapperProfile
    {
        public SubstituteRules(Child substitute)
        {
            CreateMap<Child, Child>().ForMember(d => d.Value, s => s.Value + 100);
            CreateMap<ChildSource, ChildTarget>().ForMember(d => d.Child, options => options.NullSubstitute(substitute));
        }
    }
    public sealed class Same { public int Value { get; set; } }
    private sealed class SameRules : MapperProfile
    {
        public SameRules() => CreateMap<Same, Same>().ForMember(d => d.Value, options => options.Condition((s, d, value) => false));
    }
    public sealed class MissingSource { public int Id { get; set; } }
    public sealed class MissingTarget { public int Id { get; set; } public string? Unmapped { get; set; } }
    private sealed class MissingRules : MapperProfile { public MissingRules() => CreateMap<MissingSource, MissingTarget>(); }
    private sealed class IgnoredMissingRules : MapperProfile { public IgnoredMissingRules() => CreateMap<MissingSource, MissingTarget>().Ignore(d => d.Unmapped!); }
    public sealed class AmbiguousSource { public int ID { get; set; } public int I_D { get; set; } }
    public sealed class AmbiguousTarget { public int Id { get; set; } }
    private sealed class AmbiguousRules : MapperProfile { public AmbiguousRules() => CreateMap<AmbiguousSource, AmbiguousTarget>(); }
    private sealed class ExplicitAmbiguousRules : MapperProfile
    {
        public ExplicitAmbiguousRules() => CreateMap<AmbiguousSource, AmbiguousTarget>().ForMember(d => d.Id, s => s.ID);
    }
    public sealed class ReadonlyTarget { public int Value => 41; }
    private sealed class IgnoredReadonlyRules : MapperProfile
    {
        public IgnoredReadonlyRules() => CreateMap<Source, ReadonlyTarget>().Ignore(d => d.Value);
    }
    public sealed class AmbiguousConstructor
    {
        public int Id { get; }
        public AmbiguousConstructor(int id) => Id = id;
        public AmbiguousConstructor(long id) => Id = checked((int)id);
    }
    private sealed class AmbiguousConstructorRules : MapperProfile
    {
        public AmbiguousConstructorRules() => CreateMap<MissingSource, AmbiguousConstructor>();
    }
    public sealed class MissingConstructor { public MissingConstructor(int noSource) { } }
    private sealed class MissingConstructorRules : MapperProfile
    {
        public MissingConstructorRules() => CreateMap<MissingSource, MissingConstructor>();
    }
    public interface ITarget { int Id { get; set; } }
    private sealed class InterfaceRules : MapperProfile
    {
        public InterfaceRules() => CreateMap<MissingSource, ITarget>();
    }
    public sealed class ConverterSource { }
    public sealed class ConverterTarget { public int Value { get; set; } }
    private sealed class ConverterRules : MapperProfile { public ConverterRules() => CreateMap<ConverterSource, ConverterTarget>().Rename(d => d.Value, "Missing.Path"); }
    private sealed class ComputedReverseRules : MapperProfile
    {
        public ComputedReverseRules() => CreateMap<Same, Same>().ForMember(d => d.Value, s => s.Value + 1).ReverseMap();
    }
    private sealed class ConditionalReverseRules : MapperProfile
    {
        public ConditionalReverseRules() => CreateMap<Same, Same>().ForMember(d => d.Value, options => options.PreCondition(s => s.Value > 0)).ReverseMap();
    }
    public sealed class NestedTarget { public Child? Child { get; set; } }
    private sealed class NestedRules : MapperProfile
    {
        public NestedRules() => CreateMap<Source, NestedTarget>().ForMember(d => d.Child!.Value, options => options.MapFrom(s => s.Text).PreCondition(s => s.Enabled));
    }
}
