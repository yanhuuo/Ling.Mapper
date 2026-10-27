using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Ling.Mapper.Helpers;
using Ling.Mapper.Models;
using Ling.Mapper.Utils;

namespace Ling.Mapper.Configuration;

internal static class MappingConfigurationValidator
{
    internal static void Validate(MapperConfiguration configuration)
    {
        var errors = new List<MappingConfigurationError>();
        var options = configuration.EffectiveDefaultOptions;
        var selectedMappings = new List<IMappingConfig>();
        foreach (var group in configuration.Configs.GroupBy(item => (item.SourceType, item.DestType)))
        {
            // A registered whole-object converter supplies the final result: member profiles are not involved.
            if (TypeConverterRegistry.TryGetEffective(group.Key.SourceType, group.Key.DestType, out _)) continue;
            if (group.Count(item => !item.IsReverseGenerated) > 1)
                errors.Add(new(group.Key.SourceType, group.Key.DestType, "$", "Multiple explicit maps target this type pair. Keep one explicit map or combine its rules."));
            var selected = group.OrderBy(item => item.IsReverseGenerated).First();
            selectedMappings.Add(selected);
            ValidatePair(selected, configuration, options, errors);
        }
        var plans = new Mapper.Mapper(configuration, compileConfiguredMaps: false);
        foreach (var mapping in selectedMappings)
        {
            if (errors.Any(error => error.SourceType == mapping.SourceType && error.DestinationType == mapping.DestType)) continue;
            try { plans.ValidateConfiguredPlan(mapping.SourceType, mapping.DestType); }
            catch (Exception error) when (error is ArgumentException or InvalidOperationException or NotSupportedException or MissingMethodException or AmbiguousMatchException)
            {
                errors.Add(new(mapping.SourceType, mapping.DestType, "$", $"The mapping plan cannot be built: {error.Message}"));
            }
        }
        if (errors.Count != 0) throw new MappingConfigurationException(errors);
    }

