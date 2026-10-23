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

## 本轮不是完整 Native AOT 支持

`RuntimeFeature.IsDynamicCodeSupported` 检测的是动态代码能力，不是唯一的 AOT 身份开关。表达式解释执行也不能解决元数据裁剪、运行时泛型实例化和全部类型发现问题。

仓库的 `GeneratedMapperFactoryProxy` 目前只是占位入口，尚无真实 Source Generator。消费者程序集不能通过同名 partial class 覆盖已编译库的方法。没有发布并运行实际 AOT 产物前，不宣称 AOT 验收通过；多运行时 CoreCLR 回归也不等于 Native AOT 验证。

未来可保持 `a.Adapt<T>()` 调用方式，采用运行时缓存与编译期生成两条后端，共享转换策略。编译时能识别的类型对可自动生成；`object` 在运行时可识别实际类型并查询已生成映射，不应仅因声明为 `object` 就拒绝。对于编译时无法发现且未登记的类型对，应明确报错，不能在 AOT 下静默退回任意反射构建。类型识别本身不能凭空生成被裁剪的代码。

依据：[Microsoft Native AOT 部署说明](https://learn.microsoft.com/en-us/dotnet/core/deploying/native-aot/)，其中明确列出动态代码生成、裁剪和表达式解释执行的限制。
