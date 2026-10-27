using System.Globalization;
using System.Text;
using Ling.Mapper.Configuration;
using Ling.Mapper.Utils;

namespace Ling.Mapper;

/// <summary>Policy for mapping null to a non-nullable destination.</summary>
public enum NullValueHandling
{
    /// <summary>Use the destination type's default value.</summary>
    UseDefault,
    /// <summary>Throw a MappingException.</summary>
    Throw
}

/// <summary>A mapping failure with a full destination path and safe diagnostics.
/// The original exception is deliberately not retained: its message, Data or nested
/// exceptions may contain secrets. InnerException is a type-only safe snapshot.</summary>
public sealed class MappingException : Exception
{
    /// <summary>Declared source type of the failed conversion.</summary>
    public Type SourceType { get; }
    /// <summary>Destination type of the failed conversion.</summary>
    public Type DestinationType { get; }
    /// <summary>Full destination member/index path, rooted at $.</summary>
    public string Path { get; }
    /// <summary>Optional bounded diagnostic preview; never the original object.</summary>
    public string? SourceValue { get; }
    /// <summary>Type name of the original exception. No unsafe original instance is retained.</summary>
    public string OriginalExceptionType { get; }

    internal object? Origin { get; }

    private MappingException(Type source, Type destination, string path, string? preview, Exception error, object? origin)
        : base($"Mapping {source.FullName} to {destination.FullName} failed at {path}." + (preview == null ? "" : $" Source value: {preview}."),
            new Exception($"Original exception type: {error.GetType().FullName}. Original details omitted for data safety."))
    {
        Origin = origin;
        SourceType = source;
        DestinationType = destination;
        Path = path;
        SourceValue = preview;
        OriginalExceptionType = error.GetType().FullName ?? error.GetType().Name;
    }

    private static readonly string[] SensitivePathWords = ["password", "passwd", "pwd", "token", "secret", "credential", "encrypt", "cipher", "privatekey", "apikey"];
    internal static bool IsSensitivePath(string path)
    {
        foreach (var word in SensitivePathWords)
            if (path.Contains(word, StringComparison.OrdinalIgnoreCase)) return true;
        return false;
    }

    // Limit counts UTF-16 code units, but never emits half of a valid surrogate pair.
    // Normalize controls before logging; this is a diagnostic copy, never the source value.
    private static string SanitizePreview(string text, int limit)
    {
        var result = new StringBuilder(Math.Min(text.Length, limit));
        for (var index = 0; index < text.Length && result.Length < limit; index++)
        {
            var character = text[index];
            if (char.IsHighSurrogate(character) && index + 1 < text.Length && char.IsLowSurrogate(text[index + 1]))
            {
                if (CharUnicodeInfo.GetUnicodeCategory(text, index) == UnicodeCategory.Format)
                {
                    result.Append(' ');
                    index++;
                    continue;
                }
                if (result.Length + 2 > limit) break;
                result.Append(character).Append(text[++index]);
                continue;
            }
            if (char.IsSurrogate(character)) { result.Append('\uFFFD'); continue; }
            var category = char.GetUnicodeCategory(character);
            result.Append(char.IsControl(character) || category is UnicodeCategory.Format or UnicodeCategory.LineSeparator or UnicodeCategory.ParagraphSeparator ? ' ' : character);
        }
        return result.ToString();
    }

    internal static MappingException Create(Type source, Type destination, string path, object? value, Exception error, MapperConfiguration config, bool sensitive = false, object? origin = null)
    {
        string? preview = null;
        if (config.IncludeSourceValueInExceptions)
        {
            sensitive |= IsSensitivePath(path);
            if (sensitive || value is byte[] || (value != null && !TypeUtils.IsSimple(value.GetType()))) preview = "[REDACTED]";
            else
            {
                preview = value == null ? "null" : (string)ScalarConversion.Convert(value, typeof(string));
                var limit = Math.Max(0, config.MaxDiagnosticValueLength);
                preview = SanitizePreview(preview, limit);
            }
        }
        return new MappingException(source, destination, path, preview, error, origin);
    }
}
