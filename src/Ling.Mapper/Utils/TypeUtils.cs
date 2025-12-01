using System;
using System.Collections;
using System.Linq;

namespace Ling.Mapper
{
    /// <summary>
    /// 类型相关的工具方法集合。
    /// 提供简单类型判断、集合类型判断以及集合元素类型获取等功能。
    /// </summary>
    internal static class TypeUtils
    {
        public static bool IsNullable(Type type)
        {
            if (type == null) return false;
            return Nullable.GetUnderlyingType(type) != null;
        }

        /// <summary>
        /// 判断一个类型是否为“简单类型”。
        /// 简单类型包含：原始类型、枚举、string、decimal、DateTime、Guid。
        /// </summary>
        public static bool IsSimple(Type type)
        {
            var t = Nullable.GetUnderlyingType(type) ?? type;
            return t.IsPrimitive || t.IsEnum || t == typeof(string) || t == typeof(decimal) || t == typeof(DateTime) || t == typeof(Guid);
        }

        /// <summary>
        /// 判断一个类型是否为集合类型（实现 IEnumerable 且不是 string）。
        /// </summary>
        public static bool IsCollection(Type type) => typeof(IEnumerable).IsAssignableFrom(type) && type != typeof(string);

        /// <summary>
        /// 获取集合类型的元素类型。
        /// 支持数组、泛型集合、实现 IEnumerable&lt;T&gt; 的类型。
        /// 如果无法推断，返回 null。
        /// </summary>
        public static Type? GetElementType(Type collectionType)
        {
            if (collectionType == null) return null;

            if (collectionType.IsArray)
                return collectionType.GetElementType();

            if (collectionType.IsGenericType)
            {
                var args = collectionType.GetGenericArguments();
                if (args.Length == 1)
                    return args[0];
            }

            // 查找 IEnumerable<T>
            var ifaces = collectionType.GetInterfaces();
            foreach (var i in ifaces)
            {
                if (i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IEnumerable<>))
                {
                    return i.GetGenericArguments()[0];
                }
            }

            return null;
        }
    }
}
