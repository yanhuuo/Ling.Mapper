using System;
using Ling.Mapper.Models;

namespace Ling.Mapper.Mapper;

/// <summary>
/// 映射器核心接口。
/// 定义了对象间高性能转换的标准契约，支持表达式树编译、链式回调织入及动态字段忽略。
/// </summary>
public interface IMapper
{
    /* ============================================================
     * 一、 泛型映射方法 (通用 API)
     * ============================================================ */

    /// <summary>
    /// 将源对象映射到指定的目标类型（使用默认配置）。
    /// </summary>
    /// <typeparam name="TDestination">目标类型</typeparam>
    /// <param name="source">源对象</param>
    /// <returns>映射后的目标对象。若源为 null 则返回 default。</returns>
    TDestination? Map<TDestination>(object? source);

    /// <summary>
    /// 将源对象映射到指定的目标类型，并支持高级功能。
    /// </summary>
    /// <typeparam name="TDestination">目标类型</typeparam>
    /// <param name="source">源对象</param>
    /// <param name="options">映射选项（如忽略大小写、忽略空值等）</param>
    /// <param name="afterMapItem"></param>
    /// 映射完成后的回调动作。
    /// <para>1. 对于单对象映射，参数为 (目标实例, 原始源实例)。</para>
    /// <para>2. 对于集合映射，在 Mapper 的一次循环内对每个元素触发：(目标项, 原始源项)。</para>
    /// <param name="ignoreNames">运行时需要动态过滤掉的目标属性名集合。</param>
    /// <returns>映射后的目标对象实例。</returns>
    TDestination? Map<TDestination>(
        object? source,
        AdaptOptions options,
        Action<object, object>? afterMapItem = null,
        string[]? ignoreNames = null);

    /* ============================================================
     * 二、 非泛型映射方法 (运行时/动态场景)
     * ============================================================ */

    /// <summary>
    /// 将源对象映射到指定的目标类型（非泛型版本，使用默认配置）。
    /// </summary>
    /// <param name="source">源对象</param>
    /// <param name="sourceType">源对象的运行时类型</param>
    /// <param name="destType">目标对象的类型</param>
    /// <returns>映射后的目标对象实例。</returns>
    object? Map(object? source, Type sourceType, Type destType);

    /// <summary>
    /// 将源对象映射到指定的目标类型（非泛型版本，支持完整高级配置）。
    /// </summary>
    /// <param name="source">源对象</param>
    /// <param name="sourceType">源对象的运行时类型</param>
    /// <param name="destType">目标对象的类型</param>
    /// <param name="options">映射选项</param>
    /// <param name="afterMapItem"></param>
    /// 映射完成后的级联回调动作。参数为 (目标, 源)。
    /// 该逻辑已被织入底层编译委托，确保在映射流水线中一次性执行。
    /// <param name="ignoreNames">运行时动态忽略的目标字段名称数组。</param>
    /// <returns>映射后的目标对象实例。</returns>
    object? Map(
        object? source,
        Type sourceType,
        Type destType,
        AdaptOptions options,
        Action<object, object>? afterMapItem = null,
        string[]? ignoreNames = null);
}
