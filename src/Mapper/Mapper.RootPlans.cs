using System.Linq.Expressions;

namespace Ling.Mapper.Mapper;

internal sealed partial class Mapper
{
    // Reuse the object plan's complete member pipeline. Eligibility is proven on
    // the resulting expression rather than inferred from benchmark DTO names or
    // a second member resolver. Any graph/context operation forces the old path.
    private Func<object, Action<object, object>?, object?>? TryCompileRootPlan(
        Expression body, ParameterExpression source, ParameterExpression callback,
        ParameterExpression context, Type sourceType, Type destinationType)
    {
        var lazyContext = Expression.Variable(typeof(MappingContext), "diagnosticContext");
        var visitor = new RootContextRewriter(context, lazyContext);
        var rewritten = visitor.Visit(body)!;
        if (!visitor.Eligible) return null;
        var result = Expression.Variable(typeof(object), "result");
        var error = Expression.Parameter(typeof(Exception), "error");
        var fail = Expression.Call(Expression.Constant(this), nameof(RootFailure), null,
            error, Expression.Constant(sourceType), Expression.Constant(destinationType), source, lazyContext);
        var execution = Expression.Block(
            Expression.Assign(result, rewritten),
            Expression.IfThen(Expression.AndAlso(Expression.NotEqual(callback, Expression.Constant(null, callback.Type)),
                    Expression.NotEqual(result, Expression.Constant(null))),
                Expression.Invoke(callback, result, source)), result);
        return CompileLambda(Expression.Lambda<Func<object, Action<object, object>?, object?>>(
            Expression.Block(new[] { lazyContext, result }, Expression.TryCatch(execution,
                Expression.Catch(error, Expression.Throw(fail, typeof(object))))), source, callback));
    }

    private MappingException RootFailure(Exception error, Type sourceType, Type destinationType, object source, MappingContext? context)
    {
        context ??= new MappingContext();
        if (error is MappingException mapped && ReferenceEquals(mapped.Origin, context.Origin)) return mapped;
        return MappingException.Create(sourceType, destinationType, "$", source, error, _config, false, context.Origin);
    }

    private sealed class RootContextRewriter(ParameterExpression context, ParameterExpression lazyContext) : ExpressionVisitor
    {
        internal bool Eligible = true;
        protected override Expression VisitMethodCall(MethodCallExpression node)
        {
            if (node.Method.DeclaringType == typeof(Mapper) && node.Method.Name == nameof(MemberFailure))
            {
                var arguments = node.Arguments.Select(argument => argument == context
                    ? (Expression)Expression.Coalesce(lazyContext, Expression.Assign(lazyContext, Expression.New(typeof(MappingContext))))
                    : Visit(argument)!).ToArray();
                return node.Update(Visit(node.Object), arguments);
            }
            return base.VisitMethodCall(node);
        }
        protected override Expression VisitParameter(ParameterExpression node)
        {
            if (node == context) Eligible = false;
            return node;
        }
    }
}
