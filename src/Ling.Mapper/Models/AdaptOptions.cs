using Ling.Mapper.Extensions;

namespace Ling.Mapper.Models;
/// <summary>
/// Adapt 映射语义选项（位标志）
/// </summary>
[Flags]
public enum AdaptOptions
{
    /// <summary>
    /// 不启用任何特殊规则（严格匹配）
    /// </summary>
    Strict = 0,

    /// <summary>
    /// 忽略大小写
    /// </summary>
    IgnoreCase = 1 << 0,

    /// <summary>
    /// 忽略下划线（user_name → UserName）
    /// </summary>
    IgnoreUnderscore = 1 << 1,

    /// <summary>
    /// 忽略源对象中的 null 值
    /// </summary>
    IgnoreNullValues = 1 << 2,

    /// <summary>
    /// 默认选项：
    /// 忽略大小写 + 忽略下划线
    /// </summary>
    Default = IgnoreCase | IgnoreUnderscore,

    /// <summary>
    /// 兼容旧命名：仅忽略下划线
    /// </summary>
    IgnoreUnderscoreOption = IgnoreUnderscore,

    /// <summary>
    /// 兼容旧命名：组合选项（IgnoreCase + IgnoreUnderscore）
    /// </summary>
    FlexibleOption = IgnoreCase | IgnoreUnderscore
}
