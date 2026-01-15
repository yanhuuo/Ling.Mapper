using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;

namespace Ling.Mapper
{
    /// <summary>
    /// 映射器接口。
    /// 提供运行时从一个对象映射到另一个类型实例的能力。
    /// </summary>
    public interface IMapper
    {
        /// <summary>
        /// 将源对象映射为指定目标类型 <typeparamref name="TDestination"/> 的新实例。
        /// </summary>
        /// <typeparam name="TDestination">目标类型。</typeparam>
        /// <param name="source">源对象实例。</param>
        /// <returns>映射得到的目标类型实例。</returns>
        TDestination? Map<TDestination>(object? source);

        /// <summary>
        /// 将源对象映射为指定目标类型的实例，
        /// 使用运行时传入的源类型和目标类型信息。
        /// </summary>
        /// <param name="source">源对象实例。</param>
        /// <param name="sourceType">源对象类型。</param>
        /// <param name="destType">目标类型。</param>
        /// <returns>映射得到的目标类型实例。</returns>
        object? Map(object? source, Type sourceType, Type destType);
    }

    
}
