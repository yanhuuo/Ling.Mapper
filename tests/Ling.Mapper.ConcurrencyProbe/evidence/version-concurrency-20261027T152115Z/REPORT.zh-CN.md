# Version acquire读：真实C100并发归因（新云环境）

**结论：本窗口2/4worker模式均有一致并发收益证据；1worker收益未定。未证明固定核额度、全场景提速或默认Mapster目标达标。**

Baseline35d1fe5048ae0f0aba81e63603e5c1ee10940a85；candidatee0af897cc4412a95749d807073cff758980ce2ab，仅Version getter Interlocked.Read→Volatile.Read。冻结正常DLL分别312d1fa10ff6ae083864763ed400e3c7d633b14d96c348a0affe2cd167fa862f、7ce0405c357cd18c38ca34ef634f4314083ff1e2bc2a75dfe2cbc9204b4d36f1。各进程返回的实际加载hash均核验一致，测前后文件hash通过。不是旧并发数据恢复。

24独立进程，4模式×2版本×3launch。每模式pair1 B→C、pair2 C→B、pair3 B→C；全部结果保留。长期线程共享同一只读100unique输入；shared模式共用warmed mapper，private4每线程独立mapper但仍共享全局registry。2秒warm完成barrier后统一3秒deadline，64次映射查时钟；配置/compile/预检不计量。每次读取输出100个Value汇总14950校验，双方工作相同；本地ops/checksum/alloc，无每map共享Interlocked累计，Trace=null，registry无计时写入。

## 吞吐和CPU成本

中位数[min,max]均来自3个独立进程；配对范围不挑选最好样本。

| 模式 | Baseline ops/s | Candidate ops/s | 三对吞吐比C/B | CPU µs/op B→C中位 | B/op B→C | 判读 |
|---|---:|---:|---|---:|---:|---|
| shared-1 | 106,688 [97,361, 112,439] | 105,725 [95,437, 118,109] | 1.086× / 0.895× / 1.050× | 9.45→9.56 | 16472→16472 | 方向混合/重叠，未定 |
| shared-2 | 136,252 [132,343, 138,198] | 167,737 [161,246, 169,039] | 1.267× / 1.183× / 1.223× | 13.99→11.28 | 16472→16472 | 本窗口一致并发收益 |
| shared-4 | 147,945 [142,667, 149,766] | 248,024 [207,026, 268,331] | 1.382× / 1.676× / 1.881× | 23.41→12.96 | 16472→16472 | 本窗口一致并发收益 |
| private-4 | 142,720 [138,601, 144,048] | 232,060 [212,184, 246,853] | 1.531× / 1.626× / 1.714× | 23.87→12.97 | 16472→16472 | 本窗口一致并发收益 |

## 实际CPU、GC和调度（不是配额承诺）

| 模式/侧 | CPU秒/wall秒 中位[min,max] | GC0/1/2各进程 | 最大worker起始偏差ms | 最大worker结束差ms |
|---|---:|---|---:|---:|
| shared-1/baseline | 1.009 [1.008, 1.011] | [1150, 0, 0] ; [1261, 0, 0] ; [1328, 0, 0] | 0.031 | 0.070 |
| shared-1/candidate | 1.010 [1.008, 1.011] | [1249, 0, 0] ; [1129, 0, 0] ; [1396, 0, 0] | 0.057 | 0.135 |
| shared-2/baseline | 1.916 [1.906, 1.921] | [1641, 0, 0] ; [1690, 0, 0] ; [1716, 0, 0] | 0.049 | 0.209 |
| shared-2/candidate | 1.893 [1.892, 1.897] | [2082, 0, 0] ; [2002, 0, 0] ; [2099, 0, 0] | 0.072 | 0.222 |
| shared-4/baseline | 3.505 [3.452, 3.542] | [1897, 0, 0] ; [1874, 0, 0] ; [1808, 0, 0] | 0.290 | 0.716 |
| shared-4/candidate | 3.214 [3.028, 3.244] | [2618, 0, 0] ; [3142, 0, 0] ; [3400, 0, 0] | 0.101 | 0.999 |
| private-4/baseline | 3.356 [3.309, 3.420] | [1755, 0, 0] ; [1808, 0, 0] ; [1825, 0, 0] | 0.062 | 0.752 |
| private-4/candidate | 3.009 [2.990, 3.015] | [2687, 0, 0] ; [2939, 0, 0] ; [3128, 0, 0] | 0.576 | 0.910 |

24进程实际Environment.ProcessorCount集合=[9]，affinity mask集合=[511]（511=逻辑CPU0–8）；测量真实wall范围3.000132–3.001774秒。cgroup quota文件不可读/不存在，配额未知；4workers≠4physical/reserved cores。ProcessCPU含GC等进程线程，不等于业务worker独享CPU。

## 解释边界

- shared2/shared4及private4中候选所有launch吞吐均高于对应baseline，CPU/op下降且B/op不变；与“一行移除全局reader RMW争用”假设一致。private4仍获益支持不是仅mapper实例cache共享造成，但不能仅此证明所有因果路径。JIT与发布同步正确性由独立gate提供，性能测试未替代publication gate。
- shared1配对方向混合，不能宣称单线程提速或严格无回归。并发3launch、短固定窗口不足以保证所有环境/长时负载表现；未调线程数/参数追赢。
- 检查所有worker的ops>0、checksum=14950×ops、输入未变、成功与DLLhash；别名/cycle/null/两次100custom trace的严格先验及shared/private checksum预检全部PASS。完整worker计数/偏差、GC和所有原始进程log/result.json保留。
- 第一版probe仅构建缺失裸DLL的直接引用而失败，未计时；补同一个MapperLibraryPath显式引用后再全量build/preflight通过。未改生产DLL或测量方案。
- 无Mapster计时，不能把本结果当默认Mapster速度目标通过。固定输出checksum额外工作已说明，不直接与BDN单线程ns/op混算。x86/AOT、Windows、默认tiering/PGO、冷态及registry并发写时性能未测。未修改已发布包。
