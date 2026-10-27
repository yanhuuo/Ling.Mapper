# Guarded typed collection execution

The exact standard `List<T>` and SZArray source paths can construct and populate
List/array-compatible destinations without non-generic enumeration or `IList.Add`.
A List struct enumerator preserves concurrent-modification checks; the array loop
uses ordinary typed index reads. Runtime exact-type checks keep overridden/custom
subclass enumeration on the original path.

An immutable identity delegate or lazily acquired child MappingPlan is cached in
the containing plan, not per element or per mapping invocation. Registry generation
is captured before eligibility and checked at each element dispatch. Null values,
polymorphic values, nullable boxing, callbacks, update contexts, assignable-target
redirection, and registry changes retain the MapValue fallback. The shared helper
is also used by the existing dictionary value path. Lazy acquisition avoids
recursive plan compilation; volatile publication supports concurrent first use.

Dictionary enumeration uses a stack-held cursor with an exact standard Dictionary
struct-enumerator arm and an interface-enumerator arm for every other runtime type.
The discriminator is separate from the custom enumerator reference: a null custom
enumerator still fails, as before. Key collision preflight remains before value
mapping; sensitive Key and indexed Value diagnostics, comparer preservation and
user enumeration disposal remain unchanged. No CollectionsMarshal reference spans
user code, and no per-call local-function capture or per-element delegate is added.

## Local evidence (2026-10-27)

Compared with accepted commit `5db129273ff4486aa9331e50d24619fc44c9f435`, a separate
.NET 10 Release, non-timing GC probe with 20 elements (2,000 warmups, 5,000 mappings)
reported:

| Mapping | Baseline bytes/op | Typed bytes/op |
|---|---:|---:|
| List<Node> → List<Node> | 3552 | 3512 |
| List<Node> → Node[] | 3552 | 3480 |
| Node[] → Node[] | 3544 | 3480 |
| Dictionary<int,Node> | 5432 | 5376 |
| List<int> → List<int> | 1120 | 600 |
| Dictionary<int,int> | 2080 | 2032 |

A separate instrumented copy measured 21 MapValue / 21 GetOrCompile calls falling
to 1 / 1 for all three ordinary Node collection cases and the atomic List case.
Dictionary calls remain 1 / 1 because baseline already cached dictionary child
plans. These are allocation/dispatch observations, **not throughput or Mapster
parity claims**; benchmark decisions require the combined candidate's BDN run.

The focused regression suite checks independent clones, aliases and cycles, nulls,
per-value data, ignored members, polymorphism, assignable targets, custom subclass
enumeration, null enumerators, disposal, registry changes inside a converter and
getter, user operators, nullable conversions, callback counts, first-use failure
retry, indexed errors, List mutation checks and concurrent first publication.

Validation: Release builds and all 62 suites passed on net6.0, net8.0, net9.0 and
net10.0. Independent source reviews found no remaining blockers. The focused
suite additionally asserts source/result mutation isolation and independent
containers/nodes across separate mapping calls.
