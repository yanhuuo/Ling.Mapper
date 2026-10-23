using System.Globalization;
using Ling.Mapper.Utils;

namespace Ling.Mapper;

/// <summary>Shared invariant-culture scalar conversions for runtime and generated mapping code.
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
        if (TypeUtils.IsSafelyAssignable(source, target)) return value;
        if (!TypeUtils.IsSimple(source) || !TypeUtils.IsSimple(target)) throw new InvalidCastException("Not a scalar conversion.");
        var culture = CultureInfo.InvariantCulture;
        if (target == typeof(string))
            return value switch
            {
                DateTime date => date.ToString("O", culture),
                DateTimeOffset date => date.ToString("O", culture),
                DateOnly date => date.ToString("O", culture),
                TimeOnly time => time.ToString("O", culture),
                TimeSpan time => time.ToString("c", culture),
                IFormattable formattable => formattable.ToString(null, culture),
                _ => System.Convert.ToString(value, culture)!
            };
        if (target.IsEnum)
        {
            if (value is string text) return Enum.Parse(target, text, true);
            if (!source.IsEnum && !TypeUtils.IsNumeric(source)) throw new InvalidCastException();
            return Enum.ToObject(target, System.Convert.ChangeType(value, Enum.GetUnderlyingType(target), culture));
        }
        if (value is string s)
        {
            if (target == typeof(Guid)) return Guid.Parse(s);
            if (target == typeof(DateTime)) return DateTime.Parse(s, culture, DateTimeStyles.AdjustToUniversal);
            if (target == typeof(DateTimeOffset)) return DateTimeOffset.Parse(s, culture, DateTimeStyles.AssumeUniversal);
            if (target == typeof(DateOnly)) return DateOnly.Parse(s, culture);
            if (target == typeof(TimeOnly)) return TimeOnly.Parse(s, culture);
            if (target == typeof(TimeSpan)) return TimeSpan.Parse(s, culture);
        }
        if (value is DateTime dt && target == typeof(DateOnly)) return DateOnly.FromDateTime(dt);
        if (value is DateTime timeValue && target == typeof(TimeOnly)) return TimeOnly.FromDateTime(timeValue);
        if (value is DateOnly day && target == typeof(DateTime)) return day.ToDateTime(TimeOnly.MinValue);
        if (value is IConvertible && typeof(IConvertible).IsAssignableFrom(target))
            return System.Convert.ChangeType(value, target, culture);
        throw new InvalidCastException("Unsupported scalar conversion.");
    }
}
