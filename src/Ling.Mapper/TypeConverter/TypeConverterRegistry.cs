using System;
using System.Collections.Concurrent;

namespace Ling.Mapper
{
    /// <summary>
    /// 泛型类型转换器接口。
    /// 你可以实现该接口，用于定义从 TSource 到 TDestination 的自定义转换逻辑。
    /// </summary>
    /// <typeparam name="TSource">源类型。</typeparam>
    /// <typeparam name="TDestination">目标类型。</typeparam>
    public interface ITypeConverter<TSource, TDestination>
    {
        /// <summary>
        /// 将源对象转换为目标类型实例。
        /// </summary>
        /// <param name="source">源对象实例。</param>
        /// <returns>转换后的目标类型实例。</returns>
        TDestination Convert(TSource source);
    }

    /// <summary>
    /// 类型转换器注册中心。
    /// 用于注册和查找不同类型之间的自定义转换委托。
    /// 提供 JSON 转换 快捷注册方法。
    /// </summary>
    public static class TypeConverterRegistry
    {
        /// <summary>
        /// 内部转换器字典，键为 (源类型, 目标类型)，值为对应的委托。
        /// </summary>
        private static readonly ConcurrentDictionary<(Type, Type), Delegate> _registry = new();

        /// <summary>
        /// 注册一个类型转换器。
        /// </summary>
        /// <param name="src">源类型。</param>
        /// <param name="dest">目标类型。</param>
        /// <param name="converter">用于转换的委托，通常是 Func&lt;TSource, TDestination&gt;。</param>
        public static void Register(Type src, Type dest, Delegate converter)
        {
            if (src == null) throw new ArgumentNullException(nameof(src));
            if (dest == null) throw new ArgumentNullException(nameof(dest));
            if (converter == null) throw new ArgumentNullException(nameof(converter));

            _registry[(src, dest)] = converter;
        }

        /// <summary>
        /// 尝试获取指定源类型和目标类型的转换器。
        /// </summary>
        /// <param name="src">源类型。</param>
        /// <param name="dest">目标类型。</param>
        /// <param name="converter">输出转换委托，如果存在则返回。</param>
        /// <returns>如果找到了对应的转换器则返回 true，否则返回 false。</returns>
        public static bool TryGet(Type src, Type dest, out Delegate? converter)
        {
            if (src == null) throw new ArgumentNullException(nameof(src));
            if (dest == null) throw new ArgumentNullException(nameof(dest));

            return _registry.TryGetValue((src, dest), out converter);
        }

        /// <summary>
        /// 为某个类型注册 JSON 互转转换器。
        /// 会同时注册 string -&gt; T（反序列化）和 T -&gt; string（序列化）两个方向的转换。
        /// </summary>
        /// <typeparam name="T">需要进行 JSON 转换的类型。</param>
        public static void RegisterJson<T>()
        {
            Register(typeof(string), typeof(T), new Func<string, T?>(s =>
                string.IsNullOrWhiteSpace(s) ? default : System.Text.Json.JsonSerializer.Deserialize<T>(s)));

            Register(typeof(T), typeof(string), new Func<T, string?>(o => o == null ? null : System.Text.Json.JsonSerializer.Serialize(o)));
        }
    }
}
