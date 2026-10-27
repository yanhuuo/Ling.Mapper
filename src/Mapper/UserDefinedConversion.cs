using System.Collections.Concurrent;
using System.Reflection;
using Ling.Mapper.Utils;

namespace Ling.Mapper;

/// <summary>Discovers one exact user-declared conversion without numeric coercion or chaining.</summary>
internal static class UserDefinedConversion
{
    private sealed record Resolution(MethodInfo? Method, bool Ambiguous);
    private static readonly ConcurrentDictionary<(Type Source, Type Destination), Resolution> Cache = new();

    internal static MethodInfo? Find(Type source, Type destination)
    {
        // Preserve the declared nullable pair for overload selection. Merely
        // boxing a non-null nullable must not erase a more specific declaration.
        if (TypeUtils.IsSimple(source) && TypeUtils.IsSimple(destination)) return null;
        var resolution = Cache.GetOrAdd((source, destination), static pair =>
        {
            var sourceValue = Nullable.GetUnderlyingType(pair.Source) ?? pair.Source;
            var destinationValue = Nullable.GetUnderlyingType(pair.Destination) ?? pair.Destination;
            var operators = new[] { sourceValue, destinationValue }.Distinct()
                .SelectMany(type => type.GetMethods(BindingFlags.Public | BindingFlags.Static | BindingFlags.DeclaredOnly))
                .Where(method => method.IsSpecialName && !method.ContainsGenericParameters &&
                    method.Name is "op_Implicit" or "op_Explicit" && method.GetParameters().Length == 1)
                .ToArray();
            var matches = operators.Where(method => method.GetParameters()[0].ParameterType == pair.Source && method.ReturnType == pair.Destination).ToArray();
            if (matches.Length == 0)
                matches = operators.Where(method =>
                    (Nullable.GetUnderlyingType(method.GetParameters()[0].ParameterType) ?? method.GetParameters()[0].ParameterType) == sourceValue &&
                    (method.ReturnType == pair.Destination ||
                     (Nullable.GetUnderlyingType(pair.Destination) != null && method.ReturnType == destinationValue)))
                    .ToArray();
            // Nullable promotion/lifting is allowed, but never numeric coercion,
            // chaining, or implicit unwrapping of a nullable operator result.
            return new Resolution(matches.Length == 1 ? matches[0] : null, matches.Length > 1);
        });
        if (resolution.Ambiguous)
            throw new NotSupportedException("Multiple exact user-defined conversions exist. Register an explicit type converter.");
        return resolution.Method;
    }
}
