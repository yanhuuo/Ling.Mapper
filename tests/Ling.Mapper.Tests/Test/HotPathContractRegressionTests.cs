using Ling.Mapper;
using Ling.Mapper.Configuration;
using Ling.Mapper.Models;

namespace TestConsole.Test;

public static class HotPathContractRegressionTests
{
    public static void Run()
    {
        int assertions = 0;
        void Check(bool condition) { assertions++; if (!condition) throw new Exception("Hot-path semantic guard failed."); }
        var config = new MapperConfiguration(); config.AddProfile(new Rules()); var mapper = config.CreateMapper();
        var shared = new Derived { Value = 3 };
        var source = new Source { Base = shared, Derived = shared, Object = shared, Again = shared };
        var result = mapper.Map<Target>(source)!;
        Check(result.Base!.Value == 103); Check(result.Derived!.Value == 203); Check(result.Object!.Value == 303);
        Check(ReferenceEquals(result.Base, result.Again)); Check(!ReferenceEquals(result.Base, result.Derived));
        Check(!ReferenceEquals(result.Base, result.Object));
        var again = mapper.Map<Target>(source)!;
        Check(!ReferenceEquals(result.Base, again.Base)); Check(ReferenceEquals(again.Base, again.Again));
        var destination = new Target { Base = new Result(), Derived = new Result(), Object = new Result(), Again = new Result() };
        Check(ReferenceEquals(destination, mapper.MapInto(source, destination)));
        Check(destination.Base!.Value == 103 && destination.Derived!.Value == 203 && destination.Object!.Value == 303);
        Check(ReferenceEquals(destination.Base, destination.Again));
        // One source identity, two destination types: compact tokens must encode
        // destination type as well as configured source-plan scope.
        var unconfigured = new MapperConfiguration().CreateMapper();
        var pair = unconfigured.Map<TwoDestinations>(new Pair { A = shared, B = shared })!;
        Check(pair.A!.Value == 3 && pair.B!.Value == 3);
        var node = new CycleDerived { Value = 9 }; node.Next = node;
        var cycle = unconfigured.Map<CycleResult>(node)!;
        Check(ReferenceEquals(cycle, cycle.Next)); Check(cycle.Value == 9);
        // Projected and complete results for the same object cannot share entries.
        var projectionConfig = new MapperConfiguration(); projectionConfig.AddProfile(new ProjectionRules());
        var projected = projectionConfig.CreateMapper().Map<ProjectedTarget>(new Pair { A = shared, B = shared })!;
        Check(projected.A!.Value == 0); Check(projected.B!.Value == 3); Check(!ReferenceEquals(projected.A, projected.B));
        // A declared base profile must survive heterogeneous runtime elements.
        var list = mapper.Map<List<Result>>(new List<Base> { shared, new Base { Value = 5 }, shared })!;
        Check(list[0].Value == 103 && list[1].Value == 105); Check(ReferenceEquals(list[0], list[2]));
        // Hoisting immutable element metadata must NOT freeze mutable converter
        // registry versions at collection-plan creation or at batch entry.
        int callbacks = 0;
        TypeConverterRegistry.Register(typeof(ConvertSource), typeof(ConvertTarget),
            new Func<ConvertSource, ConvertTarget>(s => new() { Value = s.Value }));
        try
        {
            var converted = unconfigured.Map<List<ConvertTarget>>(
                new[] { new ConvertSource { Value = 1 }, new ConvertSource { Value = 2 }, new ConvertSource { Value = 3 } },
                AdaptOptions.Strict, (_, _) =>
                {
                    if (++callbacks == 1)
                        TypeConverterRegistry.Register(typeof(ConvertSource), typeof(ConvertTarget),
                            new Func<ConvertSource, ConvertTarget>(s => new() { Value = s.Value + 100 }));
                })!;
            Check(converted[0].Value == 1 && converted[1].Value == 102 && converted[2].Value == 103); Check(callbacks == 3);
        }
        finally { TypeConverterRegistry.Register(typeof(ConvertSource), typeof(ConvertTarget), new Func<ConvertSource, ConvertTarget>(s => new() { Value = s.Value })); }
        // The hot sensitive-path scan remains ordinal case-insensitive and keeps
        // all existing keyword matches, including substrings.
        var diagnostics = new MapperConfiguration { IncludeSourceValueInExceptions = true }.CreateMapper();
        foreach (var property in typeof(Secrets).GetProperties())
        {
            var secret = new Secrets(); property.SetValue(secret, "do-not-log-7391");
            try { diagnostics.Map<Numbers>(secret); throw new Exception("Expected conversion failure."); }
            catch (MappingException error)
            {
                Check(error.Path == "$." + property.Name);
                Check(!error.Message.Contains("do-not-log-7391", StringComparison.Ordinal));
                Check(error.SourceValue == "[REDACTED]");
            }
        }
        Parallel.For(0, 128, _ =>
        {
            var mapped = mapper.Map<Target>(source)!;
            if (mapped.Base!.Value != 103 || mapped.Derived!.Value != 203 || mapped.Object!.Value != 303 ||
                !ReferenceEquals(mapped.Base, mapped.Again) || ReferenceEquals(mapped.Base, mapped.Derived))
                throw new Exception("Concurrent compact scope token crossed plan identities.");
        });
        assertions += 128 * 5;
        Console.WriteLine($"Hot-path contract regression: {assertions} assertions passed.");
    }
    public class Base { public int Value { get; set; } }
    public sealed class Derived : Base { }
    public sealed class Result { public int Value { get; set; } }
    public sealed class OtherResult { public int Value { get; set; } }
    public sealed class Source { public Base? Base { get; set; } public Derived? Derived { get; set; } public object? Object { get; set; } public Base? Again { get; set; } }
    public sealed class Target { public Result? Base { get; set; } public Result? Derived { get; set; } public Result? Object { get; set; } public Result? Again { get; set; } }
    public sealed class Pair { public Base? A { get; set; } public Base? B { get; set; } }
    public sealed class TwoDestinations { public Result? A { get; set; } public OtherResult? B { get; set; } }
    public sealed class ProjectedTarget { public Result? A { get; set; } public Result? B { get; set; } }
    public class CycleBase { public int Value { get; set; } public CycleBase? Next { get; set; } }
    public sealed class CycleDerived : CycleBase { }
    public sealed class CycleResult { public int Value { get; set; } public CycleResult? Next { get; set; } }
    public sealed class ConvertSource { public int Value { get; set; } }
    public sealed class ConvertTarget { public int Value { get; set; } }
    public sealed class Secrets
    {
        public string? PASSWORD { get; set; } public string? passWD { get; set; } public string? myPWD { get; set; }
        public string? bearerTOKEN { get; set; } public string? SECRET { get; set; } public string? CREDENTIAL { get; set; }
        public string? ENCRYPT { get; set; } public string? CIPHER { get; set; } public string? PRIVATEKEY { get; set; } public string? APIKEY { get; set; }
    }
    public sealed class Numbers
    {
        public int PASSWORD { get; set; } public int passWD { get; set; } public int myPWD { get; set; }
        public int bearerTOKEN { get; set; } public int SECRET { get; set; } public int CREDENTIAL { get; set; }
        public int ENCRYPT { get; set; } public int CIPHER { get; set; } public int PRIVATEKEY { get; set; } public int APIKEY { get; set; }
    }
    public sealed class Rules : MapperProfile
    {
        public Rules()
        {
            CreateMap<Base, Result>().ForMember(d => d.Value, s => s.Value + 100);
            CreateMap<Derived, Result>().ForMember(d => d.Value, s => s.Value + 200);
            CreateMap<object, Result>().ForMember(d => d.Value, s => ((Base)s).Value + 300);
        }
    }
    public sealed class ProjectionRules : MapperProfile { public ProjectionRules() { CreateMap<Pair, ProjectedTarget>().Ignore(d => d.A!.Value); } }
}
