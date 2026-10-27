using System.Linq.Expressions;
using Ling.Mapper.Models;

namespace Ling.Mapper.Mapper;

/// <summary>Optional capability for building provider-facing expression trees without executing a mapping.</summary>
public interface IProjectionMapper
{
    /// <summary>Build a reusable source-to-destination lambda. Unsupported runtime-only rules throw before query execution.</summary>
    /// <remarks>The query provider remains responsible for translating the returned expression. This is not a promise
    /// that every SQL provider supports every scalar operation, constructor, or nested collection.</remarks>
    LambdaExpression GetProjection(Type sourceType, Type destinationType, AdaptOptions options);
}

/// <summary>A projection cannot represent a configured mapping without runtime-only execution.</summary>
public sealed class ProjectionException : NotSupportedException
{
    internal ProjectionException(Type sourceType, Type destinationType, string memberPath, string reason)
        : base($"Cannot project {sourceType} to {destinationType} at '{memberPath}': {reason}")
    { SourceType = sourceType; DestinationType = destinationType; MemberPath = memberPath; }

    /// <summary>The source type at the unsupported projection boundary.</summary>
    public Type SourceType { get; }
    /// <summary>The destination type at the unsupported projection boundary.</summary>
    public Type DestinationType { get; }
    /// <summary>The destination member path at the unsupported projection boundary.</summary>
    public string MemberPath { get; }
}
