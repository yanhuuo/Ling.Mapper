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
        var arrayCopy = mapper.Map<int[]>(source)!;
        var objectCopy = (int[])mapper.Map<object>(source)!;
        TestHelper.AssertTrue(!ReferenceEquals(source, arrayCopy) && arrayCopy.SequenceEqual(source), "mapper independently clones same-type arrays");
        TestHelper.AssertTrue(!ReferenceEquals(source, objectCopy) && objectCopy.SequenceEqual(source), "object target preserves array shape without aliasing");
        arrayCopy[0] = 9; objectCopy[1] = 8;
        TestHelper.AssertTrue(source.SequenceEqual(new[] { 1, 2 }), "mapped array mutations cannot affect source");
        TestHelper.AssertTrue(mapper.Map<uint[]>(source)!.SequenceEqual(new uint[] { 1, 2 }), "signed array conversion remains element-wise");
    }
}
