# Conversion boundary matrix (2026-10-27)

## Scope and independent expectations

`ConversionTypeMatrixRegressionTests` supplements the existing conversion suites: it does not duplicate their ordinary `42`-valued numeric pair checks. Expected primitive results/exceptions come from `System.Convert.ChangeType` with the active culture, not Ling's own conversion helper. Consequently banker's rounding, signed/unsigned overflow, NaN and infinities retain the established BCL contract rather than cast/truncation assumptions.

Each case checks the actual mapped value and type, or `MappingException.OriginalExceptionType` and `Path`. Root, member (`$.Value`) and dynamic-object routes are distinguished. Null is explicitly tested through nullable source/destination declarations; boxed non-null nullable values are not treated as evidence of null handling.

Set `LING_CONVERSION_MATRIX` to a writable JSON path. Output rows contain culture, source/target, sample, route, `Contract` (`SUPPORTED`, `REJECTED`, or `NOT_AVAILABLE`), exception/path, and `Result` (`PASS`, `FAIL`, or `NOT_RUN`). Rejected conversions are successful checks only when their exact diagnostic contract matches; they are not counted as supported.

| Coverage | Cases / evidence |
|---|---|
| sbyte, byte, short, ushort, int, uint, long, ulong | each Min/Max/zero, cross primitive destinations, root/member/object |
| float, double, decimal | Min/Max, negative fraction, zero; float/double NaN and ±Infinity |
| bool, char, string | false/true, NUL/ASCII/max-char, malformed/empty/whitespace/signed/overflow/culture-sensitive text |
| enum / Flags / EnumMember | all 8 underlying types at Min/Max across enum targets; high-bit aliases, nullable flags and whitespace rejection |
| nullable | present -> nonnullable, nonnullable -> nullable, null -> nullable, null -> default, null -> string |
| Guid, DateTime, DateTimeOffset, TimeSpan, DateOnly, TimeOnly | text roundtrip with independent parse oracle, invalid/empty/whitespace, nullable routes |
| Half | self/nullable/object/array-element identity by value, exact float operators both directions; string parsing rejected |
| Int128 / UInt128 | self/nullable/object/array-element maximum values; exact long/ulong operators both directions on net8/9/10; string parsing rejected; unavailable on net6 (NOT_RUN) |
| nint / nuint | self/nullable/object/array elements supported by value; automatic long/ulong numeric conversion explicitly rejected; no blanket native-number conversion claim |
| CurrentCulture | en-US, de-DE, zh-CN, restored on exit |

`ConversionAssignableRegressionTests` adds the new ordinary-map ownership contract: same type/base/interface/object roots, nested polymorphic interface member, mutable children, mutation isolation, base-to-derived values, and MapInto root preservation. It also checks operator/registry/member-selector precedence and execution counts. A ForMember returning a cached target-shaped value remains a **selector**, so the result is copied. An explicit final registry converter may intentionally return source/cache identities; its output is not remapped through a same-type converter.

`MemberFinalConversionMatrixRegressionTests` checks the selected-member pipeline (`MapFrom` or convention then `ConvertUsing`) separately: selector/converter calls, final output identity, per-invocation context, constructor/nested/MapInto paths, conditions, nulls, substitutions and exception paths. Mutable target-typed `NullSubstitute` values are independently mapped per call (including nested lists), but are not fed to the source-typed converter; scalar substitutes remain unchanged.

## Existing complementary coverage (not duplicated)

- `ConversionRegressionTests`: normal numeric pair matrix, enums across underlying integral types, collection/dictionary errors and nullable policy.
- `DeclaredConversionRegressionTests`: Flags and EnumMember names/aliases/ambiguity, high/signed bits, user operators and nullable signatures, malformed enum/error paths, user registration and replacement, explicit member rules and execution counts.
- `CurrentCultureConversionRegressionTests`: repeated culture switching through hot cached plans, async culture isolation, constructor/collection/dictionary routes, explicit invariant-culture user expressions.
- Deep dictionary/graph ownership is covered separately by `DictionaryDeepRegressionTests` and the graph contract suites.

These are concrete supported/rejected probes, not a claim that every .NET type pair or every custom collection is supported.

## Reproduction

Use installed SDK `/workspace/shared/dotnet/dotnet`, with writable `DOTNET_CLI_HOME`, `XDG_RUNTIME_DIR`, `XDG_DATA_HOME`, and cached `NUGET_PACKAGES=/workspace/shared/nuget-packages`.

```sh
# Repeat tfm=net6.0, net8.0, net9.0, net10.0 separately.
dotnet build tests/Ling.Mapper.Tests/TestConsole.csproj -c Release -m:1 \
  -p:TestTargetFrameworks=$tfm -f $tfm -p:NuGetAudit=false \
  -p:UseSharedCompilation=false -nr:false
LING_CONVERSION_MATRIX=/absolute/path/$tfm.json \
  dotnet tests/Ling.Mapper.Tests/bin/Release/$tfm/TestConsole.dll \
  --suite ConversionTypeMatrixRegressionTests
dotnet tests/Ling.Mapper.Tests/bin/Release/$tfm/TestConsole.dll \
  --suite ConversionAssignableRegressionTests
dotnet tests/Ling.Mapper.Tests/bin/Release/$tfm/TestConsole.dll \
  --suite MemberFinalConversionMatrixRegressionTests
```

No benchmarks or performance timing are part of these tests. Final per-framework results are recorded in the accompanying validation run logs, not inferred from another runtime's pass.

## Verified final run (2026-10-27 04:34 UTC)

The nine focused suites in the run manifest passed on all four runtimes: **36/36 focused suite runs**. This is focused conversion/ownership/API verification, not a claim that unrelated repository tests or performance gates ran here.

| Runtime | Matrix supported | Matrix rejected as specified | Passed matrix checks | NOT_RUN |
|---|---:|---:|---:|---:|
| net6.0 | 5,961 | 3,153 | 9,114 | 9 (Int128/UInt128 unavailable) |
| net8.0 | 6,141 | 3,171 | 9,312 | 0 |
| net9.0 | 6,141 | 3,171 | 9,312 | 0 |
| net10.0 | 6,141 | 3,171 | 9,312 | 0 |

Each runtime also passed: Assignable 33 assertions, MemberFinal 66 assertions, Conversion 539 cases, DeclaredConversion 2,761 assertions, ScalarBoundary 10 assertions, PublicContract 90 cases, ValueAndApiBoundary 905 cases, and CurrentCulture 10,649 assertions. Counts are suite-specific units and are not conflated into an artificial assertion total.

Archived local machine evidence is in `../mapper-independent-graphs-validation-20261027/focused-artifacts/conversion-type-matrix/`: per-TFM JSON rows, `{Suite}-{tfm}.log` files, and `summary.json` containing all 36 outcomes plus SHA-256 hashes of the tested TestConsole/Ling.Mapper assemblies. These generated artifacts are local verification evidence, not source inputs or models.

Final focused verification was rerun against the unified binaries **after** the covariant-container declared-converter/operator correction. All 36 runs passed again. The earlier 04:29 summary is retained separately as historical evidence, not labeled as the final build. Final net10 Ling.Mapper SHA-256: `f608520632fe92c1e40f00c4da7b96d33728c974ca7ebe3e57323d65eaca8ecf`; final 122-source manifest SHA-256: `8c22d4027c609b56e6026da33722b6bf9fea52a6ddf50bd06e41f12786584ca9`. No rebuild or source/test edits were made during this rerun.
