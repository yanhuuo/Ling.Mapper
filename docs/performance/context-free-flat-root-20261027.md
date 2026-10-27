# Context-free flat-root execution candidate (2026-10-27)

Base: `5db129273ff4486aa9331e50d24619fc44c9f435`, plus the member-null shortcut prerequisite `98fdc12` (locally cherry-picked as `3d2067a`). This is an execution-layer candidate, not a timing result or a release claim.

## Eligibility and ownership

- Public root mapping must have an exact runtime/declared source type, without an assignable destination requiring polymorphic clone resolution.
- The existing object-plan builder must select ordinary construction, no constructor/factory guard, no graph reference tracking, no existing-target update, and a reference-type destination.
- An expression visitor proves that the completed member pipeline uses `MappingContext` only in `MemberFailure`. Every other context dependency rejects the shortcut.
- Eligible plans reuse that exact member-resolution/conversion/condition/assignment expression. There is no alternate name resolver or conversion implementation. Custom scalar selectors and conditions remain supported when this proof succeeds.
- The normal graph delegate remains unchanged for nested objects, including flat children in a shared-reference graph. `MapInto`, constructor-bound objects, factories, complex member dispatch, and polymorphic requests retain the full path.
- Each plan-cache entry stores both delegates under the existing registry-generation and options/ignore key. It never stores source data, mapped results, or diagnostic state. Every mutable root result is newly constructed.

The successful root delegate has no `MappingContext`, reference table, or `MapValue` dispatch allocation. A local diagnostic context is allocated only if a member fails. Constructor/callback failures receive a root diagnostic context. Same-origin mapping failures propagate unchanged; foreign mapping failures are wrapped at the current boundary. No source selector, getter, setter, constructor, condition, converter, or callback is retried to reconstruct a diagnostic.

Scalar nullable branches now inline the existing `MapValue` null policy (default/null, or `InvalidCastException` under `NullValueHandling.Throw`) instead of entering graph dispatch. Conversion and failure wrapping still use the existing member pipeline. This does not capture culture: scalar conversion continues to read current culture when executed.

## Verification

`FlatRootPlanRegressionTests` adds 40 assertions covering typed/untyped mapping, fresh objects, MapInto, root null, ignore/null options, callbacks, aliases, conversion overflow, non-replayed getter/setter/constructor errors, foreign exception origin, runtime culture changes, custom selector/condition counts, registry replacement, nullable null policy, and allocation.

The isolated allocation probe warms a nullable-int to int DTO plan, then compares 1,000 mappings against 1,000 direct destination allocations. Both allocate 24,000 bytes (24 bytes per destination); this checks allocations only and is not a throughput benchmark.

With the member-null prerequisite included, the final root snapshot passes all 63 suites on net6.0, net8.0, net9.0, and net10.0 (0 failed on each). Build/regression logs are `/tmp/typed-root-integrated-{build,all}-<TFM>.log`. All four frameworks report the same 24,000-byte allocation result.

Run all regression suites for each TFM with:

```sh
source /workspace/scratch/574bb39da4d0/.toolchains/env.sh
for tfm in net6.0 net8.0 net9.0 net10.0; do
  dotnet build tests/Ling.Mapper.Tests/TestConsole.csproj -c Release -m:1 \
    -p:TestTargetFrameworks=$tfm -p:BuildInParallel=false
  dotnet tests/Ling.Mapper.Tests/bin/Release/$tfm/TestConsole.dll --all
done
```

Timing/Mapster parity remains to be measured in the parent task's isolated benchmark window. This candidate does not claim NativeAOT support and changes no package version.
