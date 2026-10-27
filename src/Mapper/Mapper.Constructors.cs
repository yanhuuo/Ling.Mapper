using System.Linq.Expressions;
using System.Reflection;
using Ling.Mapper.Helpers;
using Ling.Mapper.Models;
using Ling.Mapper.Utils;

namespace Ling.Mapper.Mapper;

internal sealed partial class Mapper
{
    private sealed record ConstructorArgument(ParameterInfo Parameter, string Member, string SourceName,
        Expression? Value, bool IsCustom, bool IsIgnored);

    // Constructor discovery is metadata-only. We do not speculate by invoking a
    // constructor, getter, resolver, or converter and then trying another overload.
    private Expression BuildConstructorExpression(Type sourceType, Type destination, Expression source,
        AdaptOptions options, string[]? ignoreNames, MappingExpressionBase? rules,
        ParameterExpression context, out HashSet<string> boundMembers)
    {
        var ignoreCase = options.HasFlag(AdaptOptions.IgnoreCase);
        var ignoreUnderscore = options.HasFlag(AdaptOptions.IgnoreUnderscore);
        var destinationProperties = destination.GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(property => property.GetIndexParameters().Length == 0).ToArray();
        var candidates = new List<(ConstructorInfo Constructor, ConstructorArgument[] Arguments)>();
        foreach (var constructor in destination.GetConstructors(BindingFlags.Public | BindingFlags.Instance))
        {
            var parameters = constructor.GetParameters();
            var arguments = new List<ConstructorArgument>();
            var names = new HashSet<string>(StringComparer.Ordinal);
            foreach (var parameter in parameters)
            {
                if (parameter.Name == null || parameter.ParameterType.IsByRef || parameter.ParameterType.IsPointer || parameter.ParameterType.IsByRefLike) break;
                // C# constructor parameter casing is independent of source naming
                // policy: `name` initializes `Name`, even under AdaptOptions.Strict.
                var matches = destinationProperties.Where(property =>
                    NormalizeNameStatic(property.Name, true, ignoreUnderscore) == NormalizeNameStatic(parameter.Name, true, ignoreUnderscore)).ToArray();
                var property = matches.FirstOrDefault(property => property.Name == parameter.Name);
                if (property == null && matches.Length > 1)
                    throw new AmbiguousMatchException($"Constructor parameter '{parameter.Name}' matches more than one destination property.");
                property ??= matches.SingleOrDefault();
                var member = property?.Name ?? parameter.Name;
                if (!names.Add(member)) break;
                var ignored = rules?.IsIgnored(member) == true || ignoreNames?.Contains(member, StringComparer.Ordinal) == true;
                if (ignored)
                {
                    arguments.Add(new(parameter, member, member, null, false, true));
                    continue;
                }
                var custom = rules?.GetCustomBinding(member);
                var renamed = rules?.GetRenamedSource(member);
                var sourceName = renamed ?? member;
                var value = custom != null ? NullSafeCustomResolution(SubstituteSource(custom, source))
                    : TryBuildSourceAccess(source, sourceType, sourceName, ignoreCase, ignoreUnderscore, renamed == null, out _);
                if (value == null && renamed != null)
                    throw new NotSupportedException($"The configured source path for constructor member '{member}' does not exist or is not publicly readable.");
                if (value == null && !parameter.HasDefaultValue && !parameter.IsOptional) break;
                arguments.Add(new(parameter, member, sourceName, value, custom != null, false));
            }
            // The object-plan caller retains the normal parameterless/default
            // constructor path. Empty readonly structs still reject rather than
            // silently discarding every source member.
            if (parameters.Length != 0 && arguments.Count == parameters.Length)
                candidates.Add((constructor, arguments.ToArray()));
        }
        var ordered = candidates.OrderByDescending(candidate => candidate.Arguments.Length).ToArray();
        if (ordered.Length == 0)
            throw new MissingMethodException($"Destination '{destination.FullName}' has no public constructor whose required parameters can be bound. Configure ConstructUsing or explicit member mappings.");
        if (ordered.Length > 1 && ordered[0].Arguments.Length == ordered[1].Arguments.Length)
            throw new AmbiguousMatchException($"Destination '{destination.FullName}' has multiple equally preferred public constructors. Configure ConstructUsing to select construction explicitly.");
        var selected = ordered[0];
        boundMembers = new HashSet<string>(selected.Arguments.Select(argument => argument.Member), StringComparer.Ordinal);
        return Expression.New(selected.Constructor, selected.Arguments.Select(argument =>
            BuildConstructorArgument(argument, sourceType, source, options, ignoreNames, rules, context)));
    }

