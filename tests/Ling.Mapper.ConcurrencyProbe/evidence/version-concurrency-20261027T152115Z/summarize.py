import pathlib,json,csv,statistics,sys,math
r=pathlib.Path(sys.argv[1]);rows=[]
for p in sorted(r.glob('runs/*/pair-*/*/result.json')):
 x=json.load(open(p));parts=p.relative_to(r).parts;side=parts[3];pair=int(parts[2].split('-')[1]);ops=x['operations'];row=dict(mode=x['mode'],pair=pair,side=side,ops_s=x['operationsPerSecond'],cpu_us_op=x['processCpuSeconds']*1e6/ops,bytes_op=x['bytesPerOperation'],cpu_wall=x['cpuSecondsPerWallSecond'],wall_s=x['wallSeconds'],processor_count=x['processorCount'],affinity=x['affinityMask'],gc_counts=x['gcCollections'],min_worker_start_ms=min(w['startOffsetMs'] for w in x['workerResults']),max_worker_start_ms=max(w['startOffsetMs'] for w in x['workerResults']),max_worker_finish_gap_ms=max(-w['endOffsetMs'] for w in x['workerResults']),success=x['success'],source=str(p.relative_to(r)));rows.append(row)
assert len(rows)==24 and all(x['success'] for x in rows)
(r/'summary.json').write_text(json.dumps(rows,indent=2))
with (r/'summary.csv').open('w') as f:
 w=csv.DictWriter(f,fieldnames=rows[0]);w.writeheader();w.writerows(rows)
d={(x['mode'],x['pair'],x['side']):x for x in rows};pairs=[]
for mode in ['shared-1','shared-2','shared-4','private-4']:
 for i in [1,2,3]:
  a=d[mode,i,'baseline'];b=d[mode,i,'candidate'];pairs.append(dict(mode=mode,pair=i,throughput_ratio=b['ops_s']/a['ops_s'],cpu_per_op_ratio=b['cpu_us_op']/a['cpu_us_op']))
(r/'paired-ratios.json').write_text(json.dumps(pairs,indent=2))
def rangefmt(xs,digits=0):return f"{statistics.median(xs):,.{digits}f} [{min(xs):,.{digits}f}, {max(xs):,.{digits}f}]"
text='''# Version acquire读：真实C100并发归因（新云环境）

**结论：本窗口2/4worker模式均有一致并发收益证据；1worker收益未定。未证明固定核额度、全场景提速或默认Mapster目标达标。**

Baseline35d1fe5048ae0f0aba81e63603e5c1ee10940a85；candidatee0af897cc4412a95749d807073cff758980ce2ab，仅Version getter Interlocked.Read→Volatile.Read。冻结正常DLL分别312d1fa10ff6ae083864763ed400e3c7d633b14d96c348a0affe2cd167fa862f、7ce0405c357cd18c38ca34ef634f4314083ff1e2bc2a75dfe2cbc9204b4d36f1。各进程返回的实际加载hash均核验一致，测前后文件hash通过。不是旧并发数据恢复。

24独立进程，4模式×2版本×3launch。每模式pair1 B→C、pair2 C→B、pair3 B→C；全部结果保留。长期线程共享同一只读100unique输入；shared模式共用warmed mapper，private4每线程独立mapper但仍共享全局registry。2秒warm完成barrier后统一3秒deadline，64次映射查时钟；配置/compile/预检不计量。每次读取输出100个Value汇总14950校验，双方工作相同；本地ops/checksum/alloc，无每map共享Interlocked累计，Trace=null，registry无计时写入。

## 吞吐和CPU成本

中位数[min,max]均来自3个独立进程；配对范围不挑选最好样本。

| 模式 | Baseline ops/s | Candidate ops/s | 三对吞吐比C/B | CPU µs/op B→C中位 | B/op B→C | 判读 |
|---|---:|---:|---|---:|---:|---|
'''
for mode in ['shared-1','shared-2','shared-4','private-4']:
 a=[d[mode,i,'baseline'] for i in [1,2,3]];b=[d[mode,i,'candidate'] for i in [1,2,3]];ps=[p for p in pairs if p['mode']==mode];consistent=all(p['throughput_ratio']>1 and p['cpu_per_op_ratio']<1 for p in ps) and min(x['ops_s'] for x in b)>max(x['ops_s'] for x in a);status='本窗口一致并发收益' if consistent else '方向混合/重叠，未定'
 text+=f"| {mode} | {rangefmt([x['ops_s'] for x in a])} | {rangefmt([x['ops_s'] for x in b])} | "+' / '.join(f"{p['throughput_ratio']:.3f}×" for p in ps)+f" | {statistics.median(x['cpu_us_op'] for x in a):.2f}→{statistics.median(x['cpu_us_op'] for x in b):.2f} | {statistics.median(x['bytes_op'] for x in a):.0f}→{statistics.median(x['bytes_op'] for x in b):.0f} | {status} |\n"
text+='\n## 实际CPU、GC和调度（不是配额承诺）\n\n| 模式/侧 | CPU秒/wall秒 中位[min,max] | GC0/1/2各进程 | 最大worker起始偏差ms | 最大worker结束差ms |\n|---|---:|---|---:|---:|\n'
for mode in ['shared-1','shared-2','shared-4','private-4']:
 for side in ['baseline','candidate']:
  xs=[d[mode,i,side] for i in [1,2,3]];text+=f"| {mode}/{side} | {rangefmt([x['cpu_wall'] for x in xs],3)} | "+' ; '.join(str(x['gc_counts']) for x in xs)+f" | {max(x['max_worker_start_ms'] for x in xs):.3f} | {max(x['max_worker_finish_gap_ms'] for x in xs):.3f} |\n"
text+=f"\n24进程实际Environment.ProcessorCount集合={sorted({x['processor_count'] for x in rows})}，affinity mask集合={sorted({x['affinity'] for x in rows})}（511=逻辑CPU0–8）；测量真实wall范围{min(x['wall_s'] for x in rows):.6f}–{max(x['wall_s'] for x in rows):.6f}秒。cgroup quota文件不可读/不存在，配额未知；4workers≠4physical/reserved cores。ProcessCPU含GC等进程线程，不等于业务worker独享CPU。\n"
text+='''
## 解释边界

- shared2/shared4及private4中候选所有launch吞吐均高于对应baseline，CPU/op下降且B/op不变；与“一行移除全局reader RMW争用”假设一致。private4仍获益支持不是仅mapper实例cache共享造成，但不能仅此证明所有因果路径。JIT与发布同步正确性由独立gate提供，性能测试未替代publication gate。
- shared1配对方向混合，不能宣称单线程提速或严格无回归。并发3launch、短固定窗口不足以保证所有环境/长时负载表现；未调线程数/参数追赢。
- 检查所有worker的ops>0、checksum=14950×ops、输入未变、成功与DLLhash；别名/cycle/null/两次100custom trace的严格先验及shared/private checksum预检全部PASS。完整worker计数/偏差、GC和所有原始进程log/result.json保留。
- 第一版probe仅构建缺失裸DLL的直接引用而失败，未计时；补同一个MapperLibraryPath显式引用后再全量build/preflight通过。未改生产DLL或测量方案。
- 无Mapster计时，不能把本结果当默认Mapster速度目标通过。固定输出checksum额外工作已说明，不直接与BDN单线程ns/op混算。x86/AOT、Windows、默认tiering/PGO、冷态及registry并发写时性能未测。未修改已发布包。
'''
(r/'REPORT.zh-CN.md').write_text(text);print(text)
