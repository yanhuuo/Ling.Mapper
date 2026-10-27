# Regression console

This project is an executable test harness, not a test-SDK project. `dotnet test`
does not execute its scenarios. Use the .NET 10 SDK (the source uses C# 14).

```sh
dotnet build tests/Ling.Mapper.Tests/TestConsole.csproj -c Release -p:TargetFrameworks=net10.0
dotnet tests/Ling.Mapper.Tests/bin/Release/net10.0/TestConsole.dll --list
dotnet tests/Ling.Mapper.Tests/bin/Release/net10.0/TestConsole.dll --all
```

`--all` discovers every public static parameterless `Run` method in
`TestConsole.Test`, including methods on internal types. Every suite starts in a
fresh process with the original console profiles and converter registration.
A failed or crashed suite does not prevent the remaining suites from running.
A suite is terminated after ten minutes; timeout is a failure, not a pass.
Exit status is 0 for no detected failures, 1 for detected failures, and 2 for an
invalid command. `--suite Name` runs one suite. With no arguments, the original
menu is retained; end-of-input exits normally, and menu 0 uses the same runner.

## What the results mean

`LegacyContractRegressionTests` supplies deterministic checks for profiles,
JSON conversion, option and ignore-cache isolation, callbacks, cycles and
provider initialization. `ConversionRegressionTests` checks conversion contracts.
`AdaptCombinationRegressionTests` exercises all option/ignore/callback combinations,
consumer call forms, null and nullable behavior, callback ordering/counts, and
legacy overload semantics. The generated/AOT smoke also exercises the new entries.
The common `TestHelper` records assertions before throwing, so a legacy test
cannot accidentally erase a failure by catching the assertion exception.
`PrintActualExpected` now also asserts equality rather than only printing it.

Many older suites still contain demonstrations or partially asserted scenarios.
The runner reports these as **COMPLETED**, not "all assertions passed", and
prints a coverage warning. It observes their private failure counters and
explicit failure diagnostics to detect errors previously swallowed by their
catch blocks. Expected-exception prose and the `0 failures` summary are not
classified as failures. These compatibility diagnostics are not a substitute
for explicit assertions, and an old suite with no detected failures is not
proof that every printed expectation was checked. Performance threshold
warnings remain advisory; they are machine-dependent.

## Clean benchmark

```sh
dotnet tests/Ling.Mapper.Tests/bin/Release/net10.0/TestConsole.dll --benchmark-clean
```

The clean benchmark validates outputs, warms each workload, then records five
rounds without console I/O in the timed loops. It reports median/min/max ns/op
and median allocated bytes/op for simple same-type objects, numeric properties,
object collections and root numeric collections. Input allocation and forced
GC are outside the measurement. It measures warm mapping, not first-use plan
compilation. Run Release builds on the same runtime and machine, without
concurrent tests. Compare repeated runs rather than treating one duration as a
portable pass/fail threshold.

For a binary-compatible baseline, copy the complete built console output to a
separate directory, replace only its `Ling.Mapper.dll` with the baseline DLL,
and run the same `--benchmark-clean` command there. Keep the baseline tree and
current output separate. If an old implementation cannot satisfy a benchmark's
correctness check, report the failure instead of comparing invalid timings.

The library targets net6.0, net8.0, net9.0 and net10.0; the harness defaults to
net10.0. To opt into the runtime matrix, use a sufficiently new SDK and build:

```sh
for framework in net6.0 net8.0 net9.0 net10.0; do
  dotnet build tests/Ling.Mapper.Tests/TestConsole.csproj -c Release -p:TestTargetFrameworks="$framework" || exit 1
  dotnet "tests/Ling.Mapper.Tests/bin/Release/$framework/TestConsole.dll" --all || exit 1
done
```

Execute each output directory's TestConsole.dll with the matching installed
runtime. These four entries use the matching library target. Building all targets
is distinct from actually executing all four runtimes.

An optional net11.0 forward-compatibility run uses the net10.0 library and requires
a .NET 11 SDK, not just its runtime. The previously requested SDK/runtime pair was
11.0.100-rc.2.26504.105 / 11.0.0-rc.2.26504.105; it is a preview/RC environment,
not a stable .NET 11 release. It was **not** part of the Adapt validation below.
`LongToDateTimeDemo` now explicitly registers ticks converters and member
renames; nullable cases exercise lifting of the underlying registered rules.
This corrects its former fixture,
not an implicit timestamp-unit guarantee of the mapper.

## Concurrent throughput and allocations

```sh
dotnet tests/Ling.Mapper.Tests/bin/Release/net10.0/TestConsole.dll --benchmark-concurrent
```

This optional benchmark uses the same shared mapper and numeric DTO workload at
worker degrees 1, 2 and 8. Each worker owns its mutable source, changes its ID on
every operation, and validates every destination field. Dedicated workers are
created and warmed up before a shared start gate opens. Each degree has three
rounds of 50,000 mappings per worker, with median operations/second and allocated
bytes/operation reported. There is no machine-dependent pass threshold.

Allocation is measured with process-wide `GC.GetTotalAllocatedBytes(true)`;
it includes scheduling/completion measurement costs and any concurrent process
allocations. It is not the initiating thread's allocation counter. Timing also
includes correctness validation, gate release and worker completion, while task
creation, warmup, forced GC and console output are outside it. Run this mode
separately from other suites or benchmarks. The mode only uses the original
public mapper API, so the same binary-compatible baseline replacement procedure
above applies. A failed degree does not suppress other degrees' results, but the
aggregate failure produces a nonzero exit and invalidates performance comparison
for that degree.


## Adapt 组合入口验证（2026-10-27）

基线为 `55a3a427e76be58ec202d109b09726bc3d558338`，本轮仅验证新增扩展入口，
不代表新版本已发布。使用 .NET SDK 10.0.403：

| 验证 | 实际结果 |
| --- | --- |
| 库 net6.0 / net8.0 / net9.0 / net10.0 构建 | 全部成功；仍有原有 XML 文档和环境相关 NuGet 审计警告 |
| .NET 6.0.36 / 8.0.33 / 9.0.22 / 10.0.14 实际运行 | 每个运行时 42 suites，0 failed；旧演示仍不等于完整断言覆盖 |
| 新增 `AdaptCombinationRegressionTests` | 每个运行时 28 个场景通过，每场景含一项或多项显式检查 |
| 生成映射 smoke（JIT） | 76 assertions 通过；`dynamic-code-supported=True` |
| 生成器负向诊断 | 预期编译失败，实际包含 LMG001 和 LMG002 |
| 源码调用及旧 consumer 替换 DLL | 见 [API 兼容性 fixture](../Ling.Mapper.ApiCompatibility/README.md)，包括明确记录的无类型 null 歧义 |
| NativeAOT 发布及执行 | **未成功执行**；发布阶段 MSBuild TaskHost/命名管道受沙箱限制，报 MSB4216 / MSB4027 |

生成映射 JIT 与负向诊断可在仓库根目录复现：

```sh
dotnet run --project tests/Ling.Mapper.AotSmoke/Ling.Mapper.AotSmoke.csproj -c Release
# 此项目应编译失败；检查日志同时包含 LMG001 与 LMG002，而非把任意失败当作通过。
dotnet build tests/Ling.Mapper.GeneratorDiagnostics/Ling.Mapper.GeneratorDiagnostics.csproj -c Release
```

JIT smoke 通过不能代替 NativeAOT 通过。请在允许 TaskHost/命名管道的正常构建环境中
另行完成 NativeAOT 发布与原生可执行文件运行；本轮未绕过沙箱限制，也未验证 .NET 11。

## 转换与自定义字段规则验证（2026-10-27）

在扩展入口提交 `10322b34d2572e7ae67166f1965d9c3775a82ea7` 之后，
本轮增加以下有限契约回归。数字文本从 InvariantCulture 改为 CurrentCulture
是用户明确选择的默认行为变更，不是保留旧数字默认的兼容修复。

| 验证 | 实际结果 |
| --- | --- |
| SDK / 库目标构建 | SDK 10.0.403；net6.0 / net8.0 / net9.0 / net10.0 全部成功 |
| 四 runtime 实际全量运行 | .NET 6.0.36 / 8.0.33 / 9.0.22 / 10.0.14 各 45 suites，0 failed |
| `CurrentCultureConversionRegressionTests` | 每 runtime 10,649 断言：四 culture、同 mapper 热缓存切换、async culture 隔离、显式用户 culture、null/格式/溢出 |
| `DeclaredConversionRegressionTests` | 每 runtime 2,749 断言：implicit/explicit/nullable 声明、字段优先级、enum aliases/Flags/歧义、各入口/错误路径、注册替换及并发 |
| `SameTypeMemberRulesRegressionTests` | 每 runtime 596 断言：同型字段生效、已解析结果不二次映射、声明类型保留、缓存规则隔离、原有 cycle/identity 及并发 |
| .NET 10 三独立时区进程 | UTC / Asia/Shanghai / America/New_York 各运行 DateCulture（1,880 检查）和 CurrentCulture（10,649 断言），均通过 |

构建仍含仓库已有的 nullable/XML 文档警告、net6.0 停止支持提示以及受环境影响的
NuGet 审计警告；没有将这些警告改成忽略设置。旧 suite 的 COMPLETED 仍不代表其每个
演示都有断言。以上只证明列出的运行时与有限输入，不覆盖任意 CLR 类型。

生成器与 Native AOT 属于独立验证范围：详见[生成器说明](../../generators/Ling.Mapper.Generator/README.md)。
本轮 runtime 全量通过不意味着新原生产物已经发布或执行；未发布 NuGet、未改版本/标签/CI。

## 2026-10-27 运行时功能补齐

四 runtime（6.0.36、8.0.33、9.0.22、10.0.14）冻结源码各 **50 suites / 0 failed**。新增套件可用上面的 `--suite` 单独运行：

| 套件 | 每 runtime 明确检查 |
| --- | --- |
| `ExistingDestinationRegressionTests` | 894：root/member/collection/dictionary/nullable/ignore/condition/refs/callback/cache/concurrency、immutable 替换、init-only 路径拒绝、comparer 碰撞保留原内容 |
| `ConstructionFlatteningRegressionTests` | 47 cases / 124 assertions：class/record/struct 构造、重载/缺失/可选参数、工厂、flatten/GetX、有限 ignore 投影与真循环 |
| `MemberConfigurationRegressionTests` | 80：独立类型 MapFrom、条件时序、最终 null substitute、IgnoreNull 覆盖、验证与 ReverseMap 边界 |
| `MappingCallContextRegressionTests` | 1,453：参数/服务、scope 异常恢复、async/parallel 隔离、已有目标 context 入口 |
| `ProjectionRegressionTests` | 77：表达式形状、构造/嵌套/集合、延迟查询、unsupported；不是 SQL 翻译验收 |

独立 `tests/Ling.Mapper.ProjectionContextSmoke` 检查生成后端 context / projection 明确拒绝；API fixture 位于 `Ling.Mapper.ApiCompatibility` 和 `Ling.Mapper.MemberApiCompatibility`。这些不是 Native AOT 执行证据。契约参见[运行时补齐](../../docs/runtime-completion.md)与[投影/context](../../docs/projection-context.md)。

## 2026-10-27 独立图最终候选（新默认行为）

普通 Map/Adapt 现在复制支持范围内的可变图；普通 ForMember/MapFrom 是源选择后继续映射，显式 final converter 返回值不再复制。旧 identity 断言已按新契约迁移并检查数据及修改隔离，历史快照不再定义当前语义。详见[独立图契约](../../docs/independent-graphs.md)。

最终同一源码清单的 .NET 6/8/9/10 各 **58 suites / 0 failed**，普通库四目标构建 0 warnings / 0 errors（测试项目仍有 nullable/历史演示警告及 net6 EOL 提示）。新增矩阵 net6 为 9,114 个实际检查及 9 个不可用类型项目，其余 runtime 为 9,312；DictionaryDeep 3,166、DictionaryClone 67、MemberFinal 66、IndependentGraphSafety 27 cases / 53 assertions、ProjectionIndependentGraph 32。不同单位不累加成虚假总断言数。

```sh
# 一次一个 TFM，避免自定义 MSBuild 属性的分号转义及共享 obj 竞争。
for tfm in net6.0 net8.0 net9.0 net10.0; do
  dotnet build tests/Ling.Mapper.Tests/TestConsole.csproj -c Release -m:1 \
    -p:BuildInParallel=false -p:TestTargetFrameworks=$tfm -p:TargetFrameworks=$tfm || exit 1
  dotnet tests/Ling.Mapper.Tests/bin/Release/$tfm/TestConsole.dll --all || exit 1
done
```

外部独立 consumer 23 图安全 + 10 scope/concurrency + 8 declared-container 组通过。生成 JIT Graph 普通/checked 各 252、原 AotSmoke 普通/checked 各 115、context guard 12；两组负向项目各 9 条预期诊断。API source/binary 与新增 final converter consumer 通过；本批未执行 Native AOT、SQL provider 翻译或最终性能采样。小汇总见[验证清单](../../docs/validation/independent-graphs-20261027.json)。
