# Version reader concurrency stage: VALIDATED NEW EVIDENCE

Normal frozen baseline35d1fe5048ae0f0aba81e63603e5c1ee10940a85 / candidatee0af897cc4412a95749d807073cff758980ce2ab. DLLhashes312d1fa10ff6ae083864763ed400e3c7d633b14d96c348a0affe2cd167fa862f and7ce0405c357cd18c38ca34ef634f4314083ff1e2bc2a75dfe2cbc9204b4d36f1; no production compilation in this benchmark. No oldraw recovery claims.

Independent source/build/preflight on both exact DLLs PASS; full reconstructed custom-trace/alias/cycle/null/values/mutation gates plus14950checksum shared/private-instance checks PASS. First build was rejected for an explicit-DLL reference not propagating through ProjectReference; fixed only probe.csproj direct reference, before any timing. The actual measured source snapshots are preserved under measured-source/.

24/24 formal processes completed15:24:16–15:26:30UTC2026-10-27. Four modes(shared1/2/4,private4),3pairedlaunch each,2s warm+3s measured, same readonly C100source, no timingTrace/registry writes/shared permap atomic counters. All success/checksums/actual loaded DLLhashes PASS; before/after frozen-integrity PASS. CPU lock released, no additional timing performed.

Findings:consistent throughput/CPU-per-op improvement for2/4workers in this window;1worker mixed/inconclusive. Allocations unchanged16472B/op. ProcessorCount9/affinity511 recorded, quota unknown; no reserved4core or linear-scaling claim. No Mapster comparison in this concurrency stage, no overall default-speed-target pass. Publication/JIT correctness is separately gated, not inferred from throughput.

Repository-native project defaults were adapted only for sibling test-directory layout and rebuilt/preflighted using the same frozenDLL; source Program.cs is unchanged. This packaging check was not an additional timed process. Initial --no-restore packaging failed because the new obj lacked assets; official restore/build andpreflight thenPASS. Logs preserve both.

No binary/package archives committed; textsource,24processlogs+results, manifests, environment,GC/CPU/skew/full summaries retained. Baseline/candidate release source and published package unchanged by this evidence checkpoint. Do not mix the default/preserve singlethread benchmark with this extra-checksum concurrency workload.
