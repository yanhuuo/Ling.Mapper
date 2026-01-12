using System.Collections.Generic;
using System.Runtime.CompilerServices;

namespace Ling.Mapper
{
    /// <summary>
    /// 引用相等比较器，用于运行时循环引用检测
    /// 使用对象引用而非值相等进行比较
    /// </summary>
    internal sealed class ReferenceEqualityComparer : IEqualityComparer<object>
    {
        /// <summary>
        /// 单例实例
        /// </summary>
        public static readonly ReferenceEqualityComparer Instance = new ReferenceEqualityComparer();

        private ReferenceEqualityComparer()
        {
        }

        /// <summary>
        /// 使用引用相等比较两个对象
        /// </summary>
        public new bool Equals(object? x, object? y)
        {
            return ReferenceEquals(x, y);
        }

        /// <summary>
        /// 获取对象的哈希码（基于对象引用）
        /// </summary>
        public int GetHashCode(object obj)
        {
            return RuntimeHelpers.GetHashCode(obj);
        }
    }
}
