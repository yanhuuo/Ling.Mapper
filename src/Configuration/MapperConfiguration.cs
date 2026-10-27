using System;
using System.Collections.Generic;
using Ling.Mapper.Mapper;
using Ling.Mapper.Models;

namespace Ling.Mapper.Configuration
{
    /// <summary>
    /// 映射器全局配置。
    /// 用于注册 Profile、配置全局约定，以及创建 IMapper 实例。
    /// </summary>
    public class MapperConfiguration
    {
        /// <summary>
        /// 内部存储的所有映射配置。
        /// </summary>
        private readonly List<IMappingConfig> _configs = new();

        /// <summary>
        /// 全局约定配置的委托列表，用于延迟构建。
        /// </summary>
        private readonly List<Action<GlobalConventionOptions>> _globalConventions = new();
        private int _builtConventionCount;

        /// <summary>
        /// 全局约定选项实例。
        /// </summary>
        internal GlobalConventionOptions GlobalOptions { get; } = new();

        /// <summary>
        /// 🆕 默认的 AdaptOptions，用于所有 Adapt 调用（如果没有显式指定）。
        /// 默认值：忽略大小写和下划线（FlexibleOption）。
        /// </summary>
        /// <remarks>
        /// 可以在应用启动时配置：
        /// <code>
        /// config.DefaultAdaptOptions = new AdaptOptions 
        /// { 
        ///     IgnoreCase = true, 
        ///     IgnoreUnderscore = true 
        /// };
        /// </code>
        /// 或者禁用默认行为：
        /// <code>
        /// config.DefaultAdaptOptions = null;  // 精确匹配
        /// </code>
        /// </remarks>
        private AdaptOptions? _defaultAdaptOptions = AdaptOptions.Default;
        private bool _hasExplicitDefault;
        public AdaptOptions? DefaultAdaptOptions
        {
            get => _defaultAdaptOptions;
            set { _defaultAdaptOptions = value; _hasExplicitDefault = true; }
        }
        internal AdaptOptions EffectiveDefaultOptions => _hasExplicitDefault || _globalConventions.Count == 0
            ? _defaultAdaptOptions ?? AdaptOptions.Default
            : (GlobalOptions.CaseInsensitiveNameMatch ? AdaptOptions.IgnoreCase : AdaptOptions.Strict) |
              (GlobalOptions.IgnoreSpecialCharacters ? AdaptOptions.IgnoreUnderscore : AdaptOptions.Strict);

        /// <summary>
        /// 所有注册的映射配置集合。
        /// </summary>
        internal IEnumerable<IMappingConfig> Configs => _configs;
        internal bool HasExplicitConventions => _globalConventions.Count != 0;

        /// <summary>
        /// 是否启用严格模式，默认为 false。
        /// 若为 true，不可构造的目标对象在映射时抛出异常；否则未注册且无可绑定构造函数的引用类型映射可返回 null。
        /// 显式注册映射的构造函数冲突或缺少必要输入始终在计划构建时失败；可用 ConstructUsing 选择构造方式。
        /// 此选项不改变未匹配属性的默认忽略行为，也不抑制标量转换错误。
        /// </summary>
        public bool StrictMode { get; set; } = false;

        /// <summary>Validate registered maps for unmapped destination members when CreateMapper is called.
        /// Defaults to false; this is separate from StrictMode and does not restrict unregistered runtime type pairs.</summary>
        public bool RequireDestinationMemberSource { get; set; }

        /// <summary>Validate registered maps, including writable destination source coverage and constructor binding, without executing source getters or user converters.
        /// Throws an aggregate MappingConfigurationException containing type pairs and member paths.</summary>
        public void AssertConfigurationIsValid()
        {
            BuildGlobalConventions();
            MappingConfigurationValidator.Validate(this);
        }

