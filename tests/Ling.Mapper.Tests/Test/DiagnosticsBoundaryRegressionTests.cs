using System.Globalization;
using Ling.Mapper;
using Ling.Mapper.Configuration;
using TestConsole.Utils;

namespace TestConsole.Test;

public static class DiagnosticsBoundaryRegressionTests
{
    public static void Run()
    {
        const string secret = "DEEP_DIAGNOSTIC_SECRET_319";
        foreach (var control in new[] { '\0', '\t', '\r', '\n', '\u001b', '\u007f', '\u0085', '\u2028', '\u2029', '\u202e', '\u2066' })
        {
            var error = Capture(() => Config(true, 128).CreateMapper().Map<int>("a" + control + "b"));
            TestHelper.AssertTrue(error.SourceValue == "a b", "preview normalizes control and directional characters");
            TestHelper.AssertTrue(!error.Message.Contains(control), "message excludes control character");
        }
        foreach (var (value, limit, expected) in new[] {
            ("😀x", 0, ""), ("😀x", 1, ""), ("😀x", 2, "😀"), ("😀x", 3, "😀x"),
            ("x😀z", 2, "x"), ("x😀z", 3, "x😀"), ("\ud800x", 2, "\ufffdx"), ("\udc00x", 2, "\ufffdx"),
            ("a\u202eb", 2, "a "), ("e\u0301z", 2, "e\u0301"), ("\U000E0001x", 1, " "), ("\U000E0001x", 2, " x") })
        {
            var mapper = Config(true, limit).CreateMapper();
            var error = Capture(() => mapper.Map<int>(value));
            TestHelper.AssertTrue(error.SourceValue == expected, "bounded valid UTF16 diagnostic preview");
            TestHelper.AssertTrue(error.SourceValue!.Length <= limit, "UTF16 length limit");
            TestHelper.AssertTrue(mapper.Map<int>("42") == 42, "failure preview leaves next root usable");
        }
        foreach (var preview in new[] { false, true })
        {
            var mapper = Config(preview, 128).CreateMapper();
            var bytes = new Root<byte[]> { Inner = new() { Items = new[] { new Box<byte[]> { Value = System.Text.Encoding.UTF8.GetBytes(secret) } } } };
            var error = Capture(() => mapper.Map<Root<int>>(bytes));
            TestHelper.AssertTrue(error.Path == "$.Inner.Items[0].Value", "deep byte failure path");
            TestHelper.AssertTrue(error.SourceValue == (preview ? "[REDACTED]" : null), "deep bytes redacted");
            TestHelper.AssertTrue(!error.ToString().Contains(secret), "deep byte contents never logged");
            Poison.Calls = 0;
            var poison = new Root<Poison> { Inner = new() { Items = new[] { new Box<Poison> { Value = new Poison() } } } };
            error = Capture(() => mapper.Map<Root<int>>(poison));
            TestHelper.AssertTrue(Poison.Calls == 0 && !error.ToString().Contains(secret), "complex ToString never used");
            TestHelper.AssertTrue(error.SourceValue == (preview ? "[REDACTED]" : null), "deep complex redacted");
            TypeConverterRegistry.Register(typeof(ForeignSource), typeof(ForeignTarget), new Func<ForeignSource, ForeignTarget>(_ =>
            {
                var nested = new InvalidOperationException(secret, new Exception(secret)); nested.Data["secret"] = secret; throw nested;
            }));
            error = Capture(() => mapper.Map<Root<ForeignTarget>>(new Root<ForeignSource> { Inner = new() { Items = new[] { new Box<ForeignSource> { Value = new ForeignSource() } } } }));
            TestHelper.AssertTrue(error.Path == "$.Inner.Items[0].Value", "deep converter failure path");
            TestHelper.AssertTrue(!error.ToString().Contains(secret) && error.Data.Count == 0 && error.InnerException!.Data.Count == 0, "deep external exception is a sanitized snapshot");
            var success = mapper.Map<Root<int>>(new Root<string> { Inner = new() { Items = new[] { new Box<string> { Value = "7" } } } });
            TestHelper.AssertTrue(success!.Inner!.Items![0].Value == 7, "deep failure recovery");
        }
    }
    private static MapperConfiguration Config(bool preview, int limit) => new() { IncludeSourceValueInExceptions = preview, MaxDiagnosticValueLength = limit };
    private static MappingException Capture(Action action)
    {
        try { action(); } catch (MappingException error) { return error; }
        throw new InvalidOperationException("Expected mapping failure.");
    }
    public sealed class Root<T> { public Container<T>? Inner { get; set; } }
    public sealed class Container<T> { public Box<T>[]? Items { get; set; } }
    public sealed class Box<T> { public T? Value { get; set; } }
    public sealed class Poison { public static int Calls; public override string ToString() { Calls++; throw new Exception("DEEP_DIAGNOSTIC_SECRET_319"); } }
    public sealed class ForeignSource { }
    public sealed class ForeignTarget { }
}
