> Preserved approved design snapshot. Execution later completed with 24/24 successful processes; see evidence/version-concurrency-20261027T152115Z/STATUS.md and REPORT.zh-CN.md.

# New concurrency-only attribution plan — PREPARED / NOT_BUILT / NOT_RUN

## Scope (await root approval and exact source gates)

Baseline remote35d member-restoration stage; candidate only registry Version read Interlocked.Read→Volatile.Read. Exact full commits and normal DLL hashes required before build/timing; never infer them from names. Exclude acquire/fusion/compact historical experiments and cached business objects. The hypothesis is global static reader read-modify-write contention, not absence of static plans.

Four predeclared modes: shared warmed mapper with1,2,4 long-lived threads;4 long-lived threads with one mapper per thread. Same real C100 mapping and default Ling identity semantics,one shared read-only input list of100unique source objects with Id0..99 across all workers. Per-instance mode still shares the process-global registry, deliberately separating instance cache effects from a global-version hypothesis.

Each mode: baseline/candidate pair ×3 independent process launches =24 processes total. No Mapster timing, no extra modes or shape matrix. Suggested alternation: pair1 baseline→candidate; pair2 candidate→baseline; pair3 baseline→candidate for each mode. Every launch retained. Fixed2-second warmup and3-second measurement; startup/configuration/JIT/expression compilation/trace validation excluded. Approximate active process duration120seconds plus startup/preflight/coordination, not a production latency claim.

## Prepared implementation

`Program.cs` is NEW source, not compiled/validated yet. References the reconstructed parity model/Transform and its semantic preflight through a configurable ParityHarnessProject. Build each version against independently frozen normal binaries or archives with separate obj/output paths only after gate.

- Explicit threads, not ThreadPool tasks. Shared mapper or private mapper instances created before threads; actual instances precompiled and fully value checked before timing.
- Full reconstructed alias/cycle/null/custom trace preflight outside timing. UserWork.Trace=null for all concurrent work. Registry is not mutated during timing; publication synchronization correctness is a separate gate.
- All workers read the same immutable-in-use input list; each owns only local operation/checksum counters and allocation counter. No per-map Interlocked/Add/atomic shared counter. Synchronization only at barriers/deadline and once-per64-operation time checks; no new per-item contention introduced.
- Every measured map output reads all100 result values into a thread-local checksum (expected14950/map). This fixed checksum overhead is identical on both sides and explicitly reported; do not compare these ops/s directly to old BDN ns/op without this work.
- Two-second warm deadline then an explicit all-warm-done barrier; process CPU/GC snapshots and common three-second measure deadline. Workers return counts once after their loops. Real total wall time includes start skew and batch-end overshoot; record per-worker offsets rather than pretending exact equal activity.
- Bytes/op from per-worker GC.GetAllocatedBytesForCurrentThread deltas; global GC0/1/2 counts recorded, not per-worker GC attribution. Process.TotalProcessorTime/wall includes all process threads including GC; it is not evidence of reserved core quota.
- Post-measure source checks and all expected result checksums must pass. Failure invalidates that launch; retain it and report, never silently replace/cherry-pick.

## CPU/environment safeguards

Do NOT reuse CPU2 single-core affinity. Use a newly verified common allowed multi-CPU affinity set for every process; record Environment.ProcessorCount, affinity mask/list, process and system metadata, runtime/GC mode, CPU quota/cgroup info when readable. If quota is absent or unreadable, say unknown. Four workers does NOT mean four physical or guaranteed cores. If actual allowed affinity has only one CPU, stop and ask root rather than claiming a multicore test. No environment/runtime setting changes to manufacture scaling.

Use release configuration and NoTiering consistently; no cold timing claims. No forcedGC between cases unless separately specified (none planned). Publication gate and correctness stress must finish before CPU window.

## Recorded outputs / decision

Per launch: ops/s, total ops, per-worker ops/checksum, allocatedB/op, globalGC counts, processCPU seconds, wall seconds/CPU-to-wall ratio, scheduling offsets, source+assembly hash, ProcessorCount/affinity, runtime and success. Include all3 launch distributions for every mode and both versions, plus environment snapshots. Report paired throughput and CPU-per-operation ratios, not only the best sample or aggregate4-thread gain.

A concurrency gain must be consistent across independent pairs and materially exceed observed run variation; no fixed allowed slowdown or statistical-equivalence claim.1-thread row is a regression/overhead control. Compare shared4 vs private4 effects without asserting causality from one alone; a process-global read should remain global in both. Mixed/scaling-limited/CPU-quota-limited results remain inconclusive.3 launches are limited evidence, not a robust production guarantee.

If credible regression occurs, retain it. If unresolved, report HOLD/no established concurrency benefit; do not tune thread counts, durations, affinity or Version semantics to chase favorable data. This experiment only decides whether the one-line read change has measured concurrent benefit; it cannot establish default Mapster parity, overall13-case completion, linear multicore scaling or publication safety.

No build, timing, JIT/disassembly probe or push is authorized by this plan alone. Root must provide exact baseline/candidate+gates+exclusiveCPU GO. Only then a small separately approved JIT probe may verify lock-prefixed vs acquire-load code; static assembly differences are not throughput evidence.