        /// <summary>How null values map to non-nullable destinations. Defaults to UseDefault.</summary>
        public NullValueHandling NullValueHandling { get; set; } = NullValueHandling.UseDefault;
        /// <summary>Include a bounded safe scalar preview in mapping errors. Defaults to false.
        /// Sensitive paths, byte arrays and complex values are always redacted.</summary>
        public bool IncludeSourceValueInExceptions { get; set; }
        /// <summary>Maximum length of a diagnostic scalar preview. Defaults to 128.</summary>
        public int MaxDiagnosticValueLength { get; set; } = 128;

        /// <summary>
        /// 添加一个映射配置 Profile。
        /// </summary>
        /// <param name="profile">映射配置 Profile 实例。</param>
        public void AddProfile(MapperProfile profile)
        {
            if (profile == null) throw new ArgumentNullException(nameof(profile));
            _configs.AddRange(profile.Configs);
        }

        /// <summary>
        /// 批量添加多个映射配置 Profile。
        /// </summary>
        /// <param name="profiles">Profile 实例数组。</param>
        public void AddProfiles(params MapperProfile[] profiles)
        {
            if (profiles == null) return;
            foreach (var p in profiles)
            {
                AddProfile(p);
            }
        }

        /// <summary>
        /// 配置全局映射约定，例如属性名称大小写不敏感匹配等。
        /// 该方法可以被调用多次，所有配置会累积应用。
        /// </summary>
        /// <param name="convention">用于配置全局约定的委托。</param>
        public void ConfigureConventions(Action<GlobalConventionOptions> convention)
        {
            if (convention == null) return;
            _globalConventions.Add(convention);
        }

        /// <summary>
        /// 构建全局约定配置。
        /// 在创建 IMapper 之前会调用该方法，以应用用户配置的所有约定。
        /// </summary>
        internal void BuildGlobalConventions()
        {
            while (_builtConventionCount < _globalConventions.Count)
            {
                _globalConventions[_builtConventionCount](GlobalOptions);
                _builtConventionCount++;
            }
        }

        /// <summary>
        /// 创建一个新的 IMapper 实例。
        /// 内部会构建全局约定并编译所有已注册的映射表达式。
        /// </summary>
        /// <returns>IMapper 实例。</returns>
        public IMapper CreateMapper()
        {
            if (NullValueHandling != NullValueHandling.UseDefault && NullValueHandling != NullValueHandling.Throw)
                throw new ArgumentOutOfRangeException(nameof(NullValueHandling));
            if (MaxDiagnosticValueLength < 0)
                throw new ArgumentOutOfRangeException(nameof(MaxDiagnosticValueLength));
            BuildGlobalConventions();
            if (RequireDestinationMemberSource) MappingConfigurationValidator.Validate(this);
            if (!System.Runtime.CompilerServices.RuntimeFeature.IsDynamicCodeSupported)
            {
                if (_configs.Count != 0 || HasExplicitConventions)
                    throw new NotSupportedException("Generated mapping does not support runtime profiles or conventions. Use explicitly generated pairs and supported options.");
                return GeneratedMapperRegistry.CreateMapper(this);
            }
            return new Mapper.Mapper(this);
        }
    }

    /// <summary>
    /// 全局映射约定选项。
    /// 可以通过 <see cref="MapperConfiguration.ConfigureConventions"/> 配置。
    /// </summary>
    public class GlobalConventionOptions
    {
        /// <summary>
        /// 是否在属性名匹配时忽略大小写，默认 true。
        /// </summary>
        public bool CaseInsensitiveNameMatch { get; set; } = true;
        /// <summary>
        /// 是否忽略特殊字符（如下划线），并自动进行命名规范化。
        /// 例如 a_type -> AType。
        /// </summary>
        public bool IgnoreSpecialCharacters { get; set; } = false;

        /// <summary>Opt in to flattening names such as CustomerName from Customer.Name. Direct members take precedence.</summary>
        public bool EnableFlattening { get; set; }

        /// <summary>Opt in to public, parameterless GetX methods as source members. Properties take precedence.</summary>
        public bool EnableGetMethods { get; set; }
    }
}
