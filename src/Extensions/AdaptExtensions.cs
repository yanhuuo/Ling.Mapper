using System;
using Ling.Mapper.Models;
using Ling.Mapper.Provider;

namespace Ling.Mapper.Extensions
{
    /// <summary>
    /// Adapt convenience methods. Options, ignored destination properties and
    /// callbacks can be combined without changing the underlying mapper contract.
    /// </summary>
    public static class AdaptExtensions
    {
        private static AdaptOptions GetDefaultOptions(Ling.Mapper.Mapper.IMapper mapper) => mapper is Ling.Mapper.Mapper.IDefaultMappingOptions defaults ? defaults.DefaultOptions : AdaptOptions.Default;

        extension(object? source)
        {
            /// <summary>Map using the current mapper's default options.</summary>
            public TDestination Adapt<TDestination>()
                => source.InternalAdapt<TDestination>(null, null, null);

            /// <summary>Map using explicit options, including Strict (zero).</summary>
            public TDestination Adapt<TDestination>(AdaptOptions options)
                => source.InternalAdapt<TDestination>(options, null, null);

            /// <summary>Map while ignoring the named destination properties.</summary>
            public TDestination Adapt<TDestination>(string firstIgnore, params string[] otherIgnores)
                => source.InternalAdapt<TDestination>(null, null, CombineIgnores(firstIgnore, otherIgnores));

            /// <summary>Map while ignoring a runtime array of destination property names.</summary>
            public TDestination Adapt<TDestination>(string[] ignoreNames)
                => source.InternalAdapt<TDestination>(null, null, ValidateIgnores(ignoreNames));

            /// <summary>Map with explicit options and ignored destination properties.</summary>
            public TDestination Adapt<TDestination>(AdaptOptions options, params string[] ignoreNames)
                => source.InternalAdapt<TDestination>(options, null, ValidateIgnores(ignoreNames));
        }

        /// <summary>Update a supplied mutable object/collection in place using the current mapper.
        /// The distinct name avoids ambiguity with existing string-ignore and callback Adapt overloads.</summary>
        public static TDestination AdaptInto<TDestination>(this object? source, TDestination destination) where TDestination : class
        {
            var mapper = MapperProvider.Current;
            return mapper.MapInto(source, destination);
        }

        /// <summary>Update a supplied destination with explicit options and ignored paths.</summary>
        public static TDestination AdaptInto<TDestination>(this object? source, TDestination destination, AdaptOptions options, params string[] ignoreNames) where TDestination : class
        {
            var mapper = MapperProvider.Current;
            return mapper.MapInto(source, destination, options, ignoreNames: ValidateIgnores(ignoreNames));
        }

        /// <summary>Update a supplied destination, then invoke one result callback after success (including a null-source no-op).</summary>
        public static TDestination AdaptInto<TDestination>(this object? source, TDestination destination, Action<TDestination> afterMap, params string[] ignoreNames) where TDestination : class
        {
            ArgumentNullException.ThrowIfNull(afterMap);
            var mapper = MapperProvider.Current;
            var result = mapper.MapInto(source, destination, GetDefaultOptions(mapper), ignoreNames: ValidateIgnores(ignoreNames));
            afterMap(result);
            return result;
        }

        /// <summary>Update with explicit options/ignored paths, then invoke the result callback once.</summary>
        public static TDestination AdaptInto<TDestination>(this object? source, TDestination destination, AdaptOptions options, Action<TDestination> afterMap, params string[] ignoreNames) where TDestination : class
        {
            ArgumentNullException.ThrowIfNull(afterMap);
            var mapper = MapperProvider.Current;
            var result = mapper.MapInto(source, destination, options, ignoreNames: ValidateIgnores(ignoreNames));
            afterMap(result);
            return result;
        }

        // Result callbacks run exactly once after mapping, including a null result.
        // They are deliberately not forwarded as the mapper's per-item callback.

        /// <summary>Invoke a callback once with the complete mapping result.</summary>
        public static TDestination Adapt<TDestination>(this object? source, Action<TDestination> afterMap)
            => AdaptResult(source, null, afterMap, null);

        /// <summary>Ignore destination properties, then invoke the result callback once.</summary>
        public static TDestination Adapt<TDestination>(this object? source, Action<TDestination> afterMap, params string[] ignoreNames)
            => AdaptResult(source, null, afterMap, ValidateIgnores(ignoreNames));

        /// <summary>Combine explicit options, ignored properties and a result callback.</summary>
        public static TDestination Adapt<TDestination>(this object? source, AdaptOptions options, Action<TDestination> afterMap, params string[] ignoreNames)
            => AdaptResult(source, options, afterMap, ValidateIgnores(ignoreNames));

        /// <summary>Invoke a strongly typed callback with the mapped object and its source.</summary>
        public static TDestination Adapt<TDestination, TSource>(this TSource source, Action<TDestination, TSource> afterMapItem)
            => AdaptObject(source, null, afterMapItem, null);

