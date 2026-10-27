# Compiled member null shortcut (2026-10-27)

Baseline: `5db129273ff4486aa9331e50d24619fc44c9f435` (accepted complex dictionary value plan).

## Scope and equivalence

Only `Mapper.BuildMemberConversion` changes. For a nullable/reference source and a statically reference/nullable destination, a null value now produces `default(destination)` without calling `MapMember`/`MapValue`. Reference checks use `ReferenceEqual`; nullable structs use `!HasValue`, never a user equality operator.

`MapValue` already returns null before runtime type resolution, converter lookup, reference tracking, or callbacks for precisely these cases. The shortcut stays inside conversion: source resolution, null substitute choice, precondition, ignore-null, condition, assignment and their exception wrappers remain in their original order. Ordinary sources and constructor arguments are locals; null substitutes passed into this builder are converted constants. No selector is duplicated. Non-nullable value destinations retain the original fallback and Throw/UseDefault policy. Numeric conversion methods and the selector-only NRE/ArgumentNullException compatibility catch are unchanged.

Constructor arguments share this builder, so they benefit without editing constructor code. Existing-target conventional complex members have a dedicated `MapMemberInto` branch and are intentionally not changed. No root dispatch, collection loop, path pool, cache, TLS, version, or publication changes.

## Correctness

Added `MemberNullShortcutRegressionTests`: 45 throwing assertions covering null complex/object/array/nullable numeric members, source-read count, root callback count and source identity, null final converter bypass, ctor null arguments, ignore-null initial values, actual scalar values, clone isolation, aliases across object/array/member views, array count/null entries, setter NRE path/type, constructor ArgumentNullException path/type, strict/default null-to-value behavior, independent mutable null substitutes, precondition/condition order, and overloaded equality operators that throw if invoked.

The new test passed first on unchanged baseline .NET 10 before the implementation. It was subsequently also run against the unchanged accepted baseline on .NET 6/8/9/10: 45 assertions each. Candidate complete suite on every TFM: **62 suites, 0 failed**, including the new 45-assertion contract. Legacy demonstration suites remain explicitly unasserted; suite completion is not a claim that their printed expectations are all assertions.

Build/run pattern (SDK from `../.toolchains/env.sh`):

```sh
dotnet build tests/Ling.Mapper.Tests -c Release -m:1 \
  -p:BuildInParallel=false -p:TestTargetFrameworks=net10.0 \
  -p:TargetFrameworks=net10.0 --disable-build-servers -v:q
dotnet tests/Ling.Mapper.Tests/bin/Release/net10.0/TestConsole.dll --all
```

Repeat for net6.0, net8.0, net9.0. `git diff --check` passes. Initial parallel builds returned `Build FAILED` with zero diagnostic errors; single-node builds above completed successfully.

## Non-timing probe

Copied the existing complex-cost harness into isolated baseline/candidate source copies; instrumented only `MapValue`, its null entries, and `MapMember` entry counters. Warmed both scenarios, measured `GC.GetAllocatedBytesForCurrentThread` for one completed map, and retained the harness's full Deep/Mixed value/count/alias/cycle/null/mutation-isolation assertions. No Stopwatch or BenchmarkDotNet measurement was run.

| Scenario | Metric | Baseline | Candidate |
|---|---|---:|---:|
| Deep | MapValue calls | 65 | 50 |
| Deep | null MapValue calls | 31 | 16 |
| Deep | MapMember calls | 48 | 33 |
| Deep | allocated bytes | 15768 | 15768 |
| Mixed dictionaries | MapValue calls | 16 | 15 |
| Mixed dictionaries | null MapValue calls | 3 | 2 |
| Mixed dictionaries | MapMember calls | 8 | 7 |
| Mixed dictionaries | allocated bytes | 4560 | 4528 |

Exactly **15 of the Deep graph's 31 null calls** disappear: the null Alias members. The remaining 16 null dictionary values are outside this change. The older 81 total calls came from an earlier baseline; the specified accepted baseline already has the guarded dictionary value plan and measured 65. No speedup is claimed from call counts; consolidated timing is required before adoption.

Local detailed logs and reproducible probe copies: `.work/null-evidence/` (ignored experiment artifacts, not production source).
