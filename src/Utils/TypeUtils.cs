using System.Collections;

namespace Ling.Mapper.Utils
{
    /// <summary>
    /// 类型相关的工具方法集合。
    /// 提供简单类型判断、数值类型判断、集合类型判断以及集合元素类型获取等功能。
    /// </summary>
    internal static class TypeUtils
    {
        // 定义 .NET 中的基元数值类型集合，用于物理转换判定
        private static readonly HashSet<Type> NumericTypes =
        [
            typeof(byte), typeof(sbyte),
            typeof(short), typeof(ushort),
            typeof(int), typeof(uint),
            typeof(long), typeof(ulong),
            typeof(float), typeof(double),
            typeof(decimal)
        ];

        /// <summary>
        /// 判断是否为可空类型 (Nullable&lt;T&gt;)
        /// </summary>
        public static bool IsNullable(Type? type)
        {
            if (type == null) return false;
            return Nullable.GetUnderlyingType(type) != null;
        }

        /// <summary>
        /// 判断一个类型是否为“数值类型”。
        /// 支持 Nullable 数值类型。
        /// </summary>
        public static bool IsNumeric(Type type)
        {
            if (type == null) return false;
            // 提取底层类型，支持 int? 等
            var t = Nullable.GetUnderlyingType(type) ?? type;
            return NumericTypes.Contains(t);
        }

        /// <summary>
        /// 判断一个类型是否为“简单类型”。
        /// 简单类型包含：原始类型、枚举、string、decimal、DateTime、Guid、TimeSpan。
        /// 简单类型通常直接在 ConvertValueExpression 中处理，而非简单类型则进入递归映射。
        /// </summary>
        public static bool IsSimple(Type? type)
        {
            if (type == null) return false;
            var t = Nullable.GetUnderlyingType(type) ?? type;
            return t.IsPrimitive ||
                   t.IsEnum ||
                   t == typeof(string) ||
                   t == typeof(decimal) ||
                   t == typeof(DateTime) ||
                   t == typeof(Guid) ||
                   t == typeof(TimeSpan);
        }

        /// <summary>
        /// 判断一个类型是否为集合类型（实现 IEnumerable 且不是 string）。
        /// </summary>
        public static bool IsCollection(Type type)
            => typeof(IEnumerable).IsAssignableFrom(type) && type != typeof(string);

        /// <summary>
        /// 获取集合类型的元素类型。
        /// 支持数组、泛型集合、实现 IEnumerable&lt;T&gt; 的类型。
        /// 如果无法推断，返回 null。
        /// </summary>
        public static Type? GetElementType(Type? collectionType)
        {
            if (collectionType == null) return null;

            // 1. 数组处理
            if (collectionType.IsArray)
                return collectionType.GetElementType();

            // 2. 泛型接口/类直接获取 (如 List<T>)
            if (collectionType.IsGenericType && collectionType.GetGenericArguments().Length == 1)
            {
                // 如果是接口本身如 IEnumerable<T>
                return collectionType.GetGenericArguments()[0];
            }

            // 3. 查找类实现的 IEnumerable<T> 接口
            var iEnum = collectionType.GetInterfaces()
                .FirstOrDefault(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IEnumerable<>));

            return iEnum?.GetGenericArguments()[0];
        }
    }
}
