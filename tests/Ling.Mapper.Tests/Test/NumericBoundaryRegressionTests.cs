using System.Globalization;
using Ling.Mapper;
using Ling.Mapper.Configuration;
using Ling.Mapper.Mapper;

namespace TestConsole.Test;

/// <summary>
/// Deterministic boundary matrix, not an exhaustive proof over every numeric value.
/// BCL Convert.ChangeType with InvariantCulture is an independent oracle. Each
/// input/target pair is checked through root, member, collection, and nested paths.
/// </summary>
public static class NumericBoundaryRegressionTests
{
    private static readonly Type[] NumericTypes =
    [
        typeof(sbyte), typeof(byte), typeof(short), typeof(ushort), typeof(int), typeof(uint),
        typeof(long), typeof(ulong), typeof(float), typeof(double), typeof(decimal)
    ];

    public static void Run()
    {
        var mapper = new MapperConfiguration().CreateMapper();
        var failures = new List<Exception>();
        var counts = new int[4];
        var successfulValues = new int[4];
        var expectedErrors = new int[4];
        var names = new[] { "root", "member", "collection", "nested" };
        var paths = new[] { "$", "$.Value", "$[0]", "$.Child.Value" };
        var inputs = BuildInputs();
        var cases = 0;

        foreach (var input in inputs)
        foreach (var target in NumericTypes)
        {
            object? expected = null;
            Exception? oracleError = null;
            try { expected = Convert.ChangeType(input.Value, target, CultureInfo.InvariantCulture); }
            catch (Exception error) when (error is FormatException or OverflowException or InvalidCastException)
            { oracleError = error; }

            for (var entry = 0; entry < names.Length; entry++)
            {
                counts[entry]++;
                cases++;
                var label = $"{input.Source.Name}/{input.Label} -> {target.Name} ({names[entry]})";
                try
                {
                    object? actual;
                    try { actual = Execute(mapper, input.Value, input.Source, target, entry); }
                    catch (MappingException error)
                    {
                        if (oracleError == null)
                            throw new Exception("Expected successful BCL conversion, but mapping failed.", error);
                        Require(error.SourceType == input.Source, $"SourceType expected {input.Source}, actual {error.SourceType}");
                        Require(error.DestinationType == target, $"DestinationType expected {target}, actual {error.DestinationType}");
                        Require(error.Path == paths[entry], $"Path expected {paths[entry]}, actual {error.Path}");
                        Require(error.OriginalExceptionType == oracleError.GetType().FullName,
                            $"Original exception expected {oracleError.GetType().FullName}, actual {error.OriginalExceptionType}");
                        expectedErrors[entry]++;
                        continue;
                    }
                    if (oracleError != null)
                        throw new Exception($"Expected {oracleError.GetType().Name} wrapped in MappingException, but mapping succeeded.");
                    // Object.Equals handles NaN == NaN for boxed float/double and checks the
                    // resulting CLR numeric type as well as the value. No lossy coercion.
                    Require(Equals(expected, actual), $"Expected {Render(expected)}, actual {Render(actual)}");
                    Require(actual?.GetType() == target, $"Result CLR type expected {target}, actual {actual?.GetType()}");
                    successfulValues[entry]++;
                }
                catch (Exception error)
                {
                    failures.Add(new Exception(label, error));
                    Console.WriteLine($"FAIL numeric boundary: {label}: {error.Message}");
                }
            }
        }

        Console.WriteLine($"Numeric boundary regression: {cases - failures.Count}/{cases} passed; {inputs.Count} fixed inputs; {NumericTypes.Length} numeric targets.");
        for (var entry = 0; entry < names.Length; entry++)
            Console.WriteLine($"  {names[entry]}: {counts[entry]} checks, {successfulValues[entry]} successful-value matches, {expectedErrors[entry]} expected-error contracts.");
        Console.WriteLine("Coverage: integer min/max/0/1/-1 where representable; floating min/max/epsilon/NaN/infinities/half-way rounding; decimal bounds/tiny fractions; fixed valid and invalid numeric strings. This is a finite boundary set, not exhaustive coverage.");
        if (failures.Count != 0) throw new AggregateException("Numeric boundary regression failures", failures);
    }

    private static object? Execute(IMapper mapper, object value, Type source, Type target, int entry)
    {
        if (entry == 0) return mapper.Map(value, source, target);
        if (entry == 2)
        {
            var array = Array.CreateInstance(source, 1);
            array.SetValue(value, 0);
            var result = (Array)mapper.Map(array, array.GetType(), target.MakeArrayType())!;
            Require(result.Length == 1, "Collection output length changed");
            return result.GetValue(0);
        }
        var sourceBoxType = typeof(Box<>).MakeGenericType(source);
        var targetBoxType = typeof(Box<>).MakeGenericType(target);
        var box = Activator.CreateInstance(sourceBoxType)!;
        sourceBoxType.GetProperty(nameof(Box<int>.Value))!.SetValue(box, value);
        if (entry == 1)
        {
            var mapped = mapper.Map(box, sourceBoxType, targetBoxType)!;
            return targetBoxType.GetProperty(nameof(Box<int>.Value))!.GetValue(mapped);
        }
        var sourceOuterType = typeof(Outer<>).MakeGenericType(source);
        var targetOuterType = typeof(Outer<>).MakeGenericType(target);
        var outer = Activator.CreateInstance(sourceOuterType)!;
        sourceOuterType.GetProperty(nameof(Outer<int>.Child))!.SetValue(outer, box);
        var mappedOuter = mapper.Map(outer, sourceOuterType, targetOuterType)!;
        var mappedChild = targetOuterType.GetProperty(nameof(Outer<int>.Child))!.GetValue(mappedOuter)!;
        return targetBoxType.GetProperty(nameof(Box<int>.Value))!.GetValue(mappedChild);
    }

