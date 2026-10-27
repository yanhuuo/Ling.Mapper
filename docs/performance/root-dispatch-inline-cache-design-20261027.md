# Root dispatch: source evidence and bounded inline-cache design

Read-only analysis of code commit `7876a4e`. No build, execution probe, allocation measurement, or timing was run while the parent's benchmark window was active.

## What the warm eligible path actually does

For `Map<TDestination>(nonNullSource)` with no ignores and a flat eligible object plan:

1. `Map<TDestination>(source)` reads `GetDefaultOptions()` (`Mapper.cs:63`).
2. Generic overload obtains `source.GetType()` and passes it to `MapCore` (`:68`).
3. `MapCore` validates flags and checks ignores/declared-source guards (`:83-94`).
4. It obtains the same `source.GetType()` again and checks destination assignability (`:98-99`).
5. `GetPlanEntry` calls `CurrentGeneration` once (`:184-189`). This reads `_generation` and `TypeConverterRegistry.Version` (`:168-178`).
6. It constructs a value-tuple key with `AdaptOptionsKey` and calls `Plans.TryGetValue` **once**, not multiple times. The options key hashes the flags through `HashCode`; the tuple also hashes source and destination types.
7. `entry.Root` is invoked (`:104`). Its expression executes the original member pipeline, then a callback-null branch (`Mapper.RootPlans.cs`).
8. The generic overload checks/casts the returned object (`:69`).

A warm eligible root does **not** enter `MapValue`, allocate a `MappingContext`, perform a second plan lookup, or execute reference tracking. Claims that this path currently performs repeated dictionary lookups are unsupported. The redundant work is repeated runtime-type/assignability validation and the one general-purpose plan lookup for an already-known request shape.

`TypeConverterRegistry.Version` currently uses `Interlocked.Read`. Its emitted JIT instructions and contribution are not established here. They must be inspected after the CPU window is released; no latency claim is made from source alone.

## Proposed next candidate

- Reuse `PlanEntry`; add immutable source type, destination type, and options metadata when the plan entry is first constructed.
- Keep one last-root `PlanEntry` reference on each `PlanGeneration`. No separate per-request wrapper, source/result instance, callback, ignore array, or diagnostic context is cached.
- After unchanged public validation, get the generation once and compare the cached entry's type/option keys for no-ignore requests. A match directly invokes its existing proven `Root` delegate.
- On a miss, retain the original exact-source/polymorphic guard and dictionary path. Pass the already-observed generation to the lookup, rather than reading the registry again.
- Publish only a fully-built, eligible no-ignore plan by replacing the generation's reference. Alternating warmed types can miss and replace the reference, but do not allocate new entries or recompile plans.
- Remove the redundant second runtime-type read only when the generic overload supplied the runtime type itself. Explicit `Type` API calls still validate declared-source compatibility and exactness. Nonexact declared rules, polymorphic clones, null, MapInto, and ignored-member requests keep the complete path.
- Registry replacement retires the generation and its inline-cache reference together. In-flight calls keep the same old-generation semantics as the existing plan cache.
- Do not initially split typed/object return delegates or callback delegates. Existing error handling, member execution, callback counts, culture reads, and origin behavior remain byte-for-byte the same compiled pipeline.

## Evidence required before acceptance

After the parent releases the benchmark CPU window:

1. Instrument isolated copies (not production DLLs) to count `GetType`/guard sites, generation reads, plan dictionary queries, misses, compilation, and root invocations after warmup.
2. Prove warm stable-shape dictionary queries fall from one per call to zero. Prove alternating warmed types do not recompile or allocate cache entries.
3. Check options, mutable ignore arrays, typed/untyped declared rules, registry changes including user-code-triggered changes, callbacks/reentry, failures without replay, foreign `MappingException`, runtime culture, independent results, and graph fallbacks.
4. Run production allocation probes and all four-framework suites. Keep benchmark timing under the parent's paired-window control.
5. Inspect the root JIT/call chain and registry version load only after baseline source-count evidence is captured. Any additional synchronization change must be a separate reviewed change with its own evidence.

## Executed evidence after benchmark-window release

The isolated instrumented baseline and candidate copies count actual warm-path calls; neither instrumented library is a production/benchmark artifact. For 1,000 mappings per scenario:

| Counter | Stable generic baseline → candidate | Alternating generic baseline → candidate | Stable explicit-Type baseline → candidate |
|---|---|---|---|
| Generation read | 1000 → 1000 | 1000 → 1000 | 1000 → 1000 |
| Plan dictionary lookup | 1000 → 0 | 1000 → 1000 | 1000 → 0 |
| Plan construction | 0 → 0 | 0 → 0 | 0 → 0 |
| Repeated exact-source GetType site | 1000 → 0 | 1000 → 0 | 1000 → 1000 |
| Destination assignability guard | 1000 → 0 | 1000 → 1000 | 1000 → 0 |
| Root delegate invocation | 1000 → 1000 | 1000 → 1000 | 1000 → 1000 |

The explicit-Type public validation remains present. The single-slot cache is a monomorphic hint: alternating types intentionally keep the dictionary miss path, but reuse the existing entries rather than allocating a cache wrapper.

The production `RootDispatchCacheRegressionTests` passes 224 assertions. Its allocation-only probe performs 2,000 alternating warmed root mappings: 48,000 bytes, identical to direct allocation of the 2,000 destination objects. It also checks plan-entry identity reuse, mutable ignore arrays, option identity, declared base/derived rules, null/invalid inputs, callback reentry, concurrent first publication, and registry replacement.

Normal-JIT FullOpts disassembly of `CurrentGeneration` confirms the existing `Interlocked.Read` version load emits `lock cmpxchg` on this machine. This synchronization behavior is **not changed by the root-inline-cache implementation** and no latency is inferred from the instruction listing.

Evidence directory: `/workspace/scratch/574bb39da4d0/mapper-root-dispatch-count-evidence-20261027`, including `{baseline,candidate}/counts.jsonl`, isolated instrumented sources, production focused output, and `current-generation-jit.txt`. No timing run was performed.

The root-inline-cache-only candidate passes all 65 suites on net6.0, net8.0, net9.0, and net10.0 after nonincremental Release builds (0 failures/errors). The production allocation probe reports 48,000 bytes on every framework. Logs: `root-build-<TFM>.log` and `root-all-<TFM>.log` in the evidence directory above.
