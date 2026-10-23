# 架构与 Native AOT 边界

## 当前职责

- `Extensions/AdaptExtensions.cs`：方便的 `source.Adapt<T>()` 及结果/元素回调入口。
- `Configuration/MapperConfiguration.cs`：Profile、默认选项、空值及异常诊断策略。
- `Mapper/Mapper.cs`：映射计划选择、表达式构建与缓存；直接赋值、数值及已知自定义规则保留编译后的快路径。
- `Mapper/Mapper.Collections.cs`：集合构造与逐项映射；反射构造信息在计划建立时处理，而非每项反复查找。
- `Mapper/ScalarConversion.cs`：统一标量转换，不承担对象图映射。
- `Mapper/MappingContext.cs`：每次顶层映射独立的循环/共享引用与错误路径状态。
- `Mapper/MappingException.cs`：类型、路径、空值策略与安全诊断信息。
- `TypeConverter/TypeConverterRegistry.cs`：显式转换规则及委托缓存。

删除的旧内部转换/循环/嵌套辅助实现应以调用点核对为依据，不保留同一功能的第二套不被调用实现。公开扩展方法与包名称不因文件整理改变。

## 反射不等于每次反射赋值

当前常规运行时通过反射读取类型结构，构建表达式树并缓存委托。缓存热路径执行编译后的委托，但类型发现、首次构造与动态集合类型仍依赖运行时能力；因此不能描述成“零反射”。性能回归需分别检查首次构建、缓存热调用、集合与并发场景。

## 两个后端与明确边界

常规运行时保留表达式计划后端。`Mapper.Dictionaries.cs` 负责泛型键值映射及碰撞检测，`Mapper.Constructors.cs` 负责不可变结构体构造绑定，`Mapper.DestinationPaths.cs` 负责嵌套写入和 struct 写回。MappingContext 按源引用、声明目标类型、忽略投影隔离目标实例，每次 root 调用独立。

`generators/Ling.Mapper.Generator` 是真实增量生成器；assembly `GeneratedMap(typeof(Source), typeof(Target))` 声明显式类型对，生成强类型构造/赋值代码及 ModuleInitializer 注册。`GeneratedMapperRegistry` 与 `GeneratedMappingContext` 提供登记、无反射对象构建的调度、共享引用/循环和安全异常。无动态代码能力时 CreateMapper 选择此后端；未登记且不可安全赋值的类型对明确失败，禁止回退到反射/表达式构建。

生成后端支持显式登记的标量对、公开无参 DTO、已登记子类型对，以及由登记元素对派生的 List/一维数组和 IEnumerable/IList/IReadOnlyList 目标。`object` 入口按实际类型查询登记。标量使用共享转换规则，不能以普通 C# 浮点强转替代 Convert 的舍入语义。

当前不解释 runtime Profile、TypeConverterRegistry、任意 conventions；相关配置明确拒绝。不可变构造绑定、字典、nested target、计算 ReverseMap 不因运行时支持而自动成为生成后端能力。生成成员不支持的类型组合产生诊断，未知动态类型运行时失败；顶级忽略名可用，dotted 忽略路径拒绝。非 DTO、required/init 目标等需按生成器诊断调整或显式手写 AOT-safe 注册。

源码使用方式与最小可运行工程见 [AOT smoke 项目](../tests/Ling.Mapper.AotSmoke/Ling.Mapper.AotSmoke.csproj)：生成器需作为 Analyzer ProjectReference 引用，不能仅引用主库便假定生成器已安装。Native AOT 发布脚本见 [run-native.sh](../tests/Ling.Mapper.AotSmoke/run-native.sh)。生成器采用独立 `Ling.Mapper.Generator` analyzer 包，必须与 `Ling.Mapper` runtime 包一起引用；接入与支持范围见[生成器说明](../generators/Ling.Mapper.Generator/README.md)。本地包验收不等同于 NuGet 发布。

`RuntimeFeature.IsDynamicCodeSupported` 是后端能力检测，不代表所有模型可 AOT。常规 CoreCLR 测试、生成代码编译、生成后端 JIT smoke、原生 publish 和原生产物执行是不同证据；实际结果单独记录在[新增功能与验证记录](新增功能与验证记录.md)，不能互相替代。不宣称全部 CLR 类型或完整运行时 API 已经 AOT 兼容。
