using System.Globalization;
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

    internal static bool IsSensitivePath(string path) => new[] { "password", "passwd", "pwd", "token", "secret", "credential", "encrypt", "cipher", "privatekey", "apikey" }.Any(word => path.Contains(word, StringComparison.OrdinalIgnoreCase));

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
                if (preview.Length > limit) preview = preview.Substring(0, limit);
                preview = preview.Replace("\r", " ").Replace("\n", " ");
            }
        }
        return new MappingException(source, destination, path, preview, error, origin);
    }
}
