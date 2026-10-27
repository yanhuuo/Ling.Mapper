# Ling.Mapper.Generator

This is a real, explicit-pair Roslyn incremental source generator. It emits typed
DTO construction/member assignment and module-initializer registration for the
reflection-free `GeneratedMapperRegistry`. It does **not** infer every pair from
`Adapt<T>()` calls and does not interpret runtime Profiles.

## Consumer setup

The generator is a **separate analyzer package**, version 1.3.2, kept in sync
with the runtime release. The 1.3.2 additions below do not interpret
runtime Profiles. Source version alone does not prove public package availability. Version 1.3.0 was published and independently
consumed from NuGet; the 1.3.1 source version alone is not publication evidence.
Both packages are released manually. No active GitHub Actions workflow remains;
see [manual release](../../docs/manual-release/README.md).

The generator is a **separate analyzer package**. Reference both `Ling.Mapper`
and `Ling.Mapper.Generator`; installing only the runtime package does not enable
generation. For a source checkout, use a normal project reference to the runtime
and an analyzer-only reference to this project:

```xml
<ProjectReference Include="path/to/src/Ling.Mapper.csproj" />
<ProjectReference Include="path/to/generators/Ling.Mapper.Generator/Ling.Mapper.Generator.csproj"
                  OutputItemType="Analyzer" ReferenceOutputAssembly="false" />
```

For packaged consumption, use a normal `PackageReference` to `Ling.Mapper` and
an analyzer `PackageReference` to `Ling.Mapper.Generator` with `PrivateAssets="all"`.
The generator package contains its DLL under `analyzers/dotnet/cs`; Roslyn loads
it automatically. The analyzer targets netstandard2.0 and uses Roslyn 4.14 APIs;
the runtime source currently requires a C# 14-capable SDK. Runtime package target
frameworks remain net6.0/net8.0/net9.0/net10.0. The native smoke project targets
net10.0; no claim is made that every library target supports Native AOT publishing.

```csharp
using Ling.Mapper;
using Ling.Mapper.Extensions;
using Ling.Mapper.Provider;
[assembly: GeneratedMap(typeof(Source), typeof(Destination))]

MapperProvider.SetCurrent(GeneratedMapperRegistry.CreateMapper());
var mapped = new Source { Id = "42" }.Adapt<Destination>();
public sealed class Source { public string Id { get; set; } = ""; }
public sealed class Destination { public int Id { get; set; } }
```

On Native AOT, default `MapperConfiguration.CreateMapper()` uses generated
dispatch. On JIT, explicitly select `GeneratedMapperRegistry.CreateMapper()` to
compare generated behavior with the runtime mapper. The object overload uses the
actual source type, so a registered type boxed as object works; an unknown pair
throws a safe `MappingException` identifying `NotSupportedException`, without
falling back to reflection.

## Generated contract and deliberate boundaries

Supported: concrete DTOs with public parameterless constructors and public
read/write properties; explicit scalar pairs; matching options, ignore names,
null policy, callbacks; registered nested pairs; arrays/lists (including common
read-only/enumerable destination interfaces); per-root cycle/shared-reference
context; checked scalar errors with safe member/index paths. Numeric conversion
uses the shared runtime scalar conversion semantics, including midpoint-to-even
rounding, not truncating casts. Enum and text conversions use the shared scalar
contract; known enum metadata is preserved as described below.

Different-type nested object pairs still require a `GeneratedMap` declaration.
Same-type mutable DTO children and supported array/list element shapes are now
discovered recursively from a declared root pair; cycles are deduplicated during discovery.
Declared pairs also register supported array/list collection shapes. Nullable source pairs
account for CLR boxing of non-null nullable values. Getter, constructor and setter
exceptions are wrapped without retaining unsafe original exceptions.

Dotted ignored-property paths are explicitly rejected in generated mode, rather
than silently ignored. Top-level masks are preserved per collection item and
reference-cache projection.

Not interpreted: runtime Profiles, ForMember expressions, ReverseMap, arbitrary
conventions, and TypeConverterRegistry registrations. These must not silently
change generated behavior. Unsupported destination construction (including
required/init-only DTOs) is a build error `LMG001`; unsupported matched member
conversions such as undeclared nested pairs or dictionaries produce `LMG002`.
Immutable-constructor/dictionary capabilities of the runtime mapper must not be
presented as automatically supported by this generated subset. A deliberately
hand-written AOT-safe mapping can be registered with the typed
`GeneratedMapperRegistry.Register<TSource, TDestination>` API.

## Native verification

The executable test is `tests/Ling.Mapper.AotSmoke`, not the interactive legacy
console. It checks generated execution, dynamic object dispatch, null policy,
rounding, enums, collections, cycles, context isolation, concurrent roots and
safe diagnostic paths. EmitCompilerGeneratedFiles preserves the generated code
under its obj/generated directory.

