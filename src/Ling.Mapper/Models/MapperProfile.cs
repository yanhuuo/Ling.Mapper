using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;

namespace Ling.Mapper
{
    /// <summary>
    /// 映射器配置 Profile 基类。
    /// 用户应继承该类，在构造函数中调用 <see cref="CreateMap{TSource,TDestination}"/> 
    /// 方法添加一组映射配置。
    /// </summary>
    public abstract class MapperProfile
    {
        /// <summary>
        /// 当前 Profile 下定义的映射配置集合。
        /// </summary>
        internal readonly List<IMappingConfig> Configs = new();

        /// <summary>
        /// 创建一个从 TSource 到 TDestination 的映射配置。
        /// </summary>
        /// <typeparam name="TSource">源类型。</typeparam>
        /// <typeparam name="TDestination">目标类型。</typeparam>
        /// <returns>用于进一步配置的 <see cref="MappingExpression{TSource,TDestination}"/> 实例。</returns>
        [return: NotNull]
        protected MappingExpression<TSource, TDestination> CreateMap<TSource, TDestination>()
        {
            var expr = new MappingExpression<TSource, TDestination>();
            Configs.Add(new MappingConfig<TSource, TDestination>(expr));
            return expr;
        }
    }

    /// <summary>
    /// 映射配置描述接口，用于在运行时保存源类型、目标类型以及表达式对象。
    /// </summary>
    internal interface IMappingConfig
    {
        /// <summary>
        /// 源类型。
        /// </summary>
        Type SourceType { get; }

        /// <summary>
        /// 目标类型。
        /// </summary>
        Type DestType { get; }

        /// <summary>
        /// 映射表达式对象（即 <see cref="MappingExpression{TSource,TDestination}"/> 实例）。
        /// 使用 object 类型保存，以便统一处理。
        /// </summary>
        object ExpressionObject { get; }
    }

    /// <summary>
    /// 映射配置具体实现。
    /// </summary>
    /// <typeparam name="TSource">源类型。</typeparam>
    /// <typeparam name="TDestination">目标类型。</typeparam>
    internal class MappingConfig<TSource, TDestination> : IMappingConfig
    {
        /// <inheritdoc />
        public Type SourceType => typeof(TSource);

        /// <inheritdoc />
        public Type DestType => typeof(TDestination);

        /// <inheritdoc />
        public object ExpressionObject => Expression;

        /// <summary>
        /// 映射表达式。
        /// </summary>
        public MappingExpression<TSource, TDestination> Expression { get; }

        /// <summary>
        /// 使用给定的映射表达式构造配置实例。
        /// </summary>
        /// <param name="expr">映射表达式。</param>
        public MappingConfig(MappingExpression<TSource, TDestination> expr)
        {
            Expression = expr;
        }
    }
}
