using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Ling.Mapper.Configuration
{
    /// <summary>
    /// Source Generator 代理入口（默认实现必须存在）。
    /// - Generator 可以生成同名 partial class 来覆盖 TryGetMapper 返回 true
    /// - 不使用 partial method，避免“必须实现”的编译规则冲突
    /// </summary>
    internal static partial class GeneratedMapperFactoryProxy
    {
        public static bool TryGetMapper(Type src, Type dest, out Func<object, object?>? mapper)
        {
            mapper = null;
            return false;
        }
    }
}
