using System.Collections.Concurrent;
using System.Reflection;
using System.Runtime.Serialization;

namespace Ling.Mapper;

/// <summary>EnumMember aliases extend, rather than replace, CLR enum names and numeric text.</summary>
internal static class EnumTextConversion
{
    private sealed record Metadata(Dictionary<string, string> NamesToAliases, Dictionary<string, string> AliasesToNames);
    private static readonly ConcurrentDictionary<Type, Lazy<Metadata>> Cache = new();

    private static Metadata GetMetadata(Type type) => Cache.GetOrAdd(type, static enumType =>
        new Lazy<Metadata>(() => Build(enumType), System.Threading.LazyThreadSafetyMode.ExecutionAndPublication)).Value;

    private static Metadata Build(Type type)
    {
        var namesToAliases = new Dictionary<string, string>(StringComparer.Ordinal);
        var aliasesToNames = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var aliasValues = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
        var flags = type.IsDefined(typeof(FlagsAttribute), false);
        var fields = type.GetFields(BindingFlags.Public | BindingFlags.Static);
        foreach (var field in fields)
        {
            var alias = field.GetCustomAttribute<EnumMemberAttribute>()?.Value;
            if (alias == null) continue;
            if (string.IsNullOrWhiteSpace(alias) || alias != alias.Trim() || (flags && alias.Contains(',')))
                throw new NotSupportedException("EnumMember aliases must be nonempty, have no surrounding whitespace, and must not contain commas for Flags enums.");
            var value = field.GetValue(null)!;
            // Never let an alias silently change a real CLR name, numeric literal,
            // or an existing flags expression to mean a different enum value.
            var standardParsed = Enum.TryParse(type, alias, true, out var standard);
            if (fields.Any(other => string.Equals(other.Name, alias, StringComparison.OrdinalIgnoreCase) && !Equals(other.GetValue(null), value)) ||
                (standardParsed && !Equals(standard, value)) || (!standardParsed && IsNumericLiteral(alias)) ||
                (aliasValues.TryGetValue(alias, out var previous) && !Equals(previous, value)))
                throw new NotSupportedException("EnumMember alias conflicts with an enum name, numeric value, flags expression, or another alias.");
            namesToAliases.Add(field.Name, alias);
            aliasesToNames[alias] = field.Name;
            aliasValues[alias] = value;
        }
        return new Metadata(namesToAliases, aliasesToNames);
    }

    private static bool IsNumericLiteral(string text)
    {
        var start = text[0] is '+' or '-' ? 1 : 0;
        if (start == text.Length) return false;
        for (var index = start; index < text.Length; index++)
            if (text[index] is < '0' or > '9') return false;
        return true;
    }

    internal static object Parse(Type type, string text)
    {
        var metadata = GetMetadata(type);
        var trimmed = text.Trim();
        if (metadata.AliasesToNames.TryGetValue(trimmed, out var name)) return Enum.Parse(type, name);
        if (metadata.AliasesToNames.Count != 0 && trimmed.Contains(','))
        {
            var parts = trimmed.Split(',');
            for (var index = 0; index < parts.Length; index++)
                if (metadata.AliasesToNames.TryGetValue(parts[index].Trim(), out name)) parts[index] = name;
            trimmed = string.Join(",", parts);
        }
        return Enum.Parse(type, trimmed, true);
    }

    internal static string Format(object value)
    {
        var metadata = GetMetadata(value.GetType());
        var text = value.ToString()!;
        if (metadata.NamesToAliases.TryGetValue(text, out var alias)) return alias;
        if (metadata.NamesToAliases.Count == 0 || !text.Contains(',')) return text;
        var parts = text.Split(',');
        for (var index = 0; index < parts.Length; index++)
        {
            var name = parts[index].Trim();
            parts[index] = metadata.NamesToAliases.TryGetValue(name, out alias) ? alias : name;
        }
        return string.Join(", ", parts);
    }
}
