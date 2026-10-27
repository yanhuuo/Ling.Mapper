#!/usr/bin/env bash
set -euo pipefail
cd "$(dirname "$0")"
exec 9>../reconstructed-cpu-window.lock;flock -n 9 || exit 3
source ../../toolchains/env.sh
export DOTNET_TieredCompilation=0 MAPPER_RECONSTRUCTED_TIMING_GO=1
# Environment MSBuild property: all generated BDN projects use the exact frozen normal release DLL.
# Do NOT resolve the active repository source (which may contain candidate changes).
export MapperLibraryPath="$PWD/frozen-library/Ling.Mapper.dll"
unset MapperSourceProject || true
{ date -u; uname -a; cat /proc/sys/kernel/random/boot_id; cat /proc/cpuinfo; dotnet --info; taskset -pc $$; grep Cpus_allowed_list /proc/self/status; cat /proc/loadavg; env | grep -E '^(DOTNET_|MapperLibraryPath|MAPPER_)'; } > environment-start.txt
taskset -c 2 true
find frozen-harness frozen-library -type f ! -path '*/obj/*' -print0 | sort -z | xargs -0 sha256sum > frozen-before.sha256
sha256sum -c frozen-before.sha256 > frozen-integrity-before.log
dotnet frozen-harness/bin/Release/net10.0/Reconstructed.dll --validate > semantic-before.log
for case in Custom100DefaultBench Custom100PreserveControl List100DefaultBench List100PreserveControl;do
 dest="$PWD/results/$case";mkdir -p "$dest";echo "START $case $(date -u +%FT%TZ)"
 (cd frozen-harness;dotnet bin/Release/net10.0/Reconstructed.dll --filter "*.$case.*" --artifacts "$dest") > "$dest/run.log" 2>&1
 python3 - "$dest" <<'PY'
import json,pathlib,sys
bs=[b for f in pathlib.Path(sys.argv[1]).glob('results/*-report-full.json') for b in json.load(open(f))['Benchmarks']]
assert len(bs)==2 and all(b.get('Statistics') for b in bs)
for b in bs:print(b['Type'],b['Method'],b['Statistics']['Mean'],'ns',b['Memory']['BytesAllocatedPerOperation'],'B')
PY
 echo "DONE $case $(date -u +%FT%TZ)"
done
sha256sum -c frozen-before.sha256 > frozen-integrity-after.log
{ date -u; cat /proc/loadavg; } > environment-end.txt
date -u > ALL_TIMING_COMPLETE
