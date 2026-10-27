# Typed scalar member expressions

This candidate replaces only the scalar-expression selection inside
`BuildMemberConversion`. Registry converter precedence, source null policy,
constructor/member handling, mapping context and exception-path wrapping remain
at their existing call sites. Root/dynamic scalar mapping keeps its existing
implementation. This is not a timing result or a Mapster parity claim.

## Supported specialization

- Existing numeric-to-numeric `Convert.ToX(T)` calls remain typed, including
  decimal/floating-point rounding and checked overflow semantics.
- Known bool/char/numeric pairs select an exact `Convert.ToX(T)` overload when
  present, not a cast or an object overload.
- String-to-numeric/bool/char uses `Convert.ToX(string, IFormatProvider)` with a
  runtime `CultureInfo.CurrentCulture` read. String-to-DateTime uses the same
  `Convert.ToDateTime(string)` as the shared scalar contract.
- String-to-Guid/DateTimeOffset/DateOnly/TimeOnly/TimeSpan uses the same
  `Parse(string)` call as `ScalarConversion`.

Enums, aliases, extended/native numerics, operators, formatting and all other
unsupported specializations retain their previous paths. The helper requires
non-null, non-nullable input and an unwrapped target; the caller still owns
nullable lifting and null policy. No TryParse/default-on-error behavior is added.

## Reproduction and evidence

Use the .NET 10 SDK. For each `net6.0`, `net8.0`, `net9.0`, `net10.0`:

```sh
dotnet build tests/Ling.Mapper.Tests -c Release -f "$tfm" \
  -p:TestTargetFrameworks="$tfm" -m:1
LING_CONVERSION_MATRIX="matrix-$tfm.json" \
  dotnet "tests/Ling.Mapper.Tests/bin/Release/$tfm/TestConsole.dll" --all
```

`TypedScalarExpressionRegressionTests` checks the selected expression's typed
BCL method and absence of object conversion nodes, changes culture after
compilation, and checks nullable mapping. Its allocation-only loop compares
10,000 typed string-to-decimal calls with the original object bridge. The typed
route permits under 1 KiB total runtime bookkeeping rather than demanding
literally zero bytes; the object route allocates at least one box per call.
All results contribute to a checked checksum. No stopwatch is used.

The independent allocation-only ordinary member-map probe on .NET 9 measured
1,040,000 bytes for 10,000 candidate mappings versus 1,360,000 bytes with the
accepted `5db1292` DLL: 32 bytes removed per mapping. The identical executable,
source input and verified decimal checksum were used for both DLLs. This is
allocation evidence, not execution-speed evidence. `NumericNullableBench` is
not expected to gain from this change: its numeric conversions already used
typed overloads before this candidate.

The boundary matrix is compared byte-for-byte with the same test executable
using the accepted baseline DLL. It includes 9,123 rows on net6 (9 explicitly
unavailable Int128 rows) and 9,312 on later frameworks, with three cultures,
exception types/paths, overflow, NaN/infinity, nullable, enum and date cases.

Validation on this candidate: all four frameworks completed 62 suites with
zero failures, and each JSON boundary matrix was byte-identical to baseline.
Typed-loop allocation totals were 0 bytes on net6/net8 and 24 bytes on
net9/net10, versus 320,000 bytes through the original bridge on every framework.
The full regression runner includes legacy performance demonstrations; these
outputs were not treated as timing evidence. No dedicated benchmark was run.
