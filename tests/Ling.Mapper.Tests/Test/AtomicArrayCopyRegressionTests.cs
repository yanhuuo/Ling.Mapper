using Ling.Mapper;
using Ling.Mapper.Configuration;
using Ling.Mapper.Mapper;
using Ling.Mapper.Models;

namespace TestConsole.Test;

public static class AtomicArrayCopyRegressionTests
{
    public static void Run()
    {
        var mapper = new MapperConfiguration().CreateMapper();
        void Check(bool condition, string message) => TestConsole.Utils.TestHelper.AssertTrue(condition, message);
        foreach (var length in new[] { 0, 3, 100 })
        {
            var input = Enumerable.Range(0, length).Select(i => (byte)i).ToArray();
            var result = mapper.Map<byte[]>(input)!;
            Check(!ReferenceEquals(input, result) && input.SequenceEqual(result), "fresh atomic array with equal values");
            if (length != 0) { result[0] = 201; Check(input[0] == 0, "atomic array mutation isolation"); }
        }
        int?[] nullable = [1, null, 3];
        var nullableCopy = mapper.Map<int?[]>(nullable)!;
        Check(!ReferenceEquals(nullable, nullableCopy) && nullable.SequenceEqual(nullableCopy), "nullable scalar values");
        string?[] strings = ["a", null, "b"];
        var stringsCopy = mapper.Map<string?[]>(strings)!;
        Check(!ReferenceEquals(strings, stringsCopy) && strings.SequenceEqual(stringsCopy), "string null values");
        var bytes = new byte[] { 1, 2, 3 };
        var pair = mapper.Map<ArrayPair>(new ArrayPair { A = bytes, B = bytes })!;
        Check(!ReferenceEquals(pair.A, bytes) && ReferenceEquals(pair.A, pair.B), "atomic arrays preserve shared aliases");
        var pairAgain = mapper.Map<ArrayPair>(new ArrayPair { A = bytes, B = bytes })!;
        Check(!ReferenceEquals(pair.A, pairAgain.A), "atomic arrays remain independent across calls");
        int callbacks = 0;
        var callbackCopy = mapper.Map<byte[]>(bytes, AdaptOptions.Strict, (result, source) =>
        {
            Check(result is byte && source is byte && Equals(result, source), "callback element values");
            callbacks++;
        })!;
        Check(callbacks == bytes.Length && !ReferenceEquals(bytes, callbackCopy), "callback per atomic item preserved");
        callbacks = 0;
        mapper.Map<string?[]>(strings, AdaptOptions.Strict, (_, _) => callbacks++);
        Check(callbacks == 2, "null elements do not acquire new callbacks");
        try
        {
            mapper.Map<byte[]>(bytes, AdaptOptions.Strict, (_, _) => throw new InvalidOperationException("callback marker"));
            throw new Exception("callback exception was swallowed");
        }
        catch (MappingException error)
        {
            Check(error.Path == "$[0]", "callback error keeps indexed path");
        }
        var mutable = new Mutable[] { new() { Value = 4 } };
        var mutableCopy = mapper.Map<Mutable[]>(mutable)!;
        Check(!ReferenceEquals(mutable, mutableCopy) && !ReferenceEquals(mutable[0], mutableCopy[0]) && mutableCopy[0].Value == 4,
            "mutable elements never use shallow array copy");
        var refs = new ReferencedStruct[] { new() { Item = mutable[0] } };
        var refsCopy = mapper.Map<ReferencedStruct[]>(refs)!;
        Check(!ReferenceEquals(refs[0].Item, refsCopy[0].Item) && refsCopy[0].Item.Value == 4, "struct referenced child is independent");
        var projected = mapper.Map<byte[]>(bytes, AdaptOptions.Strict, ignoreNames: ["unused"]);
        Check(projected != null && !ReferenceEquals(projected, bytes) && projected.SequenceEqual(bytes), "scalar ignores keep previous semantics");
        var profileConfig = new MapperConfiguration();
        profileConfig.AddProfile(new AtomicProfile());
        var profiled = profileConfig.CreateMapper().Map<byte[]>(bytes)!;
        Check(!ReferenceEquals(profiled, bytes) && profiled.SequenceEqual(bytes), "explicit atomic element profile remains supported");
        var strictNullMapper = new MapperConfiguration { NullValueHandling = NullValueHandling.Throw }.CreateMapper();
        Check(strictNullMapper.Map<int?[]>(nullable)!.SequenceEqual(nullable), "nullable array nulls retain Throw semantics");
        var unsigned = mapper.Map<uint[]>(new int[] { 1, 2 })!;
        Check(unsigned.SequenceEqual(new uint[] { 1, 2 }), "signed unsigned arrays convert rather than alias");
        var signedConfig = new MapperConfiguration();
        signedConfig.AddProfile(new SignedArrayProfile());
        var signedInput = new SignedArray { Items = (int[])(object)new uint[] { 1, 2 } };
        var signedResult = signedConfig.CreateMapper().Map<SignedArray>(signedInput)!;
        Check(signedResult.Items.GetType() == typeof(int[]) && !ReferenceEquals(signedInput.Items, signedResult.Items) &&
            signedResult.Items.SequenceEqual(new[] { 1, 2 }), "CLR signed/unsigned alias cannot bypass exact runtime array guard");
        // A warm atomic-array plan must not survive replacement by a whole-type converter.
        TypeConverterRegistry.Register(typeof(byte[]), typeof(byte[]), new Func<byte[], byte[]>(input => [91, input[0]]));
        var replaced = mapper.Map<byte[]>(bytes)!;
        Check(replaced.SequenceEqual(new byte[] { 91, 1 }), "hot registry replaces cached atomic array plan");
        TypeConverterRegistry.Register(typeof(byte[]), typeof(byte[]), new Func<byte[], byte[]>(input => [92]));
        Check(mapper.Map<byte[]>(bytes)!.SequenceEqual(new byte[] { 92 }), "second hot registry replacement");
        Console.WriteLine("PASS atomic array independence, aliases, callbacks, diagnostics, nullable, mutable guards and registry replacement");
    }

    public sealed class SignedArray { public int[] Items { get; set; } = []; }
    public sealed class SignedArrayProfile : MapperProfile { public SignedArrayProfile() { CreateMap<int[], int[]>(); } }
    public sealed class AtomicProfile : MapperProfile { public AtomicProfile() { CreateMap<byte, byte>(); } }
    public sealed class ArrayPair { public byte[] A { get; set; } = []; public byte[] B { get; set; } = []; }
    public sealed class Mutable { public int Value { get; set; } }
    public struct ReferencedStruct { public Mutable Item { get; set; } }
}