    private Expression BuildConstructorArgument(ConstructorArgument argument, Type sourceType, Expression source,
        AdaptOptions options, string[]? ignoreNames, MappingExpressionBase? rules, ParameterExpression context)
    {
        var parameter = argument.Parameter;
        var fallback = ConstructorParameterDefault(parameter);
        if (argument.IsIgnored) return fallback;
        var rule = rules?.GetMemberOptions(argument.Member);
        if (rule?.Condition != null)
            throw new NotSupportedException($"Condition for constructor-bound member '{argument.Member}' needs an existing destination. Use PreCondition or ConstructUsing instead.");
        if (argument.Value == null) return fallback;
        var custom = rules?.GetCustomBinding(argument.Member);
        var sensitive = MappingException.IsSensitivePath(argument.Member) || MappingException.IsSensitivePath(argument.SourceName) ||
            (custom != null && MappingException.IsSensitivePath(custom.Body.ToString()));
        var value = argument.Value;
        var local = Expression.Variable(value.Type, "argument");
        var error = Expression.Parameter(typeof(Exception), "error");
        var nestedIgnores = (rules?.IgnoredPaths ?? Enumerable.Empty<string>()).Concat(rules?.ConfiguredPaths ?? Enumerable.Empty<string>()).Concat(ignoreNames ?? Array.Empty<string>())
            .Where(path => path.StartsWith(argument.Member + ".", StringComparison.Ordinal)).Select(path => path.Substring(argument.Member.Length + 1)).ToArray();
        Expression converted = rule?.FinalConverter is { } finalConverter
            ? BuildFinalMemberConversion(local, finalConverter, parameter.ParameterType, options, argument.Member, sensitive, context, nestedIgnores.Length == 0 ? null : nestedIgnores)
            : BuildMemberConversion(local, parameter.ParameterType, options, argument.Member, sensitive, context, nestedIgnores.Length == 0 ? null : nestedIgnores);
        if (!value.Type.IsValueType || Nullable.GetUnderlyingType(value.Type) != null)
        {
            Expression? whenNull = null;
            if (rule?.HasNullSubstitute == true)
            {
                if (rule.NullSubstitute != null && !TypeUtils.IsSafelyAssignableValue(rule.NullSubstitute, parameter.ParameterType))
                    throw new NotSupportedException($"NullSubstitute for '{argument.Member}' is not assignable to its constructor parameter.");
                whenNull = rule.NullSubstitute == null ? Expression.Default(parameter.ParameterType)
                    : BuildMemberConversion(Expression.Convert(Expression.Constant(rule.NullSubstitute), parameter.ParameterType),
                        parameter.ParameterType, options, argument.Member, sensitive, context, nestedIgnores.Length == 0 ? null : nestedIgnores);
            }
            else if (rule?.IgnoreNullValues ?? options.HasFlag(AdaptOptions.IgnoreNullValues)) whenNull = fallback;
            if (whenNull != null) converted = Expression.Condition(Expression.Equal(local, Expression.Default(value.Type)), whenNull, converted);
        }
        var failure = Expression.Call(Expression.Constant(this), nameof(MemberFailure), null, error,
            Expression.Constant(value.Type), Expression.Constant(parameter.ParameterType), Expression.Constant(argument.Member),
            Expression.Constant(sensitive), Expression.Convert(local, typeof(object)), context);
        var readFailure = Expression.Call(Expression.Constant(this), nameof(MemberFailure), null, error,
            Expression.Constant(argument.IsCustom ? sourceType : value.Type), Expression.Constant(parameter.ParameterType), Expression.Constant(argument.Member),
            Expression.Constant(sensitive), Expression.Constant(null, typeof(object)), context);
        var read = Expression.TryCatch(Expression.Assign(local, value), Expression.Catch(error, Expression.Throw(readFailure, value.Type)));
        Expression result = Expression.Block(new[] { local }, read,
            Expression.TryCatch(converted, Expression.Catch(error, Expression.Throw(failure, parameter.ParameterType))));
        if (rule?.PreCondition is { } precondition)
        {
            var test = new ParameterSubstitution(precondition.Parameters[0], source).Visit(precondition.Body)!;
            // A precondition failure is a member failure, never a null-safe source
            // expression; false skips both getter and conversion entirely.
            test = Expression.TryCatch(test, Expression.Catch(error, Expression.Throw(readFailure, typeof(bool))));
            result = Expression.Condition(test, result, fallback);
        }
        return result;
    }

    private static Expression ConstructorParameterDefault(ParameterInfo parameter)
    {
        if (!parameter.HasDefaultValue || parameter.DefaultValue == null || parameter.DefaultValue == DBNull.Value || parameter.DefaultValue == Missing.Value)
            return Expression.Default(parameter.ParameterType);
        var value = parameter.DefaultValue;
        var type = Nullable.GetUnderlyingType(parameter.ParameterType) ?? parameter.ParameterType;
        if (type.IsEnum && value.GetType() != type) value = Enum.ToObject(type, value);
        var constant = Expression.Constant(value, type);
        return type == parameter.ParameterType ? constant : Expression.Convert(constant, parameter.ParameterType);
    }

    private MappingPlan GuardConstructorPlan(MappingPlan compiled, Type sourceType, Type destination, string[]? ignoreNames)
    {
        var referenceScope = ReferenceSourceScope(sourceType, destination);
        return (source, callback, context) =>
        {
            if (context.TryGet(source, referenceScope, destination, out var existing, ignoreNames)) return existing;
            // The destination does not exist until all arguments are evaluated.
            // A re-entrant immutable constructor graph cannot preserve identity.
            context.EnterConstruction(source, referenceScope, destination, ignoreNames);
            try { return compiled(source, callback, context); }
            finally { context.ExitConstruction(source, referenceScope, destination, ignoreNames); }
        };
    }

}