        /// <summary>Combine ignored properties with a strongly typed object callback.</summary>
        public static TDestination Adapt<TDestination, TSource>(this TSource source, Action<TDestination, TSource> afterMapItem, params string[] ignoreNames)
            => AdaptObject(source, null, afterMapItem, ValidateIgnores(ignoreNames));

        /// <summary>Combine explicit options, ignored properties and an object callback.</summary>
        public static TDestination Adapt<TDestination, TSource>(this TSource source, AdaptOptions options, Action<TDestination, TSource> afterMapItem, params string[] ignoreNames)
            => AdaptObject(source, options, afterMapItem, ValidateIgnores(ignoreNames));

        /// <summary>Map a collection and invoke a typed callback for each non-null mapped item.</summary>
        public static List<TTargetItem> Adapt<TTargetItem, TSourceItem>(this IEnumerable<TSourceItem> source, Action<TTargetItem, TSourceItem> afterMapItem)
            where TTargetItem : class, new()
            => AdaptItems(source, null, afterMapItem, null);

        /// <summary>Ignore destination-item properties before each collection-item callback.</summary>
        public static List<TTargetItem> Adapt<TTargetItem, TSourceItem>(this IEnumerable<TSourceItem> source, Action<TTargetItem, TSourceItem> afterMapItem, params string[] ignoreNames)
            where TTargetItem : class, new()
            => AdaptItems(source, null, afterMapItem, ValidateIgnores(ignoreNames));

        /// <summary>Combine explicit options, ignored item properties and an item callback.</summary>
        public static List<TTargetItem> Adapt<TTargetItem, TSourceItem>(this IEnumerable<TSourceItem> source, AdaptOptions options, Action<TTargetItem, TSourceItem> afterMapItem, params string[] ignoreNames)
            where TTargetItem : class, new()
            => AdaptItems(source, options, afterMapItem, ValidateIgnores(ignoreNames));

        private static TDestination AdaptResult<TDestination>(object? source, AdaptOptions? options, Action<TDestination> afterMap, string[]? ignores)
        {
            ArgumentNullException.ThrowIfNull(afterMap);
            var result = source.InternalAdapt<TDestination>(options, null, ignores);
            afterMap(result);
            return result;
        }

        private static TDestination AdaptObject<TDestination, TSource>(TSource source, AdaptOptions? options, Action<TDestination, TSource> afterMapItem, string[]? ignores)
        {
            ArgumentNullException.ThrowIfNull(afterMapItem);
            // Preserve the null-source callback contract for custom IMapper implementations.
            if (source == null) return source.InternalAdapt<TDestination>(options, null, ignores);
            return source.InternalAdapt<TDestination>(options, (destination, original) =>
            {
                if (destination is TDestination dest && original is TSource src)
                    afterMapItem(dest, src);
            }, ignores);
        }

        private static List<TTargetItem> AdaptItems<TTargetItem, TSourceItem>(IEnumerable<TSourceItem> source, AdaptOptions? options, Action<TTargetItem, TSourceItem> afterMapItem, string[]? ignores)
            where TTargetItem : class, new()
        {
            ArgumentNullException.ThrowIfNull(afterMapItem);
            // Preserve the existing collection-item overload's null -> empty contract.
            if (source == null) return new List<TTargetItem>();
            return source.InternalAdapt<List<TTargetItem>>(options, (destination, original) =>
            {
                if (destination is TTargetItem dest && original is TSourceItem src)
                    afterMapItem(dest, src);
            }, ignores) ?? new List<TTargetItem>();
        }

        private static string[]? ValidateIgnores(string[] ignoreNames)
        {
            ArgumentNullException.ThrowIfNull(ignoreNames);
            if (ignoreNames.Any(string.IsNullOrWhiteSpace))
                throw new ArgumentException("Ignored property names cannot be null or empty.", nameof(ignoreNames));
            return ignoreNames.Length == 0 ? null : ignoreNames.ToArray();
        }

        private static string[] CombineIgnores(string firstIgnore, string[] otherIgnores)
        {
            ArgumentNullException.ThrowIfNull(firstIgnore);
            ArgumentNullException.ThrowIfNull(otherIgnores);
            if (string.IsNullOrWhiteSpace(firstIgnore)) throw new ArgumentException("Ignored property name cannot be empty.", nameof(firstIgnore));
            if (otherIgnores.Any(string.IsNullOrWhiteSpace)) throw new ArgumentException("Ignored property names cannot be null or empty.", nameof(otherIgnores));
            var ignores = new string[otherIgnores.Length + 1];
            ignores[0] = firstIgnore;
            Array.Copy(otherIgnores, 0, ignores, 1, otherIgnores.Length);
            return ignores;
        }

        internal static TDestination InternalAdapt<TDestination>(
            this object? source,
            AdaptOptions? options,
            Action<object, object>? afterMap,
            string[]? ignores)
        {
            // Read the provider once so the mapper and its defaults are from one instance.
            var mapper = MapperProvider.Current;
            var effectiveOptions = options ?? GetDefaultOptions(mapper);
            // Preserve the mapper's result, including an explicit converter's null.
            return mapper.Map<TDestination>(source, effectiveOptions, afterMap, ignores)!;
        }
    }
}
