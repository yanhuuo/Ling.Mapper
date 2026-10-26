using System.Text;
using Ling.Mapper;
using Ling.Mapper.Configuration;
using Ling.Mapper.Models;

namespace TestConsole.Test;

/// <summary>Expression resolution alone tolerates null exceptions; assignment/conversion does not.</summary>
public static class CustomExpressionNullRegressionTests
{
    public static void Run()
    {
        int count = 0;
        void Equal(object? expected, object? actual) { count++; if (!Equals(expected, actual)) throw new Exception($"Expected {expected ?? "null"}; got {actual ?? "null"}."); }
        void Failure(Action action, Type errorType, string path)
        {
            count++;
            try { action(); } catch (MappingException e) { if (e.OriginalExceptionType == errorType.FullName && e.Path == path) return; throw; }
            throw new Exception("Expected mapping failure.");
        }
        var config = new MapperConfiguration(); config.AddProfile(new Profile()); var mapper = config.CreateMapper();
        var source = new Input();
        Equal(null, mapper.Map<Text>(source)!.Value);
        Equal("INITIAL", mapper.Map<Text>(source, AdaptOptions.IgnoreNullValues)!.Value);
        Equal("", mapper.Map<Text>(new Input { Bytes = [] })!.Value);
        Equal("hello", mapper.Map<Text>(new Input { Bytes = Encoding.UTF8.GetBytes("hello") })!.Value);
        Equal(null, mapper.Map<Chain>(source)!.Value);
        Equal("nested", mapper.Map<Chain>(new Input { Child = new Child { Value = "nested" } })!.Value);
        Equal(0, mapper.Map<Length>(source)!.Value);
        Equal(null, mapper.Map<NullableLength>(source)!.Value);
        Equal("FALLBACK", mapper.Map<Fallback>(source)!.Value);
        Equal("FALLBACK", mapper.Map<Fallback>(source, AdaptOptions.IgnoreNullValues)!.Value);
        Equal(null, mapper.Map<MethodNull>(source)!.Value); // Explicit user ArgumentNullException, matching expression MapFrom.
        Equal(null, mapper.Map<MethodReference>(source)!.Value);
        Equal(null, mapper.Map<Immutable>(source).Value);
        Equal(null, mapper.Map<Immutable>(source, AdaptOptions.IgnoreNullValues).Value);
        Equal(0, mapper.Map<MethodValue>(source)!.Value);
        Failure(() => mapper.Map<NullableAccess>(source), typeof(InvalidOperationException), "$.Value");
        Failure(() => mapper.Map<ImmutableParsed>(source), typeof(FormatException), "$.Value");
        Failure(() => mapper.Map<ImmutableConverted>(new Input { Amount = 2147483648m }), typeof(OverflowException), "$.Value");
        Failure(() => mapper.Map<Text>(source, AdaptOptions.Default, (_, _) => throw new ArgumentNullException("callback")), typeof(ArgumentNullException), "$");
        Equal("hello", mapper.Map<Immutable>(new Input { Bytes = Encoding.UTF8.GetBytes("hello") })!.Value);
        Equal(null, mapper.Map<Nested>(source)!.Child!.Value);
        Equal(null, mapper.Map<Nested>(source, AdaptOptions.IgnoreNullValues)!.Child); // Skip assignment/child creation.
        Equal("INITIAL", mapper.Map<Text>(source, AdaptOptions.Default, ignoreNames: ["Value"])!.Value);
        Equal(0, source.Reads);
        var once = new Input { Counted = Encoding.UTF8.GetBytes("once") };
        Equal("once", mapper.Map<Counted>(once)!.Value); Equal(1, once.Reads);
        Failure(() => mapper.Map<Parsed>(source), typeof(FormatException), "$.Value");
        Failure(() => mapper.Map<Parsed>(new Input { Amount = 1.00m }), typeof(FormatException), "$.Value");
        Failure(() => mapper.Map<Converted>(new Input { Amount = 2147483648m }), typeof(OverflowException), "$.Value");
        Equal(null, mapper.Map<Converted>(source)!.Value);
        Equal(2, mapper.Map<Converted>(new Input { Amount = 1.5m })!.Value);
        Equal(2, mapper.Map<Converted>(new Input { Amount = 2.5m })!.Value);
        Failure(() => mapper.Map<Business>(source), typeof(InvalidOperationException), "$.Value");
        Failure(() => mapper.Map<Setter>(source), typeof(ArgumentNullException), "$.Value");
        Failure(() => mapper.Map<ThrowingConstructor>(source), typeof(ArgumentNullException), "$");
        Failure(() => mapper.Map<Automatic>(new AutomaticSource()), typeof(ArgumentNullException), "$.Value");
        Failure(() => mapper.Map<NestedBad>(source), typeof(FormatException), "$.Child.Value");
        Failure(() => mapper.Map<List<Parsed>>(new[] { source }), typeof(FormatException), "$[0].Value");
        // Registered converters run after the safe expression resolution; their failures must not disappear.
        TypeConverterRegistry.Register(typeof(Token), typeof(string), new Func<Token, string>(_ => throw new ArgumentNullException("converter")));
        Failure(() => mapper.Map<ConverterTarget>(new ConverterSource()), typeof(ArgumentNullException), "$.Value");
        var strictConfig = new MapperConfiguration { NullValueHandling = NullValueHandling.Throw }; strictConfig.AddProfile(new Profile());
        Equal(null, strictConfig.CreateMapper().Map<NullableLength>(source)!.Value);
        Equal(0, strictConfig.CreateMapper().Map<Length>(source)!.Value); // Expression type default, not source-null conversion.
        Console.WriteLine($"Custom expression null regression: {count}/{count} passed.");
    }
    public sealed class MethodValue { public int Value { get; set; } = 99; }
    public sealed class NullableAccess { public decimal Value { get; set; } }
    public readonly struct ImmutableParsed { public int? Value { get; } public ImmutableParsed(int? value) => Value = value; }
    public readonly struct ImmutableConverted { public int? Value { get; } public ImmutableConverted(int? value) => Value = value; }
    private static int NullValueMethod() => throw new ArgumentNullException("explicit");
    public sealed class Input { public byte[]? Bytes { get; set; } public Child? Child { get; set; } public decimal? Amount { get; set; } public byte[]? Counted { get; set; } public int Reads; public byte[]? Once { get { Reads++; return Counted; } } }
    public sealed class Child { public string? Value { get; set; } }
    public sealed class Text { public string? Value { get; set; } = "INITIAL"; }
    public sealed class Chain { public string? Value { get; set; } }
    public sealed class Length { public int Value { get; set; } = 99; }
    public sealed class NullableLength { public int? Value { get; set; } = 99; }
    public sealed class Fallback { public string? Value { get; set; } }
    public sealed class MethodNull { public string? Value { get; set; } }
    public sealed class MethodReference { public string? Value { get; set; } }
    public readonly struct Immutable { public string? Value { get; } public Immutable(string? value) => Value = value; }
    public sealed class Nested { public Child? Child { get; set; } }
    public sealed class Counted { public string? Value { get; set; } }
    public sealed class Parsed { public int? Value { get; set; } }
    public sealed class Converted { public int? Value { get; set; } }
    public sealed class Business { public string? Value { get; set; } }
    public sealed class Setter { public string? Value { get => null; set => throw new ArgumentNullException("setter"); } }
    public sealed class ThrowingConstructor { public string? Value { get; set; } public ThrowingConstructor() => throw new ArgumentNullException("ctor"); }
    public sealed class AutomaticSource { public string Value => throw new ArgumentNullException("getter"); }
    public sealed class Automatic { public string? Value { get; set; } }
    public sealed class NestedBad { public Child? Child { get; set; } }
    public sealed class Token { }
    public sealed class ConverterSource { public Token Value { get; set; } = new(); }
    public sealed class ConverterTarget { public string? Value { get; set; } }
    private static string NullMethod() => throw new ArgumentNullException("explicit");
    private static string ReferenceMethod() => throw new NullReferenceException("explicit");
    private static string BusinessMethod() => throw new InvalidOperationException("business");
    private static string BadMethod() => throw new FormatException("invalid");
    public sealed class Profile : MapperProfile
    {
        public Profile()
        {
            CreateMap<Input, MethodValue>().ForMember(d => d.Value, s => NullValueMethod());
            CreateMap<Input, NullableAccess>().ForMember(d => d.Value, s => s.Amount!.Value);
            CreateMap<Input, ImmutableParsed>().ForMember(d => d.Value, s => (int?)int.Parse(s.Amount.ToString()!));
            CreateMap<Input, ImmutableConverted>().ForMember(d => d.Value, s => s.Amount.HasValue ? (int?)Convert.ToInt32(s.Amount.Value) : null);
            CreateMap<Input, Text>().ForMember(d => d.Value, s => Encoding.UTF8.GetString(s.Bytes!));
            CreateMap<Input, Chain>().ForMember(d => d.Value, s => s.Child!.Value);
            CreateMap<Input, Length>().ForMember(d => d.Value, s => s.Child!.Value!.Length);
            CreateMap<Input, NullableLength>().ForMember(d => d.Value, s => (int?)s.Child!.Value!.Length);
            CreateMap<Input, Fallback>().ForMember(d => d.Value, s => s.Bytes == null ? "FALLBACK" : Encoding.UTF8.GetString(s.Bytes));
            CreateMap<Input, MethodNull>().ForMember(d => d.Value, s => NullMethod());
            CreateMap<Input, MethodReference>().ForMember(d => d.Value, s => ReferenceMethod());
            CreateMap<Input, Immutable>().ForMember(d => d.Value, s => Encoding.UTF8.GetString(s.Bytes!));
            CreateMap<Input, Nested>().ForMember(d => d.Child!.Value, s => Encoding.UTF8.GetString(s.Bytes!));
            CreateMap<Input, Counted>().ForMember(d => d.Value, s => Encoding.UTF8.GetString(s.Once!));
            CreateMap<Input, Parsed>().ForMember(d => d.Value, s => (int?)int.Parse(s.Amount.ToString()!));
            CreateMap<Input, Converted>().ForMember(d => d.Value, s => s.Amount.HasValue ? (int?)Convert.ToInt32(s.Amount.Value) : null);
            CreateMap<Input, Business>().ForMember(d => d.Value, s => BusinessMethod());
            CreateMap<Input, Setter>().ForMember(d => d.Value, s => "value");
            CreateMap<Input, ThrowingConstructor>().ForMember(d => d.Value, s => "value");
            CreateMap<Input, NestedBad>().ForMember(d => d.Child!.Value, s => BadMethod());
        }
    }
}
