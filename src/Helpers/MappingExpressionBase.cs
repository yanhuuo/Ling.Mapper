using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Linq.Expressions;
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
        private readonly IDictionary<string, LambdaExpression>? _bindings;

        public MappingExpressionBase(object expr)
        {
            _expr = expr;
            _bindings = expr.GetType().GetField("CustomMemberBindings", BindingFlags.Instance | BindingFlags.NonPublic)?.GetValue(expr) as IDictionary<string, LambdaExpression>;
            var t = expr.GetType();

            _ignored = t.GetField("IgnoredMembers", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
            _renamed = t.GetField("RenamedMembers", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
        }

        public IEnumerable<string> ConfiguredPaths => (_bindings?.Keys ?? Enumerable.Empty<string>())
            .Concat((_renamed?.GetValue(_expr) as IDictionary)?.Keys.Cast<string>() ?? Enumerable.Empty<string>())
            .Distinct(StringComparer.Ordinal);
        public IEnumerable<string> IgnoredPaths => (_ignored?.GetValue(_expr) as IEnumerable)?.Cast<string>() ?? Enumerable.Empty<string>();

        public LambdaExpression? GetCustomBinding(string name) => _bindings != null && _bindings.TryGetValue(name, out var binding) ? binding : null;

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
