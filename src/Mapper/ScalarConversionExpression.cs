using System.Globalization;
using System.Linq.Expressions;
using Ling.Mapper.Utils;

namespace Ling.Mapper;

/// <summary>Compile-time selection of exact BCL equivalents of ScalarConversion.
/// Input and target are non-nullable scalar types; input is known non-null.
/// The caller owns null policy, registry precedence and mapping error context.</summary>
internal static class ScalarConversionExpression
{
    internal static Expression? TryBuild(Expression input, Type target)
    {
        var source = input.Type;
        // Do not infer enum, native integer or user-defined conversion semantics.
        if (source == typeof(string))
        {
            if (target == typeof(Guid) || target == typeof(DateTimeOffset) ||
                target == typeof(DateOnly) || target == typeof(TimeOnly) || target == typeof(TimeSpan))
                return Expression.Call(target.GetMethod("Parse", new[] { typeof(string) })!, input);
            if (target == typeof(DateTime))
                return Expression.Call(typeof(Convert).GetMethod(nameof(Convert.ToDateTime), new[] { typeof(string) })!, input);
            if (IsConvertiblePrimitive(target))
                return Expression.Call(typeof(Convert).GetMethod("To" + target.Name,
                    new[] { typeof(string), typeof(IFormatProvider) })!, input,
                    Expression.Property(null, typeof(CultureInfo), nameof(CultureInfo.CurrentCulture)));
        }
        // Convert.ChangeType dispatches primitive IConvertible methods to these
        // exact overloads. Use Convert rather than casts to preserve rounding,
        // overflow and unsupported bool/char conversion exceptions.
        if (IsConvertiblePrimitive(source) && IsConvertiblePrimitive(target))
        {
            var method = typeof(Convert).GetMethod("To" + target.Name, new[] { source });
            if (method != null) return Expression.Call(method, input);
        }
        return null;
    }

    private static bool IsConvertiblePrimitive(Type type)
        => TypeUtils.IsNumeric(type) || type == typeof(bool) || type == typeof(char);
}
