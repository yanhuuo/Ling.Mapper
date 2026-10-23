using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Ling.Mapper.Configuration
{
    /// <summary>
    /// Reserved generated-mapper lookup boundary. No source generator ships in this
    /// package yet; runtime reflection/interpretation alone does not guarantee NativeAOT.
    /// A future generator integration must provide dispatch here, not redeclare this method.
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
