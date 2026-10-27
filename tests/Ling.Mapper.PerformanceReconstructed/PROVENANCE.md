# NEW reconstructed harness — not recovered historical evidence

Created2026-10-27 after the prior executor files disappeared. Source is reconstructed from this agent's retained conversation containing its own earlier executed source-writing commands and benchmark output. We have NOT recovered the former harness files, former byte hashes, binaries, or raw samples. New build/preflight/results must establish their own evidence lineage.

Historical observations (context only): old C output9131.394ns/16472B vs1007.545ns/3256B was emitted at12:46:59UTC in this agent's conversation. It is not a new result and must not be pooled into future data. Historical accurate-contract definitions below are retained source facts, not recovered artifacts.

## High-confidence reconstructed original definitions

1. C:100 distinct CustomSource objects with Id0..99 mapped to100 CustomDestination objects with Value. Both member expressions call the SAME [MethodImpl(NoInlining)] UserWork.Transform(id), defined as `Trace?.Add(id); return checked(id*3+1);`. The public dynamic-source IMapper.Map<List<CustomDestination>>(source) entry is used for both libraries. No user custom getter or callback. Untimed trace must be exactly0..99 for each library on two independent calls; results must equal3*id+1; mutable outputs cannot overlap source/other call. Trace=null for timing.
2. Ordinary List100:100 unique DTOs,8 scalar/immutable properties(Id,Name,Age,Active,Amount,Created,Key,Note). All fields identical to retained historical builder; Note null. Root/items new, all values correct and mutation isolation.
3. Mapster defaults for the two primary unique-input cases: no PreserveReference. Ling retains its ordinary built-in identity behavior. The performance target remains these default Mapster rows, not the controls.

## New independent diagnostic controls (explicitly not historical reconstruction)

Custom100PreserveControl and List100PreserveControl map the SAME100-unique input constructors, but enable Mapster reference preservation for every relevant DTO/root-list mapping. Separate untouched default cases remain the target denominator. Controls are not allowed to replace the default result or declare default parity success.

Probes verify repeated element aliases, root array/list scope, same container through two properties, elements shared across list/array, null elements, node cycles, shared dictionaries/lists/byte arrays, exact scalar values, fresh mutable graphs, separate-call disjointness and result mutation isolation. Repeated custom-source probes require one Transform call per unique source and the same sequence in both libraries. Unsupported shape/trace mismatch must fail preflight or be explicitly excluded from a diagnostic comparison; never weaken assertions or handwrite a mapper substitute.

Only four timed fixtures currently reconstructed/prepared =8 methods. This is NOT the full original13 restored. Remaining numeric/string/nested/dictionary/deep fixtures can be rebuilt later with new provenance, but should not be assumed available or previously validated now.

## State and execution

Source only. Not built, restored, preflighted or timed at creation. Wait for recovered toolchain and root gates. Repo-native default source reference points to../../src/Ling.Mapper.csproj when placed under tests/Ling.Mapper.PerformanceReconstructed; the standalone recovery directory requires an explicit frozen reference. For independent frozen compilation pass `-p:MapperSourceProject=/absolute/frozen/source/Ling.Mapper.csproj`, or `-p:MapperLibraryPath=/absolute/normal/Ling.Mapper.dll`; never share active workers' obj directories during measurement.

Pins: Mapster10.0.13, BDN0.15.8, net10.0. Intended timing job preserves CPU2/NoTiering/Release,2launch×6warm×12measure×250ms; current host/runtime/cpuset must be recorded and verified anew. `MAPPER_RECONSTRUCTED_TIMING_GO=1` is additionally required to enter timing. Prior SDK/CPU/runtime/old hashes are NOT assumed to survive the reset.

No MemoryCache/IMemoryCache/DI dependency. No production caching of business objects. Benchmark fixtures necessarily own their input lists; the only user-method static trace records ints during untimed checks and resets in finally. No production code changed by this reconstruction.

## Newly established baseline evidence

2026-10-27 14:47UTC: official NuGet restore and Release/net10.0 build succeeded against an independent git archive of release2d5218a7d0a2ddbd25f2601299864c42f39bcc87. Runtime10.0.14/SDK10.0.403 were read anew. All4fixtures and full graph/container/array/custom-trace probes PASSED. This is NEW correctness evidence. Formal timing remains NOT_RUN. See repository evidence subdirectory for logs and hashes; old raw datasets remain unavailable.
