using System.Linq.Expressions;
using Ling.Mapper.Mapper;
using Ling.Mapper.Models;
using Ling.Mapper.Provider;

namespace Ling.Mapper.Extensions;

/// <summary>Build expression projections or append a deferred Queryable.Select without compiling or enumerating the source.</summary>
public static class ProjectionExtensions
{
    /// <summary>Get a reusable typed projection using the mapper's default naming options.</summary>
    public static Expression<Func<TSource, TDestination>> GetProjection<TSource, TDestination>(this IMapper mapper)
        => GetProjection<TSource, TDestination>(mapper, DefaultOptions(mapper));

    /// <summary>Get a reusable typed projection using explicit options.</summary>
    public static Expression<Func<TSource, TDestination>> GetProjection<TSource, TDestination>(this IMapper mapper, AdaptOptions options)
        => (Expression<Func<TSource, TDestination>>)GetProjection(mapper, typeof(TSource), typeof(TDestination), options);

    /// <summary>Append a deferred projection using the supplied mapper or MapperProvider.Current.</summary>
    public static IQueryable<TDestination> ProjectTo<TSource, TDestination>(this IQueryable<TSource> source, IMapper? mapper = null)
    {
        ArgumentNullException.ThrowIfNull(source);
        mapper ??= MapperProvider.Current;
        return Queryable.Select(source, mapper.GetProjection<TSource, TDestination>());
    }

    /// <summary>Append a deferred projection, specifying only the destination type.</summary>
    /// <remarks>The source's declared ElementType is used; no element is read to infer its runtime type.</remarks>
    public static IQueryable<TDestination> ProjectTo<TDestination>(this IQueryable source, IMapper? mapper = null)
    {
        ArgumentNullException.ThrowIfNull(source);
        mapper ??= MapperProvider.Current;
        return ProjectTo<TDestination>(source, DefaultOptions(mapper), mapper);
    }

    /// <summary>Append a deferred projection using explicit naming options.</summary>
    public static IQueryable<TDestination> ProjectTo<TDestination>(this IQueryable source, AdaptOptions options, IMapper? mapper = null)
    {
        ArgumentNullException.ThrowIfNull(source);
        mapper ??= MapperProvider.Current;
        var projection = GetProjection(mapper, source.ElementType, typeof(TDestination), options);
        var select = Expression.Call(typeof(Queryable), nameof(Queryable.Select), new[] { source.ElementType, typeof(TDestination) }, source.Expression, Expression.Quote(projection));
        return source.Provider.CreateQuery<TDestination>(select);
    }

    private static AdaptOptions DefaultOptions(IMapper mapper)
    {
        ArgumentNullException.ThrowIfNull(mapper);
        return mapper is IDefaultMappingOptions defaults ? defaults.DefaultOptions : AdaptOptions.Default;
    }

    private static LambdaExpression GetProjection(IMapper mapper, Type source, Type destination, AdaptOptions options)
    {
        ArgumentNullException.ThrowIfNull(mapper);
        return mapper is IProjectionMapper projection ? projection.GetProjection(source, destination, options)
            : throw new NotSupportedException("This mapper does not support expression projection. Runtime and generated mapping delegates are not substituted into IQueryable queries.");
    }
}
