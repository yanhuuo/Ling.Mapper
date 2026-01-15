using System;

namespace Ling.Mapper
{
    /// <summary>
    /// 映射时的属性匹配规则选项
    /// </summary>
    public class AdaptOptions
    {
        /// <summary>
        /// 获取或设置是否忽略属性名称的大小写匹配。默认值：false
        /// </summary>
        /// <remarks>
        /// 当设置为 true 时，源属性 "userName" 可以匹配目标属性 "UserName"
        /// </remarks>
        public bool IgnoreCase { get; set; }

        /// <summary>
        /// 获取或设置是否忽略属性名称中的下划线字符。默认值：false
        /// </summary>
        /// <remarks>
        /// 当设置为 true 时，源属性 "user_name" 可以匹配目标属性 "UserName" 或 "username"
        /// </remarks>
        public bool IgnoreUnderscore { get; set; }

        /// <summary>
        /// 获取或设置要忽略映射的属性名称集合
        /// </summary>
        /// <remarks>
        /// 这些属性在映射后会被重置为默认值
        /// </remarks>
        public string[]? IgnoreProperties { get; set; }

        /// <summary>
        /// 获取或设置是否忽略 null 值属性的映射。默认值：false
        /// </summary>
        /// <remarks>
        /// 当设置为 true 时，源对象中值为 null 的属性不会覆盖目标对象
        /// </remarks>
        public bool IgnoreNullValues { get; set; }

        /// <summary>
        /// 创建默认的映射选项（不启用任何特殊规则）
        /// </summary>
        public static AdaptOptions Default => new AdaptOptions();

        /// <summary>
        /// 创建启用忽略大小写的映射选项
        /// </summary>
        public static AdaptOptions IgnoreCaseOption => new AdaptOptions { IgnoreCase = true };

        /// <summary>
        /// 创建启用忽略下划线的映射选项
        /// </summary>
        public static AdaptOptions IgnoreUnderscoreOption => new AdaptOptions { IgnoreUnderscore = true };

        /// <summary>
        /// 创建启用忽略大小写和下划线的映射选项
        /// </summary>
        public static AdaptOptions FlexibleOption => new AdaptOptions 
        { 
            IgnoreCase = true, 
            IgnoreUnderscore = true 
        };
    }
}
