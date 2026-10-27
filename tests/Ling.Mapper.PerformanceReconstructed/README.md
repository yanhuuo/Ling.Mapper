# Reconstructed mapper parity controls

New reconstruction after cloud environment loss. **Not the recovered original13-case harness or original raw samples.** See PROVENANCE.md. Current4cases/8methods:

- List100DefaultBench, Custom100DefaultBench: primary comparisons to ordinary Mapster.
- List100PreserveControl, Custom100PreserveControl: independent same-input preservation-cost diagnostics, not replacement denominators.

Baseline semantic validation: PASS. Formal timing: NOT_RUN in this reconstruction stage.

From repository root with.NET10 SDK:

```sh
dotnet restore tests/Ling.Mapper.PerformanceReconstructed/Reconstructed.csproj --source https://api.nuget.org/v3/index.json
dotnet build tests/Ling.Mapper.PerformanceReconstructed/Reconstructed.csproj -c Release -m:1
dotnet tests/Ling.Mapper.PerformanceReconstructed/bin/Release/net10.0/Reconstructed.dll --validate
```

For isolated frozen source, pass `-p:MapperSourceProject=/absolute/frozen/src/Ling.Mapper.csproj`; for a frozen normal assembly use `-p:MapperLibraryPath=/absolute/Ling.Mapper.dll`. Do not share active worker source obj during measurement. All benchmark sources are unique for timed inputs; alias/cycle/array/container cases are strict untimed eligibility probes. If a shape fails, report it rather than weakening the contract.

Timing additionally requires `MAPPER_RECONSTRUCTED_TIMING_GO=1`, an authorized exclusive CPU window, newly recorded environment, and source/harness/binary hashes. This guard is not authorization. Keep all results; compare defaults separately from preservation controls. No statistical or performance claims follow from passing preflight.
