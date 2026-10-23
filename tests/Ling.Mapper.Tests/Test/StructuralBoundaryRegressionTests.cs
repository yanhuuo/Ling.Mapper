using System.Collections;
using Ling.Mapper;
using Ling.Mapper.Configuration;
using Ling.Mapper.Extensions;
using Ling.Mapper.Mapper;
using Ling.Mapper.Models;
using Ling.Mapper.Provider;

namespace TestConsole.Test;

/// <summary>Finite deterministic structural/API boundary probes, not an exhaustive type universe.</summary>
public static class StructuralBoundaryRegressionTests
{
    public static void Run()
    {
        var failures = new List<Exception>(); var count = 0;
        void Case(string name, Action action) { count++; try { action(); Console.WriteLine($"PASS structural boundary: {name}"); } catch (Exception e) { failures.Add(new Exception(name, e)); Console.WriteLine($"FAIL structural boundary: {name}: {e.Message}"); } }
        var m = new MapperConfiguration().CreateMapper();
        var strict = new MapperConfiguration { StrictMode = true }.CreateMapper();
        foreach (var invalid in new[] { typeof(List<>), typeof(int).MakeByRefType(), typeof(int).MakePointerType(), typeof(void) })
        {
            Case($"invalid destination Type {invalid} rejects before null short circuit", () => Throws<ArgumentException>(() => m.Map(null, typeof(object), invalid)));
            Case($"invalid declared source Type {invalid} rejects before null short circuit", () => Throws<ArgumentException>(() => m.Map(null, invalid, typeof(Target))));
        }
        Case("private constructor non-strict returns null", () => Equal(null, m.Map<PrivateConstructor>(new Source { Value = 4 })));
        Case("private constructor strict has root failure", () => Path(() => strict.Map<PrivateConstructor>(new Source { Value = 4 }), "$"));
        Case("abstract destination non-strict returns null", () => Equal(null, m.Map<AbstractTarget>(new Source { Value = 4 })));
        Case("abstract destination strict has root failure", () => Path(() => strict.Map<AbstractTarget>(new Source { Value = 4 }), "$"));
        Case("interface destination non-strict returns null", () => Equal(null, m.Map<ITarget>(new Source { Value = 4 })));
        Case("interface destination strict has root failure", () => Path(() => strict.Map<ITarget>(new Source { Value = 4 }), "$"));
        Case("nested no-constructor non-strict sets null", () => Equal(null, m.Map<NoCtorRoot>(new SourceRoot { Child = new() { Value = 4 } })!.Child));
        Case("nested no-constructor strict has member failure", () => Path(() => strict.Map<NoCtorRoot>(new SourceRoot { Child = new() { Value = 4 } }), "$.Child"));
        Case("null nested no-constructor stays null even in strict mode", () => Equal(null, strict.Map<NoCtorRoot>(new SourceRoot())!.Child));
        Case("Adapt non-strict private constructor preserves mapper null", () => WithProvider(m, () => Equal(null, new Source().Adapt<PrivateConstructor>())));
        Case("Adapt strict private constructor preserves MappingException", () => WithProvider(strict, () => Path(() => new Source().Adapt<PrivateConstructor>(), "$")));
        Case("Adapt strict interface preserves MappingException", () => WithProvider(strict, () => Path(() => new Source().Adapt<ITarget>(), "$")));

        Case("Rename null reference intermediate maps numeric default", () => { var map = With(new ReferenceRenameProfile()); Equal(0, map.Map<Target>(new SourceRoot())!.Value); });
        Case("Rename reference intermediate maps value", () => { var map = With(new ReferenceRenameProfile()); Equal(19, map.Map<Target>(new SourceRoot { Child = new() { Value = 19 } })!.Value); });
        Case("Rename nullable struct present maps underlying property", () => Equal(23, With(new NullableRenameProfile()).Map<Target>(new NullableRoot { Child = new ValueChild { Number = 23 } })!.Value));
        Case("Rename nullable struct absent maps default", () => Equal(0, With(new NullableRenameProfile()).Map<Target>(new NullableRoot())!.Value));
        Case("Rename four-level path reads each getter exactly once", () => {
            var end = new Level4(); var third = new Level3(end); var second = new Level2(third); var first = new Level1(second);
            Equal(29, With(new FourLevelProfile()).Map<Target>(first)!.Value); Equal(1, first.Reads); Equal(1, second.Reads); Equal(1, third.Reads); Equal(1, end.Reads);
        });
        Case("derived new property wins over hidden base property", () => { var source = new DerivedSource { Value = 19 }; ((BaseSource)source).Value = 7; Equal(19, m.Map<Target>(source)!.Value); });
        Case("strict names distinguish normalization candidates", () => { var d = m.Map<ConflictTarget>(new ConflictSource { UserId = 7, user_id = 19 }, AdaptOptions.Strict)!; Equal(7, d.UserId); Equal(19, d.user_id); });

        Case("HashSet constructor deduplicates after conversion", () => { var d = m.Map<HashSet<int>>(new[] { "1", "01", "2" })!; Equal(2, d.Count); Equal(true, d.SetEquals(new[] { 1, 2 })); });
        Case("Queue constructor preserves enumeration order", () => { var d = m.Map<Queue<long>>(new[] { 3, 1, 3 })!; Equal(3, d.Count); Equal(3L, d.Dequeue()); Equal(1L, d.Dequeue()); Equal(3L, d.Dequeue()); });
        Case("single-use enumerable is consumed exactly once", () => { var source = new SingleUse(); Sequence(new long[] { 3, 1, 4 }, m.Map<long[]>(source)!); Equal(1, source.Enumerations); });
        Case("non-generic enumerable maps runtime item types", () => { var source = new ArrayList { "3", 1, 4L }; Sequence(new long[] { 3, 1, 4 }, m.Map<long[]>(source)!); });
        Case("non-generic enumerable null element uses default", () => Sequence(new long[] { 3, 0, 4 }, m.Map<long[]>(new ArrayList { "3", null, 4 })!));
        Case("non-zero lower-bound array maps enumeration order", () => {
            var source = Array.CreateInstance(typeof(int), new[] { 3 }, new[] { 5 }); source.SetValue(3, 5); source.SetValue(1, 6); source.SetValue(4, 7);
            Sequence(new long[] { 3, 1, 4 }, m.Map<long[]>(source)!);
        });
        Case("same multidimensional array preserves identity", () => { var source = new int[,] { { 1, 2 }, { 3, 4 } }; Same(source, m.Map<int[,]>(source)); Equal(4, m.Map<int[,]>(source)![1, 1]); });
        Case("cross-type multidimensional destination rejects", () => Path(() => m.Map<long[,]>(new int[,] { { 1, 2 }, { 3, 4 } }), "$"));
        Case("jagged conversion failure reports full item indices", () => Path(() => m.Map<int[][]>(new[] { new[] { "1" }, new[] { "2", "bad" } }), "$[1][1]"));
        Case("distinct equal nodes must not collapse reference identity", () => {
            var a = new EqualSource { Value = 9 }; var b = new EqualSource { Value = 9 }; Equal(true, a.Equals(b));
            var d = m.Map<Target[]>(new[] { a, b, a })!; Equal(3, d.Length); Equal(9, d[0].Value); Equal(9, d[1].Value); NotSame(d[0], d[1]); Same(d[0], d[2]);
        });
        Case("Rename explicitly missing source path rejects during plan compilation", () => Throws<NotSupportedException>(() => With(new MissingRenameProfile())));
        Case("Rename private source getter is never invoked", () => {
            var child = new PrivateGetterChild(); Throws<NotSupportedException>(() => With(new PrivateGetterRenameProfile())); Equal(0, child.Reads);
        });
        Case("ambiguous normalized source properties reject deterministically", () => Path(() => m.Map<SingleConflictTarget>(new ConflictSource { UserId = 7, user_id = 19 }), "$"));
        Case("enumerable with conflicting generic element types rejects", () => Path(() => m.Map<long[]>(new AmbiguousEnumerable()), "$"));
        Case("ICollection count larger than enumeration rejects without padded defaults", () => Path(() => m.Map<long[]>(new Miscounted(4)), "$"));
        Case("ICollection count smaller than enumeration rejects", () => Path(() => m.Map<long[]>(new Miscounted(1)), "$"));
        Case("honest ICollection count maps every enumerated value", () => Sequence(new long[] { 3, 1 }, m.Map<long[]>(new Miscounted(2))!));

        Case("Rename explicit Nullable.Value path present reads leaf", () => Equal(31, With(new ExplicitNullableRenameProfile()).Map<Target>(new NullableRoot { Child = new ValueChild { Number = 31 } })!.Value));
        Case("Rename explicit Nullable.Value path absent is null-safe", () => Equal(0, With(new ExplicitNullableRenameProfile()).Map<Target>(new NullableRoot())!.Value));
        Case("negative ICollection count rejects explicitly", () => Path(() => m.Map<long[]>(new Miscounted(-1)), "$"));

        Case("Adapt non-strict abstract destination preserves mapper null", () => WithProvider(m, () => Equal(null, new Source().Adapt<AbstractTarget>())));
        Case("Adapt non-strict interface destination preserves mapper null", () => WithProvider(m, () => Equal(null, new Source().Adapt<ITarget>())));
        Case("Adapt non-strict nested no-constructor preserves null member", () => WithProvider(m, () => Equal(null, new SourceRoot { Child = new() { Value = 4 } }.Adapt<NoCtorRoot>().Child)));

        Case("duplicate explicit Profile type pair uses first registration", () => {
            var config = new MapperConfiguration(); config.AddProfile(new DuplicateProfile(10)); config.AddProfile(new DuplicateProfile(20));
            Equal(13, config.CreateMapper().Map<Target>(new Source { Value = 3 })!.Value);
        });
        Case("duplicate explicit Profile reversed order changes winner", () => {
            var config = new MapperConfiguration(); config.AddProfile(new DuplicateProfile(20)); config.AddProfile(new DuplicateProfile(10));
            Equal(23, config.CreateMapper().Map<Target>(new Source { Value = 3 })!.Value);
        });
        Case("explicit reverse registered after automatic reverse takes priority", () => {
            var config = new MapperConfiguration(); config.AddProfile(new AutomaticReverseProfile()); config.AddProfile(new ExplicitReverseProfile()); var map = config.CreateMapper();
            Equal(3, map.Map<Target>(new Source { Value = 3 })!.Value); Equal(103, map.Map<Source>(new Target { Value = 3 })!.Value);
        });
        Case("explicit reverse registered before automatic reverse takes priority", () => {
            var config = new MapperConfiguration(); config.AddProfile(new ExplicitReverseProfile()); config.AddProfile(new AutomaticReverseProfile()); var map = config.CreateMapper();
            Equal(3, map.Map<Target>(new Source { Value = 3 })!.Value); Equal(103, map.Map<Source>(new Target { Value = 3 })!.Value);
        });

        Case("init-only class properties are populated on a fresh destination", () => {
            var source = new Source { Value = 37 }; var first = m.Map<InitTarget>(source)!; var second = m.Map<InitTarget>(source)!;
            Equal(37, first.Value); Equal(37, second.Value); NotSame(first, second); Equal(37, source.Value);
        });
        Case("ordinary fields do not participate in property mapping", () => {
            Equal(0, m.Map<Target>(new FieldSource { Value = 37 })!.Value);
            Equal(61, m.Map<FieldTarget>(new Source { Value = 37 })!.Value);
        });
        Case("IList destination converts elements and preserves order", () => {
            var source = new[] { 3, -1, 3 }; var d = m.Map<IList<long>>(source)!;
            Equal(3, d.Count); Sequence(new long[] { 3, -1, 3 }, d); d[0] = 9; Equal(3, source[0]);
        });
        Case("ICollection destination converts elements and preserves duplicates", () => {
            var d = m.Map<ICollection<long>>(new[] { 3, -1, 3 })!; Equal(3, d.Count); Sequence(new long[] { 3, -1, 3 }, d);
        });
        Case("IReadOnlyCollection destination converts elements and preserves order", () => {
            var d = m.Map<IReadOnlyCollection<long>>(new[] { 3, -1, 3 })!; Equal(3, d.Count); Sequence(new long[] { 3, -1, 3 }, d);
        });
#if NET8_0_OR_GREATER
        Case("required class member with matching source is populated", () => {
            var d = m.Map<RequiredTarget>(new { Name = "present" })!; Equal("present", d.Name);
        });
        Case("required class member missing source has no runtime required validation", () => {
            var d = m.Map<RequiredTarget>(new Source { Value = 37 })!; Equal(null, d.Name);
        });
#else
        Console.WriteLine("NOT EXECUTED structural boundary: required-member class cases require NET8_0_OR_GREATER; no net6 compiler-attribute polyfill is used.");
#endif

        Console.WriteLine($"Structural boundary regression: {count - failures.Count}/{count} passed.");
        if (failures.Count != 0) throw new AggregateException("Structural boundary regressions failed", failures);
    }
    private static IMapper With(MapperProfile profile) { var c = new MapperConfiguration(); c.AddProfile(profile); return c.CreateMapper(); }
    private static void WithProvider(IMapper mapper, Action action) { var old = MapperProvider.Current; try { MapperProvider.SetCurrent(mapper); action(); } finally { MapperProvider.SetCurrent(old); } }
    private static T Throws<T>(Action action) where T : Exception { try { action(); } catch (T e) { return e; } throw new Exception($"Expected {typeof(T).Name}."); }
    private static void Path(Action action, string path) => Equal(path, Throws<MappingException>(action).Path);
    private static void Equal(object? expected, object? actual) { if (!Equals(expected, actual)) throw new Exception($"Expected {expected ?? "null"}; actual {actual ?? "null"}."); }
    private static void Same(object? a, object? b) { if (!ReferenceEquals(a, b)) throw new Exception("Reference identity was lost."); }
    private static void NotSame(object? a, object? b) { if (ReferenceEquals(a, b)) throw new Exception("Distinct source objects collapsed into one target."); }
    private static void Sequence<T>(IEnumerable<T> expected, IEnumerable<T> actual) { if (!expected.SequenceEqual(actual)) throw new Exception("Sequence values or order differ."); }
    public sealed class InitTarget { public int Value { get; init; } }
    public sealed class FieldSource { public int Value; }
    public sealed class FieldTarget { public int Value = 61; }
#if NET8_0_OR_GREATER
    public sealed class RequiredTarget { public required string Name { get; init; } }
#endif
    public sealed class DuplicateProfile : MapperProfile { public DuplicateProfile(int offset) => CreateMap<Source, Target>().ForMember(d => d.Value, s => s.Value + offset); }
    public sealed class AutomaticReverseProfile : MapperProfile { public AutomaticReverseProfile() => CreateMap<Source, Target>().ForMember(d => d.Value, s => s.Value).ReverseMap(); }
    public sealed class ExplicitReverseProfile : MapperProfile { public ExplicitReverseProfile() => CreateMap<Target, Source>().ForMember(d => d.Value, s => s.Value + 100); }
    public sealed class MissingRenameProfile : MapperProfile { public MissingRenameProfile() => CreateMap<SourceRoot, Target>().Rename(d => d.Value, "Child.Missing"); }
    public sealed class PrivateGetterChild { public int Reads; public int Value { private get { Reads++; throw new InvalidOperationException("private getter must not run"); } set { } } }
    public sealed class PrivateGetterRoot { public PrivateGetterChild? Child { get; set; } }
    public sealed class PrivateGetterRenameProfile : MapperProfile { public PrivateGetterRenameProfile() => CreateMap<PrivateGetterRoot, Target>().Rename(d => d.Value, "Child.Value"); }
    public sealed class SingleConflictTarget { public int UserId { get; set; } }
    public sealed class AmbiguousEnumerable : IEnumerable<int>, IEnumerable<string>
    {
        IEnumerator<int> IEnumerable<int>.GetEnumerator() => ((IEnumerable<int>)new[] { 1, 2 }).GetEnumerator();
        IEnumerator<string> IEnumerable<string>.GetEnumerator() => ((IEnumerable<string>)new[] { "7", "8" }).GetEnumerator();
        IEnumerator IEnumerable.GetEnumerator() => ((IEnumerable<int>)this).GetEnumerator();
    }
    public sealed class Miscounted(int count) : ICollection
    {
        public int Count => count;
        public bool IsSynchronized => false;
        public object SyncRoot => this;
        public void CopyTo(Array array, int index) { array.SetValue(3, index); array.SetValue(1, index + 1); }
        public IEnumerator GetEnumerator() => new[] { 3, 1 }.GetEnumerator();
    }
    public class Source { public int Value { get; set; } }
    public class Target { public int Value { get; set; } }
    public sealed class PrivateConstructor { private PrivateConstructor() { } public int Value { get; set; } }
    public abstract class AbstractTarget { public int Value { get; set; } }
    public interface ITarget { int Value { get; set; } }
    public sealed class NoCtorTarget { public NoCtorTarget(int value) => Value = value; public int Value { get; set; } }
    public sealed class NoCtorRoot { public NoCtorTarget? Child { get; set; } }
    public sealed class SourceRoot { public Source? Child { get; set; } }
    public struct ValueChild { public int Number { get; set; } }
    public sealed class NullableRoot { public ValueChild? Child { get; set; } }
    public sealed class ReferenceRenameProfile : MapperProfile { public ReferenceRenameProfile() => CreateMap<SourceRoot, Target>().Rename(d => d.Value, "Child.Value"); }
    public sealed class ExplicitNullableRenameProfile : MapperProfile { public ExplicitNullableRenameProfile() => CreateMap<NullableRoot, Target>().Rename(d => d.Value, "Child.Value.Number"); }
    public sealed class NullableRenameProfile : MapperProfile { public NullableRenameProfile() => CreateMap<NullableRoot, Target>().Rename(d => d.Value, "Child.Number"); }
    public sealed class Level1(Level2 child) { public int Reads; public Level2 A { get { Reads++; return child; } } }
    public sealed class Level2(Level3 child) { public int Reads; public Level3 B { get { Reads++; return child; } } }
    public sealed class Level3(Level4 child) { public int Reads; public Level4 C { get { Reads++; return child; } } }
    public sealed class Level4 { public int Reads; public int Value { get { Reads++; return 29; } } }
    public sealed class FourLevelProfile : MapperProfile { public FourLevelProfile() => CreateMap<Level1, Target>().Rename(d => d.Value, "A.B.C.Value"); }
    public class BaseSource { public int Value { get; set; } }
    public sealed class DerivedSource : BaseSource { public new int Value { get; set; } }
    public sealed class ConflictSource { public int UserId { get; set; } public int user_id { get; set; } }
    public sealed class ConflictTarget { public int UserId { get; set; } public int user_id { get; set; } }
    public sealed class SingleUse : IEnumerable<int>
    {
        public int Enumerations;
        public IEnumerator<int> GetEnumerator() { if (++Enumerations != 1) throw new InvalidOperationException("Source was enumerated more than once."); return ((IEnumerable<int>)new[] { 3, 1, 4 }).GetEnumerator(); }
        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }
    public sealed class EqualSource { public int Value { get; set; } public override bool Equals(object? obj) => obj is EqualSource x && x.Value == Value; public override int GetHashCode() => Value; }
}
