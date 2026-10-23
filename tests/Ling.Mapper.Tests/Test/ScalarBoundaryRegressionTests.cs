using Ling.Mapper;
using Ling.Mapper.Configuration;
using TestConsole.Utils;

namespace TestConsole.Test;

/// <summary>The public scalar helper does not discover collection element types or map objects.</summary>
public static class ScalarBoundaryRegressionTests
{
    public static void Run()
    {
        TestHelper.AssertTrue(Equals(42, ScalarConversion.Convert(42, typeof(int))), "scalar identity");
        TestHelper.AssertTrue(Equals(42, ScalarConversion.Convert(42, typeof(int?))), "nullable scalar identity");
        foreach (var value in new object[] { new object(), new[] { 1 }, new byte[] { 1 }, new List<int> { 1 } })
        {
            var rejected = false;
            try { ScalarConversion.Convert(value, value.GetType()); }
            catch (InvalidCastException) { rejected = true; }
            TestHelper.AssertTrue(rejected, "complex identity is outside the scalar helper contract");
        }
        var mapper = new MapperConfiguration().CreateMapper();
        var source = new[] { 1, 2 };
        TestHelper.AssertTrue(ReferenceEquals(source, mapper.Map<int[]>(source)), "mapper still preserves safe array identity");
        TestHelper.AssertTrue(ReferenceEquals(source, mapper.Map<object>(source)), "mapper still assigns array to object");
        TestHelper.AssertTrue(mapper.Map<uint[]>(source)!.SequenceEqual(new uint[] { 1, 2 }), "signed array conversion remains element-wise");
    }
}
