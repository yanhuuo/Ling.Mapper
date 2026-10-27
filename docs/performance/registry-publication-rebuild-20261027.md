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
