using System.Linq.Expressions;
using System.Reflection;
using Ling.Mapper.Utils;

namespace Ling.Mapper.Mapper;

internal sealed partial class Mapper
{
    // Conventions are opt-in. Exact/normalized public properties always win over
    // GetX methods and flattened paths, preserving the existing member contract.
    private Expression? TryBuildSourceAccess(Expression source, Type sourceType, string sourceName,
        bool ignoreCase, bool ignoreUnderscore, bool allowConventions, out Type? valueType)
    {
        var path = ResolveSourceMemberPath(sourceType, sourceName, ignoreCase, ignoreUnderscore,
            allowConventions && _config.GlobalOptions.EnableFlattening,
            allowConventions && _config.GlobalOptions.EnableGetMethods);
        valueType = null;
        if (path == null) return null;
        // MapValue has already handled a null root. Keep the hot direct-property
        // path equivalent to the original plan (the caller owns its read local).
        if (path.Length == 1 && Nullable.GetUnderlyingType(source.Type) == null)
        {
            valueType = SourceMemberType(path[0]);
            return path[0] is PropertyInfo directProperty ? Expression.Property(source, directProperty) : Expression.Call(source, (MethodInfo)path[0]);
        }
        var locals = new List<ParameterExpression>();
        var steps = new List<Expression>();
        var current = source;
        foreach (var member in path)
        {
            var underlying = Nullable.GetUnderlyingType(current.Type);
            var owner = underlying != null && member.DeclaringType != current.Type && member.DeclaringType!.IsAssignableFrom(underlying)
                ? Expression.Property(current, nameof(Nullable<int>.Value)) : current;
            Expression access = member is PropertyInfo property
                ? Expression.Property(owner, property) : Expression.Call(owner, (MethodInfo)member);
            if (underlying != null)
                access = Expression.Condition(Expression.Property(current, nameof(Nullable<int>.HasValue)), access, Expression.Default(access.Type));
            else if (!current.Type.IsValueType)
                access = Expression.Condition(Expression.Equal(current, Expression.Constant(null, current.Type)), Expression.Default(access.Type), access);
            var local = Expression.Variable(access.Type, "sourceMember");
            locals.Add(local);
            steps.Add(Expression.Assign(local, access));
            current = local;
        }
        valueType = current.Type;
        steps.Add(current);
        return Expression.Block(locals, steps);
    }

    // Shared with configuration validation: this method only inspects metadata;
    // choosing a constructor or validating a map must never execute a getter.
    internal static MemberInfo[]? ResolveSourceMemberPath(Type sourceType, string sourceName,
        bool ignoreCase, bool ignoreUnderscore, bool enableFlattening, bool enableGetMethods)
    {
        MemberInfo? Direct(Type owner, string name)
        {
            var properties = BuildSourcePropertyMap(owner.GetProperties(BindingFlags.Public | BindingFlags.Instance)
                .Where(p => p.GetMethod?.IsPublic == true && !p.GetMethod.IsStatic && p.GetIndexParameters().Length == 0).ToArray(), ignoreCase, ignoreUnderscore);
            var normalized = NormalizeNameStatic(name, ignoreCase, ignoreUnderscore);
            if (properties.TryGetValue(normalized, out var property)) return property;
            if (Nullable.GetUnderlyingType(owner) is { } underlying) return Direct(underlying, name);
            if (!enableGetMethods) return null;
            var methods = GetConventionMethods(owner)
                .Where(method => NormalizeNameStatic(method.Name.Substring(3), ignoreCase, ignoreUnderscore) == normalized).ToArray();
            if (methods.Length > 1)
                throw new AmbiguousMatchException($"Source method convention for '{name}' is ambiguous.");
            return methods.SingleOrDefault();
        }

        MemberInfo[]? Resolve(Type owner, string name)
        {
            if (Direct(owner, name) is { } direct) return new[] { direct };
            if (!enableFlattening) return null;
            owner = Nullable.GetUnderlyingType(owner) ?? owner;
            var normalized = NormalizeNameStatic(name, ignoreCase, ignoreUnderscore);
            var candidates = new List<MemberInfo[]>();
            var properties = owner.GetProperties(BindingFlags.Public | BindingFlags.Instance)
                .Where(p => p.GetMethod?.IsPublic == true && !p.GetMethod.IsStatic && p.GetIndexParameters().Length == 0).ToArray();
            IEnumerable<MemberInfo> members = properties;
            if (enableGetMethods)
            {
                // Apply property-over-method precedence at every flattened
                // segment, not only when the complete destination name matches.
                var propertyNames = new HashSet<string>(properties.Select(property => NormalizeNameStatic(property.Name, ignoreCase, ignoreUnderscore)), StringComparer.Ordinal);
                members = members.Concat(GetConventionMethods(owner).Where(method =>
                    !propertyNames.Contains(NormalizeNameStatic(method.Name.Substring(3), ignoreCase, ignoreUnderscore))));
            }
            foreach (var member in members)
            {
                var memberName = member is MethodInfo ? member.Name.Substring(3) : member.Name;
                var prefix = NormalizeNameStatic(memberName, ignoreCase, ignoreUnderscore);
                if (prefix.Length == 0 || prefix.Length >= normalized.Length || !normalized.StartsWith(prefix, StringComparison.Ordinal)) continue;
                var memberType = SourceMemberType(member);
                if (TypeUtils.IsSimple(memberType) || TypeUtils.IsCollection(memberType) || memberType.IsPointer || memberType.IsByRef || memberType.IsByRefLike) continue;
                if (Resolve(memberType, normalized.Substring(prefix.Length)) is not { } tail) continue;
                candidates.Add(new[] { member }.Concat(tail).ToArray());
            }
            if (candidates.Count > 1)
                throw new AmbiguousMatchException($"Flattened source member '{name}' has more than one matching path. Configure an explicit Rename or ForMember.");
            return candidates.SingleOrDefault();
        }

        if (!sourceName.Contains('.')) return Resolve(sourceType, sourceName);
        var result = new List<MemberInfo>();
        foreach (var segment in sourceName.Split('.'))
        {
            if (segment.Length == 0 || Direct(sourceType, segment) is not { } member) return null;
            result.Add(member);
            sourceType = SourceMemberType(member);
        }
        return result.ToArray();
    }

    private static Type SourceMemberType(MemberInfo member) => member is PropertyInfo property
        ? property.PropertyType : ((MethodInfo)member).ReturnType;

    private static IEnumerable<MethodInfo> GetConventionMethods(Type type) => type.GetMethods(BindingFlags.Public | BindingFlags.Instance)
        .Where(method => method.DeclaringType != typeof(object) && method.Name.StartsWith("Get", StringComparison.Ordinal) && method.Name.Length > 3 &&
            !method.IsSpecialName && !method.IsGenericMethod && method.GetParameters().Length == 0 && method.ReturnType != typeof(void) &&
            !method.ReturnType.IsByRef && !method.ReturnType.IsPointer && !method.ReturnType.IsByRefLike);
}
