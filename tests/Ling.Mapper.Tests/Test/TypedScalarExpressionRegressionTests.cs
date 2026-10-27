using System.Globalization;
using System.Linq.Expressions;
using System.Reflection;
using Ling.Mapper;
using Ling.Mapper.Configuration;
using TestConsole.Utils;

namespace TestConsole.Test;

public static class TypedScalarExpressionRegressionTests
{
    public static void Run()
    {
        var build = typeof(ScalarConversion).Assembly.GetType("Ling.Mapper.ScalarConversionExpression")!
            .GetMethod("TryBuild", BindingFlags.Static | BindingFlags.NonPublic)!;
        Expression? Build(Expression input, Type target) => (Expression?)build.Invoke(null, new object[] { input, target });
        var parameter = Expression.Parameter(typeof(string), "text");
        var expression = Build(parameter, typeof(decimal))!;
        var visitor = new BoxVisitor(); visitor.Visit(expression);
        TestHelper.AssertTrue(visitor.Boxes == 0 && expression is MethodCallExpression call &&
            call.Method.DeclaringType == typeof(Convert) && call.Method.Name == "ToDecimal" &&
            call.Method.GetParameters()[0].ParameterType == typeof(string), "String decimal conversion must call a typed BCL overload without object bridge");
        var convert = Expression.Lambda<Func<string, decimal>>(expression, parameter).Compile();
        var original = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("en-US");
            TestHelper.AssertEqual(15m, convert("1,5"), "Compiled conversion reads en-US at execution");
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("de-DE");
            TestHelper.AssertEqual(1.5m, convert("1,5"), "Same compiled conversion reads changed culture at execution");
            // Allocation evidence only: no timing. Warm both routes before measuring.
            decimal checksum = 0;
            for (var i = 0; i < 1000; i++) { checksum += convert("42"); checksum += (decimal)ScalarConversion.Convert("42", typeof(decimal)); }
            var before = GC.GetAllocatedBytesForCurrentThread();
            for (var i = 0; i < 10000; i++) checksum += convert("42");
            var typedBytes = GC.GetAllocatedBytesForCurrentThread() - before;
            before = GC.GetAllocatedBytesForCurrentThread();
            for (var i = 0; i < 10000; i++) checksum += (decimal)ScalarConversion.Convert("42", typeof(decimal));
            var objectBytes = GC.GetAllocatedBytesForCurrentThread() - before;
            Console.WriteLine($"Typed scalar allocation evidence: typed={typedBytes} bytes; object bridge={objectBytes} bytes; 10000 conversions each (not timed).");
            // Permit small fixed runtime overhead observed on some frameworks;
            // the bound is far below even one box per conversion.
            TestHelper.AssertTrue(typedBytes < 1024 && objectBytes >= 10000 * 16, "Typed decimal conversion eliminates per-value boxed result");
            TestHelper.AssertEqual(924000m, checksum, "Allocation loops preserve all results");
            var mapper = new MapperConfiguration().CreateMapper();
            TestHelper.AssertEqual(1.5m, mapper.Map<Target>(new Source { Value = "1,5" })!.Value!.Value, "Nullable target uses same typed conversion");
            TestHelper.AssertTrue(mapper.Map<Target>(new Source())!.Value == null, "Null source retains mapping null policy");
        }
        finally { CultureInfo.CurrentCulture = original; }
        TestHelper.AssertTrue(Build(parameter, typeof(DayOfWeek)) == null, "Enums remain on semantic fallback");
        TestHelper.AssertTrue(Build(parameter, typeof(Half)) == null, "Extended numerics remain on semantic fallback");
    }
    public sealed class Source { public string? Value { get; set; } }
    public sealed class Target { public decimal? Value { get; set; } }
    private sealed class BoxVisitor : ExpressionVisitor
    {
        public int Boxes;
        protected override Expression VisitUnary(UnaryExpression node)
        {
            if (node.NodeType == ExpressionType.Convert && node.Type == typeof(object)) Boxes++;
            return base.VisitUnary(node);
        }
    }
}
