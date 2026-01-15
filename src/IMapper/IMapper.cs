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
        TDestination? Map<TDestination>(object? source);
        TDestination? Map<TDestination>(object source, AdaptOptions options);

        object? Map(object? source, Type sourceType, Type destType);
        object? Map(object? source, Type sourceType, Type destType, AdaptOptions options);
    }


}
