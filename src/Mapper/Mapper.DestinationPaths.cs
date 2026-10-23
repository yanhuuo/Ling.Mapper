using System.Linq.Expressions;
using System.Reflection;

namespace Ling.Mapper.Mapper;

internal sealed partial class Mapper
{
    private static PropertyInfo ResolveDestinationProperty(Type type, string path)
    {
        PropertyInfo? property = null;
        foreach (var segment in path.Split('.'))
        {
            property = type.GetProperty(segment, BindingFlags.Instance | BindingFlags.Public);
            if (property == null || property.GetIndexParameters().Length != 0)
                throw new NotSupportedException($"Destination path '{path}' does not identify a public property.");
            type = property.PropertyType;
        }
        return property!;
    }

    private static Expression BuildDestinationAssignment(Expression root, string path, Expression value)
    {
        var segments = path.Split('.');
        Expression Write(Expression owner, int index)
        {
            var property = owner.Type.GetProperty(segments[index], BindingFlags.Public | BindingFlags.Instance)!;
            if (property == null || property.GetIndexParameters().Length != 0)
                throw new NotSupportedException($"Invalid destination path '{path}'.");
            var access = Expression.Property(owner, property);
            var writable = property.SetMethod?.IsPublic == true && !property.SetMethod.IsStatic;
            if (index == segments.Length - 1)
            {
                if (!writable) throw new NotSupportedException($"Destination path '{path}' is not writable.");
                return Expression.Assign(access, value);
            }
            if (property.GetMethod?.IsPublic != true || property.GetMethod.IsStatic)
                throw new NotSupportedException($"Destination path '{path}' has an unreadable intermediate property.");
            if (property.PropertyType.IsValueType && !writable)
                throw new NotSupportedException($"Destination path '{path}' requires a writable value-type intermediate property.");
            var local = Expression.Variable(property.PropertyType, "child");
            var steps = new List<Expression> { Expression.Assign(local, access) };
            if (!property.PropertyType.IsValueType)
            {
                var constructor = property.PropertyType.GetConstructor(Type.EmptyTypes);
                Expression initialize = writable && constructor != null && !property.PropertyType.IsAbstract
                    ? Expression.Assign(local, Expression.New(constructor))
                    : Expression.Throw(Expression.New(typeof(InvalidOperationException).GetConstructor(new[] { typeof(string) })!, Expression.Constant("Null destination path cannot be initialized.")), property.PropertyType);
                steps.Add(Expression.IfThen(Expression.Equal(local, Expression.Constant(null, property.PropertyType)), initialize));
            }
            steps.Add(Write(local, index + 1));
            if (writable) steps.Add(Expression.Assign(access, local));
            steps.Add(Expression.Empty());
            return Expression.Block(new[] { local }, steps);
        }
        return Write(root, 0);
    }
}
