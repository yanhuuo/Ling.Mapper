using Ling.Mapper;
using Ling.Mapper.Configuration;
using Ling.Mapper.Extensions;
using Ling.Mapper.Mapper;
using Ling.Mapper.Models;
using Ling.Mapper.Provider;

namespace TestConsole.Test;

/// <summary>Independent output and object-graph assertions for structural mapping features.</summary>
public static class AdvancedMappingRegressionTests
{
    public static void Run()
    {
        var failures = new List<Exception>();
        var count = 0;
        void Case(string name, Action action)
        {
            count++;
            try { action(); Console.WriteLine($"PASS advanced: {name}"); }
            catch (Exception e) { failures.Add(new Exception(name, e)); Console.WriteLine($"FAIL advanced: {name}: {e.Message}"); }
        }
        var m = new MapperConfiguration().CreateMapper();
        Case("dictionary converts keys and values", () => {
            var d = m.Map<Dictionary<int, decimal>>(new Dictionary<string, string> { ["12"] = "3.5", ["-7"] = "0" })!;
            Equal(2, d.Count); Equal(3.5m, d[12]); Equal(0m, d[-7]);
        });
        Case("dictionary runtime non-generic API", () => {
            var d = (Dictionary<long, long>)m.Map(new Dictionary<int, int> { [2] = 8 }, typeof(Dictionary<int, int>), typeof(Dictionary<long, long>))!;
            Equal(1, d.Count); Equal(8L, d[2]);
        });
        Case("dictionary interface destination", () => {
            var d = m.Map<IDictionary<long, long>>(new Dictionary<int, int> { [2] = 8 })!; Equal(1, d.Count); Equal(8L, d[2]);
        });
        Case("dictionary readonly interface destination", () => {
            var d = m.Map<IReadOnlyDictionary<long, long>>(new Dictionary<int, int> { [2] = 8 })!; Equal(1, d.Count); Equal(8L, d[2]);
        });
        Case("dictionary readonly source", () => {
            var s = new System.Collections.ObjectModel.ReadOnlyDictionary<int, int>(new Dictionary<int, int> { [2] = 8 });
            var d = m.Map<Dictionary<long, long>>(s)!; Equal(1, d.Count); Equal(8L, d[2]);
        });
        Case("dictionary null reference stays null", () => Equal(null, m.Map<Dictionary<long, long>>(null)));
        Case("dictionary empty input retains empty shape", () => { var d = m.Map<Dictionary<long, long>>(new Dictionary<int, int>())!; Equal(0, d.Count); Equal(typeof(Dictionary<long, long>), d.GetType()); });
        Case("dictionary nullable values present and absent", () => { var d = m.Map<Dictionary<long, long?>>(new Dictionary<int, int?> { [1] = 3, [2] = null })!; Equal(3L, d[1]); Equal(null, d[2]); });
        Case("dictionary nested member conversion", () => { var d = m.Map<DictHolder<long, decimal>>(new DictHolder<string, string> { Values = new() { ["1"] = "2.75" } })!; Equal(2.75m, d.Values![1]); });
        Case("dictionary inside collection conversion", () => { var d = m.Map<List<Dictionary<long, long>>>(new[] { new Dictionary<int, int> { [1] = 9 }, new Dictionary<int, int>() })!; Equal(2, d.Count); Equal(9L, d[0][1]); Equal(0, d[1].Count); });
        Case("dictionary source is unchanged", () => { var s = new Dictionary<int, string> { [1] = "9" }; var d = m.Map<Dictionary<long, int>>(s)!; d[1] = 10; Equal("9", s[1]); });
        Case("dictionary key collision is not silent overwrite", () => Failure(() => m.Map<Dictionary<int, int>>(new Dictionary<string, string> { ["1"] = "9", ["01"] = "10" })));
        Case("dictionary invalid key has key member path", () => { var e = Failure(() => m.Map<Dictionary<int, int>>(new Dictionary<string, string> { ["bad"] = "9" })); Equal("$[0].Key", e.Path); });
        Case("dictionary invalid value has value member path", () => { var e = Failure(() => m.Map<Dictionary<int, int>>(new Dictionary<string, string> { ["1"] = "bad" })); Equal("$[0].Value", e.Path); });
        Case("dictionary nested failure retains full path", () => { var e = Failure(() => m.Map<DictHolder<int, int>>(new DictHolder<string, string> { Values = new() { ["1"] = "bad" } })); Equal("$.Values[0].Value", e.Path); });
        Case("dictionary shared mapped values retain aliases", () => {
            var v = new NodeSource { Value = 7 }; var d = m.Map<Dictionary<long, NodeTarget>>(new Dictionary<int, NodeSource> { [1] = v, [2] = v })!;
            Equal(7L, d[1].Value); Same(d[1], d[2]); NotSame(v, d[1]);
        });
        Case("dictionary cyclic value resolves to mapped graph", () => {
            var v = new NodeSource { Value = 7 }; v.Next = v;
            var d = m.Map<Dictionary<long, NodeTarget>>(new Dictionary<int, NodeSource> { [1] = v })!;
            Equal(7L, d[1].Value); Same(d[1], d[1].Next); NotSame(v, d[1]);
        });
        Case("dictionary shared member retains mapped dictionary alias", () => {
            var dict = new Dictionary<int, int> { [1] = 9 }; var d = m.Map<TwoDicts<long>>(new TwoDicts<int> { First = dict, Second = dict })!;
            Same(d.First, d.Second); Equal(9L, d.First![1]);
        });
        Case("dictionary explicit converter still takes priority", () => {
            TypeConverterRegistry.Register(typeof(Dictionary<short, short>), typeof(Dictionary<long, long>), new Func<Dictionary<short, short>, Dictionary<long, long>>(_ => new() { [99] = 42 }));
            var d = m.Map<Dictionary<long, long>>(new Dictionary<short, short> { [1] = 2 })!; Equal(1, d.Count); Equal(42L, d[99]);
        });

        Case("immutable struct constructor binds properties and converts", () => { var d = m.Map<ImmutableValue>(new MutableValue { Id = "12", Name = "name" }); Equal(12L, d.Id); Equal("name", d.Name); });
        Case("immutable struct runtime entry point", () => { var d = (ImmutableValue)m.Map(new MutableValue { Id = "12", Name = "runtime" }, typeof(MutableValue), typeof(ImmutableValue))!; Equal(12L, d.Id); Equal("runtime", d.Name); });
        Case("immutable nullable destination holds constructed value", () => { var d = m.Map<ImmutableValue?>(new MutableValue { Id = "12", Name = "nullable" }); Equal(true, d.HasValue); Equal(12L, d!.Value.Id); Equal("nullable", d.Value.Name); });
        Case("immutable nullable absent remains absent", () => Equal(null, m.Map<ImmutableValue?>(null)));
        Case("immutable nested member is populated", () => { var d = m.Map<Box<ImmutableValue>>(new Box<MutableValue> { Value = new() { Id = "12", Name = "nested" } })!; Equal(12L, d.Value.Id); Equal("nested", d.Value.Name); });
        Case("immutable collection preserves each constructor argument", () => { var d = m.Map<ImmutableValue[]>(new[] { new MutableValue { Id = "12", Name = "a" }, new MutableValue { Id = "-4", Name = "b" } })!; Equal(2, d.Length); Equal(12L, d[0].Id); Equal("a", d[0].Name); Equal(-4L, d[1].Id); Equal("b", d[1].Name); });
        Case("readonly record struct constructor mapping", () => { var d = m.Map<RecordValue>(new MutableValue { Id = "12", Name = "record" }); Equal(new RecordValue(12, "record"), d); });
        Case("keyvaluepair readonly structure preserves both fields", () => { var d = m.Map<KeyValuePair<long, decimal>>(new KeyValuePair<int, string>(4, "2.5")); Equal(4L, d.Key); Equal(2.5m, d.Value); });
        Case("immutable conversion failure reports argument member path", () => { var e = Failure(() => m.Map<ImmutableValue>(new MutableValue { Id = "bad", Name = "name" })); Equal("$.Id", e.Path); });
        Case("immutable unmatched constructor rejects instead of zero fields", () => Failure(() => m.Map<UnmatchableValue>(new MutableValue { Id = "12", Name = "name" })));
        Case("immutable equally viable constructors reject ambiguity", () => Failure(() => m.Map<AmbiguousValue>(new MutableValue { Id = "12", Name = "name" })));
        Case("immutable throwing constructor is safely contextualized", () => { var e = Failure(() => m.Map<ThrowingValue>(new MutableValue { Id = "12" })); if (e.ToString().Contains("PRIVATE_CONSTRUCTOR")) throw new Exception("Unsafe constructor exception leaked."); });

        Case("nested target class chain is created and bound", () => { var x = With(new NestedClassProfile()).Map<ClassRoot>(new Flat { Number = 17, Text = "kept" })!; Equal(17, x.Child!.Number); Equal("kept", x.Text); });
        Case("nested target struct chain is written back", () => { var x = With(new NestedStructProfile()).Map<StructRoot>(new Flat { Number = 17 })!; Equal(17, x.Child.Number); });
        Case("nested target class to struct to class chain", () => { var x = With(new DeepProfile()).Map<DeepRoot>(new Flat { Number = 17 })!; Equal(17, x.Middle.Child!.Number); });
        Case("nested Rename converts source scalar type", () => { var x = With(new NestedRenameProfile()).Map<ClassRoot>(new FlatText { Number = "23" })!; Equal(23, x.Child!.Number); });
        Case("nested target runtime entry point", () => { var map = With(new NestedClassProfile()); var x = (ClassRoot)map.Map(new Flat { Number = 17 }, typeof(Flat), typeof(ClassRoot))!; Equal(17, x.Child!.Number); });
        Case("nested target collection entries do not share initialized chains", () => { var x = With(new NestedClassProfile()).Map<List<ClassRoot>>(new[] { new Flat { Number = 17 }, new Flat { Number = 18 } })!; Equal(17, x[0].Child!.Number); Equal(18, x[1].Child!.Number); NotSame(x[0].Child, x[1].Child); });
        Case("nested target conversion failure retains full destination path", () => { var e = Failure(() => With(new NestedRenameProfile()).Map<ClassRoot>(new FlatText { Number = "bad" })); Equal("$.Child.Number", e.Path); });
        Case("nested Ignore preserves other nested mapped properties", () => { var x = With(new NestedIgnoreProfile()).Map<ClassRoot>(new ClassSourceRoot { Child = new ClassSourceChild { Number = 17, Other = "other" } })!; Equal(0, x.Child!.Number); Equal("other", x.Child.Other); });

        Case("ReverseMap registers reverse rename", () => { var map = With(new ReverseRenameProfile()); Equal("forward", map.Map<Renamed>(new Named { Name = "forward" })!.Title); Equal("reverse", map.Map<Named>(new Renamed { Title = "reverse" })!.Name); });
        Case("ReverseMap member path reverses flattening", () => { var map = With(new ReversePathProfile()); Equal(17, map.Map<Flat>(new ClassRoot { Child = new() { Number = 17 } })!.Number); Equal(18, map.Map<ClassRoot>(new Flat { Number = 18 })!.Child!.Number); });
        Case("ReverseMap supports subsequent reverse customization", () => { var map = With(new ReverseCustomProfile()); Equal("reverse!", map.Map<Named>(new Renamed { Title = "reverse" })!.Name); Equal("forward", map.Map<Renamed>(new Named { Name = "forward" })!.Title); });
        Case("ReverseMap reverse collection validates every element", () => { var d = With(new ReverseRenameProfile()).Map<Named[]>(new[] { new Renamed { Title = "a" }, new Renamed { Title = "b" } })!; Equal(2, d.Length); Equal("a", d[0].Name); Equal("b", d[1].Name); });
        Case("ReverseMap non-invertible arithmetic rejects explicitly", () => Reject(() => With(new IrreversibleProfile())));

        Case("dictionary null converted key rejects with key path", () => {
            TypeConverterRegistry.Register(typeof(NullKey), typeof(string), new Func<NullKey, string>(_ => null!));
            var e = Failure(() => m.Map<Dictionary<string, int>>(new Dictionary<NullKey, int> { [new NullKey(1)] = 2 })); Equal("$[0].Key", e.Path);
        });
        Case("dictionary private keys never enter exception diagnostics", () => {
            var map = new MapperConfiguration { IncludeSourceValueInExceptions = true }.CreateMapper();
            var e = Failure(() => map.Map<Dictionary<string, int>>(new Dictionary<string, string> { ["PRIVATE_KEY_871"] = "bad" }));
            Equal("$[0].Value", e.Path); if (e.ToString().Contains("PRIVATE_KEY_871")) throw new Exception("Dictionary key exposed in diagnostics.");
        });
        Case("dictionary graph back edge returns mapped dictionary", () => {
            var s = new Dictionary<int, DictionaryNodeSource>(); var node = new DictionaryNodeSource { Owner = s }; s[1] = node;
            var d = m.Map<Dictionary<long, DictionaryNodeTarget>>(s)!; Same(d, d[1].Owner);
        });
        Case("immutable most complete constructor wins", () => { var d = m.Map<PreferredValue>(new MutableValue { Id = "12", Name = "full" }); Equal(12L, d.Id); Equal("full", d.Name); });
        Case("nested getter-only initialized reference is populated", () => { var d = With(new GetterOnlyProfile()).Map<GetterOnlyRoot>(new Flat { Number = 17 })!; Equal(17, d.Child.Number); Equal("initializer", d.Child.Other); });
        Case("nested getter-only null reference fails explicitly", () => Failure(() => With(new NullGetterOnlyProfile()).Map<NullGetterOnlyRoot>(new Flat { Number = 17 })));
        Case("nested getter-only struct fails rather than mutates a copy", () => Reject(() => With(new GetterOnlyStructProfile()).Map<GetterOnlyStructRoot>(new Flat { Number = 17 })));
        Case("nested setter error carries full path and redacts message", () => { var e = Failure(() => With(new ThrowingNestedProfile()).Map<ThrowingRoot>(new Flat { Number = 17 })); Equal("$.Child.Number", e.Path); if (e.ToString().Contains("PRIVATE_SETTER")) throw new Exception("Setter secret leaked."); });
        Case("nested Ignore overrides explicit ForMember only at that path", () => { var d = With(new NestedPrecedenceProfile()).Map<ClassRoot>(new Flat { Number = 17, Text = "other" })!; Equal(0, d.Child!.Number); Equal("other", d.Child.Other); });

        Case("conventions case-insensitive mapping applies without explicit default", () => { var c = new MapperConfiguration(); c.ConfigureConventions(o => { o.CaseInsensitiveNameMatch = true; o.IgnoreSpecialCharacters = false; }); var map = c.CreateMapper(); Equal(13, map.Map<ConventionTarget>(new ConventionSource { userid = 13 })!.UserId); });
        Case("conventions case-sensitive mapping leaves unmatched initializer", () => { var c = new MapperConfiguration(); c.ConfigureConventions(o => { o.CaseInsensitiveNameMatch = false; o.IgnoreSpecialCharacters = false; }); Equal(0, c.CreateMapper().Map<ConventionTarget>(new ConventionSource { userid = 13 })!.UserId); });
        Case("conventions underscore normalization is effective", () => { var c = new MapperConfiguration(); c.ConfigureConventions(o => { o.CaseInsensitiveNameMatch = true; o.IgnoreSpecialCharacters = true; }); Equal(13, c.CreateMapper().Map<ConventionTarget>(new UnderscoreSource { user_id = 13 })!.UserId); });
        Case("explicit Strict option overrides flexible conventions", () => { var c = new MapperConfiguration(); c.ConfigureConventions(o => { o.CaseInsensitiveNameMatch = true; o.IgnoreSpecialCharacters = true; }); var map = c.CreateMapper(); Equal(0, map.Map<ConventionTarget>(new UnderscoreSource { user_id = 13 }, AdaptOptions.Strict)!.UserId); });
        Case("explicit flexible option overrides strict conventions", () => { var c = new MapperConfiguration(); c.ConfigureConventions(o => { o.CaseInsensitiveNameMatch = false; o.IgnoreSpecialCharacters = false; }); Equal(13, c.CreateMapper().Map<ConventionTarget>(new UnderscoreSource { user_id = 13 }, AdaptOptions.Default)!.UserId); });
        Case("explicit configuration default overrides conventions", () => { var c = new MapperConfiguration { DefaultAdaptOptions = AdaptOptions.Strict }; c.ConfigureConventions(o => { o.CaseInsensitiveNameMatch = true; o.IgnoreSpecialCharacters = true; }); Equal(0, c.CreateMapper().Map<ConventionTarget>(new UnderscoreSource { user_id = 13 })!.UserId); });
        Case("later convention callbacks override earlier convention values", () => { var c = new MapperConfiguration(); c.ConfigureConventions(o => o.CaseInsensitiveNameMatch = false); c.ConfigureConventions(o => o.CaseInsensitiveNameMatch = true); Equal(13, c.CreateMapper().Map<ConventionTarget>(new ConventionSource { userid = 13 })!.UserId); });
        Case("mapper convention configurations remain isolated when interleaved", () => { var a = new MapperConfiguration(); var b = new MapperConfiguration(); a.ConfigureConventions(o => o.CaseInsensitiveNameMatch = false); b.ConfigureConventions(o => o.CaseInsensitiveNameMatch = true); var ma = a.CreateMapper(); var mb = b.CreateMapper(); var value = new ConventionSource { userid = 13 }; Equal(0, ma.Map<ConventionTarget>(value)!.UserId); Equal(13, mb.Map<ConventionTarget>(value)!.UserId); Equal(0, ma.Map<ConventionTarget>(value)!.UserId); });
        Case("conventions apply to non-generic entry point", () => { var c = new MapperConfiguration(); c.ConfigureConventions(o => { o.CaseInsensitiveNameMatch = false; o.IgnoreSpecialCharacters = false; }); var d = (ConventionTarget)c.CreateMapper().Map(new ConventionSource { userid = 13 }, typeof(ConventionSource), typeof(ConventionTarget))!; Equal(0, d.UserId); });
        Case("Adapt no-options honors configured conventions", () => { var previous = MapperProvider.Current; try { var c = new MapperConfiguration(); c.ConfigureConventions(o => { o.CaseInsensitiveNameMatch = false; o.IgnoreSpecialCharacters = false; }); MapperProvider.SetCurrent(c.CreateMapper()); Equal(0, new ConventionSource { userid = 13 }.Adapt<ConventionTarget>().UserId); Equal(13, new ConventionSource { userid = 13 }.Adapt<ConventionTarget>(AdaptOptions.IgnoreCase).UserId); } finally { MapperProvider.SetCurrent(previous); } });

        Case("activity computed forward and explicit reverse preserve supported fields", () => {
            var map = With(new ActivityProfile());
            var entity = map.Map<MallActivityEntity>(new ActivityDto { FirstName = "Ada", LastName = "Lovelace", Uid = 41, User = new() { NickName = "ada" }, Items = [new() { Key = "item", Price = 3.5m }] })!;
            Equal("Ada Lovelace", entity.Name); Equal(41, entity.UserId); Equal("ada", entity.User!.NickName); Equal(3.5m, entity.Items![0].Price);
            var dto = map.Map<ActivityDto>(entity)!; Equal(41, dto.Uid); Equal("ada", dto.User!.NickName); Equal(1, dto.Items!.Count); Equal("item", dto.Items[0].Key); Equal(3.5m, dto.Items[0].Price);
            Equal(null, dto.FirstName); Equal(null, dto.LastName);
        });
        Case("immutable runtime ignored constructor argument does not read getter", () => {
            var source = new IgnoredCtorSource { Name = "keep" };
            var d = m.Map<ImmutableValue>(source, AdaptOptions.Default, ignoreNames: ["Id"]); Equal(0L, d.Id); Equal("keep", d.Name); Equal(0, source.Reads);
        });
        Case("immutable profile ignored constructor argument uses default", () => {
            var source = new IgnoredCtorSource { Name = "keep" };
            var d = With(new IgnoredCtorProfile()).Map<ImmutableValue>(source); Equal(0L, d.Id); Equal("keep", d.Name); Equal(0, source.Reads);
        });
        Case("shared source with different nested ignore projections cannot reuse filtered child", () => {
            var child = new ClassSourceChild { Number = 17, Other = "keep" }; var source = new ProjectionSource { First = child, Second = child };
            var d = With(new ProjectionProfile()).Map<ProjectionTarget>(source)!;
            Equal(0, d.First!.Number); Equal(17, d.Second!.Number); Equal("keep", d.First.Other); Equal("keep", d.Second.Other); NotSame(d.First, d.Second);
        });
        Case("opt-in invalid dictionary key diagnostics redact raw key", () => {
            var map = new MapperConfiguration { IncludeSourceValueInExceptions = true, MaxDiagnosticValueLength = 128 }.CreateMapper();
            var e = Failure(() => map.Map<Dictionary<int, int>>(new Dictionary<string, int> { ["PRIVATE_KEY_SECRET_927"] = 1 }));
            Equal("$[0].Key", e.Path); if (e.ToString().Contains("PRIVATE_KEY_SECRET_927") || (e.SourceValue?.Contains("PRIVATE_KEY_SECRET_927") ?? false)) throw new Exception("Invalid dictionary key leaked into diagnostics.");
        });

        Case("dictionary cross-type values honor runtime element ignore", () => {
            var child = new ClassSourceChild { Number = 17, Other = "keep" }; var source = new Dictionary<int, ClassSourceChild> { [1] = child };
            var d = m.Map<Dictionary<long, ClassChild>>(source, AdaptOptions.Default, ignoreNames: ["Number"])!;
            Equal(1, d.Count); Equal(0, d[1].Number); Equal("keep", d[1].Other); Equal(17, child.Number); NotSame(child, d[1]);
        });
        Case("same-type list ignore copies filtered elements without mutating source", () => {
            var child = new ClassChild { Number = 17, Other = "keep" }; var source = new List<ClassChild> { child, child };
            var d = m.Map<List<ClassChild>>(source, AdaptOptions.Default, ignoreNames: ["Number"])!;
            Equal(2, d.Count); Equal(0, d[0].Number); Equal("keep", d[0].Other); Equal(17, source[0].Number); NotSame(source, d); NotSame(child, d[0]); Same(d[0], d[1]);
        });
        Case("same-type array ignore copies filtered elements", () => {
            var child = new ClassChild { Number = 17, Other = "keep" }; var source = new[] { child };
            var d = m.Map<ClassChild[]>(source, AdaptOptions.Default, ignoreNames: ["Number"])!;
            Equal(1, d.Length); Equal(0, d[0].Number); Equal("keep", d[0].Other); Equal(17, child.Number); NotSame(source, d); NotSame(child, d[0]);
        });
        Case("same-type dictionary ignore copies filtered values and retains keys", () => {
            var child = new ClassChild { Number = 17, Other = "keep" }; var source = new Dictionary<int, ClassChild> { [1] = child, [2] = child };
            var d = m.Map<Dictionary<int, ClassChild>>(source, AdaptOptions.Default, ignoreNames: ["Number"])!;
            Equal(2, d.Count); Equal(0, d[1].Number); Equal("keep", d[1].Other); Equal(17, source[1].Number); NotSame(source, d); NotSame(child, d[1]); Same(d[1], d[2]);
        });
        Case("shared dictionary values keep separate filtered and full projections", () => {
            var child = new ClassSourceChild { Number = 17, Other = "keep" }; var values = new Dictionary<int, ClassSourceChild> { [1] = child };
            var d = m.Map<DictionaryProjectionTarget>(new DictionaryProjectionSource { First = values, Second = values }, AdaptOptions.Default, ignoreNames: ["First.Number"])!;
            Equal(0, d.First![1].Number); Equal(17, d.Second![1].Number); Equal("keep", d.First[1].Other); Equal("keep", d.Second[1].Other); Equal(17, child.Number); NotSame(d.First, d.Second); NotSame(d.First[1], d.Second[1]);
        });
        Case("shared same-type lists keep separate filtered and full projections", () => {
            var child = new ClassChild { Number = 17, Other = "keep" }; var values = new List<ClassChild> { child };
            var d = m.Map<ListProjection>(new ListProjection { First = values, Second = values }, AdaptOptions.Default, ignoreNames: ["First.Number"])!;
            Equal(0, d.First![0].Number); Equal(17, d.Second![0].Number); Equal("keep", d.First[0].Other); Equal(17, child.Number); NotSame(d.First, d.Second); NotSame(d.First[0], d.Second[0]);
        });

        Case("shared dictionary full projection first cannot poison later filtered projection", () => {
            var child = new ClassSourceChild { Number = 17, Other = "keep" }; var values = new Dictionary<int, ClassSourceChild> { [1] = child };
            var d = m.Map<DictionaryProjectionTarget>(new DictionaryProjectionSource { First = values, Second = values }, AdaptOptions.Default, ignoreNames: ["Second.Number"])!;
            Equal(17, d.First![1].Number); Equal(0, d.Second![1].Number); Equal("keep", d.Second[1].Other); Equal(17, child.Number); NotSame(d.First, d.Second); NotSame(d.First[1], d.Second[1]);
        });
        Case("same-type list full projection first cannot poison later filtered projection", () => {
            var child = new ClassChild { Number = 17, Other = "keep" }; var values = new List<ClassChild> { child };
            var d = m.Map<ListProjection>(new ListProjection { First = values, Second = values }, AdaptOptions.Default, ignoreNames: ["Second.Number"])!;
            Equal(17, d.First![0].Number); Equal(0, d.Second![0].Number); Equal("keep", d.Second[0].Other); Equal(17, child.Number); NotSame(d.First, d.Second); NotSame(d.First[0], d.Second[0]);
        });

        Case("opaque converter rejects nonempty projection before executing", () => {
            var calls = 0;
            TypeConverterRegistry.Register(typeof(OpaqueSource), typeof(OpaqueTarget), new Func<OpaqueSource, OpaqueTarget>(value => { calls++; return new() { Number = value.Number }; }));
            var e = Failure(() => m.Map<OpaqueTarget>(new OpaqueSource { Number = 17 }, AdaptOptions.Default, ignoreNames: ["Number"]));
            Equal(typeof(NotSupportedException).FullName, e.OriginalExceptionType); Equal("$", e.Path); Equal(0, calls);
        });
        Case("empty ignore arrays preserve independent graphs and converter behavior", () => {
            var child = new ClassChild { Number = 17, Other = "keep" }; var list = new List<ClassChild> { child, child }; var dictionary = new Dictionary<int, ClassChild> { [1] = child, [2] = child };
            foreach (var ignores in new string[]?[] { [], null })
            {
                var copied = m.Map<ClassChild>(child, AdaptOptions.Default, ignoreNames: ignores)!;
                NotSame(child, copied); Equal(17, copied.Number); Equal("keep", copied.Other);
                copied.Number = 99; Equal(17, child.Number);
                var copiedList = m.Map<List<ClassChild>>(list, AdaptOptions.Default, ignoreNames: ignores)!;
                NotSame(list, copiedList); Equal(2, copiedList.Count); NotSame(child, copiedList[0]); Same(copiedList[0], copiedList[1]); Equal(17, copiedList[0].Number); Equal("keep", copiedList[0].Other);
                copiedList[0].Other = "target-only"; Equal("keep", child.Other);
                var copiedDictionary = m.Map<Dictionary<int, ClassChild>>(dictionary, AdaptOptions.Default, ignoreNames: ignores)!;
                NotSame(dictionary, copiedDictionary); Equal(2, copiedDictionary.Count); NotSame(child, copiedDictionary[1]); Same(copiedDictionary[1], copiedDictionary[2]); Equal(17, copiedDictionary[1].Number); Equal("keep", copiedDictionary[1].Other);
                child.Number = 23; Equal(17, copiedDictionary[1].Number); child.Number = 17;
            }
            var calls = 0;
            TypeConverterRegistry.Register(typeof(EmptyIgnoreSource), typeof(EmptyIgnoreTarget), new Func<EmptyIgnoreSource, EmptyIgnoreTarget>(value => { calls++; return new() { Number = value.Number + 1 }; }));
            Equal(18, m.Map<EmptyIgnoreTarget>(new EmptyIgnoreSource { Number = 17 }, AdaptOptions.Default, ignoreNames: [])!.Number);
            Equal(19, m.Map<EmptyIgnoreTarget>(new EmptyIgnoreSource { Number = 18 }, AdaptOptions.Default, ignoreNames: null)!.Number);
            Equal(2, calls); Equal(17, child.Number);
        });

        Console.WriteLine($"Advanced mapping regression: {count - failures.Count}/{count} passed.");
        if (failures.Count != 0) throw new AggregateException("Advanced mapping regressions failed", failures);
    }
    private static IMapper With(MapperProfile profile) { var c = new MapperConfiguration(); c.AddProfile(profile); return c.CreateMapper(); }
    private static MappingException Failure(Action action) { try { action(); } catch (MappingException e) { return e; } throw new Exception("Expected MappingException, not a silent default."); }
    private static void Reject(Action action) { try { action(); } catch (NotSupportedException) { return; } catch (InvalidOperationException) { return; } catch (MappingException) { return; } throw new Exception("Expected explicit unsupported configuration rejection."); }
    private static void Equal(object? expected, object? actual) { if (!Equals(expected, actual)) throw new Exception($"Expected '{expected ?? "null"}', actual '{actual ?? "null"}'."); }
    private static void Same(object? a, object? b) { if (!ReferenceEquals(a, b)) throw new Exception("Mapped reference alias was lost."); }
    private static void NotSame(object? a, object? b) { if (ReferenceEquals(a, b)) throw new Exception("Expected independent mapped instances."); }
    public sealed class OpaqueSource { public int Number { get; set; } }
    public sealed class OpaqueTarget { public int Number { get; set; } }
    public sealed class EmptyIgnoreSource { public int Number { get; set; } }
    public sealed class EmptyIgnoreTarget { public int Number { get; set; } }
    public sealed class DictionaryProjectionSource { public Dictionary<int, ClassSourceChild>? First { get; set; } public Dictionary<int, ClassSourceChild>? Second { get; set; } }
    public sealed class DictionaryProjectionTarget { public Dictionary<long, ClassChild>? First { get; set; } public Dictionary<long, ClassChild>? Second { get; set; } }
    public sealed class ListProjection { public List<ClassChild>? First { get; set; } public List<ClassChild>? Second { get; set; } }
    public sealed class IgnoredCtorSource { public int Reads; public string Id { get { Reads++; throw new Exception("ignored getter must not execute"); } } public string? Name { get; set; } }
    public sealed class IgnoredCtorProfile : MapperProfile { public IgnoredCtorProfile() => CreateMap<IgnoredCtorSource, ImmutableValue>().Ignore(d => d.Id); }
    public sealed class ProjectionSource { public ClassSourceChild? First { get; set; } public ClassSourceChild? Second { get; set; } }
    public sealed class ProjectionTarget { public ClassChild? First { get; set; } public ClassChild? Second { get; set; } }
    public sealed class ProjectionProfile : MapperProfile { public ProjectionProfile() => CreateMap<ProjectionSource, ProjectionTarget>().Ignore(d => d.First!.Number); }
    public readonly record struct NullKey(int Value);
    public sealed class DictionaryNodeSource { public Dictionary<int, DictionaryNodeSource>? Owner { get; set; } }
    public sealed class DictionaryNodeTarget { public Dictionary<long, DictionaryNodeTarget>? Owner { get; set; } }
    public readonly struct PreferredValue { public long Id { get; } public string? Name { get; } public PreferredValue(long id) { Id = id; Name = "wrong constructor"; } public PreferredValue(long id, string? name) { Id = id; Name = name; } }
    public sealed class GetterOnlyRoot { public ClassChild Child { get; } = new() { Other = "initializer" }; }
    public sealed class NullGetterOnlyRoot { public ClassChild? Child => null; }
    public sealed class GetterOnlyStructRoot { public StructChild Child { get; } }
    public sealed class ThrowingRoot { public ThrowingChild? Child { get; set; } }
    public sealed class ThrowingChild { public int Number { get => 0; set => throw new InvalidOperationException("PRIVATE_SETTER"); } }
    public sealed class GetterOnlyProfile : MapperProfile { public GetterOnlyProfile() => CreateMap<Flat, GetterOnlyRoot>().ForMember(d => d.Child.Number, s => s.Number); }
    public sealed class NullGetterOnlyProfile : MapperProfile { public NullGetterOnlyProfile() => CreateMap<Flat, NullGetterOnlyRoot>().ForMember(d => d.Child!.Number, s => s.Number); }
    public sealed class GetterOnlyStructProfile : MapperProfile { public GetterOnlyStructProfile() => CreateMap<Flat, GetterOnlyStructRoot>().ForMember(d => d.Child.Number, s => s.Number); }
    public sealed class ThrowingNestedProfile : MapperProfile { public ThrowingNestedProfile() => CreateMap<Flat, ThrowingRoot>().ForMember(d => d.Child!.Number, s => s.Number); }
    public sealed class NestedPrecedenceProfile : MapperProfile { public NestedPrecedenceProfile() => CreateMap<Flat, ClassRoot>().ForMember(d => d.Child!.Number, s => s.Number).ForMember(d => d.Child!.Other, s => s.Text).Ignore(d => d.Child!.Number); }
    public sealed class ConventionSource { public int userid { get; set; } }
    public sealed class UnderscoreSource { public int user_id { get; set; } }
    public sealed class ConventionTarget { public int UserId { get; set; } }
    public sealed class DictHolder<K, V> where K : notnull { public Dictionary<K, V>? Values { get; set; } }
    public sealed class TwoDicts<T> where T : notnull { public Dictionary<T, T>? First { get; set; } public Dictionary<T, T>? Second { get; set; } }
    public sealed class NodeSource { public int Value { get; set; } public NodeSource? Next { get; set; } }
    public sealed class NodeTarget { public long Value { get; set; } public NodeTarget? Next { get; set; } }
    public sealed class Box<T> { public T Value { get; set; } = default!; }
    public sealed class MutableValue { public string? Id { get; set; } public string? Name { get; set; } }
    public readonly struct ImmutableValue { public long Id { get; } public string? Name { get; } public ImmutableValue(long id, string? name) { Id = id; Name = name; } }
    public readonly record struct RecordValue(long Id, string? Name);
    public readonly struct UnmatchableValue { public long Missing { get; } public UnmatchableValue(long missing) { Missing = missing; } }
    public readonly struct AmbiguousValue { public long Id { get; } public string? Name { get; } public AmbiguousValue(long id, string? name) { Id = id; Name = name; } public AmbiguousValue(int id, string? name) { Id = id; Name = name; } }
    public readonly struct ThrowingValue { public long Id { get; } public ThrowingValue(long id) { throw new InvalidOperationException("PRIVATE_CONSTRUCTOR"); } }
    public sealed class Flat { public int Number { get; set; } public string? Text { get; set; } }
    public sealed class FlatText { public string? Number { get; set; } }
    public sealed class ClassRoot { public ClassChild? Child { get; set; } public string? Text { get; set; } }
    public sealed class ClassChild { public int Number { get; set; } public string? Other { get; set; } }
    public sealed class ClassSourceRoot { public ClassSourceChild? Child { get; set; } }
    public sealed class ClassSourceChild { public int Number { get; set; } public string? Other { get; set; } }
    public struct StructChild { public int Number { get; set; } }
    public sealed class StructRoot { public StructChild Child { get; set; } }
    public struct Middle { public ClassChild? Child { get; set; } }
    public sealed class DeepRoot { public Middle Middle { get; set; } }
    public sealed class Named { public string? Name { get; set; } }
    public sealed class Renamed { public string? Title { get; set; } }
    public sealed class NestedClassProfile : MapperProfile { public NestedClassProfile() => CreateMap<Flat, ClassRoot>().ForMember(d => d.Child!.Number, s => s.Number); }
    public sealed class NestedStructProfile : MapperProfile { public NestedStructProfile() => CreateMap<Flat, StructRoot>().ForMember(d => d.Child.Number, s => s.Number); }
    public sealed class DeepProfile : MapperProfile { public DeepProfile() => CreateMap<Flat, DeepRoot>().ForMember(d => d.Middle.Child!.Number, s => s.Number); }
    public sealed class NestedRenameProfile : MapperProfile { public NestedRenameProfile() => CreateMap<FlatText, ClassRoot>().Rename(d => d.Child!.Number, "Number"); }
    public sealed class NestedIgnoreProfile : MapperProfile { public NestedIgnoreProfile() => CreateMap<ClassSourceRoot, ClassRoot>().Ignore(d => d.Child!.Number); }
    public sealed class ReverseRenameProfile : MapperProfile { public ReverseRenameProfile() => CreateMap<Named, Renamed>().Rename(d => d.Title!, "Name").ReverseMap(); }
    public sealed class ReversePathProfile : MapperProfile { public ReversePathProfile() => CreateMap<ClassRoot, Flat>().ForMember(d => d.Number, s => s.Child!.Number).ReverseMap(); }
    public sealed class ReverseCustomProfile : MapperProfile { public ReverseCustomProfile() => CreateMap<Named, Renamed>().Rename(d => d.Title!, "Name").ReverseMap().ForMember(d => d.Name, s => s.Title + "!"); }
    public sealed class IrreversibleProfile : MapperProfile { public IrreversibleProfile() => CreateMap<Flat, ClassChild>().ForMember(d => d.Number, s => s.Number + 1).ReverseMap(); }
}
