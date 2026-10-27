import json,pathlib,collections,statistics,csv,sys
r=pathlib.Path(sys.argv[1]);rows=[]
for p in sorted(r.glob('results/*/results/*-report-full.json')):
 for b in json.load(open(p))['Benchmarks']:
  s=b['Statistics'];ls=collections.defaultdict(list)
  for m in b['Measurements']:
   if m['IterationMode']=='Workload' and m['IterationStage']=='Result':ls[m['LaunchIndex']].append(m['Nanoseconds']/m['Operations'])
  assert len(ls)==2
  means={k:statistics.mean(v) for k,v in ls.items()}
  rows.append(dict(case=b['Type'],method=b['Method'],mean_ns=s['Mean'],bytes=b['Memory']['BytesAllocatedPerOperation'],sd_ns=s['StandardDeviation'],n=s['N'],ci_lower=s['ConfidenceInterval']['Lower'],ci_upper=s['ConfidenceInterval']['Upper'],cv_pct=s['StandardDeviation']/s['Mean']*100,launch_means_ns=means,launch_spread_pct=100*(max(means.values())/min(means.values())-1),source=str(p.relative_to(r))))
assert len(rows)==8
(r/'summary.json').write_text(json.dumps(rows,indent=2))
with (r/'summary.csv').open('w') as f:
 w=csv.DictWriter(f,fieldnames=rows[0]);w.writeheader();w.writerows(rows)
d={(v['case'],v['method']):v for v in rows}
def line(case,mm):
 l=d[case,'Ling'];m=d[case,mm];noisy=any(x['cv_pct']>10 or x['launch_spread_pct']>10 for x in (l,m));overlap=not(l['ci_upper']<m['ci_lower'] or m['ci_upper']<l['ci_lower']);status='未定（波动/CI重叠）' if noisy or overlap else '本窗口Ling均值更低' if l['mean_ns']<m['mean_ns'] else 'Ling更慢';return f"| {case} | {l['mean_ns']:.2f} / {l['bytes']:,} | {m['mean_ns']:.2f} / {m['bytes']:,} | {l['mean_ns']/m['mean_ns']:.3f}× | {l['cv_pct']:.1f}% / {m['cv_pct']:.1f}% | {l['launch_spread_pct']:.1f}% / {m['launch_spread_pct']:.1f}% | {status} |\n"
header='| 场景 | Ling ns/B | Mapster ns/B | Ling/Mapster | CV L/M | launch跨度 L/M | 描述 |\n|---|---:|---:|---:|---:|---:|---|\n'
text='''# 新重建云环境：release1.3.2默认比较与独立保引用控制

**这是新重建源码和新环境首次正式结果，不是旧文件/hash/raw恢复，也不是原13场景恢复。** 只测新4case/8methods；源码base固定2d5218a7d0a2ddbd25f2601299864c42f39bcc87，不含20a候选。严格预检全部PASS，计时窗口2026-10-27 15:04:15–15:06:47UTC。

SDK10.0.403/runtime.NET10.0.14；Mapster10.0.13/BDN0.15.8；Release、CPU2（当前cpuset0–8已验证）、NoTiering；2launch×6warm×12measure×250ms。运行前/后冻结hash通过，BDN生成子程序中的Ling.Mapper.dll hash与已预检正常release DLL均为7d142760f0dad1ad19b135d669241e3f5d1e9d734d067e9bc1635461b479e8f0。通过MapperLibraryPath绑定冻结DLL，未重编活跃候选源。

## 原默认目标比较（不能替换分母）

'''+header+line('Custom100DefaultBench','Mapster')+line('List100DefaultBench','Mapster')+'''
## 独立同保引用诊断（不等于默认目标通过）

同一100unique输入、同一用户Transform，Mapster对DTO及根容器显式开启PreserveReference；本表不可替换上表。相关根list/array、跨容器alias、完整node cycle与共享dict/list/byte[]预检均PASS。

'''+header+line('Custom100PreserveControl','MapsterPreserve')+line('List100PreserveControl','MapsterPreserve')+'''
## 判断与限制

- 默认目标仍not achieved：C默认差距与List默认差距明显。控制组即使Ling更快，也不能冒称默认速度达标。
- C两库均使用相同[NoInlining] Transform(id)=checked(id*3+1)，每次100调用、顺序0..99，两个独立调用均验证；无用户自定义getter/callback，自动属性getter并非0。计时Trace=null，输出值与独立图/修改隔离相同。不能解释成Mapster少执行用户方法。
- 开启保引用后Mapster时间和分配显著增加，支持默认引用保障成本是主要差异来源之一；这不是把所有CPU差额唯一归因于引用表的因果证明，编译路径和调度也可能变化。控制组与default处于不同进程，应结合完整launch波动看待。
- CV/launch>10%仅筛噪声，CI重叠保守标未定，不是等效证明或允许差距。完整SD、CI、N、进程均值及原始路径见summary.json/csv。未恢复/重测原完整13场景、冷态、并发、Windows、AOT或生产PGO；未改变发布包或生产源码。
'''
(r/'REPORT.zh-CN.md').write_text(text);print(text)
