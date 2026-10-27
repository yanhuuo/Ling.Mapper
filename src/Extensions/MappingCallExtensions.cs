using Ling.Mapper.Mapper;
using Ling.Mapper.Models;
using Ling.Mapper.Provider;

namespace Ling.Mapper.Extensions;

/// <summary>Explicit per-call context entries, kept separate from existing Map and Adapt overloads.</summary>
public static class MappingCallExtensions
{
    /// <summary>Map using the default mapping options inside a context scope.</summary>
    public static TDestination? MapWithContext<TDestination>(this IMapper mapper, object? source, MappingCallContext context)
    {
        ValidateContext(mapper, context);
        using var scope = context.BeginScope();
        return mapper.Map<TDestination>(source);
    }

    /// <summary>Map using explicit options and per-item callbacks inside a context scope.</summary>
    public static TDestination? MapWithContext<TDestination>(this IMapper mapper, object? source, MappingCallContext context,
        AdaptOptions options, Action<object, object>? afterMapItem = null, string[]? ignoreNames = null)
    {
        ValidateContext(mapper, context);
        using var scope = context.BeginScope();
        return mapper.Map<TDestination>(source, options, afterMapItem, ignoreNames);
    }

    /// <summary>Map declared types with per-call context and default options.</summary>
    public static object? MapWithContext(this IMapper mapper, object? source, Type sourceType, Type destinationType, MappingCallContext context)
    {
        ValidateContext(mapper, context);
        using var scope = context.BeginScope();
        return mapper.Map(source, sourceType, destinationType);
    }

    /// <summary>Map declared types with per-call context, explicit options, callbacks and ignored properties.</summary>
    public static object? MapWithContext(this IMapper mapper, object? source, Type sourceType, Type destinationType, MappingCallContext context,
        AdaptOptions options, Action<object, object>? afterMapItem = null, string[]? ignoreNames = null)
    {
        ValidateContext(mapper, context);
        using var scope = context.BeginScope();
        return mapper.Map(source, sourceType, destinationType, options, afterMapItem, ignoreNames);
    }

    /// <summary>Adapt with the current mapper and a per-call context.</summary>
    public static TDestination? AdaptWithContext<TDestination>(this object? source, MappingCallContext context)
        => MapperProvider.Current.MapWithContext<TDestination>(source, context);

    /// <summary>Adapt with a per-call context, explicit options, per-item callbacks and ignored properties.</summary>
    public static TDestination? AdaptWithContext<TDestination>(this object? source, MappingCallContext context,
        AdaptOptions options, Action<object, object>? afterMapItem = null, string[]? ignoreNames = null)
        => MapperProvider.Current.MapWithContext<TDestination>(source, context, options, afterMapItem, ignoreNames);

    /// <summary>Update a caller-owned destination inside a per-call context scope using default options.</summary>
    public static TDestination MapIntoWithContext<TDestination>(this IMapper mapper, object? source, TDestination destination, MappingCallContext context)
        where TDestination : class
    {
        ValidateContext(mapper, context);
        using var scope = context.BeginScope();
        return mapper.MapInto(source, destination);
    }

    /// <summary>Update a caller-owned destination with explicit options and per-call context.</summary>
    public static TDestination MapIntoWithContext<TDestination>(this IMapper mapper, object? source, TDestination destination, MappingCallContext context,
        AdaptOptions options, Action<object, object>? afterMapItem = null, string[]? ignoreNames = null)
        where TDestination : class
    {
        ValidateContext(mapper, context);
        using var scope = context.BeginScope();
        return mapper.MapInto(source, destination, options, afterMapItem, ignoreNames);
    }

    /// <summary>Update a destination using declared types and a per-call context.</summary>
    public static object MapIntoWithContext(this IMapper mapper, object? source, object destination, Type sourceType, Type destinationType, MappingCallContext context)
    {
        ValidateContext(mapper, context);
        using var scope = context.BeginScope();
        return mapper.MapInto(source, destination, sourceType, destinationType);
    }

    /// <summary>Update a destination using declared types, explicit options and a per-call context.</summary>
    public static object MapIntoWithContext(this IMapper mapper, object? source, object destination, Type sourceType, Type destinationType, MappingCallContext context,
        AdaptOptions options, Action<object, object>? afterMapItem = null, string[]? ignoreNames = null)
    {
        ValidateContext(mapper, context);
        using var scope = context.BeginScope();
        return mapper.MapInto(source, destination, sourceType, destinationType, options, afterMapItem, ignoreNames);
    }

    /// <summary>Adapt into a caller-owned destination using the current mapper and a per-call context.</summary>
    public static TDestination AdaptIntoWithContext<TDestination>(this object? source, TDestination destination, MappingCallContext context)
        where TDestination : class
        => MapperProvider.Current.MapIntoWithContext(source, destination, context);

    /// <summary>Adapt into a caller-owned destination with per-call context, explicit options, callbacks and ignores.</summary>
    public static TDestination AdaptIntoWithContext<TDestination>(this object? source, TDestination destination, MappingCallContext context,
        AdaptOptions options, Action<object, object>? afterMapItem = null, string[]? ignoreNames = null)
        where TDestination : class
        => MapperProvider.Current.MapIntoWithContext(source, destination, context, options, afterMapItem, ignoreNames);

    internal static void ValidateContext(IMapper mapper, MappingCallContext context)
    {
        ArgumentNullException.ThrowIfNull(mapper);
        ArgumentNullException.ThrowIfNull(context);
        if (mapper is IRuntimeContextUnsupported && (context.Parameters.Count != 0 || context.Services != null))
            throw new NotSupportedException("Generated mapping does not support per-call parameters or service resolution. Use runtime mapping for context-dependent profiles.");
    }
}

// Implemented by the generated backend, not by arbitrary caller-owned IMapper implementations.
internal interface IRuntimeContextUnsupported { }
