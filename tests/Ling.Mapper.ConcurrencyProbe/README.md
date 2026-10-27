# C100 Version-reader concurrency attribution probe

New cloud-only harness. No production MemoryCache/DI or business-object caching. Models/Transform and semantic gates are reused from sibling Ling.Mapper.PerformanceReconstructed. This is not BDN or original historical concurrency evidence.

Modes:shared-1,shared-2,shared-4,private-4. All workers share a read-only100unique source list. Per worker local ops/checksum/alloc, no per-map shared atomic accumulator.2s warm completes a barrier before3s common-deadline measurement. Every64ops checks clock; reports real elapsed and worker skew. Every mapped result reads100 output values, sum14950; this extra work is explicit and identical across versions. No trace or registry writes during measurement.

Build/preflight with an independently frozen normal mapper DLL (no active production obj):

```sh
dotnet build tests/Ling.Mapper.ConcurrencyProbe/ConcurrencyProbe.csproj -c Release -m:1 -p:MapperLibraryPath=/absolute/frozen/Ling.Mapper.dll
dotnet tests/Ling.Mapper.ConcurrencyProbe/bin/Release/net10.0/ConcurrencyProbe.dll --validate
```

Override `ParityHarnessProject` to point at a separate frozen sibling harness project if concurrent builds require isolation. Prepare separate output/obj trees for baseline/candidate; do not race active worktrees.

Timing requires explicit exclusiveCPU/source/gate approval and `MAPPER_CONCURRENCY_TIMING_GO=1`; use `DOTNET_TieredCompilation=0`, an actually allowed MULTICPU affinity (not historicalCPU2 pin), and `MAPPER_SOURCE_SHA` identifying exact source. `--mode shared-1|shared-2|shared-4|private-4` executes one process with fixed durations. Capture every process log and RESULT_JSON, verify all checksums, DLLhashes and metadata. Approval is not granted by this README or environment switch.

First validated experiment:baseline35d1fe5→candidatee0af897. Three independent paired launches per mode; full evidence under evidence/version-concurrency-20261027T152115Z. Multiworker benefit observed,1worker unresolved; CPU quota unknown. No claim of4reserved cores, universal speedup, AOT/x86 validation, or defaultMapster parity.

Integration note: the repository includes historical `.cs` snapshots under `evidence/`. The project explicitly excludes `evidence/**/*.cs` from compilation so archived baseline/candidate programs do not become duplicate live build inputs. This packaging-only correction does not change the measured Program or raw results; the original duplicate-source build failure and successful preflight are retained in the integration evidence.
