using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using Ling.Mapper.Models;

namespace Ling.Mapper
{
    /// <summary>
    /// 映射器接口。
    /// 提供运行时从一个对象映射到另一个类型实例的能力。
    /// </summary>
    public interface IMapper
    {
        /// <summary>
        /// 将源对象映射到指定的目标类型。
        /// </summary>
        /// <typeparam name="TDestination">目标类型</typeparam>
        /// <param name="source">源对象</param>
        /// <returns>映射后的目标对象</returns>
        TDestination? Map<TDestination>(object? source);

        /// <summary>
        /// 将源对象映射到指定的目标类型，并使用自定义映射选项。
        /// </summary>
        /// <typeparam name="TDestination">目标类型</typeparam>
        /// <param name="source">源对象</param>
        /// <param name="options">映射选项</param>
        /// <returns>映射后的目标对象</returns>
        TDestination? Map<TDestination>(object source, AdaptOptions options);

        /// <summary>
        /// 将源对象映射到指定的目标类型（非泛型版本）。
        /// </summary>
        /// <param name="source">源对象</param>
        /// <param name="sourceType">源类型</param>
        /// <param name="destType">目标类型</param>
        /// <returns>映射后的目标对象</returns>
        object? Map(object? source, Type sourceType, Type destType);

        /// <summary>
        /// 将源对象映射到指定的目标类型（非泛型版本），并使用自定义映射选项。
        /// </summary>
        /// <param name="source">源对象</param>
        /// <param name="sourceType">源类型</param>
        /// <param name="destType">目标类型</param>
        /// <param name="options">映射选项</param>
        /// <returns>映射后的目标对象</returns>
        object? Map(object? source, Type sourceType, Type destType, AdaptOptions options);
    }


}
