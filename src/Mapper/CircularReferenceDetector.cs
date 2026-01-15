using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;

namespace Ling.Mapper
{
    /// <summary>
    /// 循环引用检测器，用于防止在复杂对象映射中出现无限递归（A -> B -> A -> StackOverflow）。
    /// 使用引用相等（ReferenceEquals）进行检测，仅在复杂对象递归映射时启用。
    /// </summary>
    internal sealed class CircularReferenceDetector : IDisposable
    {
        private readonly Dictionary<object, object>? _trackedObjects;
        private readonly bool _enabled;

        /// <summary>
        /// 创建循环引用检测器实例。
        /// </summary>
        /// <param name="enabled">是否启用检测（默认 true）</param>
        public CircularReferenceDetector(bool enabled = true)
        {
            _enabled = enabled;
            if (_enabled)
            {
                _trackedObjects = new Dictionary<object, object>(ReferenceEqualityComparer.Instance);
            }
        }

        /// <summary>
        /// 尝试添加源对象到跟踪列表。
        /// </summary>
        /// <param name="source">源对象</param>
        /// <param name="destination">目标对象</param>
        /// <returns>如果对象已经在跟踪中（检测到循环），返回 false；否则添加成功返回 true</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool TryTrack(object source, object destination)
        {
            if (!_enabled || _trackedObjects == null)
                return true;

            // 如果源对象已经在映射中，说明检测到循环引用
            if (_trackedObjects.ContainsKey(source))
                return false;

            _trackedObjects[source] = destination;
            return true;
        }

        /// <summary>
        /// 尝试获取已映射的目标对象（用于返回缓存的结果）。
        /// </summary>
        /// <param name="source">源对象</param>
        /// <param name="destination">输出的目标对象</param>
        /// <returns>如果找到返回 true，否则 false</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool TryGetMapped(object source, out object? destination)
        {
            destination = null;
            if (!_enabled || _trackedObjects == null)
                return false;

            return _trackedObjects.TryGetValue(source, out destination);
        }

        /// <summary>
        /// 清理跟踪的对象。
        /// </summary>
        public void Dispose()
        {
            _trackedObjects?.Clear();
        }

        /// <summary>
        /// 引用相等比较器，用于 Dictionary 键比较。
        /// </summary>
        private sealed class ReferenceEqualityComparer : IEqualityComparer<object>
        {
            public static readonly ReferenceEqualityComparer Instance = new ReferenceEqualityComparer();

            private ReferenceEqualityComparer() { }

            public new bool Equals(object? x, object? y) => ReferenceEquals(x, y);

            public int GetHashCode(object obj) => RuntimeHelpers.GetHashCode(obj);
        }
    }
}
