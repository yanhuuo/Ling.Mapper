# Accepted runtime optimization decision (2026-10-27)

## Accepted default

The accepted production and test source is exactly
`2408576544608893582fd41102410659eaf42328`, based on
`c3a435b53dfdbb568bbb5ac8c49615c2c6fbe49a`: guarded, lazy reuse of exact
**dictionary value** mapping plans. This decision adds documentation only.
The accepted branch is `perf/accepted-complex-value-plan`; the independent
worktree is `mapper-accepted-complex-value-plan`.

That source passed Release builds and all **61 suites on each of net6.0, net8.0,
net9.0 and net10.0**. Independent external validation passed clone 23,
performance-contract 10, declared-source 8 groups, atomic dictionary 136 and
value-dispatch 19 assertions. These are the existing 240 validation results,
not a new test execution for this documentation-only decision. The accepted
source does not include the path-buffer suite; do not describe its default
validation as 62 suites.

## Performance basis and limits

The fresh paired c3/240 recheck at 06:43:42–06:48:39 UTC measured:

| Case | c3 | 240 | Interpretation |
|---|---:|---:|---|
| Mixed dictionary | 6350.20 ns | 5671.19 ns | -10.7%; stable peer controls around -1.4% |
| Deep16 | 15217.40 ns | 14332.47 ns | -5.8%; weaker evidence because one peer control was noisy |
| Simple | 105.66 ns | 105.41 ns | effectively unchanged (-0.2%) |

Warm allocations are unchanged: mixed 4560 B, Deep16 15768 B, Simple 160 B.
The initial unstable measurement is preserved, not replaced by a selectively
assembled table. Limited cold samples (five processes per side) recorded
first-map allocation 92568→92576 B and median configuration allocation +896 B;
they do not prove faster cold initialization or zero setup cost. These are
fixture-specific .NET 10 Release, CPU2, TieredCompilation=0 measurements, not
production-PGO or end-to-end performance claims.

Evidence: `mapper-performance-complex-plan-20261027/recheck/REPORT.RECHECK.zh-CN.md`,
the original `REPORT.PAIRED.zh-CN.md`, and their raw samples; correctness evidence
is retained in `mapper-complex-plan-cache-evidence/` and
`/tmp/mapper-complex-dispatch-independent-audit/`.

## Not accepted as default; experiments preserved

Global/TLS path pooling, the combined f2 implementation, and the subsequent
Push(int) NoInlining experiment are **not** part of the accepted default.
They passed correctness checks and reduced allocations, but the tradeoff was
not established adequately:

- The combined f2 paired window recorded Batch100 15.692→16.835 microseconds
  (+7.3%) despite 120 B less allocation. This negative result remains recorded.
- The five-process NoInlining follow-up had launch differences above the preset
  10% threshold for all three sides (candidate 21.6%) and overlapping intervals.
  Its outcome is inconclusive: neither a verified timing repair nor a stable
  reproduced regression. The slower fifth candidate process was not discarded.
- Restoring a JIT loop shape is useful diagnostic evidence, not proof of an
  end-to-end timing benefit. Allocation reduction alone does not override an
  unresolved Batch tradeoff.

All pool/f2/NoInlining source trees, commits, diagnostics and benchmark samples
remain intact. No further pool or attribute variants are pursued. No push,
package publication, version bump or release is authorized by this decision.

## Next structural direction — proposal only

Investigate typed, compiled child mapping **at ordinary complex member edges**,
not another cache around the same dictionary value lookup. Candidate scope starts
with reference-class source/destination pairs and sealed/exact runtime source
types, preserving registry-version guards and the current fallback.

Compared with 240 (dictionary values only), an outer compiled expression could
read an eligible complex member into a typed local and call its typed child map
directly. The intended removed work is the generic MapMember→MapValue dispatch,
repeated effective-source/target refinement, Type/option-key construction and
ConcurrentDictionary lookup at those member edges, plus avoidable object casts
and intermediate delegates where code generation actually permits it. Reference
classes do not box today, so this is not a claimed boxing optimization.

It must retain graph/reference scope checks, per-edge path push/pop and exception
Origin/sensitivity, and check version immediately before each child invocation
across arbitrary getters/converters/comparers. Do not hoist guards across user
code. Recursive shapes need a safely published typed call target, not recursive
plan construction or unbounded expression expansion. Polymorphism, update,
callback, configured/nullable/conversion intent and uncertain edges use the
existing route. First demonstrate call/assembly differences for a bounded
member-graph fixture, then validate contracts and paired timings. No structural
implementation is included in this decision.