```sh
dotnet run --project tests/Ling.Mapper.AotSmoke -c Release
dotnet publish tests/Ling.Mapper.AotSmoke -c Release -r linux-x64 \
  -p:NativeSmoke=true -p:CppCompilerAndLinker=gcc
./tests/Ling.Mapper.AotSmoke/bin/Release/net10.0/linux-x64/publish/Ling.Mapper.AotSmoke --require-aot
```

A successful build alone is not an AOT validation. Run the native executable and
check `dynamic-code-supported=False` and its assertion summary. Compiler/linker
prerequisites and the matching official ILCompiler package must be installed.

### Historical 1.3.0 native build environment

The following records historical 1.3.0 validation only. Native AOT execution was
not rerun or verified for 1.3.1; see [1.3.1 validation](../../docs/1.3.1-validation.md).

The validation workspace prohibits local IPC sockets, and its escalation launcher
also failed before executing the compiler. Native publishing was completed using
MSBuild's documented `UsingTask Override="true"` and `AssemblyTaskFactory` to run
the **unmodified official** `ComputeManagedAssemblies` ILLink task in the current
process, instead of its socket-dependent TaskHost. This was a temporary build-only
target, not a modification to ILCompiler, generated source, runtime code, or OS
security settings. Standard machines do not need this adaptation. The native
binary still reports `RuntimeFeature.IsDynamicCodeSupported == false`.


## 1.3.2：operator、EnumMember 与当前区域性

生成器不再为字符串到枚举单独发出 `Enum.Parse` 而绕过运行时规则。所有标量入口
统一调用共享 scalar helper：字符串数值/日期等转换在**执行时**读取 `CurrentCulture`，
不在代码生成或映射计划编译时捕获区域性。同一个 mapper 切换区域性后立即使用新值。
用户显式手写转换方法中的 culture 不由生成器改写。

| 能力 | Runtime | Generator |
| --- | --- | --- |
| 唯一 public static 精确源/目标 `implicit` / `explicit` operator | 支持 | 值类型源根 pair、成员转换支持静态调用 |
| 引用类型源根 operator | 支持，每次映射出现都调用 | **LMG001 拒绝**；生成分派的引用缓存会改变调用次数，不能静默使用 DTO 映射代替 |
| 具体声明的引用类型源成员 operator | 支持 | 支持直接调用；相同引用的多个成员分别执行，不缓存 operator 结果 |
| abstract/interface 声明源成员上的 operator | 按实际运行时源类型分派 | **LMG002 拒绝**；不能静态调用声明类型的 operator 冒充运行时分派 |
| 完整声明的 Nullable 参数/返回 operator，以及底层 operator 的 nullable 桥接 | 声明 pair 优先；否则仅做无歧义 nullable 桥接 | 同一选择规则，null 仍先遵循 mapper 默认/Throw 策略 |
| 根 Nullable 源在 boxing 前后选到不同 operator | 各入口按实际或声明源类型选择 | **LMG001 拒绝**；相同选择时才生成，不能用根注册别名偷偷改变优先级 |
| 多个匹配 operator 候选 | 明确拒绝 | 根 LMG001、成员 LMG002，不任意选一个 |
| 内置数值之间的 decimal 等 operator | 不抢占已有 Convert 规则 | 同样不抢占，继续保留 midpoint-to-even 与溢出行为 |
| `EnumMember.Value` 字符串别名与 Flags token | 支持 | 共享同一规则，包含 nullable 成员 |
| alias 碰撞、非法 alias | 明确拒绝 | 共享同一拒绝规则，解析和格式化都检查 |
| runtime Profile / ForMember / TypeConverterRegistry | 支持的运行时配置 | 不解释，仍明确拒绝，不能宣称生成器自动支持 |

operator 搜索只认声明在源或目标类型上的单参数公开静态转换。先匹配完整声明 pair，
无精确候选时仅允许 nullable 参数提升/取值和返回值提升，不允许 nullable 返回自动拆成非 nullable。
多个桥接候选明确拒绝。根 Nullable 源还要核对 boxing 后的选择是否相同；成员则保留声明源类型。
静态调用显式使用
`unchecked`，不会因消费者开启 checked 构建而选到另一个 `op_CheckedExplicit`。不作数值链、
基类参数强转链或多步转换，也不绕过 mapper 对 ref-like 类型的限制。opaque 根 operator 不接受成员 ignore 投影。
异常保留其原始类型诊断并标出目标路径；operator 抛出的空值类异常不会被当成源表达式空值吞掉。

