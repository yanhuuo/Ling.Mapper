# Cloud recovery of guarded complex-member plans — 2026-10-27

## Provenance

The previous cloud workspace and its toolchains/evidence were lost. This recovery starts from public GitHub release commit `2d5218a7d0a2ddbd25f2601299864c42f39bcc87` in a new assistant-cloud directory. It does not run on the user's computer and does not touch unrelated repositories.

The historical local commits `18af9f1dbe5af18127336b144ba2fbd0cc76dca1` and `ffb39d20ca9a04dbdaeeac3a2b931c0de7732e08` are absent from the fresh clone. Explicit fetches by SHA returned `not our ref`. Accordingly this work reconstructs the minimal source change and regression fixture with **new commits, new binary hashes, and new test evidence**. It is not a byte-for-byte recovery of those lost commits or an assertion that historical benchmark raw data were recovered. Previously reported benchmark gains/parity conclusions must not be presented as fresh measurements of this rebuild.

The toolchain was installed from official Microsoft .NET feeds (SDK 10.0.403; runtimes 6.0.36, 8.0.33, 9.0.22, 10.0.14). NuGet restore uses the official api.nuget.org source. CLI HOME/runtime/cache paths are confined to this recovery directory because the default home paths are read-only. No SDK or package caches, credentials, or environment secrets are to be included in repository commits or recovery bundles.

## Reconstructed bounded design

Only the final complex fallback of BuildMemberConversion receives a lazy per-member slot storing a full MappingPlan delegate. Eligible pairs: no ignores; concrete non-object/non-simple reference source and destination; same type or nonassignable types. Different assignable pairs and all earlier direct/scalar/registered converter/object/interface/abstract/value/nullable branches retain their original behavior. No additional declared-intent or user-defined operator discovery is introduced. MapInto builds and final-converter null fallback explicitly disable this hint.

The version is captured before existing intent discovery. At execution, the source getter has already resolved its local; the helper performs the original path Push/try/finally/Pop and reuses MapKnownCollectionValue. That existing helper checks version, exact runtime source type, and updating state on each occurrence, with mismatches using MapValue. Child compilation remains lazy to avoid recursive type-plan construction. The cached delegate is the complete graph plan, not Root or raw object body. Slots store no source, destination, context, callback, or other business data.

No acquire-read, object-body fusion, compact-reference dictionary, API/version changes, or package publication is included. Thread-safety/publication of the existing guarded helper is unchanged. Existing inline registered-converter capture is a separate contract: replacing a registry entry while a previously compiled inline delegate is running does not promise to replace that delegate between sibling fields; new/reentrant root plans can observe the new generation.

## Subsequent static-cache design (not implemented in this recovery)

The user's permission for a process-wide thread-safe static cache does not make a global TypePair→MappingPlan dictionary safe. Plans close over their owning mapper/configuration, member rules and diagnostic policy. A static cache must not mix owners, retain obsolete configurations forever, freeze MapperProvider.Current, or store business objects.

A BCL ConditionalWeakTable keyed by mapper identity could retain owner-local cache state without permanently rooting the mapper, but it adds another lookup to caches already owned by the mapper and has no established hot-path benefit. Shared plan initialization also needs generation/version separation and must not eagerly compile recursive children or permanently cache transient failures. No such cache is being introduced merely to satisfy a wording preference.

A narrower possible future candidate is weak-key caching of pure CLR shape metadata at its existing discovery point, e.g. GetElementType's interface scan after its array/IEnumerable fast paths. ConditionalWeakTable<Type, metadata> avoids a global strong-key Type dictionary; the factory can compute immutable metadata without user code, with failed reflection/discovery not stored and without early eager scans. Such metadata is configuration/version independent by construction. This remains a design proposal requiring review and evidence; it uses no MemoryCache/IMemoryCache/DI or third-party cache dependency.

## New validation results

Reconstructed production source commit: `20a6bcc698628a286904e239f2cc3a20cf77bc12`. Fresh release baseline and candidate were built/run on net6.0/net8.0/net9.0/net10.0. Each completed the original 66-suite runner with zero failures in the final full run, plus the newly reconstructed 1,380-check member fixture. The fixture is not the old 67 + 1,352 source and deliberately does not reuse that historical count. Four fresh conversion matrices are byte-identical between release and candidate. Flat allocation remains 24,000 bytes for 1,000 maps, equal to direct construction.

One initial candidate net8 run failed the existing independent typed-decimal allocation assertion: 24,624 bytes against its unchanged 1,024-byte bound. A complete rerun of the same DLL with the same arguments and environment passed 66/0 and measured zero typed-conversion bytes. No test threshold, production source, or runtime configuration was changed. The original failed log and successful unmodified rerun are both retained; the exact cause is **not established** and this is not described as a code fix. The other three candidate frameworks passed their initial full runs.

The fresh net10 candidate DLL SHA256 is `9d09638c2ad2c55f06bff859349db9cb7c3fca94a4ff431f6f53a64667c8efb8`; the fresh release baseline DLL is `9f1dd5640c160c8f3b909e45847318ff372b133f2ad68d709c8efa47131f0d20`. All corresponding library/full-suite/smoke loaded copies were verified equal. The 137-file source manifest SHA256 is `3c13626c42d84fb67bb6a78fe951a8e979b7d91be76320187efcb43b7a00408e` (the separately reconstructed performance harness is not part of this source freeze). Per-framework hashes and the initial-failure disclosure are in the adjacent evidence JSON.

A later documentation/checkpoint commit can change informational-version metadata if rebuilt; use the stated source commit/frozen normal DLL for attribution. No benchmark timing, Mapster parity, new NativeAOT execution, or x86 execution is claimed by these functional gates.

## Fresh independent audit and checkpoint decision

Independent review passed the experimental recovery correctness gate with the allocation anomaly explicitly disclosed. A newly authored external fixture passed 36 assertions plus 64 parallel mappings on both the fresh release and candidate using the same executable with verified DLL swaps. An external copy of the reconstructed 1,380-check smoke likewise passed both. A fresh net8 focused typed-scalar test passed both with zero measured typed allocation. The auditor independently compared all four matrices and verified every file in the 137-file source manifest, and found no new eager intent discovery, global shared-plan state, or lost full-plan/context/scope guards.

This is **not** a replay of the lost historical 15-fixture suite. No new AOT/x86 coverage or timings were performed. The first net8 full-run allocation anomaly remains unresolved and retained; the audit does not call it fixed or claim an unconditional first-pass allocation guarantee. The checkpoint is suitable for the dedicated optimization branch and subsequent controlled benchmarking, not automatic main-branch merging or NuGet publication. The fresh audit summary is retained alongside the source-freeze evidence.
