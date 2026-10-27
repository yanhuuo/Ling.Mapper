# 运行时映射补齐：已有目标、字段规则与构造

本文描述本地功能候选，不代表已发布版本。普通 Map/Adapt 的独立图与 selector/final converter 行为已更新，见[当前独立图契约](independent-graphs.md)。源生成/AOT 能力是独立的受限子集，不会自动解释运行时 Profile。

## 更新已有实例

```csharp
var mapper = new MapperConfiguration().CreateMapper();
mapper.MapInto(source, destination); // 返回同一个 destination
mapper.Map(source, destination);     // 双泛型便捷入口，保留声明源类型
source.AdaptInto(destination);       // 使用 MapperProvider 的 mapper
mapper.MapInto(source, destination, AdaptOptions.IgnoreNullValues,
    afterMapItem: (target, input) => { /* 成功后的回调 */ },
    ignoreNames: ["Id", "Address.InternalCode"]);
```

- `MapInto` 不调用根目标构造器或 `ConstructUsing`，不替换根目标。目标必须非 null，且为可修改引用对象、可修改泛型集合/字典或一维零下标数组。
- 根源为 null 时保持目标不变，不触发逐项回调。`AdaptInto` 的结果回调仍在成功后执行一次。
- 未映射、Ignore、PreCondition=false、Condition=false、IgnoreNullValues 跳过的字段保留旧值。普通 null 成员仍遵循原来的 null/default/throw 配置。
- 常规复杂成员在可修改时复用旧实例；不可修改的记录/只读集合、长度变化的数组按普通映射创建替代值。普通 `ForMember`/MapFrom 是源选择后继续独立映射；仅显式成员 ConvertUsing 是最终值。目标子对象等于对应源子对象时不原地修改源。带 Condition 的成员先产生独立候选，避免 Condition=false 时已修改旧子对象。
- 同一映射调用内保留循环和共享引用；先出现的匹配源/目标规则对决定共享目标。不同显式源类型规则仍隔离缓存。不要并发修改同一个目标实例。
- 集合更新是清空后重填，不按业务键合并元素。先映射全部元素再修改容器，源与目标为同一个容器也可用。数组根长度必须一致。
- 标准 `Dictionary`、`SortedDictionary`、`SortedList`、`ConcurrentDictionary` 的 comparer 用于预检键碰撞。自定义容器的 Add/Clear/比较器、副作用和外部并发无法保证回滚。
- 对象字段更新不是事务：稍后字段出错时，早先成功赋值可能保留。集合元素转换/标准键碰撞/数组长度失败不会清除原容器；元素回调与子对象副作用不在回滚保证内。
- 根 whole-type converter / 用户转换 operator 不能保证原地身份，明确拒绝；嵌套成员仍可使用它们返回替代值。值类型/标量根、只读根、init-only 写入路径明确拒绝。不是“复制新对象后冒充原地映射”。
- 生成映射与第三方 `IMapper` 默认不支持 `MapInto`，默认接口方法明确抛 `NotSupportedException`，不让旧实现静默忽略目标。

## 字段规则顺序

```csharp
CreateMap<Input, Output>()
    .ForMember(d => d.Amount, o => o
        .MapFrom(s => s.AmountText)
        .PreCondition(s => s.Enabled)
        .NullSubstitute(0m)
        .Condition((s, d, mapped) => mapped >= 0m));
```

1. Ignore 优先；PreCondition 在读取源字段/调用 resolver 前执行。
2. `MapFrom` 源表达式求值一次，源/目标字段类型可不同。
3. 源值为 null 时，目标类型的 NullSubstitute 绕过源类型 final converter（原子直赋，复杂值独立映射），并优先于 IgnoreNullValues；否则字段级 IgnoreNullValues 覆盖全局选项。
4. 转换后，Condition 接收源、当前目标及目标类型的最终候选；通过才赋值。
5. 显式字段规则优先于约定和自动 operator；显式 whole-object converter 仍按原有整体接管语义绕过对象字段映射。

旧 `ForMember(d => d.Value, s => ...)` 保留兼容语义：仅源表达式里的 `NullReferenceException`/`ArgumentNullException` 变成默认值；不吞 Format/Overflow、转换器、构造器、setter、Condition 或回调错误。Context 版 `MapFrom((source, context) => ...)` 每次调用读取当前 context，见 [调用上下文与投影](projection-context.md)。

`AssertConfigurationIsValid()` 是显式检查，不默认开启严格目的成员完整性；它检查元数据和构建映射计划，不执行用户 getter/resolver/converter。`RequireDestinationMemberSource` 可主动要求目的成员来源。ReverseMap 对不可可靠逆转的计算/条件/null/context 规则明确拒绝，不猜反向行为。

## 构造与可选约定

- 普通有公共无参构造器的对象保留原构造路径。没有公共无参构造器时可绑定公共有参构造器，包括 class/record/readonly struct。
- 在所有必需参数可绑定的候选中选择参数数最多者；相同优先级歧义明确失败。可用 `ConstructUsing` 显式选择工厂。
- 构造参数尊重自定义字段、Rename、Ignore、PreCondition、null 规则和转换；已绑定成员不重复调用 getter 或再次赋值。需要已存在目标的 Condition 不适用于构造参数，使用 PreCondition 或工厂。
- 构造后立即注册引用，再填充其它可写字段。目标尚未构造时的真实不可变循环明确失败；不同嵌套忽略投影不应被误判为同一循环。
- 自动 Flattening 和 GetX 方法约定是 opt-in 配置，不改变默认源属性规则；同层可读属性优先于方法。显式 Rename/MapFrom 不被约定覆盖。
- 无可绑定构造器且无显式 Profile 的非严格异型引用映射仍保留旧的 null 结果；同型独立复制明确失败而非返回 null；严格或显式配置下会报告错误。不会自动执行任意复杂单参构造器来做 scalar coercion。

## 验证范围

功能测试、API 编译/替换 DLL 兼容性、生成映射 JIT/诊断与性能是不同证据。生成器不支持的运行时 Profile、已有目标、非空调用 context 不宣称 AOT 支持；真实 Native AOT 未通过时不以 JIT smoke 代替其结论。性能在功能冻结与全量回归之后独立比较。

此前 d1b717c 运行时补齐快照（尚未包含新独立图语义）：四 runtime 各 50 suites / 0 failed；独立集成 consumer 16/16。生成 JIT 普通/checked 各 115 断言、预期负诊断 9 项、独立生成 context guard 12 断言通过。测试前后 source 清单相同；完整分项见[覆盖矩阵](测试覆盖矩阵.md)。这些记录不是性能排名，也不是 Native AOT 验收。
