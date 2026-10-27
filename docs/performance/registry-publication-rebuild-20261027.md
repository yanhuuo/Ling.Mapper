# Rebuilt registry publication/read experiment — 2026-10-27

Base: remotely backed member-plan checkpoint `35d1fe5048ae0f0aba81e63603e5c1ee10940a85`. This new experiment retains that member optimization; it does not recover/reuse the lost `a373042` experiment or its evidence. No global cross-mapper plan dictionary, MemoryCache/IMemoryCache/DI dependency, API change, version change, main-branch merge, tag publication, or NuGet release is included.

## Hypothesis and strict synchronization boundary

The process-wide `TypeConverterRegistry.Version` uses Interlocked.Read. On x64 its implementation may generate a locked RMW even with only readers, potentially creating inter-core contention. The candidate is restricted to replacing that one getter with Volatile.Read(ref _version). Registry Entry publication remains a ConcurrentDictionary write followed by Interlocked.Increment. CurrentGeneration CAS/publication, per-item checks, member guards, and all writer operations remain unchanged. Actual JIT output and controlled multi-reader measurements are required; lower-level reasoning alone is not a speedup claim.

Volatile.Read is an acquire read, **not a full-fence/RMW semantic equivalent**. Readers that observe a published version must acquire the preceding entry publication. Same-thread registration and explicitly synchronized completed-registration handoffs must use the new rules. Unsynchronized concurrent calls may see a complete old or new rule, not an invented wall-clock-latest or whole-graph transaction guarantee. Existing inline converter capture remains as documented in the member recovery checkpoint.

Official documentation: https://learn.microsoft.com/en-us/dotnet/api/system.threading.volatile.read . The official runtime source (for example https://github.com/dotnet/runtime/blob/v10.0.14/src/libraries/System.Private.CoreLib/src/System/Threading/Volatile.cs) uses a native-width volatile read on 64-bit and an Interlocked.CompareExchange fallback for long reads on 32-bit. Matching source files for installed runtime versions 6.0.36/8.0.33/9.0.22/10.0.14 are retained in external evidence. This is source support only: no x86 execution has been performed.

## New baseline-first fixture

`tests/Ling.Mapper.RegistryPublicationSmoke` is newly reconstructed, not the historical 6,927-check source. On the unchanged Interlocked.Read baseline all four TFMs passed **4,953 checks each**; every loaded DLL byte-matched the corresponding frozen baseline. The full existing 66-suite runner also passed all four frameworks with zero failures in the first runs.

The fixture covers same-thread root/list/array/dictionary publication with result counts/order/keys; four reader Event handoffs after registration completion; Barrier phases permitting only coherent old/new values during concurrent replacement, with exact convergence after completion; getter/constructor/callback/converter reentry and subsequent-item behavior; projection cache invalidation; and generated mapper registry-version rejection before/after registration. Generated guards execute under ordinary JIT using public registration infrastructure, **not a NativeAOT test**. Existing member smoke and full suites retain configuration isolation and concurrent initialization coverage. No registry version is reset by reflection, and all fixture runs use fresh processes.

The candidate production line and its performance results are not included in this baseline-only checkpoint. After candidate correctness/JIT review, formal measurements must report 1/2/4 workers, wall/process CPU time, ProcessorCount, actual affinity and known/unknown CPU quota. Old CPU2-only results must not be relabeled as a multi-reader scaling test. Without measurable benefit the candidate remains HOLD.

## Candidate functional/JIT gate (performance not measured)

The subsequent candidate changes exactly one production line, at `TypeConverterRegistry.Version`: Interlocked.Read becomes Volatile.Read. Production source commit: `e0af897cc4412a95749d807073cff758980ce2ab`. Baseline and candidate each passed all four original 66-suite runs and all four 4,953-check publication runs on the first attempt in this round. The candidate also passed the 1,380-check reconstructed member fixture on every framework. The four fresh conversion matrices are byte-identical. No assertion was removed or weakened and no tiering setting was changed for the functional runs.

Normal net10 baseline DLL SHA256: `312d1fa10ff6ae083864763ed400e3c7d633b14d96c348a0affe2cd167fa862f`; candidate: `7ce0405c357cd18c38ca34ef634f4314083ff1e2bc2a75dfe2cbc9204b4d36f1`. The 141-file candidate source manifest hash is `ffcc1ba85edbfb4075a6e86b31502c2e5e2efa9cbd7d8e65ce1559cb498239a9`. Source/full-suite/publication/member loaded copies were compared to the frozen normal libraries. The adjacent source-freeze JSON has all framework hashes.

A non-timed x64 .NET 10.0.14 JIT probe used the actual normal DLLs with identical `DOTNET_TieredCompilation=0` and `DOTNET_ReadyToRun=0` settings. It invoked the real internal Version getter via a reflection-created delegate and exercised normal mapper generation refresh. Baseline emitted `lock cmpxchg` for both Version and its inlined read in CurrentGeneration. Candidate emitted ordinary native-width loads instead. The CurrentGeneration slow-path CompareExchangeObject remained in both. Raw assembly is retained beside this document; these observations do not prove throughput or full-fence equivalence.

