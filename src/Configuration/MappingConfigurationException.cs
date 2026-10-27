using System;
using System.Collections.Generic;
using System.Linq;

namespace Ling.Mapper.Configuration;

/// <summary>One configuration validation error, identified by type pair and destination member path.</summary>
public sealed class MappingConfigurationError
{
    /// <summary>Source type of the registered map.</summary>
    public Type SourceType { get; }
    /// <summary>Destination type of the registered map.</summary>
    public Type DestinationType { get; }
    /// <summary>Destination member path; "$" denotes the entire type pair.</summary>
    public string MemberPath { get; }
    /// <summary>Actionable description of the invalid configuration.</summary>
    public string Message { get; }

    internal MappingConfigurationError(Type source, Type destination, string path, string message)
    {
        SourceType = source;
        DestinationType = destination;
        MemberPath = path;
        Message = message;
    }

    /// <inheritdoc />
    public override string ToString() => $"{SourceType.FullName} -> {DestinationType.FullName} [{MemberPath}]: {Message}";
}

/// <summary>Aggregate of invalid registered mapping configurations.</summary>
public sealed class MappingConfigurationException : InvalidOperationException
{
    /// <summary>Individual errors with source/destination types and member paths.</summary>
    public IReadOnlyList<MappingConfigurationError> Errors { get; }

    internal MappingConfigurationException(IEnumerable<MappingConfigurationError> errors)
        : this(errors.ToArray()) { }

    private MappingConfigurationException(MappingConfigurationError[] errors)
        : base("Mapping configuration is invalid:" + Environment.NewLine + string.Join(Environment.NewLine, errors.Select(error => " - " + error)))
        => Errors = Array.AsReadOnly(errors);
}
