using System.Globalization;
using Ling.Mapper;
using Ling.Mapper.Configuration;
using Ling.Mapper.Extensions;
using Ling.Mapper.Mapper;
using Ling.Mapper.Models;
using Ling.Mapper.Provider;

namespace TestConsole.Test;

/// <summary>Assertion-based conversion contracts. Every case runs even when another fails.</summary>
public static class ConversionRegressionTests
{
    public static void Run()
    {
        var failures = new List<Exception>();
        var count = 0;
        void Case(string name, Action test)
        {
            count++;
            try { test(); Console.WriteLine($"PASS conversion: {name}"); }
            catch (Exception error) { failures.Add(new Exception(name, error)); Console.WriteLine($"FAIL conversion: {name}: {error.Message}"); }
        }
        var mapper = new MapperConfiguration().CreateMapper();
        Case("floating overflow follows Convert infinity semantics", () => {
            Equal(float.PositiveInfinity, mapper.Map<float>(double.MaxValue));
            Equal(float.PositiveInfinity, mapper.Map<Box<float>>(new Box<double> { Value = double.MaxValue })!.Value);
            Equal(double.PositiveInfinity, mapper.Map<double>("1e9999"));
            Equal(double.PositiveInfinity, mapper.Map<Box<double>>(new Box<string> { Value = "1e9999" })!.Value);
        });
        Case("concrete base member runtime identity precedes converter", () => {
            TypeConverterRegistry.Register(typeof(ConcreteBase), typeof(ConcreteDerived),
                new Func<ConcreteBase, ConcreteDerived>(_ => new ConcreteDerived()));
            var value = new ConcreteDerived();
            var result = mapper.Map<ConcreteDerivedHolder>(new ConcreteBaseHolder { Value = value });
            if (!ReferenceEquals(value, result!.Value)) throw new Exception("Assignable runtime value must retain identity.");
        });
        Case("configuration defaults", () => {
            var c = new MapperConfiguration(); Equal(NullValueHandling.UseDefault, c.NullValueHandling);
            Equal(false, c.IncludeSourceValueInExceptions); Equal(128, c.MaxDiagnosticValueLength);
        });
        Case("root decimal string both directions", () => { Equal("1234.56", mapper.Map<string>(1234.56m)); Equal(1234.56m, mapper.Map<decimal>("1234.56")); });
        Case("Adapt public API", () => {
            var old = MapperProvider.Current;
            try { MapperProvider.SetCurrent(mapper); Equal(25.75m, "25.75".Adapt<decimal>()); Equal("25.75", 25.75m.Adapt<string>()); }
            finally { MapperProvider.SetCurrent(old); }
        });
        Case("DTO decimal string both directions", () => {
            Equal("29.95", mapper.Map<MoneyText>(new Money { Amount = 29.95m })!.Amount);
            Equal(29.95m, mapper.Map<Money>(new MoneyText { Amount = "29.95" })!.Amount);
        });
        Case("nested members and collection values", () => {
            var result = mapper.Map<Order>(new OrderText { Detail = new MoneyText { Amount = "17.25" }, Items = [new() { Amount = "0.01" }, new() { Amount = "99.99" }] })!;
            Equal(17.25m, result.Detail!.Amount); Equal(0.01m, result.Items![0].Amount); Equal(99.99m, result.Items[1].Amount);
            Equal("99.99", mapper.Map<OrderText>(result)!.Items![1].Amount);
        });
        Case("invalid root reports types and root", () => Failure(() => mapper.Map<decimal>("invalid-amount"), typeof(string), typeof(decimal), "$"));
        Case("invalid member is not default", () => Failure(() => mapper.Map<Money>(new MoneyText { Amount = "invalid-amount" }), typeof(string), typeof(decimal), "$.Amount"));
        Case("invalid nested member path", () => Failure(() => mapper.Map<Order>(new OrderText { Detail = new() { Amount = "bad" } }), typeof(string), typeof(decimal), "$.Detail.Amount"));
        Case("invalid indexed member path", () => Failure(() => mapper.Map<Order>(new OrderText { Items = [new() { Amount = "1" }, new() { Amount = "bad" }] }), typeof(string), typeof(decimal), "$.Items[1].Amount"));
        Case("invalid root collection index", () => Failure(() => mapper.Map<List<decimal>>(new[] { "1", "bad" }), typeof(string), typeof(decimal), "$[1]"));

        var numbers = new Type[] { typeof(sbyte), typeof(byte), typeof(short), typeof(ushort), typeof(int), typeof(uint), typeof(long), typeof(ulong), typeof(float), typeof(double), typeof(decimal) };
        foreach (var source in numbers)
        foreach (var destination in numbers)
            Case($"numeric {source.Name} to {destination.Name}", () => {
                var value = Convert.ChangeType(42, source, CultureInfo.InvariantCulture);
                Equal(Convert.ChangeType(42, destination, CultureInfo.InvariantCulture), mapper.Map(value, source, destination));
            });
        foreach (var source in numbers)
        foreach (var destination in numbers)
            Case($"numeric member {source.Name} to {destination.Name}", () => {
                var sourceBox = typeof(Box<>).MakeGenericType(source); var targetBox = typeof(Box<>).MakeGenericType(destination);
                var value = Activator.CreateInstance(sourceBox)!; sourceBox.GetProperty("Value")!.SetValue(value, Convert.ChangeType(42, source, CultureInfo.InvariantCulture));
                var mapped = mapper.Map(value, sourceBox, targetBox)!;
                Equal(Convert.ChangeType(42, destination, CultureInfo.InvariantCulture), targetBox.GetProperty("Value")!.GetValue(mapped));
            });
        foreach (var type in numbers)
        {
            Case($"string to {type.Name}", () => Equal(Convert.ChangeType(42, type, CultureInfo.InvariantCulture), mapper.Map("42", typeof(string), type)));
            Case($"{type.Name} to string", () => Equal("42", mapper.Map(Convert.ChangeType(42, type, CultureInfo.InvariantCulture), type, typeof(string))));
            Case($"nullable {type.Name}", () => Equal(Convert.ChangeType(42, type, CultureInfo.InvariantCulture), mapper.Map("42", typeof(string), typeof(Nullable<>).MakeGenericType(type))));
            Case($"invalid {type.Name}", () => Failure(() => mapper.Map("no-number", typeof(string), type), typeof(string), type, "$"));
        }
        Case("numeric member overflow", () => Failure(() => mapper.Map<Box<byte>>(new Box<int> { Value = 256 }), typeof(int), typeof(byte), "$.Value"));
        Case("numeric nullable member values and null", () => {
            Equal(42L, mapper.Map<Box<long?>>(new Box<int?> { Value = 42 })!.Value);
            Equal(null, mapper.Map<Box<long?>>(new Box<int?>())!.Value);
            Equal(0L, mapper.Map<Box<long>>(new Box<int?>())!.Value);
        });
        Case("numeric member preserves Convert rounding", () => {
            Equal(2, mapper.Map<Box<int>>(new Box<decimal> { Value = 2.5m })!.Value);
            Equal(4, mapper.Map<Box<int>>(new Box<double> { Value = 3.5d })!.Value);
        });
        Case("overflow is an error", () => Failure(() => mapper.Map<byte>("256"), typeof(string), typeof(byte), "$"));
        Case("negative unsigned is an error", () => Failure(() => mapper.Map<uint>(-1), typeof(int), typeof(uint), "$"));
        var enums = new Type[] { typeof(EByte), typeof(ESByte), typeof(EShort), typeof(EUShort), typeof(EInt), typeof(EUInt), typeof(ELong), typeof(EULong) };
        foreach (var enumType in enums)
        {
            Case($"enum {enumType.Name} parse", () => Equal(Enum.ToObject(enumType, 2), mapper.Map("two", typeof(string), enumType)));
            Case($"enum {enumType.Name} number", () => Equal(Enum.ToObject(enumType, 2), mapper.Map(2, typeof(int), enumType)));
            Case($"enum {enumType.Name} numeric output", () => Equal(2L, mapper.Map(Enum.ToObject(enumType, 2), enumType, typeof(long))));
            Case($"enum {enumType.Name} nullable", () => Equal(Enum.ToObject(enumType, 2), mapper.Map("Two", typeof(string), typeof(Nullable<>).MakeGenericType(enumType))));
            foreach (var other in enums)
                Case($"enum {enumType.Name} to {other.Name}", () => Equal(Enum.ToObject(other, 2), mapper.Map(Enum.ToObject(enumType, 2), enumType, other)));
        }
        Case("flags names", () => Equal(Permissions.Read | Permissions.Write, mapper.Map<Permissions>("Read, Write")));
        Case("flags formatting", () => Equal("Read, Write", mapper.Map<string>(Permissions.Read | Permissions.Write)));
        Case("ulong enum retains high bit", () => Equal(HighBit.High, mapper.Map<HighBit>(1UL << 63)));
        Case("ulong enum numeric retains high bit", () => Equal(1UL << 63, mapper.Map<ulong>(HighBit.High)));
        Case("enum malformed is error", () => Failure(() => mapper.Map<EByte>("Missing"), typeof(string), typeof(EByte), "$"));
        Case("enum underlying overflow", () => Failure(() => mapper.Map<EByte>(256), typeof(int), typeof(EByte), "$"));
        Case("boolean and character", () => { Equal(true, mapper.Map<bool>("true")); Equal('Z', mapper.Map<char>("Z")); });
        Case("invalid bool", () => Failure(() => mapper.Map<bool>("perhaps"), typeof(string), typeof(bool), "$"));
        Case("invalid char", () => Failure(() => mapper.Map<char>("long"), typeof(string), typeof(char), "$"));

        var guid = Guid.Parse("01234567-89ab-cdef-0123-456789abcdef");
        var date = new DateTime(2026, 10, 23, 7, 30, 45, DateTimeKind.Utc).AddTicks(1234);
        var offset = new DateTimeOffset(2026, 10, 23, 7, 30, 45, TimeSpan.FromHours(5.5));
        var day = new DateOnly(2026, 10, 23); var time = new TimeOnly(7, 30, 45).Add(TimeSpan.FromTicks(1234));
        var span = TimeSpan.FromDays(2) + TimeSpan.FromMinutes(17);
        Case("Guid string roundtrip", () => Equal(guid, mapper.Map<Guid>(mapper.Map<string>(guid))));
        foreach (var value in new object[] { date, offset, day, time, span })
            Case($"{value.GetType().Name} AutoMapper-compatible default string", () => Equal(value.ToString(), mapper.Map<string>(value)));
        Case("DateTime default text does not preserve Kind", () => Equal(Convert.ToDateTime(date.ToString()).Kind, mapper.Map<DateTime>(mapper.Map<string>(date)).Kind));
        Case("DateTimeOffset preserves offset", () => Equal(offset.Offset, mapper.Map<DateTimeOffset>(mapper.Map<string>(offset)).Offset));
        Case("DateTime to DateOnly", () => Equal(day, mapper.Map<DateOnly>(date)));
        Case("DateTime to TimeOnly", () => Equal(TimeOnly.FromDateTime(date), mapper.Map<TimeOnly>(date)));
        Case("DateOnly to DateTime", () => Equal(day.ToDateTime(TimeOnly.MinValue), mapper.Map<DateTime>(day)));
        foreach (var type in new[] { typeof(Guid), typeof(DateTime), typeof(DateTimeOffset), typeof(DateOnly), typeof(TimeOnly), typeof(TimeSpan) })
            Case($"invalid {type.Name}", () => Failure(() => mapper.Map("not-a-date-or-guid", typeof(string), type), typeof(string), type, "$"));
        Case("integer date does not infer units", () => Failure(() => mapper.Map<DateTime>(1700000000L), typeof(long), typeof(DateTime), "$"));
        Case("date integer does not infer units", () => Failure(() => mapper.Map<long>(date), typeof(DateTime), typeof(long), "$"));
        foreach (var culture in new[] { "en-US", "de-DE", "fr-FR", "tr-TR", "zh-CN" })
            Case($"invariant culture {culture}", () => {
                var old = CultureInfo.CurrentCulture; var oldUI = CultureInfo.CurrentUICulture;
                try { CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo(culture); CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo(culture);
                    Equal(1234.56m, mapper.Map<decimal>("1234.56")); Equal("1234.56", mapper.Map<string>(1234.56m));
                    Equal(Convert.ToDateTime(date.ToString("O", CultureInfo.InvariantCulture)), mapper.Map<DateTime>(date.ToString("O", CultureInfo.InvariantCulture))); Equal(EInt.Two, mapper.Map<EInt>("two")); }
                finally { CultureInfo.CurrentCulture = old; CultureInfo.CurrentUICulture = oldUI; }
            });
        Case("null root default and nullable", () => { Equal(0, mapper.Map<int>(null)); Equal(null, mapper.Map<int?>(null)); Equal(null, mapper.Map<string>(null)); });
        Case("nullable member values", () => { Equal(12, mapper.Map<NullableNumber>(new NumberText { Value = "12" })!.Value); Equal(null, mapper.Map<NullableNumber>(new NumberText())!.Value); });
        Case("null member uses default", () => Equal(0, mapper.Map<Number>(new NumberText())!.Value));
        var strictNull = new MapperConfiguration { NullValueHandling = NullValueHandling.Throw }.CreateMapper();
        Case("null root throw policy", () => Failure(() => strictNull.Map(null, typeof(string), typeof(int)), typeof(string), typeof(int), "$"));
        Case("null member throw policy", () => Failure(() => strictNull.Map<Number>(new NumberText()), typeof(string), typeof(int), "$.Value"));
        Case("nullable allowed with throw policy", () => Equal(null, strictNull.Map<int?>(null)));
        Case("IgnoreNullValues preserves initializer", () => Equal(73, strictNull.Map<Number>(new NumberText(), AdaptOptions.IgnoreNullValues)!.Value));
        Case("IgnoreNullValues does not suppress invalid", () => Failure(() => strictNull.Map<Number>(new NumberText { Value = "bad" }, AdaptOptions.IgnoreNullValues), typeof(string), typeof(int), "$.Value"));
        Case("null collection item default", () => { var values = mapper.Map<List<int>>(new string?[] { "1", null, "3" })!; Equal(0, values[1]); });
        Case("null collection item throw", () => Failure(() => strictNull.Map<List<int>>(new string?[] { "1", null }), typeof(string), typeof(int), "$[1]"));
        Case("Adapt generic null reference stays null", () => {
            var old = MapperProvider.Current;
            try { MapperProvider.SetCurrent(mapper); object? source = null; Equal(null, source.Adapt<Money>()); Equal(null, source.Adapt<string>()); }
            finally { MapperProvider.SetCurrent(old); }
        });
        Case("Adapt generic null nullable stays null", () => {
            var old = MapperProvider.Current;
            try { MapperProvider.SetCurrent(mapper); object? source = null; Equal(null, source.Adapt<int?>()); Equal(null, source.Adapt<DateTime?>()); }
            finally { MapperProvider.SetCurrent(old); }
        });
        Case("Adapt generic null collection stays null", () => {
            var old = MapperProvider.Current;
            try { MapperProvider.SetCurrent(mapper); object? source = null; Equal(null, source.Adapt<List<Money>>()); Equal(null, source.Adapt<Money[]>()); Equal(null, source.Adapt<IEnumerable<Money>>()); }
            finally { MapperProvider.SetCurrent(old); }
        });
        Case("Adapt null nonnullable uses configured default", () => {
            var old = MapperProvider.Current;
            try { MapperProvider.SetCurrent(mapper); object? source = null; Equal(0, source.Adapt<int>()); Equal(0m, source.Adapt<decimal>()); }
            finally { MapperProvider.SetCurrent(old); }
        });
        Case("Adapt null reference and nullable remain legal under Throw", () => {
            var old = MapperProvider.Current;
            try { MapperProvider.SetCurrent(strictNull); object? source = null; Equal(null, source.Adapt<Money>()); Equal(null, source.Adapt<int?>()); Equal(null, source.Adapt<List<Money>>()); }
            finally { MapperProvider.SetCurrent(old); }
        });
        Case("Adapt dedicated item list null returns empty without callbacks", () => {
            var old = MapperProvider.Current; var calls = 0;
            try { MapperProvider.SetCurrent(strictNull); IEnumerable<MoneyText> source = null!;
                var result = source.Adapt<Money, MoneyText>((destination, item) => calls++);
                True(result != null, "Dedicated item overload must return an empty list"); Equal(0, result!.Count); Equal(0, calls);
            } finally { MapperProvider.SetCurrent(old); }
        });
        Case("runtime object member", () => { Equal(19.5m, mapper.Map<Money>(new DynamicMoney { Amount = "19.5" })!.Amount); Equal(20m, mapper.Map<Money>(new DynamicMoney { Amount = 20 })!.Amount); });
        Case("runtime object source", () => Equal(19.5m, mapper.Map("19.5", typeof(object), typeof(decimal))));
        Case("runtime object collection", () => { var result = mapper.Map<List<decimal>>(new object[] { "1.5", 2, 3m })!; Equal(1.5m, result[0]); Equal(2m, result[1]); Equal(3m, result[2]); });
        Case("array element mapping", () => { var result = mapper.Map<long[]>(new int[] { 1, 2 })!; Equal(2L, result[1]); });
        Case("assignable collection identity", () => { var list = new List<int> { 1, 2 }; True(ReferenceEquals(list, mapper.Map<IEnumerable<int>>(list)), "Assignable collection should be directly assigned"); });
        Case("byte array identity", () => { var bytes = new byte[] { 1, 2 }; True(ReferenceEquals(bytes, mapper.Map<byte[]>(bytes)), "byte[] should be directly assigned"); });
        Case("complex mapping avoids ToString", () => Equal("mapped", mapper.Map<PlainTarget>(new PoisonSource { Name = "mapped" })!.Name));
        Case("complex to scalar fails without ToString", () => Failure(() => mapper.Map<int>(new PoisonSource()), typeof(PoisonSource), typeof(int), "$"));
        Case("byte array not scalar string", () => Failure(() => mapper.Map<string>(new byte[] { 1, 2 }), typeof(byte[]), typeof(string), "$"));
        Case("profile ForMember Rename Ignore", () => {
            var config = new MapperConfiguration(); config.AddProfile(new RegressionProfile());
            var result = config.CreateMapper().Map<ProfileTarget>(new ProfileSource { Price = "15.75", Count = 4, Ignored = "must-not-copy" })!;
            Equal(15.75m, result.Amount); Equal(8, result.DoubleCount); Equal("untouched", result.Ignored);
        });
        Case("profile Rename failure path", () => {
            var config = new MapperConfiguration(); config.AddProfile(new RegressionProfile());
            Failure(() => config.CreateMapper().Map<ProfileTarget>(new ProfileSource { Price = "bad" }), typeof(string), typeof(decimal), "$.Amount");
        });
        Case("Ignore runtime avoids conversion", () => Equal(0m, mapper.Map<Money>(new MoneyText { Amount = "bad" }, AdaptOptions.Default, ignoreNames: ["Amount"])!.Amount));
        Case("cycles and shared nodes", () => {
            var node = new Node { Name = "first" }; node.Next = node; node.Other = node;
            var result = mapper.Map<NodeDto>(node)!; True(ReferenceEquals(result, result.Next), "Cycle identity lost"); True(ReferenceEquals(result.Next, result.Other), "Shared identity lost");
            var second = mapper.Map<NodeDto>(node)!; True(!ReferenceEquals(result, second), "Mapping context leaked across roots");
        });
        Case("failed context does not poison next root", () => { Failure(() => mapper.Map<Order>(new OrderText { Detail = new() { Amount = "bad" } }), typeof(string), typeof(decimal), "$.Detail.Amount"); Equal(8m, mapper.Map<Money>(new MoneyText { Amount = "8" })!.Amount); Failure(() => mapper.Map<int>("bad"), typeof(string), typeof(int), "$"); });
        Case("diagnostics private by default", () => { var error = Failure(() => mapper.Map<int>("customer-secret-91"), typeof(string), typeof(int), "$"); Equal(null, error.SourceValue); NotContains(error, "customer-secret-91"); True(error.InnerException != null, "Safe inner exception missing"); });
        var diagnostics = new MapperConfiguration { IncludeSourceValueInExceptions = true, MaxDiagnosticValueLength = 8 }.CreateMapper();
        Case("diagnostics bounded scalar", () => { var error = Failure(() => diagnostics.Map<int>("abcdefghijk"), typeof(string), typeof(int), "$"); Equal("abcdefgh", error.SourceValue); NotContains(error, "abcdefghijk"); });
        Case("password remains redacted", () => { var error = Failure(() => diagnostics.Map<SecretNumber>(new SecretText { Password = "password-secret-91" }), typeof(string), typeof(int), "$.Password"); NotContains(error, "password-secret-91"); });
        Case("token remains redacted", () => { var error = Failure(() => diagnostics.Map<TokenNumber>(new TokenText { Token = "token-secret-91" }), typeof(string), typeof(int), "$.Token"); NotContains(error, "token-secret-91"); });
        Case("complex preview never calls ToString", () => { var error = Failure(() => diagnostics.Map<int>(new PoisonSource()), typeof(PoisonSource), typeof(int), "$"); NotContains(error, "TOSTRING_SECRET"); });
        Case("byte diagnostic redaction", () => { var error = Failure(() => diagnostics.Map<int>(new byte[] { 65, 66, 67 }), typeof(byte[]), typeof(int), "$"); True(error.SourceValue == null || error.SourceValue.Contains("REDACTED", StringComparison.Ordinal), "byte[] diagnostic not redacted"); });
        Case("renamed password remains private", () => {
            var config = new MapperConfiguration { IncludeSourceValueInExceptions = true }; config.AddProfile(new PrivacyProfile());
            var error = Failure(() => config.CreateMapper().Map<Number>(new SecretText { Password = "RENAMED_SECRET" }), typeof(string), typeof(int), "$.Value");
            NotContains(error, "RENAMED_SECRET");
        });
        Case("ForMember exception is contextual and safe", () => {
            var config = new MapperConfiguration { IncludeSourceValueInExceptions = true }; config.AddProfile(new ThrowingProfile());
            var error = Failure(() => config.CreateMapper().Map<Number>(new ThrowingSource()), typeof(ThrowingSource), typeof(int), "$.Value");
            NotContains(error, "EXPRESSION_SECRET"); Equal(typeof(InvalidOperationException).FullName, error.OriginalExceptionType);
        });
        Case("Adapt null respects configured throw", () => {
            var old = MapperProvider.Current;
            try { MapperProvider.SetCurrent(strictNull); object? source = null;
                try { source.Adapt<int>(); } catch (MappingException error) { Equal("$", error.Path); return; }
                throw new Exception("Adapt null bypassed configured Throw policy");
            } finally { MapperProvider.SetCurrent(old); }
        });
        // Unique test-only registrations deliberately do not change common conversion pairs.
        Case("registry priority over scalar and cache refresh", () => {
            Equal("Two", mapper.Map<string>(RuleEnum.Two));
            TypeConverterRegistry.Register(typeof(RuleEnum), typeof(string), new Func<RuleEnum, string>(_ => "registered"));
            Equal("registered", mapper.Map<string>(RuleEnum.Two));
            Equal("registered", mapper.Map<Box<string>>(new Box<RuleEnum> { Value = RuleEnum.Two })!.Value);
            TypeConverterRegistry.Register(typeof(RuleEnum), typeof(string), new Func<RuleEnum, string>(_ => "replacement"));
            Equal("replacement", mapper.Map<string>(RuleEnum.Two));
        });
        Case("assignable beats registered rule", () => { var value = new Identity(); TypeConverterRegistry.Register(typeof(Identity), typeof(Identity), new Func<Identity, Identity>(_ => throw new Exception("Should not invoke"))); True(ReferenceEquals(value, mapper.Map<Identity>(value)), "Identity assignment must precede rules"); });
        Case("explicit byte array rule", () => { TypeConverterRegistry.Register(typeof(byte[]), typeof(ByteToken), new Func<byte[], ByteToken>(b => new() { Length = b.Length })); Equal(3, mapper.Map<ByteToken>(new byte[3])!.Length); });
        Case("rule exception context and safe original type", () => {
            TypeConverterRegistry.Register(typeof(RuleInput), typeof(int), new Func<RuleInput, int>(_ => { var e = new InvalidOperationException("RULE_SECRET", new Exception("INNER_SECRET")); e.Data["token"] = "DATA_SECRET"; throw e; }));
            var error = Failure(() => diagnostics.Map<Number>(new RuleContainer { Value = new RuleInput() }), typeof(RuleInput), typeof(int), "$.Value");
            Equal(typeof(InvalidOperationException).FullName, error.OriginalExceptionType); NotContains(error, "RULE_SECRET"); NotContains(error, "INNER_SECRET"); NotContains(error, "DATA_SECRET");
            True(error.InnerException != null && error.InnerException.Data.Count == 0, "Original Data leaked");
        });
        Case("foreign MappingException in sensitive converter is sanitized", () => {
            var foreign = new MapperConfiguration { IncludeSourceValueInExceptions = true }.CreateMapper();
            var original = Failure(() => foreign.Map<int>("FOREIGN_PASSWORD_SECRET"), typeof(string), typeof(int), "$");
            Equal("FOREIGN_PASSWORD_SECRET", original.SourceValue);
            TypeConverterRegistry.Register(typeof(ForeignRuleValue), typeof(int), new Func<ForeignRuleValue, int>(_ => throw original));
            var error = Failure(() => diagnostics.Map<SecretNumber>(new ForeignRuleSource { Password = new() }), typeof(ForeignRuleValue), typeof(int), "$.Password");
            NotContains(error, "FOREIGN_PASSWORD_SECRET"); True(!ReferenceEquals(original, error), "Foreign exception was reused unsafely");
        });
        Case("foreign MappingException in sensitive getter is sanitized", () => {
            var foreign = new MapperConfiguration { IncludeSourceValueInExceptions = true }.CreateMapper();
            var original = Failure(() => foreign.Map<int>("FOREIGN_GETTER_SECRET"), typeof(string), typeof(int), "$");
            var error = Failure(() => diagnostics.Map<SecretNumber>(new ForeignGetterSource(original)), typeof(string), typeof(int), "$.Password");
            NotContains(error, "FOREIGN_GETTER_SECRET"); True(!ReferenceEquals(original, error), "Foreign getter exception escaped unchanged");
        });
        Case("constructor collection cycle fails safely", () => {
            // MappingContext.EnterCollection is checked before enumeration, including pending
            // constructor-based destinations, so this cycle cannot recurse without a bound.
            var cyclic = new List<object>(); cyclic.Add(cyclic);
            Failure(() => mapper.Map<RecursiveCollection>(cyclic), typeof(List<object>), typeof(RecursiveCollection), "$[0]");
        });
        Case("flattened Rename reads intermediate getter once", () => {
            var config = new MapperConfiguration(); config.AddProfile(new CountingProfile()); var source = new CountingSource();
            Equal(37, config.CreateMapper().Map<Number>(source)!.Value); Equal(1, source.Reads);
        });
        Case("missing constructor non-strict returns null", () => Equal(null, mapper.Map<NoDefaultConstructor>(new { Value = 4 })));
        Case("missing constructor strict throws", () => {
            var strict = new MapperConfiguration { StrictMode = true }.CreateMapper(); var source = new { Value = 4 };
            Failure(() => strict.Map<NoDefaultConstructor>(source), source.GetType(), typeof(NoDefaultConstructor), "$");
        });
        Case("runtime assignability precedes declared object converter", () => {
            TypeConverterRegistry.Register(typeof(object), typeof(RuntimeIdentity), new Func<object, RuntimeIdentity>(_ => throw new Exception("Must not invoke declared object converter")));
            var value = new RuntimeIdentity(); True(ReferenceEquals(value, mapper.Map(value, typeof(object), typeof(RuntimeIdentity))), "Runtime assignability lost priority");
        });
        Case("result-level callback runs once", () => {
            var old = MapperProvider.Current; var calls = 0;
            try { MapperProvider.SetCurrent(mapper); var result = new[] { new MoneyText { Amount = "1" }, new MoneyText { Amount = "2" } }.Adapt<List<Money>>(items => { calls++; Equal(2, items.Count); }); Equal(2, result.Count); Equal(1, calls); }
            finally { MapperProvider.SetCurrent(old); }
        });
        Case("shared collection item invokes callback each time", () => {
            var item = new MoneyText { Amount = "5" }; var calls = 0;
            var result = mapper.Map<List<Money>>(new[] { item, item }, AdaptOptions.Default, (destination, source) => { calls++; True(source is MoneyText && destination is Money, "Unexpected callback pair"); })!;
            Equal(2, calls); True(ReferenceEquals(result[0], result[1]), "Shared mapped item identity changed");
        });
        Case("scalar callback invokes once", () => { var calls = 0; Equal(5m, mapper.Map<decimal>("5", AdaptOptions.Default, (_, _) => calls++)); Equal(1, calls); });
        Case("custom converter callback invokes once", () => { var calls = 0; Equal("replacement", mapper.Map<string>(RuleEnum.Two, AdaptOptions.Default, (_, _) => calls++)); Equal(1, calls); });
        var unspecifiedTimes = new[] {
            new DateTime(2026, 1, 15, 12, 34, 56, DateTimeKind.Unspecified),
            new DateTime(2026, 3, 8, 1, 59, 59, DateTimeKind.Unspecified),
            new DateTime(2026, 3, 8, 2, 30, 0, DateTimeKind.Unspecified),
            new DateTime(2026, 3, 8, 3, 0, 0, DateTimeKind.Unspecified),
            new DateTime(2026, 11, 1, 1, 30, 0, DateTimeKind.Unspecified),
            new DateTime(2026, 11, 1, 2, 0, 0, DateTimeKind.Unspecified)
        };
        foreach (var wallTime in unspecifiedTimes)
        {
            var text = wallTime.ToString("yyyy-MM-dd'T'HH:mm:ss", CultureInfo.InvariantCulture);
            Case($"AutoMapper unspecified DateTime {text}", () => {
                var actual = mapper.Map<DateTime>(text); Equal(DateTimeKind.Unspecified, actual.Kind); Equal(wallTime.Ticks, actual.Ticks);
                var member = mapper.Map<Box<DateTime>>(new Box<string> { Value = text })!.Value;
                Equal(DateTimeKind.Unspecified, member.Kind); Equal(wallTime.Ticks, member.Ticks);
            });
            Case($"AutoMapper unspecified DateTimeOffset {text}", () => {
                var expectedOffset = DateTimeOffset.Parse(text);
                var actual = mapper.Map<DateTimeOffset>(text); Equal(expectedOffset.Offset, actual.Offset); Equal(expectedOffset.Ticks, actual.Ticks);
                var member = mapper.Map<Box<DateTimeOffset>>(new Box<string> { Value = text })!.Value;
                Equal(expectedOffset.Offset, member.Offset); Equal(expectedOffset.Ticks, member.Ticks);
            });
            foreach (var offsetHours in new[] { 0, 8, -5 })
            {
                var original = new DateTimeOffset(wallTime, TimeSpan.FromHours(offsetHours));
                var encoded = text + (offsetHours == 0 ? "Z" : offsetHours > 0 ? "+08:00" : "-05:00");
                Case($"AutoMapper explicit DateTime {encoded}", () => {
                    var expectedDate = Convert.ToDateTime(encoded);
                    var actual = mapper.Map<DateTime>(encoded); Equal(expectedDate.Kind, actual.Kind); Equal(expectedDate.Ticks, actual.Ticks);
                    var member = mapper.Map<Box<DateTime>>(new Box<string> { Value = encoded })!.Value;
                    Equal(expectedDate.Kind, member.Kind); Equal(expectedDate.Ticks, member.Ticks);
                });
                Case($"AutoMapper explicit DateTimeOffset {encoded}", () => {
                    var actual = mapper.Map<DateTimeOffset>(encoded); Equal(original.Offset, actual.Offset); Equal(original.Ticks, actual.Ticks);
                    var member = mapper.Map<Box<DateTimeOffset>>(new Box<string> { Value = encoded })!.Value;
                    Equal(original.Offset, member.Offset); Equal(original.Ticks, member.Ticks);
                });
            }
        }
        Case("object member runtime identity precedes declared converter", () => {
            TypeConverterRegistry.Register(typeof(object), typeof(RuntimeIdentity), new Func<object, RuntimeIdentity>(_ => throw new Exception("Must not invoke object member converter")));
            var value = new RuntimeIdentity(); var mapped = mapper.Map<RuntimeIdentityTarget>(new ObjectIdentitySource { Value = value })!;
            True(ReferenceEquals(value, mapped.Value), "Object member runtime identity must precede declared converter");
        });
        Case("interface member runtime identity precedes declared converter", () => {
            TypeConverterRegistry.Register(typeof(IRuntimeIdentity), typeof(RuntimeIdentity), new Func<IRuntimeIdentity, RuntimeIdentity>(_ => throw new Exception("Must not invoke interface member converter")));
            var value = new RuntimeIdentity(); var mapped = mapper.Map<RuntimeIdentityTarget>(new InterfaceIdentitySource { Value = value })!;
            True(ReferenceEquals(value, mapped.Value), "Interface member runtime identity must precede declared converter");
        });
        Case("collection mapped to object invokes root callback once", () => {
            var value = new List<int> { 1, 2, 3 }; var calls = 0;
            var result = mapper.Map<object>(value, AdaptOptions.Default, (destination, source) => {
                calls++; True(ReferenceEquals(value, destination), "Root callback received an item destination"); True(ReferenceEquals(value, source), "Root callback received an item source");
            });
            True(ReferenceEquals(value, result), "Assignable collection-to-object identity lost"); Equal(1, calls);
        });
        // Register long/date only after tests proving no implicit date unit assumption.
        Case("explicit long date rule lifts nullable and exact rule wins", () => {
            var epoch = new DateTime(2020, 1, 1, 0, 0, 0, DateTimeKind.Utc);
            TypeConverterRegistry.Register(typeof(long), typeof(DateTime), new Func<long, DateTime>(value => epoch.AddDays(value)));
            Equal(epoch.AddDays(2), mapper.Map(2L, typeof(long?), typeof(DateTime?)));
            Equal(epoch.AddDays(2), mapper.Map<Box<DateTime?>>(new Box<long?> { Value = 2 })!.Value);
            Equal(null, mapper.Map<Box<DateTime?>>(new Box<long?>())!.Value);
            TypeConverterRegistry.Register(typeof(long?), typeof(DateTime?), new Func<long?, DateTime?>(value => value.HasValue ? epoch.AddYears((int)value.Value) : null));
            Equal(epoch.AddYears(2), mapper.Map(2L, typeof(long?), typeof(DateTime?)));
            Equal(epoch.AddYears(2), mapper.Map<Box<DateTime?>>(new Box<long?> { Value = 2 })!.Value);
        });
        Console.WriteLine($"Conversion regression: {count - failures.Count}/{count} passed.");
        if (failures.Count != 0) throw new AggregateException("Conversion regression failures", failures);
    }