    private static void ValidatePair(IMappingConfig mapping, MapperConfiguration configuration, AdaptOptions options, List<MappingConfigurationError> errors)
    {
        var source = mapping.SourceType;
        var destination = mapping.DestType;
        var rules = new MappingExpressionBase(mapping.ExpressionObject);
        var ignoreCase = options.HasFlag(AdaptOptions.IgnoreCase);
        var ignoreUnderscore = options.HasFlag(AdaptOptions.IgnoreUnderscore);
        var conventions = configuration.GlobalOptions;
        void Error(string path, string message) => errors.Add(new(source, destination, path, message));
        bool Ignored(string path) => rules.IgnoredPaths.Any(ignored => path == ignored || path.StartsWith(ignored + ".", StringComparison.Ordinal));

        if (TypeUtils.IsSimple(source) || TypeUtils.IsSimple(destination) || TypeUtils.IsCollection(source) || TypeUtils.IsCollection(destination))
        {
            if (rules.ConfiguredPaths.Any(path => !Ignored(path)))
                Error("$", "Member rules require an object mapping; scalar and collection roots use their conversion/collection rules instead.");
            return;
        }

        var publicProperties = destination.GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(property => property.GetIndexParameters().Length == 0).ToArray();
        if (rules.ConstructionFactory == null && (destination.IsAbstract || destination.IsInterface))
            Error("$", "An abstract/interface destination requires ConstructUsing or a registered whole-object converter.");
        var constructorMapping = rules.ConstructionFactory == null && !destination.IsInterface && !destination.IsAbstract &&
            ((!destination.IsValueType && destination.GetConstructor(Type.EmptyTypes) == null) ||
             (destination.IsValueType && !publicProperties.Any(property => property.SetMethod?.IsPublic == true)));
        var constructorParameters = constructorMapping
            ? destination.GetConstructors(BindingFlags.Public | BindingFlags.Instance).SelectMany(constructor => constructor.GetParameters()).ToArray()
            : Array.Empty<ParameterInfo>();
        // Required constructor inputs are checked by metadata-only plan building below.
        // Do not demand sources for readonly properties belonging only to an unselected constructor overload.
        var paths = publicProperties.Where(property => property.SetMethod?.IsPublic == true)
            .Select(property => property.Name).Concat(rules.ConfiguredPaths).Distinct(StringComparer.Ordinal);

        foreach (var path in paths)
        {
            if (Ignored(path)) continue;
            if (!ValidateDestinationPath(destination, path, constructorMapping, constructorParameters, out var destinationError))
            {
                Error(path, destinationError!);
                continue;
            }
            var finalConverter = rules.GetMemberOptions(path)?.FinalConverter;
            if (rules.GetCustomBinding(path) is { } selection)
            {
                if (finalConverter != null && !TypeUtils.IsSafelyAssignable(selection.ReturnType, finalConverter.Parameters[0].Type))
                    Error(path, "Final converter input does not match the selected source member type. Configure a compatible MapFrom; implicit input coercion is not performed.");
                continue;
            }
            var sourceName = rules.GetRenamedSource(path) ?? path;
            try
            {
                var members = Mapper.Mapper.ResolveSourceMemberPath(source, sourceName, ignoreCase, ignoreUnderscore,
                    rules.GetRenamedSource(path) == null && conventions.EnableFlattening,
                    rules.GetRenamedSource(path) == null && conventions.EnableGetMethods);
                if (members != null)
                {
                    var selectedType = members.Last() is PropertyInfo selectedProperty ? selectedProperty.PropertyType : ((MethodInfo)members.Last()).ReturnType;
                    if (finalConverter != null && !TypeUtils.IsSafelyAssignable(selectedType, finalConverter.Parameters[0].Type))
                        Error(path, "Final converter input does not match the conventional source member type. Configure a compatible MapFrom; implicit input coercion is not performed.");
                    continue;
                }
                if (rules.GetRenamedSource(path) == null && rules.ConfiguredPaths.Any(configured => configured.StartsWith(path + ".", StringComparison.Ordinal))) continue;
                if (rules.GetRenamedSource(path) == null && constructorParameters.Any(parameter =>
                    string.Equals(parameter.Name, path, StringComparison.OrdinalIgnoreCase) && parameter.HasDefaultValue)) continue;
                Error(path, $"Source member '{sourceName}' does not exist or is not publicly readable. Configure MapFrom/Rename, Ignore this destination member, or supply an optional constructor parameter.");
            }
            catch (Exception error) when (error is NotSupportedException or AmbiguousMatchException)
            {
                Error(path, $"Source member '{sourceName}' is ambiguous under the selected naming conventions: {error.Message} Use exact matching or an explicit MapFrom selector.");
            }
        }

        foreach (var ignored in rules.IgnoredPaths)
            if (!ValidateDestinationPath(destination, ignored, constructorMapping, constructorParameters, out var ignoredError, requireWritable: false))
                Error(ignored, ignoredError!);
    }

    private static bool ValidateDestinationPath(Type destination, string path, bool constructorMapping, ParameterInfo[] parameters, out string? error, bool requireWritable = true)
    {
        error = null;
        var segments = path.Split('.');
        for (var i = 0; i < segments.Length; i++)
        {
            PropertyInfo? property;
            try { property = destination.GetProperty(segments[i], BindingFlags.Public | BindingFlags.Instance); }
            catch (AmbiguousMatchException)
            {
                error = $"Destination path '{path}' is ambiguous at '{segments[i]}'.";
                return false;
            }
            if (property == null || property.GetIndexParameters().Length != 0)
            {
                error = $"Destination path '{path}' does not identify a public non-indexed property at '{segments[i]}'.";
                return false;
            }
            if (i == segments.Length - 1)
            {
                if (!requireWritable || property.SetMethod?.IsPublic == true || (constructorMapping && segments.Length == 1 &&
                    parameters.Any(parameter => string.Equals(parameter.Name, property.Name, StringComparison.OrdinalIgnoreCase)))) return true;
                error = $"Destination path '{path}' is not publicly writable and is not bound by a constructor parameter.";
                return false;
            }
            if (requireWritable && (property.GetMethod?.IsPublic != true || (property.PropertyType.IsValueType && property.SetMethod?.IsPublic != true)))
            {
                error = $"Destination path '{path}' has an unreadable intermediate property or a value-type intermediate without a public setter at '{segments[i]}'.";
                return false;
            }
            destination = property.PropertyType;
        }
        return true;
    }
}
