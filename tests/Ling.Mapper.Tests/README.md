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
for framework in net6.0 net8.0 net10.0 net11.0; do
  dotnet build tests/Ling.Mapper.Tests/TestConsole.csproj -c Release -p:TestTargetFrameworks="$framework" || exit 1
  dotnet "tests/Ling.Mapper.Tests/bin/Release/$framework/TestConsole.dll" --all || exit 1
done
```

Execute each output directory's TestConsole.dll with the matching installed
runtime. In particular, compiling net11.0 requires the .NET 11 SDK (currently
preview/RC when applicable); merely installing its runtime is insufficient.
The .NET 11 matrix entry tests the net10.0 library on the .NET 11 runtime;
net6.0/net8.0/net10.0 entries use the matching library target.
The requested .NET 11 environment is SDK 11.0.100-rc.2.26504.105 and runtime
11.0.0-rc.2.26504.105, not a stable .NET 11 release.
Building all targets is distinct from actually executing all four runtimes.
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
