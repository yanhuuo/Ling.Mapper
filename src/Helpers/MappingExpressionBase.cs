using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace Ling.Mapper.Helpers
{
    /// <summary>
    /// Profile 配置表达式读取器：
    /// - IgnoredMembers：忽略目标属性
    /// - RenamedMembers：destName -> srcName（支持嵌套路径）
    /// </summary>
    internal sealed class MappingExpressionBase
    {
        private readonly object _expr;
        private readonly FieldInfo? _ignored;
        private readonly FieldInfo? _renamed;

        public MappingExpressionBase(object expr)
        {
            _expr = expr;
            var t = expr.GetType();

            _ignored = t.GetField("IgnoredMembers", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
            _renamed = t.GetField("RenamedMembers", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
        }

        public bool IsIgnored(string name)
        {
            if (_ignored?.GetValue(_expr) is not IEnumerable e) return false;
            foreach (var x in e)
            {
                if (x is string s && s == name)
                    return true;
            }
            return false;
        }

        public string? GetRenamedSource(string destName)
        {
            if (_renamed?.GetValue(_expr) is not IDictionary d) return null;
            return d.Contains(destName) ? d[destName]?.ToString() : null;
        }
    }
}
