using Ling.Mapper;
using Ling.Mapper.Configuration;
using Ling.Mapper.Extensions;
using Ling.Mapper.Models;
using Ling.Mapper.Provider;

namespace TestConsole.Test;

/// <summary>Same-type ordinary mappings are independent graphs; explicit member rules and final converters retain their distinct roles.</summary>
public static class SameTypeMemberRulesRegressionTests
{
    public static void Run()
    {
        int assertions = 0;
        void Equal(object? expected, object? actual)
        {
            assertions++;
            if (!Equals(expected, actual)) throw new Exception($"Expected {expected ?? "null"}; got {actual ?? "null"}.");
        }
        var plain = new MapperConfiguration().CreateMapper();
        var value = new Same { Value = 3, Other = 7, Ignored = "source" };
        Equal(false, ReferenceEquals(value, plain.Map<Same>(value)));
        // A differently configured mapper must not reuse the first mapper's default copy plan.
        var configuration = new MapperConfiguration(); configuration.AddProfile(new Rules());
        var mapper = configuration.CreateMapper();
        Same.Reads = 0;
        var result = mapper.Map<Same>(value)!;
        Equal(false, ReferenceEquals(value, result)); Equal(103, result.Value); Equal(7, result.Other);
        Equal("initial", result.Ignored); Equal(3, value.Value); Equal("source", value.Ignored); Equal(1, Same.Reads);
        Equal(103, ((Same)mapper.Map(value, typeof(Same), typeof(Same))!).Value);
        Equal(2, Same.Reads);
        var previous = MapperProvider.Current;
        try { MapperProvider.SetCurrent(mapper); Equal(103, value.Adapt<Same>().Value); }
        finally { MapperProvider.SetCurrent(previous); }
        Equal(3, Same.Reads);
        var after = 0;
        result = mapper.Map<Same>(value, AdaptOptions.Default, (target, source) =>
        {
            after++; Equal(103, ((Same)target).Value); Equal(true, ReferenceEquals(value, source));
        })!;
        Equal(1, after); Equal(4, Same.Reads);
        Equal(0, mapper.Map<Same>(value, AdaptOptions.Default, ignoreNames: ["Value"])!.Value);
        Equal(4, Same.Reads); // Runtime Ignore prevents the custom method from executing.
        Equal(103, mapper.Map<Same>(value)!.Value); Equal(5, Same.Reads); // Ignore-plan cache cannot poison normal plan.
        Equal(null, mapper.Map<Same>(null));
        var cycle = new Same { Value = 4 }; cycle.Next = cycle;
        var copied = mapper.Map<Same>(cycle)!;
        Equal(104, copied.Value); Equal(true, ReferenceEquals(copied, copied.Next));
        var child = new Same { Value = 9 };
        var pair = mapper.Map<PairTarget>(new PairSource { First = child, Second = child })!;
        Equal(109, pair.First!.Value); Equal(true, ReferenceEquals(pair.First, pair.Second));
        Equal(false, ReferenceEquals(child, pair.First));
        Equal(103, mapper.Map<BoxTarget>(new BoxSource { Value = value })!.Value!.Value);
        Equal(103, mapper.Map<ObjectTarget>(new ObjectSource { Value = value })!.Value!.Value);
        Equal(103, mapper.Map<InterfaceTarget>(new InterfaceSource { Value = value })!.Value!.Value);
        Equal(103, mapper.Map<ImmutableHolder>(new BoxSource { Value = value }).Value.Value);
        var array = new[] { value, value };
        var arrayResult = mapper.Map<Same[]>(array)!;
        Equal(false, ReferenceEquals(array, arrayResult)); Equal(103, arrayResult[0].Value);
        Equal(true, ReferenceEquals(arrayResult[0], arrayResult[1]));
        var list = new List<Same> { value };
        Equal(103, mapper.Map<List<Same>>(list)![0].Value);
        Equal(103, mapper.Map<IEnumerable<Same>>(list)!.Single().Value);
        Equal(103, mapper.Map<Same[][]>(new[] { array })![0][0].Value);
        Equal(103, mapper.Map<BoxListTarget>(new BoxListSource { Value = list })!.Value![0].Value);
        var dictionary = new Dictionary<string, Same> { ["v"] = value };
        Equal(103, mapper.Map<Dictionary<string, Same>>(dictionary)!["v"].Value);
        Equal(103, mapper.Map<IReadOnlyDictionary<string, Same>>(dictionary)!["v"].Value);
        Equal(103, mapper.Map<Dictionary<Same, string>>(new Dictionary<Same, string> { [value] = "v" })!.Keys.Single().Value);
        // ForMember is source selection: mutable target-shaped results still map independently.
        var explicitResult = mapper.Map<ExplicitTarget>(new ExplicitSource { Value = value })!;
        Equal(false, ReferenceEquals(value, explicitResult.Value)); Equal(103, explicitResult.Value!.Value); Equal(3, value.Value);
        Equal(false, ReferenceEquals(value, mapper.Map<ImmutableExplicit>(new ExplicitSource { Value = value }).Value)); Equal(103, mapper.Map<ImmutableExplicit>(new ExplicitSource { Value = value }).Value!.Value);
        Equal(false, ReferenceEquals(value, mapper.Map<ExplicitObjectTarget>(new ExplicitObjectSource { Value = value })!.Value)); Equal(103, mapper.Map<ExplicitObjectTarget>(new ExplicitObjectSource { Value = value })!.Value!.Value);
        Equal(false, ReferenceEquals(value, mapper.Map<ImmutableObjectExplicit>(new ExplicitObjectSource { Value = value }).Value)); Equal(103, mapper.Map<ImmutableObjectExplicit>(new ExplicitObjectSource { Value = value }).Value!.Value);
        var derivedItem = new DerivedSame { Value = 3 };
        Equal(103, mapper.Map<List<Same>>(new List<Same> { derivedItem })![0].Value);
        Equal(103, mapper.Map<Same[]>(new Same[] { derivedItem })![0].Value);
        Equal(103, mapper.Map<Dictionary<string, Same>>(new Dictionary<string, Same> { ["v"] = derivedItem })!["v"].Value);
        Equal(103, ((IEnumerable<Same>)mapper.Map(new List<DerivedSame> { derivedItem }, typeof(IEnumerable<Same>), typeof(IEnumerable<Same>))!).Single().Value);
        var split = mapper.Map<PairTarget>(new SplitSource { First = derivedItem, Second = derivedItem })!;
        Equal(103, split.First!.Value); Equal(303, split.Second!.Value);
        Equal(false, ReferenceEquals(split.First, split.Second));
        Equal(303, ((Same)mapper.Map(derivedItem, typeof(DerivedSame), typeof(Same))!).Value);
        var interfaceValue = new ExplicitValue { Value = 3 };
        Equal(203, ((ExplicitValue)mapper.Map(interfaceValue, typeof(IExplicitValue), typeof(ExplicitValue))!).Value);
        Equal(203, mapper.Map<ExplicitInterfaceTarget>(new ExplicitInterfaceSource { Value = interfaceValue })!.Value!.Value);
        Equal(203, mapper.Map<ExplicitValue[]>(new IExplicitValue[] { interfaceValue })![0].Value);
        var ambiguous = new AmbiguousEnumerable();
        foreach (var active in new[] { plain, mapper })
        {
            try { active.Map<AmbiguousEnumerable>(ambiguous); throw new Exception("Ambiguous collection clone must reject."); }
            catch (MappingException error) { Equal(typeof(NotSupportedException).FullName, error.OriginalExceptionType); Equal("$", error.Path); }
        }
        Equal(3, ((AbstractOutput)mapper.Map(new AbstractConcrete { Value = 3 }, typeof(AbstractInput), typeof(AbstractOutput))!).Value);
        Equal(3, mapper.Map<AbstractHolderOutput>(new AbstractHolderInput { Value = new AbstractConcrete { Value = 3 } })!.Value!.Value);
        Equal(3, mapper.Map<AbstractOutput[]>(new AbstractInput[] { new AbstractConcrete { Value = 3 } })![0].Value);
        var ordinaryCycle = new CycleDerived(); ordinaryCycle.Next = ordinaryCycle;
        var ordinaryMapped = mapper.Map<CycleTarget>(ordinaryCycle)!;
        Equal(true, ReferenceEquals(ordinaryMapped, ordinaryMapped.Next));
        var markerValue = new MarkerSource { Value = 3 };
        var marker = mapper.Map<MarkerPairTarget>(new MarkerPairSource { First = markerValue, Second = markerValue })!;
        Equal(100, marker.First!.Value); Equal(3, marker.Second!.Value);
        Equal(false, ReferenceEquals(marker.First, marker.Second));
        // Root same-type Rename and Ignore each independently opt out of identity.
        var rename = mapper.Map<Renamed>(new Renamed { Value = 2, Other = 8 })!;
        Equal(8, rename.Value); Equal(8, rename.Other);
        var ignored = mapper.Map<Ignored>(new Ignored { Value = 2, Other = 8 })!;
        Equal(70, ignored.Value); Equal(8, ignored.Other);
        // Empty CreateMap and unconfigured mutable graphs are also independent copies.
        var untouched = new Untouched { Value = value };
        var untouchedCopy = mapper.Map<Untouched>(untouched)!; Equal(false, ReferenceEquals(untouched, untouchedCopy)); Equal(false, ReferenceEquals(value, untouchedCopy.Value)); Equal(103, untouchedCopy.Value!.Value);
        Equal(false, ReferenceEquals(value, plain.Map<Same>(value)));
        var plainList = plain.Map<List<Same>>(list)!; Equal(false, ReferenceEquals(list, plainList)); Equal(false, ReferenceEquals(value, plainList[0])); Equal(3, plainList[0].Value);
        var plainDictionary = plain.Map<Dictionary<string, Same>>(dictionary)!; Equal(false, ReferenceEquals(dictionary, plainDictionary)); Equal(false, ReferenceEquals(value, plainDictionary["v"])); Equal(3, plainDictionary["v"].Value);
        // Declared Type pair must still honor its configured rule when runtime is derived.
        var derived = new DerivedSame { Value = 6 };
        Equal(106, ((Same)mapper.Map(derived, typeof(Same), typeof(Same))!).Value);
        var derivedCopy = mapper.Map<DerivedSame>(derived)!; Equal(false, ReferenceEquals(derived, derivedCopy)); Equal(6, derivedCopy.Value); // No self pair configured.
        // Profiles/configuration must be finished before CreateMapper. Building another mapper
        // after adding a profile is supported; existing mappers are not live reconfigurable.
        var earlyConfiguration = new MapperConfiguration();
        var early = earlyConfiguration.CreateMapper(); Equal(false, ReferenceEquals(value, early.Map<Same>(value)));
        earlyConfiguration.AddProfile(new Rules());
        var later = earlyConfiguration.CreateMapper(); Equal(103, later.Map<Same>(value)!.Value);
        // Whole-type explicit converters own mutable mapping results, including
        // same-type roots and results selected by ordinary ForMember expressions.
        TypeConverterRegistry.Register(typeof(Same), typeof(Same), new Func<Same, Same>(source => new() { Value = source.Value + 1000 }));
        Equal(false, ReferenceEquals(value, plain.Map<Same>(value))); Equal(1003, plain.Map<Same>(value)!.Value);
        Equal(1003, mapper.Map<Same>(value)!.Value);
        Equal(1003, mapper.Map<BoxTarget>(new BoxSource { Value = value })!.Value!.Value);
        Equal(1003, mapper.Map<List<Same>>(list)![0].Value);
        Equal(false, ReferenceEquals(value, mapper.Map<ExplicitTarget>(new ExplicitSource { Value = value })!.Value)); Equal(1003, mapper.Map<ExplicitTarget>(new ExplicitSource { Value = value })!.Value!.Value);
        TypeConverterRegistry.Register(typeof(Same), typeof(Same), new Func<Same, Same>(source => new() { Value = source.Value + 2000 }));
        Equal(2003, mapper.Map<Same>(value)!.Value);
        Equal(2003, mapper.Map<BoxTarget>(new BoxSource { Value = value })!.Value!.Value);
        // Configuration is immutable during concurrent use; results never become cached source values.
        Parallel.For(0, 256, index =>
        {
            if (mapper.Map<Same>(new Same { Value = index })!.Value != index + 2000 ||
                mapper.Map<Renamed>(new Renamed { Other = index })!.Value != index)
                throw new Exception("Concurrent same-type member mapping failed.");
        });
        assertions += 512;
        Console.WriteLine($"Same-type member rules regression: {assertions}/{assertions} assertions passed.");
    }
    public interface ISame { }
    public class Same : ISame
    {
        public static int Reads;
        public int Value { get; set; }
        public int Other { get; set; }
        public string Ignored { get; set; } = "initial";
        public Same? Next { get; set; }
        public int Resolve() { Interlocked.Increment(ref Reads); return Value + 100; }
    }
    public sealed class DerivedSame : Same { }
    public sealed class PairSource { public Same? First { get; set; } public Same? Second { get; set; } }
    public sealed class PairTarget { public Same? First { get; set; } public Same? Second { get; set; } }
    public sealed class BoxSource { public Same? Value { get; set; } }
    public sealed class BoxTarget { public Same? Value { get; set; } }
    public sealed class ObjectSource { public object? Value { get; set; } }
    public sealed class ObjectTarget { public Same? Value { get; set; } }
    public sealed class InterfaceSource { public ISame? Value { get; set; } }
    public sealed class InterfaceTarget { public Same? Value { get; set; } }
    public readonly struct ImmutableHolder(Same value) { public Same Value { get; } = value; }
    public sealed class BoxListSource { public List<Same>? Value { get; set; } }
    public sealed class BoxListTarget { public List<Same>? Value { get; set; } }
    public sealed class ExplicitSource { public Same? Value { get; set; } }
    public sealed class ExplicitTarget { public Same? Value { get; set; } }
    public readonly struct ImmutableExplicit(Same value) { public Same Value { get; } = value; }
    public sealed class Renamed { public int Value { get; set; } public int Other { get; set; } }
    public sealed class Ignored { public int Value { get; set; } = 70; public int Other { get; set; } }
    public sealed class Untouched { public Same? Value { get; set; } }
    public sealed class ExplicitObjectSource { public object? Value { get; set; } }
    public sealed class ExplicitObjectTarget { public Same? Value { get; set; } }
    public readonly struct ImmutableObjectExplicit(Same value) { public Same Value { get; } = value; }
    public sealed class SplitSource { public Same? First { get; set; } public DerivedSame? Second { get; set; } }
    public interface IExplicitValue { int Value { get; } }
    public sealed class ExplicitValue : IExplicitValue { public int Value { get; set; } }
    public sealed class ExplicitInterfaceSource { public IExplicitValue? Value { get; set; } }
    public sealed class ExplicitInterfaceTarget { public ExplicitValue? Value { get; set; } }
    public sealed class AmbiguousEnumerable : IEnumerable<int>, IEnumerable<string>
    {
        IEnumerator<int> IEnumerable<int>.GetEnumerator() => Enumerable.Empty<int>().GetEnumerator();
        IEnumerator<string> IEnumerable<string>.GetEnumerator() => Enumerable.Empty<string>().GetEnumerator();
        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() => ((IEnumerable<int>)this).GetEnumerator();
    }
    public abstract class AbstractInput { public int Value { get; set; } }
    public sealed class AbstractConcrete : AbstractInput { public static implicit operator AbstractOutput(AbstractConcrete value) => new() { Value = 999 }; }
    public sealed class AbstractOutput { public int Value { get; set; } }
    public sealed class AbstractHolderInput { public AbstractInput? Value { get; set; } }
    public sealed class AbstractHolderOutput { public AbstractOutput? Value { get; set; } }
    public class CycleBase { public CycleBase? Next { get; set; } }
    public sealed class CycleDerived : CycleBase { }
    public sealed class CycleTarget { public CycleTarget? Next { get; set; } }
    public sealed class MarkerSource { public int Value { get; set; } }
    public sealed class MarkerTarget { public int Value { get; set; } }
    public sealed class MarkerPairSource { public object? First { get; set; } public MarkerSource? Second { get; set; } }
    public sealed class MarkerPairTarget { public MarkerTarget? First { get; set; } public MarkerTarget? Second { get; set; } }
    public sealed class Rules : MapperProfile
    {
        public Rules()
        {
            CreateMap<object, MarkerTarget>().ForMember(target => target.Value, source => 100);
            CreateMap<DerivedSame, Same>().ForMember(target => target.Value, source => source.Value + 300);
            CreateMap<IExplicitValue, ExplicitValue>().ForMember(target => target.Value, source => source.Value + 200);
            CreateMap<AbstractInput, AbstractOutput>();
            CreateMap<ExplicitObjectSource, ExplicitObjectTarget>().ForMember<object?>(target => target.Value, source => source.Value);
            CreateMap<ExplicitObjectSource, ImmutableObjectExplicit>().ForMember<object?>(target => target.Value, source => source.Value);
            CreateMap<Same, Same>().ForMember(target => target.Value, source => source.Resolve()).Ignore(target => target.Ignored);
            CreateMap<Renamed, Renamed>().Rename(target => target.Value, nameof(Renamed.Other));
            CreateMap<Ignored, Ignored>().Ignore(target => target.Value);
            CreateMap<Untouched, Untouched>();
            CreateMap<ExplicitSource, ExplicitTarget>().ForMember(target => target.Value, source => source.Value);
            CreateMap<ExplicitSource, ImmutableExplicit>().ForMember(target => target.Value, source => source.Value!);
        }
    }
}
