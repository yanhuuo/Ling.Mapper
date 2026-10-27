using System.Globalization;
using Ling.Mapper.Utils;

namespace Ling.Mapper;

/// <summary>Shared scalar conversions for runtime and generated mapping code. Numeric and date text use the culture active at the time of conversion.
/// Does not map objects or collections, or apply registry rules. Type-based enum operations
/// still require appropriate metadata in trimmed applications.</summary>
public static class ScalarConversion
{
    /// <summary>Convert a non-null scalar value, throwing for malformed, overflowing or unsupported input.</summary>
    public static object Convert(object value, Type destinationType)
    {
        ArgumentNullException.ThrowIfNull(value);
        ArgumentNullException.ThrowIfNull(destinationType);
        var target = Nullable.GetUnderlyingType(destinationType) ?? destinationType;
        var source = value.GetType();
        // This public helper handles scalar values only. Object/collection assignment
        // belongs to the mapper and must not pull reflection-based collection discovery into AOT.
        if (!TypeUtils.IsSimple(source) || !TypeUtils.IsSimple(target)) throw new InvalidCastException("Not a scalar conversion.");
        if (target.IsAssignableFrom(source)) return value;
        var culture = CultureInfo.CurrentCulture;
        if (target == typeof(string))
        {
            if (source.IsEnum) return EnumTextConversion.Format(value);
            return value switch
            {
                DateTime date => date.ToString(),
                DateTimeOffset date => date.ToString(),
                DateOnly date => date.ToString(),
                TimeOnly time => time.ToString(),
                TimeSpan time => time.ToString(),
                IFormattable formattable => formattable.ToString(null, culture),
                _ => System.Convert.ToString(value, culture)!
            };
        }
        if (target.IsEnum)
        {
            if (value is string text) return EnumTextConversion.Parse(target, text);
            if (!source.IsEnum && !TypeUtils.IsNumeric(source)) throw new InvalidCastException();
            return Enum.ToObject(target, System.Convert.ChangeType(value, Enum.GetUnderlyingType(target), culture));
        }
        if (value is string s)
        {
            if (target == typeof(Guid)) return Guid.Parse(s);
            if (target == typeof(DateTime)) return System.Convert.ToDateTime(s);
            if (target == typeof(DateTimeOffset)) return DateTimeOffset.Parse(s);
            if (target == typeof(DateOnly)) return DateOnly.Parse(s);
            if (target == typeof(TimeOnly)) return TimeOnly.Parse(s);
            if (target == typeof(TimeSpan)) return TimeSpan.Parse(s);
        }
        if (value is DateTime dt && target == typeof(DateOnly)) return DateOnly.FromDateTime(dt);
        if (value is DateTime timeValue && target == typeof(TimeOnly)) return TimeOnly.FromDateTime(timeValue);
        if (value is DateOnly day && target == typeof(DateTime)) return day.ToDateTime(TimeOnly.MinValue);
        if (value is IConvertible && typeof(IConvertible).IsAssignableFrom(target))
            return System.Convert.ChangeType(value, target, culture);
        throw new InvalidCastException("Unsupported scalar conversion.");
    }
}
