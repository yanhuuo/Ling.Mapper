# Reconstructed cloud member-plan smoke

This fixture was newly reconstructed on 2026-10-27 after the old execution environment was lost. It is **not** a byte-identical restoration of the earlier 67 + 1,352 fixture and does not claim that historical count. Its new source hashes and observed results must be recorded separately.

Public API only; supports frozen `LibraryPath` for release baseline, or a project reference for candidate runs. `TestTargetFrameworks` selects one framework. No timing or production changes.

Coverage:

- Aliases across members/list/array/dictionary, cycles and early reference registration, mutable graph isolation across roots and both mutation directions.
- Getter/constructor/setter single execution, per-non-null collection callback occurrences; MapInto, structs, constructor binding, factory, ignores, private nested DTO access, flat-root allocation equality to direct construction.
- Explicit declared base/derived rules with isolated reference scopes; alternating runtime polymorphism; Derived-to-Base runtime-shape cloning absent intent, Extra mutation isolation, explicit base projection and ignore-projection separation.
- Warm child plans followed by first registration inside source getter, destination constructor, nested getter or different-pair converter; immediate reentry, following siblings and roots; root-only and collection-item callback timing.
- Existing registered-converter inline capture: in-flight parent fields retain stamp 4 after replacement while reentry and subsequent root observe stamp 5. This is intentionally the existing release behavior, not a new per-field registry transaction promise.
- First nested getter/constructor failure followed by success, fresh data on retry, current error path/type, foreign MappingException rewrapping and no user-code replay.
- 256 parallel warm maps followed by 256 after completed registry registration, validating values and identity isolation.

Use fresh processes for each run because generic-tag registry state is intentionally not reflection-reset.

New reconstructed baseline validation: all four net6.0/net8.0/net9.0/net10.0 runs passed **1,380 checks**. Each run used a freshly built/frozen true-release DLL and byte-compared its loaded DLL copy. Flat-root allocation remained 24,000 bytes for 1,000 mappings, equal to direct new. Logs and the new source SHA-256 manifest are in recovery `evidence/reconstructed-smoke-*`; this is new evidence, not reissued historical evidence.
