# First formal benchmark after reconstruction

NEW evidence only, release2d5218 baseline. Frozen normal Ling.Mapper.dll7d142760f0dad1ad19b135d669241e3f5d1e9d734d067e9bc1635461b479e8f0; candidate20a NOT_USED. Frozen validated harness and dependencies copied to an isolated execution folder. MapperLibraryPath MSBuild environment property forced all generated BDN projects to reference that exact DLL; a generated child DLL hash was verified identical during execution. No active production project or worker obj used.

Current CPU2 availability verified; .NET10.0.403SDK/.NET10.0.14 runtime re-read. Preflight repeated immediately before timing PASS;4cases8methods completed15:04:15–15:06:47UTC2026-10-27. Frozen source/harness/package DLL hashes before/after PASS. CPU lock released; no further timing in this stage.

Default target NOT_ACHIEVED. Same-preserve controls are separate diagnostics, not substituted denominators. Raw full.json, run logs, full launch statistics, environment, package content hash metadata and frozen manifests included. Prior environment raw/13-case harness remain NOT_RECOVERED. Historical9.06×result is NOT reused in this report. Production/published package unchanged.

The attached runner records original recovery-directory execution and is not a portable project setup script. Source lives at tests/Ling.Mapper.PerformanceReconstructed/; original relative evidence paths remain represented below. Binaries/package archives are intentionally not committed; hashes retained.
