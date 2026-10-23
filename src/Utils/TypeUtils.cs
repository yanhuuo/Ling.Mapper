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
                   t == typeof(DateTimeOffset) ||
                   t == typeof(DateOnly) ||
                   t == typeof(TimeOnly) ||
                   t == typeof(Guid) ||
                   t == typeof(TimeSpan);
        }

        /// <summary>
        /// 判断一个类型是否为集合类型（实现 IEnumerable 且不是 string）。
        /// </summary>
        public static bool IsCollection(Type type)
            => typeof(IEnumerable).IsAssignableFrom(type) && type != typeof(string);

        /// <summary>Assignment compatibility without CLR signed/unsigned array aliasing.
        /// Collection covariance must also preserve its element conversion semantics.</summary>
        public static bool IsSafelyAssignable(Type source, Type destination)
            => IsSafelyAssignableCore(source, destination, null);

        private static bool IsSafelyAssignableCore(Type source, Type destination, HashSet<(Type, Type)>? visited)
        {
            if (source == destination) return true;
            if (!destination.IsAssignableFrom(source)) return false;
            if (!IsCollection(source) || !IsCollection(destination)) return true;
            var sourceElement = GetElementType(source);
            var destinationElement = GetElementType(destination);
            if (sourceElement == null || destinationElement == null) return true;
            if (sourceElement == destinationElement) return true;
            if (sourceElement.IsValueType || destinationElement.IsValueType) return false;
            if (!IsCollection(sourceElement) || !IsCollection(destinationElement))
                return destinationElement.IsAssignableFrom(sourceElement);
            visited ??= new HashSet<(Type, Type)>();
            if (!visited.Add((source, destination))) return true;
            return IsSafelyAssignableCore(sourceElement, destinationElement, visited);
        }

        /// <summary>Check actual-value assignment using the same collection safety rules.</summary>
        public static bool IsSafelyAssignableValue(object? value, Type destination)
            => value != null && IsSafelyAssignable(value.GetType(), destination);

        /// <summary>Whether a type exposes dictionary key/value semantics.</summary>
        public static bool IsDictionary(Type type)
        {
            if (typeof(IDictionary).IsAssignableFrom(type)) return true;
            static bool IsDictionaryInterface(Type candidate) => candidate.IsGenericType &&
                (candidate.GetGenericTypeDefinition() == typeof(IDictionary<,>) ||
                 candidate.GetGenericTypeDefinition() == typeof(IReadOnlyDictionary<,>));
            return IsDictionaryInterface(type) || type.GetInterfaces().Any(IsDictionaryInterface);
        }

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

            // Generic class arguments do not necessarily describe its enumerable
            // elements (for example Wrapper<T> may implement IEnumerable<int[]>).
            if (collectionType.IsGenericType && collectionType.GetGenericTypeDefinition() == typeof(IEnumerable<>))
                return collectionType.GetGenericArguments()[0];

            // 3. 查找类实现的 IEnumerable<T> 接口
            var iEnum = collectionType.GetInterfaces()
                .FirstOrDefault(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IEnumerable<>));

            return iEnum?.GetGenericArguments()[0];
        }
    }
}
