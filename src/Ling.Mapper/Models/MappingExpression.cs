using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq.Expressions;
using System.Reflection;

namespace Ling.Mapper
{
    /// <summary>
    /// 用于描述从 TSource 到 TDestination 的映射配置。
    /// 支持配置：
    /// - ForMember：为单个目标属性指定映射表达式
    /// - Ignore：忽略某个目标属性
    /// - Rename：指定源属性名与目标属性的映射
    /// - ReverseMap：声明需要生成反向映射
    /// </summary>
    /// <typeparam name="TSource">源类型。</typeparam>
    /// <typeparam name="TDestination">目标类型。</typeparam>
    public class MappingExpression<TSource, TDestination>
    {
        /// <summary>
        /// 自定义成员绑定字典，键为目标属性名，值为基于源类型的表达式。
        /// </summary>
        internal readonly Dictionary<string, LambdaExpression> CustomMemberBindings
            = new(); // destPropName -> srcExpr

        /// <summary>
        /// 被忽略的目标属性名集合。
        /// </summary>
        internal readonly HashSet<string> IgnoredMembers = new();

        /// <summary>
        /// 被重命名的属性映射，key 为目标属性名，value 为源属性名。
        /// </summary>
        internal readonly Dictionary<string, string> RenamedMembers = new();

        /// <summary>
        /// 标志是否请求了反向映射。
        /// 目前仅做标记，你可以在后续扩展里使用它来自动生成反向配置。
        /// </summary>
        internal bool ReverseMapRequested;

        /// <summary>
        /// 为指定目标属性配置一个自定义映射表达式。
        /// </summary>
        /// <typeparam name="TMember">目标属性的类型。</typeparam>
        /// <param name="destMember">目标属性选择器。</param>
        /// <param name="srcExpr">源映射表达式，从源对象映射到目标属性值。</param>
        /// <returns>当前 <see cref="MappingExpression{TSource,TDestination}"/> 实例，用于链式调用。</returns>
        [return: NotNull]
        public MappingExpression<TSource, TDestination> ForMember<TMember>(
            Expression<Func<TDestination, TMember>> destMember,
            Expression<Func<TSource, TMember>> srcExpr)
        {
            var name = GetPropName(destMember);
            CustomMemberBindings[name] = srcExpr;
            return this;
        }

        /// <summary>
        /// 将某个目标属性标记为忽略，在映射过程中不会为其赋值。
        /// </summary>
        /// <param name="destMember">目标属性选择器。</param>
        /// <returns>当前 <see cref="MappingExpression{TSource,TDestination}"/> 实例。</returns>
        [return: NotNull]
        public MappingExpression<TSource, TDestination> Ignore(
            Expression<Func<TDestination, object>> destMember)
        {
            var name = GetPropName(destMember);
            IgnoredMembers.Add(name);
            return this;
        }

        /// <summary>
        /// 配置目标属性名称与源属性名称的映射。
        /// 当目标属性名称与源属性名称不一致时使用。
        /// </summary>
        /// <param name="destMember">目标属性选择器。</param>
        /// <param name="srcName">源属性名称。</param>
        /// <returns>当前 <see cref="MappingExpression{TSource,TDestination}"/> 实例。</returns>
        [return: NotNull]
        public MappingExpression<TSource, TDestination> Rename(
            Expression<Func<TDestination, object>> destMember,
            string srcName)
        {
            var name = GetPropName(destMember);
            RenamedMembers[name] = srcName;
            return this;
        }

        /// <summary>
        /// 声明为当前映射生成反向映射配置。
        /// 当前实现仅将标志位设置为 true，返回一个新的反向映射表达式对象，
        /// 方便后续扩展更多配置。
        /// </summary>
        /// <returns>从 TDestination 到 TSource 的反向映射表达式，永不为 null。</returns>
        [return: NotNull]
        public MappingExpression<TDestination, TSource> ReverseMap()
        {
            ReverseMapRequested = true;
            // 当前简单返回一个新的反向表达式实例，如需真正使用可扩展。
            return new MappingExpression<TDestination, TSource>();
        }

        /// <summary>
        /// 从 lambda 表达式中解析出属性名称（支持多级属性访问）。
        /// 支持：x => x.Property（单级）
        /// 支持：x => x.A.B.C（多级）
        /// 自动处理 UnaryExpression（如 Convert）。
        /// </summary>
        /// <param name="expr">属性访问 lambda 表达式。</param>
        /// <returns>属性路径（如 "A.B.C"）。</returns>
        /// <exception cref="InvalidOperationException">当表达式不是属性访问时抛出。</exception>
        private string GetPropName(LambdaExpression expr)
        {
            if (expr == null)
                throw new ArgumentNullException(nameof(expr));

            // ① 处理 Unary（例如：Convert(x.Property)）
            Expression body = expr.Body;
            if (body is UnaryExpression unary && unary.NodeType == ExpressionType.Convert)
            {
                body = unary.Operand;
            }

            // ② 必须是 MemberExpression
            if (body is not MemberExpression memberExpr)
            {
                throw new InvalidOperationException(
                    $"表达式无效：{expr}. 期望形式为：x => x.Property 或 x => x.A.B.C");
            }

            // ③ 解析属性路径（支持多级）
            var propertyPath = new List<string>();
            var currentExpr = memberExpr;

            while (currentExpr != null)
            {
                // 必须是属性访问
                if (currentExpr.Member is not PropertyInfo pi)
                {
                    throw new InvalidOperationException(
                        $"表达式 {expr} 包含非属性成员访问。请使用：x => x.Property 或 x => x.A.B.C 格式。");
                }

                // 添加到路径（逆序）
                propertyPath.Insert(0, pi.Name);

                // 继续解析上一级
                if (currentExpr.Expression is MemberExpression parentMember)
                {
                    currentExpr = parentMember;
                }
                else if (currentExpr.Expression is ParameterExpression)
                {
                    // 到达根参数，结束
                    break;
                }
                else
                {
                    throw new InvalidOperationException(
                        $"表达式 {expr} 包含不支持的表达式类型。");
                }
            }

            // 返回属性路径（用 "." 连接）
            return string.Join(".", propertyPath);
        }
    }
}