    private static MappingException Failure(Action action, Type source, Type target, string path)
    {
        try { action(); } catch (MappingException error) { Equal(source, error.SourceType); Equal(target, error.DestinationType); Equal(path, error.Path); return error; }
        throw new Exception($"Expected MappingException converting {source} to {target} at {path}");
    }
    private static void Equal<T>(T expected, T actual) { if (!EqualityComparer<T>.Default.Equals(expected, actual)) throw new Exception($"Expected <{expected}>, actual <{actual}>"); }
    private static void True(bool condition, string message) { if (!condition) throw new Exception(message); }
    private static void NotContains(Exception error, string secret) { True(!error.ToString().Contains(secret, StringComparison.Ordinal), "Exception text leaked a private value"); }
    public class Box<T> { public T? Value { get; set; } }
    public class Money { public decimal Amount { get; set; } }
    public class MoneyText { public string? Amount { get; set; } }
    public class DynamicMoney { public object? Amount { get; set; } }
    public class Order { public Money? Detail { get; set; } public List<Money>? Items { get; set; } }
    public class OrderText { public MoneyText? Detail { get; set; } public List<MoneyText>? Items { get; set; } }
    public class Number { public int Value { get; set; } = 73; }
    public class NullableNumber { public int? Value { get; set; } }
    public class NumberText { public string? Value { get; set; } }
    public enum EByte : byte { Two = 2 } public enum ESByte : sbyte { Two = 2 }
    public enum EShort : short { Two = 2 } public enum EUShort : ushort { Two = 2 }
    public enum EInt : int { Two = 2 } public enum EUInt : uint { Two = 2 }
    public enum ELong : long { Two = 2 } public enum EULong : ulong { Two = 2 }
    public enum HighBit : ulong { High = 1UL << 63 }
    [Flags] public enum Permissions : ushort { Read = 1, Write = 2 }
    public enum RuleEnum { Two = 2 }
    public class PoisonSource { public string? Name { get; set; } public override string ToString() => throw new Exception("TOSTRING_SECRET"); }
    public class PlainTarget { public string? Name { get; set; } }
    public class ProfileSource { public string? Price { get; set; } public int Count { get; set; } public string? Ignored { get; set; } }
    public class ProfileTarget { public decimal Amount { get; set; } public int DoubleCount { get; set; } public string Ignored { get; set; } = "untouched"; }
    public sealed class RegressionProfile : MapperProfile { public RegressionProfile() { CreateMap<ProfileSource, ProfileTarget>().Rename(d => d.Amount, nameof(ProfileSource.Price)).ForMember(d => d.DoubleCount, s => s.Count * 2).Ignore(d => d.Ignored); } }
    public class Node { public string? Name { get; set; } public Node? Next { get; set; } public Node? Other { get; set; } }
    public class NodeDto { public string? Name { get; set; } public NodeDto? Next { get; set; } public NodeDto? Other { get; set; } }
    public class SecretText { public string? Password { get; set; } } public class SecretNumber { public int Password { get; set; } }
    public class TokenText { public string? Token { get; set; } } public class TokenNumber { public int Token { get; set; } }
    public class ForeignRuleValue { }
    public class ForeignRuleSource { public ForeignRuleValue? Password { get; set; } }
    public class ForeignGetterSource(Exception error) { public string Password => throw error; }
    public class RecursiveCollection(IEnumerable<RecursiveCollection> items) : List<RecursiveCollection>(items) { }
    public class CountingSource { public int Reads { get; private set; } public CountingChild Child { get { Reads++; return new(); } } }
    public class CountingChild { public string Value { get; set; } = "37"; }
    public sealed class CountingProfile : MapperProfile { public CountingProfile() { CreateMap<CountingSource, Number>().Rename(d => d.Value, "Child.Value"); } }
    public class NoDefaultConstructor(int value) { public int Value { get; set; } = value; }
    public interface IRuntimeIdentity { }
    public class ConcreteBase { }
    public class ConcreteDerived : ConcreteBase { }
    public class ConcreteBaseHolder { public ConcreteBase? Value { get; set; } }
    public class ConcreteDerivedHolder { public ConcreteDerived? Value { get; set; } }
    public class RuntimeIdentity : IRuntimeIdentity { }
    public class ObjectIdentitySource { public object? Value { get; set; } }
    public class InterfaceIdentitySource { public IRuntimeIdentity? Value { get; set; } }
    public class RuntimeIdentityTarget { public RuntimeIdentity? Value { get; set; } }
    public class ThrowingSource { public int ReadValue() => throw new InvalidOperationException("EXPRESSION_SECRET"); }
    public sealed class ThrowingProfile : MapperProfile { public ThrowingProfile() { CreateMap<ThrowingSource, Number>().ForMember(d => d.Value, s => s.ReadValue()); } }
    public sealed class PrivacyProfile : MapperProfile { public PrivacyProfile() { CreateMap<SecretText, Number>().Rename(d => d.Value, nameof(SecretText.Password)); } }
    public class Identity { } public class ByteToken { public int Length { get; set; } }
    public class RuleInput { } public class RuleContainer { public RuleInput? Value { get; set; } }
}
