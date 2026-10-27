# Adapt consumer API compatibility fixture

This compiles the same consumer calls against **two separate, real assemblies**:
Ling.Mapper 1.3.1 at commit `55a3a427e76be58ec202d109b09726bc3d558338`, and the
candidate DLL. Do not build the baseline from an edited working tree. This is
not a replacement for the runtime regression and generator/AOT tests.

```sh
DOTNET=/path/to/dotnet ./verify.sh /path/to/baseline/Ling.Mapper.dll \
  /path/to/candidate/Ling.Mapper.dll /path/to/validation-output
```

Requires .NET 10 SDK (C# 14). The script does not rebuild either library. It
places consumer binaries, intermediate files, SDK first-use state, logs and
summaries in the chosen output directory. Exit code 0 means all expected build
outcomes, both positive fixture runs, and the unchanged baseline-built consumer
with the candidate DLL substituted all matched; 1 means a mismatch; 2 means
invalid arguments. `dotnet-info.txt` records the actual SDK/runtime environment.

## Positive calls

- Legacy default/options/string-and-params ignores; named parameters; result,
  typed-object and collection-item callbacks; static calls and method groups.
- Candidate ordinary ignore-array, array collection expression, empty arrays,
  option/ignore/result-callback/object-callback/item-callback combinations.
- Array and expanded params, reordered named parameters, and lambda-based type
  inference for all three callback forms.

## Deliberate compile-failure probes

| Probe | Baseline 1.3.1 | Candidate |
| --- | --- | --- |
| `Adapt<T>(null)` | CS0121 ambiguous | CS0121 ambiguous |
| `Adapt<T>(null, "Value")` | Compiles; invalid at runtime | CS0121 ambiguous |
| `Adapt<T>(null, names)` | Compiles; invalid at runtime | CS0121 ambiguous |
| `Adapt<T>(options, null)` | CS1503 unsupported call shape | CS0121 ambiguous |

The two baseline null-ignore probes previously compiled but passed a null
`firstIgnore`, which the API rejects. Their new ambiguity is therefore recorded,
not described as compatibility for successful runtime behavior. Use typed casts
or named callback/ignore parameters for intentional argument-validation tests.
The script never executes a stale binary after an expected compilation failure.
