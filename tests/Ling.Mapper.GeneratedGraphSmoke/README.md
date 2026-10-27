# Generated independent graph smoke

```sh
dotnet run --project tests/Ling.Mapper.GeneratedGraphSmoke -c Release
dotnet run --project tests/Ling.Mapper.GeneratedGraphSmoke -c Release -p:CheckForOverflowUnderflow=true
```

A single Graph root self-map discovers mutable child, cycle, array/list, nested
collection, struct and nullable-struct self pairs. The fixture checks root and member
ownership, internal shared references/cycles, a 129-node cycle, mutable object/interface
slots (including dynamic collection elements), boxed values, extended immutable numeric
values, no source mutation and independent concurrent invocations.

Unknown dynamic dictionary/resource values and a boxed-value cycle that cannot be
registered before construction are explicit failures, not source sharing or stack overflow.
This is JIT execution, not NativeAOT execution or SQL-provider projection verification.

The hot-path regression cases also check nested member/index error paths, sensitive
source/destination name redaction, same-type immutable member getter/setter failures,
and path/sensitivity restoration after a hand-written delegate catches a member error.
Nested public-context helpers retain unknown option bits in their projection key,
including negative values under checked compilation; the public mapper entry still
rejects unsupported flags.
