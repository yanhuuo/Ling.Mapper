# Runtime atomic-array and dictionary hotspot investigation (2026-10-27)

Baseline: `c6846edde6d074e5145eac44e906ae1495df3d40`.

## Allocation evidence, before implementation

Release/net10.0, SDK 10.0.403 / runtime 10.0.14, warmed 100 calls followed by
1,000 calls measured with `GC.GetAllocatedBytesForCurrentThread`. No stopwatch
or ranking claims. Mapper is unconfigured, Strict, maps `byte[]` to `byte[]`;
output values and independent container identity are checked before measurement.

| Array length | Map B/op | Array.Clone B/op | Non-generic enumeration B/op |
| ---: | ---: | ---: | ---: |
| 0 | 432 | 24 | 32 |
| 3 | 632 | 32 | 104 |
| 16 | 952 | 40 | 416 |
| 100 | 3,056 | 128 | 2,432 |

The enumeration allocation is exactly 32 bytes plus 24 bytes per boxed byte.
`BuildCollectionPlan` additionally constructs an empty list for counted arrays,
creates indexed mapping state, and runs full `MapValue` dispatch for each byte.
The existing Deep16 dictionary fixture contains sixteen three-byte arrays, so
this is a measured allocation source on that workload, not an invented larger
input. Its two-entry dictionaries do not grow beyond the initial dictionary
capacity; dictionary capacity tuning was therefore not adopted for this fixture.

Raw probe: `/tmp/mapper-runtime-hotspots-probe/{Program.cs,baseline.log}`.
Historical full baseline: `mapper-performance-final-review-20261027/REPORT.FINAL.zh-CN.md`.

## Minimal changes

1. Use `Array.Clone` only when the source and destination plan types are the same
   SZArray, the runtime source type exactly matches that destination, the element
   is on the existing immutable atomic whitelist, and no declared element mapping
   intent exists. Require a null per-item callback. Reuse the same reference-scope
   lookup and registration, including ignore-projection keys. Even empty arrays
   are fresh containers; mutable elements and structs with referenced state stay
   on the recursive path. Registry version changes retire this mapping plan.
2. Evaluate safe assignability before immutable-atomic classification in
   `MapValue`'s runtime-destination-selection guard. Nonassignable DTO pairs skip
   the atomic reflection checks; neither graph behavior nor declared intent is
   removed. Timing benefit must be established by the separate benchmark worker.

A runtime exact-array-type check is necessary even with equal declared plan
types: the CLR can expose unsigned arrays through signed-array member types.
The check prevents cloning that original runtime type when an explicit declared
array profile retains the signed plan pair.

### Dictionary evidence and narrow typed identity plans

A second baseline-only probe used typed `Dictionary<int,int>` and
`Dictionary<string,string>` with equal counts. Baseline int/string map allocation
was respectively 848/736 B (3 entries), 2,056/1,512 B (16 entries), and
12,704/10,712 B (100 entries). The differences exactly match 48 B of boxed
key+value per entry, minus the known 8 B difference per dictionary entry-array
slot (summed growth capacities 3, 27, 350) and 8 B enumerator-layout difference.

The candidate uses typed identity delegates only for equal immutable atomic
source/destination types with no declared mapping intent. Keys retain the existing
null/collision/comparer and sensitive error-path checks. Values with callbacks
stay on `MapValue`; non-simple atoms with ignore projections also stay on it.
The registry version is captured **before** determining identity eligibility and
checked separately immediately before each key and value copy. A user comparer
executed by `ContainsKey` can change the version between those two checks; the
value will then fall back. Mutable values never use these identities.

Measured candidate allocation (same allocation-only probe):

| Input | Baseline B/op | Candidate B/op | Saved B/op |
| --- | ---: | ---: | ---: |
| byte[0] | 432 | 368 | 64 |
| byte[3] | 632 | 376 | 256 |
| byte[16] | 952 | 384 | 568 |
| byte[100] | 3,056 | 472 | 2,584 |
| Dictionary<int,int>, 3 entries | 848 | 704 | 144 |
| Dictionary<int,int>, 16 entries | 2,056 | 1,288 | 768 |
| Dictionary<int,int>, 100 entries | 12,704 | 7,904 | 4,800 |

