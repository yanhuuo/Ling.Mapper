using System;
using System.Linq;

namespace Ling.Mapper.Models
{
    /// <summary>
    /// 映射配置缓存键
    /// 用于在 ConcurrentDictionary 中唯一标识一组映射行为（类型对 + 映射选项 + 忽略字段）。
    /// </summary>
    /// <remarks>
    /// 该结构是不可变的，且重写了相等比较逻辑，确保映射器能够根据不同的动态配置（如忽略字段）命中不同的编译委托。
    /// </remarks>
    internal readonly struct AdaptOptionsKey : IEquatable<AdaptOptionsKey>
    {
        private readonly AdaptOptions _options;
        private readonly string[]? _ignores;

        /// <summary>
        /// 初始化映射配置缓存键
        /// </summary>
        /// <param name="options">映射选项（如忽略大小写、忽略空值等）</param>
        /// <param name="ignores">运行时指定的忽略字段名称集合</param>
        public AdaptOptionsKey(AdaptOptions options, string[]? ignores = null)
        {
            _options = options;
            // 为了保证 GetHashCode 的一致性，通常建议对忽略列表进行排序
            // 但如果扩展方法层已经保证了顺序或这部分开销过大，可以直接存储
            _ignores = ignores;
        }

        /// <summary>
        /// 指示当前键是否与另一个缓存键相等
        /// </summary>
        /// <param name="other">另一个缓存键实例</param>
        /// <returns>如果配置和忽略字段完全一致则返回 true</returns>
        public bool Equals(AdaptOptionsKey other)
        {
            // 1. 检查基础选项是否一致
            if (_options != other._options) return false;

            // 2. 检查忽略列表引用是否一致（处理均为 null 的情况）
            if (ReferenceEquals(_ignores, other._ignores)) return true;

            // 3. 检查忽略列表内容是否一致
            if (_ignores == null || other._ignores == null) return false;
            if (_ignores.Length != other._ignores.Length) return false;

            // 性能优化：逐项比对（假设忽略列表通常很小）
            for (int i = 0; i < _ignores.Length; i++)
            {
                if (_ignores[i] != other._ignores[i]) return false;
            }

            return true;
        }

        /// <summary>
        /// 确定指定的对象是否等于当前缓存键
        /// </summary>
        public override bool Equals(object? obj)
            => obj is AdaptOptionsKey other && Equals(other);

        /// <summary>
        /// 返回当前缓存键的哈希值
        /// </summary>
        /// <remarks>
        /// 哈希值的计算包含映射选项和所有忽略字段的特征，以减少哈希碰撞。
        /// </remarks>
        public override int GetHashCode()
        {
            var hash = new HashCode();
            hash.Add((int)_options);

            if (_ignores != null)
            {       
                foreach (var t in _ignores)
                {
                    hash.Add(t);
                }
            }

            return hash.ToHashCode();
        }

        /// <summary>
        /// 返回表示当前配置的字符串
        /// </summary>
        public override string ToString()
        {
            var ignoreInfo = (_ignores == null || _ignores.Length == 0)
                ? "None"
                : string.Join(",", _ignores);
            return $"Options: {_options}, Ignores: [{ignoreInfo}]";
        }

        /* ============================================================
         * 运算符重载（可选，方便调试和扩展使用）
         * ============================================================ */

        public static bool operator ==(AdaptOptionsKey left, AdaptOptionsKey right) => left.Equals(right);
        public static bool operator !=(AdaptOptionsKey left, AdaptOptionsKey right) => !left.Equals(right);
    }
}
