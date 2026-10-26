using System.Linq.Expressions;
using System.Reflection;
using Ling.Mapper.Helpers;
using Ling.Mapper.Models;

namespace Ling.Mapper.Mapper;

internal sealed partial class Mapper
{
    private MappingPlan BuildImmutablePlan(Type sourceType, Type destination, AdaptOptions options, string[]? ignoreNames)
    {
        var cfg = _config.Configs.Where(c => c.SourceType == sourceType && c.DestType == destination).OrderBy(c => c.IsReverseGenerated).FirstOrDefault();
        var rules = cfg == null ? null : new MappingExpressionBase(cfg.ExpressionObject);
        var ignoreCase = options.HasFlag(AdaptOptions.IgnoreCase);
        var ignoreUnderscore = options.HasFlag(AdaptOptions.IgnoreUnderscore);
        var source = Expression.Parameter(typeof(object));
        var callback = Expression.Parameter(typeof(Action<object, object>));
        var context = Expression.Parameter(typeof(MappingContext));
        var typed = Expression.Convert(source, sourceType);
        var sourceProperties = BuildSourcePropertyMap(sourceType.GetProperties().Where(p => p.GetMethod?.IsPublic == true && !p.GetMethod.IsStatic && p.GetIndexParameters().Length == 0).ToArray(), ignoreCase, ignoreUnderscore);
        var candidates = new List<(ConstructorInfo Constructor, Expression[] Arguments)>();
        foreach (var constructor in destination.GetConstructors(BindingFlags.Public | BindingFlags.Instance))
        {
            var arguments = new List<Expression>();
            var valid = true;
            foreach (var parameter in constructor.GetParameters())
            {
                var member = destination.GetProperties().FirstOrDefault(p => string.Equals(p.Name, parameter.Name, StringComparison.OrdinalIgnoreCase))?.Name ?? parameter.Name!;
                if (rules?.IsIgnored(member) == true || ignoreNames?.Contains(member) == true)
                {
                    arguments.Add(parameter.HasDefaultValue && parameter.DefaultValue != null ? Expression.Constant(parameter.DefaultValue, parameter.ParameterType) : Expression.Default(parameter.ParameterType));
                    continue;
                }
                var custom = rules?.GetCustomBinding(member);
                var sourceName = rules?.GetRenamedSource(member) ?? member;
                Expression? value = null;
                Type? valueType = null;
                if (custom != null) { value = NullSafeCustomResolution(new ParameterSubstitution(custom.Parameters[0], typed).Visit(custom.Body)!); valueType = custom.ReturnType; }
                else if (sourceName.Contains('.')) value = BuildNullSafeNestedAccess(typed, sourceType, sourceName, ignoreCase, ignoreUnderscore, out valueType);
                else if (sourceProperties.TryGetValue(NormalizeNameStatic(sourceName, ignoreCase, ignoreUnderscore), out var property)) { value = Expression.Property(typed, property); valueType = property.PropertyType; }
                if (value == null)
                {
                    if (parameter.HasDefaultValue)
                    {
                        arguments.Add(parameter.DefaultValue == null ? Expression.Default(parameter.ParameterType) : Expression.Constant(parameter.DefaultValue, parameter.ParameterType));
                        continue;
                    }
                    valid = false; break;
                }
                var sensitive = MappingException.IsSensitivePath(member) || MappingException.IsSensitivePath(sourceName) || (custom != null && MappingException.IsSensitivePath(custom.Body.ToString()));
                var local = Expression.Variable(valueType!, "argument");
                var error = Expression.Parameter(typeof(Exception));
                var failure = Expression.Call(Expression.Constant(this), nameof(MemberFailure), null, error, Expression.Constant(valueType!), Expression.Constant(parameter.ParameterType), Expression.Constant(member), Expression.Constant(sensitive), Expression.Convert(local, typeof(object)), context);
                var nestedIgnores = (rules?.IgnoredPaths ?? Enumerable.Empty<string>()).Concat(ignoreNames ?? Array.Empty<string>())
                    .Where(path => path.StartsWith(member + ".", StringComparison.Ordinal)).Select(path => path.Substring(member.Length + 1)).ToArray();
                var conversion = BuildMemberConversion(local, parameter.ParameterType, options, member, sensitive, context, nestedIgnores.Length == 0 ? null : nestedIgnores);
                arguments.Add(Expression.Block(new[] { local }, Expression.TryCatch(Expression.Block(Expression.Assign(local, value), conversion), Expression.Catch(error, Expression.Throw(failure, parameter.ParameterType)))));
            }
            if (valid && arguments.Count > 0) candidates.Add((constructor, arguments.ToArray()));
        }
        var selected = candidates.OrderByDescending(c => c.Arguments.Length).ToArray();
        if (selected.Length == 0 || (selected.Length > 1 && selected[0].Arguments.Length == selected[1].Arguments.Length))
            return (_, _, _) => throw new NotSupportedException("Immutable destination requires one unambiguous public constructor whose parameters can be bound from the source.");
        var build = CompileLambda(Expression.Lambda<MappingPlan>(Expression.Convert(Expression.New(selected[0].Constructor, selected[0].Arguments), typeof(object)), source, callback, context));
        return (value, cb, ctx) =>
        {
            if (ctx.TryGet(value, destination, out var existing, ignoreNames)) return existing;
            ctx.EnterCollection(value, destination);
            try
            {
                var result = build(value, cb, ctx);
                if (result != null) ctx.Register(value, destination, result, ignoreNames);
                return result;
            }
            finally { ctx.ExitCollection(value, destination); }
        };
    }
}