对 pair 及其成员/集合中静态已知的 enum，生成的 module initializer 发出
`DynamicDependency(DynamicallyAccessedMemberTypes.PublicFields, typeof(Enum))`，
明确保留共享 `EnumMember` 解析需要的公开字段元数据；它不是依赖 JIT 恰好保留字段。
这符合 .NET 的 [DynamicDependency 保留约定](https://github.com/dotnet/runtime/blob/main/docs/design/tools/illink/reflection-flow.md)。
未知 runtime 类型仍没有反射映射 fallback。

### 本轮验证边界

本轮新增 smoke 覆盖 operator 根/成员/nullable、异常路径、alias/flags/冲突、
同 mapper 切换 fr-FR/en-US，以及运行时/生成器结果对照；默认与 checked 消费构建各
通过 115 assertions（生成器 JIT）。负向项目仅出现预期的 9 个 LMG001/LMG002，包含重复 operator、
引用源根 operator、Nullable boxing 选择冲突、多候选 nullable 桥接，抽象声明源 operator，以及 ref-like 成员的明确诊断。实际结果见本轮验证归档。
**NativeAOT 发布仍被沙箱 TaskHost/命名管道限制阻塞，未成功执行原生程序**；
生成源码中的保留标注和 JIT smoke 通过不等价于已完成 trimming/NativeAOT 执行验证。


## 1.3.2：默认独立可变图

用户指定的新默认契约替换了旧的同型 identity fast path：普通生成映射不再因为类型可赋值就
返回源可变对象。`string`、已知数值/枚举/日期等 immutable atoms 可共享；DTO、数组、List 和
包含可变引用的可写属性 struct 需要独立构造。`Half`/`Int128`/`UInt128` 的同型值也按原子处理，
不把它们错误当作资源对象；这不扩大所有新数值间解析/转换支持。

```csharp
[assembly: GeneratedMap(typeof(Graph), typeof(Graph))]
// Graph 的 Child/Node 自映射及支持的数组/List元素形状自动发现；不必给每个相同类型 child 写属性。
var copy = GeneratedMapperRegistry.CreateMapper().Map<Graph>(graph);
```

输出图维护每次调用自己的引用表：同型根/成员/数组/List 不共享源可变节点，但输出内重复 child
和普通引用循环仍共享同一个新 child。支持公开可写属性 struct/nullable struct 的可变属性递归复制。
`object`/interface 中的实际可变类型（也包括 `List<object>`/`object[]` 元素）只走**已有生成注册**的
实际类型映射；未知类型明确失败，绝不以原引用代替复制。声明的具体 root self pair 仍需要存在，
不是任意运行时类型的反射复制器。一个显式 source→other-type pair 不等于自动注册所有 root self pair。

刻意限制：字典、未知集合形状/多维数组、field-only struct、私有或只读实例状态、不断扩张的泛型递归、系统资源不自动
复制。已知不支持的静态形状用 LMG001/LMG002；动态 opaque 值在运行时用安全的 MappingException
指出 NotSupportedException。可能涉及 boxed value-type 循环、且构造完成前无法登记正确值的图也明确
失败，而不是栈溢出或返回不完整值。用户手写的显式生成映射 delegate/转换 operator 仍定义其最终结果；
该用户代码的资源与返回对象所有权不能由生成器推断。

仍不解释运行时 Profile / MapFrom / member ConvertUsing / 服务；不要把这些 runtime API 宣称为生成能力。
同样，没有进行本批 NativeAOT 发布或执行；下列是 **JIT** 功能验证，正常与 checked consumer 均运行。

```sh
dotnet run --project tests/Ling.Mapper.GeneratedGraphSmoke -c Release
# 应失败，检查九条具体 LMG001，而不是把任意构建失败都当成通过。
dotnet build tests/Ling.Mapper.GeneratedGraphDiagnostics -c Release
```

生成器 JIT smoke 的历史两条同型 identity 预期已改成独立图预期；新的 graph fixture 同时检查
深链循环、图内共享、对象/interface/容器动态槽、值类型中的引用、结果修改不影响源、并发隔离和显式拒绝。

### Generated member hot path

Generated DTO members use value-type path scopes and inline guarded assignments,
not captured `Action` delegates. Identical immutable member types are assigned
without boxing through scalar conversion. Getter and setter exceptions are still
wrapped inside the active scope; null policy, ignored-null initializers and scalar
conversions between different types retain their existing paths.

The per-root context builds full diagnostic paths only on failure, tracks source and
destination sensitivity, and keys graph references by source identity, destination
type and projection. It does not trade graph ownership/cycle preservation for shallow
copies. Public `Member` delegates remain supported for hand-written registrations.
These implementation changes do not by themselves establish NativeAOT validation.

## 1.3.2 release migration and limits

Upgrade **both** runtime and generator to 1.3.2 and recompile consumers. Previously
compiled generated member assignments do not gain independent graph ownership by
replacing the runtime DLL alone. CurrentCulture is read during conversion. Runtime
MapInto, Profile/MapFrom and context features are not automatically generator features.
This release contains observable behavior changes despite its requested patch version.
Full Mapster performance parity has not been achieved. Current NativeAOT execution
and actual SQL translation remain unverified; JIT tests are not native evidence.
