# New reconstruction baseline checkpoint

Source base:2d5218a7d0a2ddbd25f2601299864c42f39bcc87 (release1.3.2). Compiled against a separate git archive, never an uncommitted candidate or the active repository src/obj. No production code changes in this checkpoint.

Official NuGet restore: PASS (Mapster10.0.13, BenchmarkDotNet0.15.8 and transitive packages recorded). SDK10.0.403/runtime10.0.14 re-read in new cloud environment. Release/net10.0 standalone build and repo-native-path build: PASS. All4fixture semantic checks and full strict alias/cycle/null/value/mutation probes: PASS. C custom transform calls exactly100 in order0..99 twice per library, with trace disabled afterwards. BDN listing:4fixtures/8methods.

Formal timing: NOT_RUN. Previous environment files, hashes and raw results: NOT_RECOVERED. This checkpoint is new source/correctness evidence only, not original13 restoration or historical performance proof. No default-vs-preserve denominator substitution.

Repo-path build used explicit MapperSourceProject pointing at independent release archive; standalone harness and repo-native source bodies are identical. Binary hashes are for this newly compiled evidence and are not claimed equal to historical/main-worktree binaries. Build warnings are the two existing release XML documentation warnings.