    private static List<Input> BuildInputs()
    {
        var result = new List<Input>();
        void Add<T>(params (string Label, T Value)[] values) where T : notnull
        {
            foreach (var (label, value) in values) result.Add(new Input(typeof(T), label, value));
        }
        Add<sbyte>(("min", sbyte.MinValue), ("max", sbyte.MaxValue), ("zero", 0), ("one", 1), ("minus-one", -1));
        Add<byte>(("min-zero", byte.MinValue), ("max", byte.MaxValue), ("one", 1));
        Add<short>(("min", short.MinValue), ("max", short.MaxValue), ("zero", 0), ("one", 1), ("minus-one", -1));
        Add<ushort>(("min-zero", ushort.MinValue), ("max", ushort.MaxValue), ("one", 1));
        Add<int>(("min", int.MinValue), ("max", int.MaxValue), ("zero", 0), ("one", 1), ("minus-one", -1));
        Add<uint>(("min-zero", uint.MinValue), ("max", uint.MaxValue), ("one", 1));
        Add<long>(("min", long.MinValue), ("max", long.MaxValue), ("zero", 0), ("one", 1), ("minus-one", -1));
        Add<ulong>(("min-zero", ulong.MinValue), ("max", ulong.MaxValue), ("one", 1));
        Add<float>(("min", float.MinValue), ("max", float.MaxValue), ("zero", 0f), ("negative-zero", -0f), ("one", 1f), ("minus-one", -1f),
            ("epsilon", float.Epsilon), ("negative-epsilon", -float.Epsilon), ("positive-infinity", float.PositiveInfinity), ("negative-infinity", float.NegativeInfinity), ("NaN", float.NaN),
            ("one-half", 1.5f), ("negative-one-half", -1.5f), ("two-half", 2.5f), ("three-half", 3.5f), ("negative-two-half", -2.5f), ("negative-three-half", -3.5f),
            ("signed-byte-edge", 127.5f), ("unsigned-byte-edge", 255.5f));
        Add<double>(("min", double.MinValue), ("max", double.MaxValue), ("zero", 0d), ("negative-zero", -0d), ("one", 1d), ("minus-one", -1d),
            ("epsilon", double.Epsilon), ("negative-epsilon", -double.Epsilon), ("positive-infinity", double.PositiveInfinity), ("negative-infinity", double.NegativeInfinity), ("NaN", double.NaN),
            ("one-half", 1.5d), ("negative-one-half", -1.5d), ("two-half", 2.5d), ("three-half", 3.5d), ("negative-two-half", -2.5d), ("negative-three-half", -3.5d),
            ("signed-int-edge", 2147483647.5d), ("unsigned-int-edge", 4294967295.5d));
        Add<decimal>(("min", decimal.MinValue), ("max", decimal.MaxValue), ("zero", 0m), ("one", 1m), ("minus-one", -1m),
            ("smallest-positive", 0.0000000000000000000000000001m), ("smallest-negative", -0.0000000000000000000000000001m),
            ("one-half", 1.5m), ("negative-one-half", -1.5m), ("two-half", 2.5m), ("three-half", 3.5m), ("negative-two-half", -2.5m), ("negative-three-half", -3.5m),
            ("long-max-exact", long.MaxValue), ("long-max-plus-one", (decimal)long.MaxValue + 1m), ("long-min-minus-one", (decimal)long.MinValue - 1m),
            ("ulong-max-exact", ulong.MaxValue), ("ulong-max-plus-one", (decimal)ulong.MaxValue + 1m));
        Add<string>(("zero", "0"), ("negative-zero", "-0"), ("plus-one", "+1"), ("minus-one", "-1"), ("decimal-half", "2.5"), ("negative-half", "-1.5"),
            ("leading-and-trailing-space", " 42 "), ("tabs-and-newline", "\t42\r\n"), ("empty", ""), ("whitespace-only", "   "),
            ("signed-byte-underflow", "-129"), ("signed-byte-overflow", "128"), ("unsigned-byte-overflow", "256"),
            ("int-min", "-2147483648"), ("int-overflow", "2147483648"), ("uint-overflow", "4294967296"),
            ("long-min", "-9223372036854775808"), ("long-max", "9223372036854775807"), ("long-overflow", "9223372036854775808"),
            ("ulong-max", "18446744073709551615"), ("ulong-overflow", "18446744073709551616"),
            ("decimal-max", "79228162514264337593543950335"), ("decimal-overflow", "79228162514264337593543950336"),
            ("exponent", "1e3"), ("negative-exponent", "1e-3"), ("huge-exponent", "1e9999"), ("tiny-exponent", "1e-9999"),
            ("thousands", "1,234.50"), ("comma-only", "1,234"), ("decimal-comma", "1,5"),
            ("NaN", "NaN"), ("positive-infinity", "Infinity"), ("negative-infinity", "-Infinity"),
            ("unicode-minus", "−1"), ("fullwidth-digits", "１２３"), ("arabic-indic-digits", "١٢٣"), ("nonbreaking-space", "\u00a042\u00a0"),
            ("currency-symbol", "$12.50"), ("parentheses", "(12)"), ("underscore", "1_000"), ("hex-prefix", "0xFF"), ("trailing-junk", "12x"),
            ("extremely-long", new string('9', 4096)));
        return result;
    }

    private static void Require(bool condition, string message) { if (!condition) throw new Exception(message); }
    private static string Render(object? value) => value is IFormattable formattable ? formattable.ToString(null, CultureInfo.InvariantCulture) : value?.ToString() ?? "null";
    private sealed record Input(Type Source, string Label, object Value);
    public class Box<T> { public T? Value { get; set; } }
    public class Outer<T> { public Box<T>? Child { get; set; } }
}
