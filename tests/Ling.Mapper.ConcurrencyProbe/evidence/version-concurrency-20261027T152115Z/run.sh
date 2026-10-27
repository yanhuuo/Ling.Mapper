#!/usr/bin/env bash
set -euo pipefail
cd "$(dirname "$0")"
exec 9>../reconstructed-cpu-window.lock;flock -n 9 || exit 3
source ../../toolchains/env.sh
export DOTNET_TieredCompilation=0 MAPPER_CONCURRENCY_TIMING_GO=1
python3 - <<'PY'
import os,pathlib,json,platform
allowed=sorted(os.sched_getaffinity(0));assert len(allowed)>1,'Multicore hypothesis cannot run under singleCPU affinity'
data={'allowed_cpus':allowed,'platform':platform.platform(),'quota':'unknown unless readable values below','files':{}}
for name in ['/proc/self/status','/proc/self/cgroup','/sys/fs/cgroup/cpu.max','/sys/fs/cgroup/cpu.stat','/sys/fs/cgroup/cpu/cpu.cfs_quota_us','/sys/fs/cgroup/cpu/cpu.cfs_period_us']:
 try:data['files'][name]=pathlib.Path(name).read_text()
 except Exception as e:data['files'][name]=type(e).__name__+': '+str(e)
pathlib.Path('cpu-environment-start.json').write_text(json.dumps(data,indent=2))
PY
{ date -u; uname -a; dotnet --info; cat /proc/sys/kernel/random/boot_id; cat /proc/loadavg; env | grep '^DOTNET_'; } > environment-start.txt
for side in baseline candidate;do
 find "$side" -type f ! -path '*/obj/*' ! -name '*.log' -print0 | sort -z | xargs -0 sha256sum > "$side-frozen-before.sha256"
 sha256sum -c "$side-frozen-before.sha256" > "$side-integrity-before.log"
done
for mode in shared-1 shared-2 shared-4 private-4;do
 for pair in 1 2 3;do
  sides='baseline candidate';[[ $pair == 2 ]] && sides='candidate baseline'
  for side in $sides;do
   dest="runs/$mode/pair-$pair/$side";mkdir -p "$dest"
   export MAPPER_SOURCE_SHA=35d1fe5048ae0f0aba81e63603e5c1ee10940a85
   [[ $side == candidate ]] && export MAPPER_SOURCE_SHA=e0af897cc4412a95749d807073cff758980ce2ab
   echo "START $mode pair$pair $side $(date -u +%FT%TZ)"
   dotnet "$side/probe/bin/Release/net10.0/ConcurrencyProbe.dll" --mode "$mode" > "$dest/process.log" 2>&1
   python3 - "$dest" "$side" <<'PY'
import json,pathlib,sys,hashlib
p=pathlib.Path(sys.argv[1]);lines=[l[len('RESULT_JSON '):] for l in (p/'process.log').read_text().splitlines() if l.startswith('RESULT_JSON ')];assert len(lines)==1
x=json.loads(lines[0]);assert x['success'] and not x['traceEnabled'] and x['operations']>0
assert x['assemblyHash']==hashlib.sha256(pathlib.Path(sys.argv[2]+'/library/Ling.Mapper.dll').read_bytes()).hexdigest()
assert all(w['Error'] is None and w['Checksum']==w['Operations']*14950 and w['Operations']>0 for w in x['workerResults'])
assert x['affinityMask'] is not None and x['affinityMask']&(x['affinityMask']-1)!=0
assert x['tieredCompilation']=='0'
(p/'result.json').write_text(json.dumps(x,indent=2))
print(x['mode'],sys.argv[2],round(x['operationsPerSecond'],2),'ops/s',round(x['bytesPerOperation'],2),'B/op','CPU/wall',round(x['cpuSecondsPerWallSecond'],3),flush=True)
PY
   echo "DONE $mode pair$pair $side $(date -u +%FT%TZ)"
  done
 done
done
for side in baseline candidate;do sha256sum -c "$side-frozen-before.sha256" > "$side-integrity-after.log";done
{ date -u; cat /proc/loadavg; } > environment-end.txt
date -u > ALL_TIMING_COMPLETE