String-dictionary allocation is unchanged. The typed dictionary path removes
exactly 48 B per int/int entry. Neither dictionary capacity nor reference-cache
semantics were changed. Raw logs are `baseline-dictionary.log` and `candidate.log`
in the same probe directory. No timing claims derive from these probes.

## Regression scope

`AtomicArrayCopyRegressionTests` checks fresh empty/nonempty arrays, mutation
isolation, aliases and independent calls, nullable scalar arrays (including Throw
null policy), string nulls, per-item callbacks and indexed callback failures,
mutable object/struct elements, ignore arguments, explicit element profile,
signed/unsigned element conversion and CLR signed-array aliases, and warm-cache
whole-array converter registration/replacement. Full existing graph/intent tests
remain mandatory; this focused test is not a substitute.

`AtomicDictionaryCopyRegressionTests` adds 112 assertions for atomic/nullable
values, retained comparer, per-value callbacks, mutable-value aliases and
self-cycles, ignores, explicit profiles, differing key conversions and collision
Key diagnostics. The array suite has 24 assertions.

## Verification status

Release builds and all **60 suites pass on each of net6.0, net8.0, net9.0 and
net10.0**, including both new suites. Each framework was forcibly rebuilt using
`-p:TargetFramework=<tfm> -p:TargetFrameworks=<tfm> -t:Rebuild -m:1`.
Production/generator/test source manifests before and after are identical.
Allocation-only before/after probes pass value/independence checks. Separately
scheduled BDN timing remains pending; this report does not claim timing results.

Durable evidence folder: `../mapper-runtime-hotspots-evidence-20261027/`, including
probe source/logs, runtime patch, four-framework build/test logs, source manifests
and DLL hashes. An initial multi-target command failed due to property-list
interpretation; the successful per-framework rebuilds above supersede it.

Normal net10 DLL after these gates:
`1f7130fba740fdd6e9351dccde970032c512bc84dfac82d30315372969259396`.
The generated-path worker may subsequently rebuild this same frozen source for
its own normal/checked integration gates; those results are reported separately.


## Integrated independent review

The same normal DLL passed external runtime fixtures (23 + 10 + 8 cases), the
136 new atomic-container assertions, projection checks (77 + 32 assertions),
generated graph (260 assertions) and context (12 assertions). A freshly rebuilt
checked runtime and checked generator passed generated graph 260; the previously
compiled graph consumer also passed its 252 assertions after replacing only the
runtime library. These are JIT correctness checks, not NativeAOT or SQL validation.
Independent audit evidence is retained alongside the other external evidence in
`independent-audit/`; no large build logs or binary artifacts are committed.

## Static identity delegate refinement

After `1a980bce83f0d216fcc4157a20fb4351839b4ff6`, the identity helper replaces
its expression construction and `CompileLambda` call with a static
`Func<TSource,TSource>` and a one-time cast to `Func<TSource,TDestination>`.
The existing `typeof(TSource) == typeof(TDestination)` guard proves the closed
delegate types are identical. The intermediate `object` is the delegate itself,
not an element value, so no per-item boxing is introduced. Eligibility and every
registry-version check remain unchanged. This removes unnecessary dynamic
compilation from this helper; cold timing improvement is not assumed and must be
measured separately. Earlier allocation measurements above refer to the initial
candidate and are not relabeled as new measurements of this refinement.

The refinement was forcibly rebuilt and passed all 60 suites on each of
net6.0/net8.0/net9.0/net10.0 with identical source manifests before and after.
Its normal net10 DLL hash is
`252142be3fab64a0c3f04ed4deda838ee1ce391289ddd7e8e844adc0cb641de9`.
The separate evidence directory is
`../mapper-runtime-static-identity-evidence-20261027/`.
Independent rebuilt consumers also passed clone 23, performance-contract 10,
declared-intent 8 (including late and within-batch registry replacement), and
atomic-container 136 assertions against this exact DLL. No new timing claim is
made from these correctness checks.