The probe observed ProcessorCount=9, affinity mask 511, allowed CPUs 0–8. `/sys/fs/cgroup/cpu.max` was unavailable, so effective CPU quota is **unknown**; nine allowed CPUs do not prove nine dedicated cores. These are probe observations, not replacements for each formal benchmark process's own CPU/wall/affinity records. The candidate is performance-pending and must not be described as accepted or faster based solely on functional/JIT evidence.

## Fresh independent review: correctness PASS, throughput pending

The independent reviewer ran the same external executables against verified baseline/candidate DLL swaps: core 36 checks plus 64 parallel mappings; a new four-reader publication/configuration-owner fixture with 4,635 checks across eight completed-write rounds; and the reconstructed 4,953-check publication fixture. All passed both versions. All four matrices and 141 source-manifest files were independently checked, and the normal-DLL JIT assembly/probe library identity were verified.

An initial reviewer-written fixture incorrectly expected an int→long converter to apply to an already-long ForMember selector. It failed on baseline; the fixture was corrected to automatic int→long Value plus a separate configured Owner field, and then both libraries passed. The original fixture/log are retained. This was a test-expectation correction, not a production fix or relaxed candidate rule.

This candidate checkpoint is **correctness-validated but performance-pending**. Dedicated-branch backup is not a main-branch acceptance decision. Formal multi-reader measurements and their uncertainty will determine whether to retain it or HOLD it; no contention/throughput gain is claimed yet.

## Completed 24-process review: retain for measured concurrent workloads

Review of the newly completed multi-reader experiment supports the decision to **retain `e0af897` as an internal concurrency performance optimization**. This does not establish a single-thread speedup, universal Mapster parity, or a release/package change. The historical lost experiment is not the evidence for this decision.

The dedicated measurement ran 24 independent processes: four modes × baseline/candidate × three launches, alternating pair order B→C / C→B / B→C without discarding results. Long-lived workers shared the same immutable 100-item input. Shared modes used one warmed mapper; private-4 used one mapper per worker while retaining the same process-global registry. After two seconds of warmup and a barrier, workers used one three-second deadline, checking time every 64 mappings. Every result's 100 output Values contributed to the same checksum (14,950 per mapping). Configuration/compilation/preflight were excluded, Trace was null, and registry entries were not written during measurement. All DLL hashes, worker success/counts/checksums and input immutability were verified.

| Mode | Baseline ops/s median [min,max] | Candidate ops/s median [min,max] | Three paired C/B throughput ratios | Median process CPU µs/op, B→C |
|---|---:|---:|---|---:|
| shared-1 | 106,688 [97,361,112,439] | 105,725 [95,437,118,109] | 1.086× / 0.895× / 1.050× | 9.45→9.56 |
| shared-2 | 136,252 [132,343,138,198] | 167,737 [161,246,169,039] | 1.267× / 1.183× / 1.223× | 13.99→11.28 |
| shared-4 | 147,945 [142,667,149,766] | 248,024 [207,026,268,331] | 1.382× / 1.676× / 1.881× | 23.41→12.96 |
| private-4 | 142,720 [138,601,144,048] | 232,060 [212,184,246,853] | 1.531× / 1.626× / 1.714× | 23.87→12.97 |

Allocation stayed **16,472 bytes/map** in all modes/versions. Every paired 2/4-worker candidate improved throughput with lower process CPU cost per operation; private-mapper results support global reader contention rather than only a shared mapper-instance effect. Together with the actual JIT RMW-to-load evidence this supports the stated hypothesis, but does not prove every possible causal path.

Single-worker pairs have mixed directions and overlapping ranges: **single-thread benefit remains undetermined**, and strict absence of a single-thread regression is not established. This short three-launch window does not guarantee gains under every machine/load. No parameters or thread counts were tuned to select a favorable outcome.

All 24 processes observed ProcessorCount=9 and affinity mask511 (logical CPUs0–8), with measured wall time 3.000132–3.001774 seconds. ProcessCPU/wall medians for baseline→candidate were shared-1 1.009→1.010, shared-2 1.916→1.893, shared-4 3.505→3.214, private-4 3.356→3.009. Process CPU includes GC and other process threads. Quota remains unknown; four workers do not imply four reserved/physical cores. Raw GC counts and per-worker start/finish deviations are retained in the formal report.

The authoritative new evidence is `../../tests/Ling.Mapper.ConcurrencyProbe/evidence/version-concurrency-20261027T152115Z/REPORT.zh-CN.md`, `summary.json`, `summary.csv` and all 24 process result/log files in the repository concurrency-probe evidence directory. The benchmark harness's first build failed from a missing direct bare-DLL reference; adding the same MapperLibraryPath reference fixed that harness-only issue before any measurement. The production DLLs and measurement plan were unchanged. There was no Mapster timing in this experiment, and its extra output checksum work prevents direct mixing with previous single-thread BDN ns/op numbers. Default tiering/PGO, Windows, x86/AOT, cold starts and performance during registry writes remain unmeasured.
