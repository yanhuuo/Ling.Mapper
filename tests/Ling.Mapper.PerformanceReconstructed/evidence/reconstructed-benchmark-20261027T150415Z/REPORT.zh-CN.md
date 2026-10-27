# 新重建云环境：release1.3.2默认比较与独立保引用控制

**这是新重建源码和新环境首次正式结果，不是旧文件/hash/raw恢复，也不是原13场景恢复。** 只测新4case/8methods；源码base固定2d5218a7d0a2ddbd25f2601299864c42f39bcc87，不含20a候选。严格预检全部PASS，计时窗口2026-10-27 15:04:15–15:06:47UTC。

SDK10.0.403/runtime.NET10.0.14；Mapster10.0.13/BDN0.15.8；Release、CPU2（当前cpuset0–8已验证）、NoTiering；2launch×6warm×12measure×250ms。运行前/后冻结hash通过，BDN生成子程序中的Ling.Mapper.dll hash与已预检正常release DLL均为7d142760f0dad1ad19b135d669241e3f5d1e9d734d067e9bc1635461b479e8f0。通过MapperLibraryPath绑定冻结DLL，未重编活跃候选源。

## 原默认目标比较（不能替换分母）

| 场景 | Ling ns/B | Mapster ns/B | Ling/Mapster | CV L/M | launch跨度 L/M | 描述 |
|---|---:|---:|---:|---:|---:|---|
| Custom100DefaultBench | 8104.10 / 16,472 | 957.16 / 3,256 | 8.467× | 3.7% / 5.0% | 0.4% / 8.8% | Ling更慢 |
| List100DefaultBench | 8995.52 / 22,872 | 1372.39 / 9,656 | 6.555× | 5.3% / 6.8% | 1.3% / 5.9% | Ling更慢 |

## 独立同保引用诊断（不等于默认目标通过）

同一100unique输入、同一用户Transform，Mapster对DTO及根容器显式开启PreserveReference；本表不可替换上表。相关根list/array、跨容器alias、完整node cycle与共享dict/list/byte[]预检均PASS。

| 场景 | Ling ns/B | Mapster ns/B | Ling/Mapster | CV L/M | launch跨度 L/M | 描述 |
|---|---:|---:|---:|---:|---:|---|
| Custom100PreserveControl | 8683.43 / 16,472 | 9870.67 / 16,352 | 0.880× | 5.3% / 11.5% | 2.7% / 4.2% | 未定（波动/CI重叠） |
| List100PreserveControl | 9017.32 / 22,872 | 9513.29 / 22,752 | 0.948× | 3.6% / 5.2% | 0.2% / 0.2% | 未定（波动/CI重叠） |

## 判断与限制

- 默认目标仍not achieved：C默认差距与List默认差距明显。控制组即使Ling更快，也不能冒称默认速度达标。
- C两库均使用相同[NoInlining] Transform(id)=checked(id*3+1)，每次100调用、顺序0..99，两个独立调用均验证；无用户自定义getter/callback，自动属性getter并非0。计时Trace=null，输出值与独立图/修改隔离相同。不能解释成Mapster少执行用户方法。
- 开启保引用后Mapster时间和分配显著增加，支持默认引用保障成本是主要差异来源之一；这不是把所有CPU差额唯一归因于引用表的因果证明，编译路径和调度也可能变化。控制组与default处于不同进程，应结合完整launch波动看待。
- CV/launch>10%仅筛噪声，CI重叠保守标未定，不是等效证明或允许差距。完整SD、CI、N、进程均值及原始路径见summary.json/csv。未恢复/重测原完整13场景、冷态、并发、Windows、AOT或生产PGO；未改变发布包或生产源码。
