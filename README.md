# Ling.Mapper

[![NuGet](https://img.shields.io/nuget/v/Ling.Mapper.svg)](https://www.nuget.org/packages/Ling.Mapper/)
[![License](https://img.shields.io/badge/license-MIT-blue.svg)](https://github.com/yanhuuo/Ling.Mapper/blob/main/LICENSE)

以 `source.Adapt<T>()` 完成 .NET 对象与值转换。映射计划基于表达式树构建并缓存，支持常见标量转换、属性匹配、嵌套对象、集合及显式转换规则。

## 安装与快速开始

```sh
dotnet add package Ling.Mapper
```

```csharp
using Ling.Mapper.Extensions;

var source = new { Id = 1, Name = "示例", Amount = "123.45" };
var target = source.Adapt<OrderDto>();
Console.WriteLine(target.Amount); // 123.45m

public sealed class OrderDto
{
    public int Id { get; set; }
    public string? Name { get; set; }
    public decimal Amount { get; set; }
}
```

包版本及目标框架以[项目文件](https://github.com/yanhuuo/Ling.Mapper/blob/main/src/Ling.Mapper.csproj)为准；安装命令取得当前已发布版本，不表示工作树中的变更已经发布。由源码构建需使用支持 C# 14 的 SDK。

## 1.3.2 升级注意

**包含行为变更，不是无风险兼容补丁。** 默认独立可变图；ForMember/MapFrom 取值后继续映射，只有显式 final converter 保留返回实例；数值/日期文本采用执行时 CurrentCulture。依赖旧引用身份或固定区域性格式的代码必须检查并显式配置。升级 runtime 与 generator 两包后重新编译生成器消费者。

本次尚未全面达到 Mapster 性能对齐目标；NativeAOT 实际执行与真实 SQL 翻译没有本轮验证结论。详见 [1.3.2 变更与迁移](https://github.com/yanhuuo/Ling.Mapper/blob/main/CHANGELOG.md)。

## 功能与重要边界

- **方便的入口**：`Adapt<T>()`、显式 `IMapper`、结果回调和集合项回调。
- **转换规则**：安全直接赋值、显式整体转换器与字段规则、精确用户定义运算符、常见标量和对象/集合映射；优先级与边界见[转换与诊断](docs/转换与诊断.md)。
- **属性规则**：大小写/下划线匹配、忽略 null、运行时忽略属性，以及 Profile 的 ForMember、Rename、Ignore；新候选支持独立源类型 MapFrom、PreCondition、Condition、NullSubstitute 和显式配置验证。
- **已有实例与构造**：运行时 `MapInto` / `AdaptInto` 更新已有目标；支持公共有参构造绑定、ConstructUsing，以及 opt-in Flattening/GetX 约定，见[运行时补齐契约](docs/runtime-completion.md)。
- **投影与调用 context**：独立表达式投影和参数/服务上下文，见[支持子集与限制](docs/projection-context.md)；未把 LINQ-to-Objects 测试称为实际 SQL 翻译验证。
- **对象图与集合**：支持嵌套映射、一维数组、List、兼容接口与可由 `IEnumerable<T>` 构造的集合；在支持的形态中保留循环与共享引用。
- **安全诊断**：错误包含目标路径；默认不输出源值，可配置有界预览，并对已识别敏感路径及复杂值脱敏。
- **并发使用**：完成配置后的 mapper 可并发映射；运行中的规则替换不保证整个对象图使用同一版本。

普通 Map/Adapt 在支持范围内默认创建独立可变对象图，包括同型根、成员及集合；保留结果内部共享引用/循环，不复用源可变数据。普通 ForMember/MapFrom 是源选择后继续映射，只有明确 final converter 尊重其返回实例。资源/字段/无法保留的只读状态不冒充完整克隆，详见[独立图契约与变更](docs/independent-graphs.md)。数字与日期字符串默认采用转换时的 CurrentCulture，固定协议格式可显式指定 converter。null 与转换失败不同：通用 `Adapt<T>(null)` 遵循空值策略，不再自动创建空 DTO；非法文本不会静默变成默认值。

工作树新增泛型字典键值转换、不可变结构体构造绑定、嵌套目标写入、可逆路径 ReverseMap 与全局命名约定。真实 Source Generator 提供显式登记类型对的受限后端，不等同于运行时全部功能；Native AOT 验证状态见[新增功能与验证记录](https://github.com/yanhuuo/Ling.Mapper/blob/main/docs/新增功能与验证记录.md)。已发布版本与工作树能力请分别查看，不以源码存在代表已发布。

## 文档导航

[版本更新日志](https://github.com/yanhuuo/Ling.Mapper/blob/main/CHANGELOG.md)：1.3.2 行为迁移、新增功能及验证限制，以及历史版本说明。


| 需要了解 | 文档 |
|---|---|
| 独立复制、selector/final converter 与边界 | [独立对象图](docs/independent-graphs.md) |
| 已有实例、条件字段、构造与约定 | [运行时补齐](docs/runtime-completion.md) |
| IQueryable 投影、单次调用参数/服务 | [查询投影与 context](docs/projection-context.md) |
| 本轮新增能力、限制与待完成验证 | [新增功能与验证记录](https://github.com/yanhuuo/Ling.Mapper/blob/main/docs/新增功能与验证记录.md) |
| 当前版本变化、验证环境与发布状态 | [版本与验证记录](https://github.com/yanhuuo/Ling.Mapper/blob/main/docs/版本与验证记录.md) |
| 如何使用 API、Profile、选项和回调 | [使用文档](https://github.com/yanhuuo/Ling.Mapper/blob/main/docs/使用文档.md) |
| 转换顺序、数值/日期、null、异常与并发契约 | [转换与诊断](https://github.com/yanhuuo/Ling.Mapper/blob/main/docs/转换与诊断.md) |
| 运行时实现、职责与 AOT 限制 | [架构与 AOT](https://github.com/yanhuuo/Ling.Mapper/blob/main/docs/架构与AOT.md) |
| 哪些情况有断言、哪些仍未测或不支持 | [测试覆盖矩阵](https://github.com/yanhuuo/Ling.Mapper/blob/main/docs/测试覆盖矩阵.md) |
| 如何跑全量回归、独立套件和性能基准 | [测试入口](https://github.com/yanhuuo/Ling.Mapper/blob/main/tests/Ling.Mapper.Tests/README.md) |

## 验证与性能

测试项目是控制台执行器，`dotnet test` 不会自动执行这些场景：

```sh
dotnet build tests/Ling.Mapper.Tests/TestConsole.csproj -c Release -p:TargetFrameworks=net10.0
dotnet tests/Ling.Mapper.Tests/bin/Release/net10.0/TestConsole.dll --all
```

多运行时、时区与覆盖范围见测试文档。历史演示完成不等于每项预期都有断言。

本页不保留缺乏同环境对照的历史吞吐量数字，也不承诺“接近手写”或适用于所有模型的性能。请使用 `--benchmark-clean` / `--benchmark-concurrent` 对比同一机器、运行时和输入，并同时检查正确性、分配量与冷启动成本。

## 项目链接

- [源码](https://github.com/yanhuuo/Ling.Mapper)
- [NuGet](https://www.nuget.org/packages/Ling.Mapper/)
- [问题反馈](https://github.com/yanhuuo/Ling.Mapper/issues)
- [MIT 许可证](https://github.com/yanhuuo/Ling.Mapper/blob/main/LICENSE)

## 手动发布

当前源码准备 runtime 与 generator 1.3.2 双包；发布状态以 NuGet 官方源实际可下载为准。仓库不配置自动 CI/发布工作流，标签不会代替手动验收或发布。参见[手动发布步骤](docs/manual-release/README.md)及[1.3.1 验证记录](docs/1.3.1-validation.md)。
