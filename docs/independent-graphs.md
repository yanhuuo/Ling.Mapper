# 独立对象图：当前本地候选契约

本文描述未发布的源码候选。与 1.3.1 及早先本地候选相比，**普通 Map/Adapt 不再把可变同型对象直接返回**。这是明确的行为变更，不是无差别序列化或任意对象内存克隆。

## 默认行为及支持范围

- 支持的 DTO、数组、集合、字典及其嵌套可变数据创建新实例。修改结果不应修改源；同一次调用内保留支持的共享引用及循环，跨调用不复用结果。
- 没有显式类型对规则时，使用实际运行时源形状；同一个 Derived 从 Base、interface、object 视图出现时不因遍历顺序丢失成员。显式 Profile（包括空 Profile）保留声明源形状并隔离对应缓存。声明源 converter/operator 同样不能被集合元素的 runtime 类型抢占。
- string、枚举、已支持的数字/日期/Guid 等不可变原子可直接赋值；额外包含 Half 与 .NET 7+ 的 Int128/UInt128。并非所有 struct 都是原子：含 List/数组属性的 struct 仍递归映射。原子同型支持不代表所有跨类型文本/数字转换均支持。
- 自动 DTO 映射面向公共属性。公共实例字段、无法构造保留的 getter-only 状态、资源对象（IDisposable/IAsyncDisposable、Task、delegate 等）不能冒充完整克隆，明确拒绝。普通构造器绑定支持只读外壳包含可变子对象；无法预构造的真实循环与 boxed value 自环明确失败。
- 支持一维数组、List、常见接口、HashSet/SortedSet、Queue/Stack、ConcurrentQueue/ConcurrentStack、Collection/ReadOnlyCollection，以及既有受限的可枚举构造形态。栈复制保留弹出顺序。多维数组、不支持的集合/非泛型 Hashtable 明确失败。
- 泛型字典分别映射键和值；键可变对象也不直接复用。转换后碰撞/null 键仍失败。Dictionary/ConcurrentDictionary 的公开 equality comparer、SortedDictionary/SortedList 的排序 comparer 在键类型不变且目标支持时保留；HashSet/SortedSet 同样保留已公开 comparer。改变键类型不强套旧 comparer。
- ReadOnlyDictionary 的源 comparer 没有公开获取 API，本实现不窥探私有字段，使用目标默认比较策略；容器与可变值仍独立。这是明确限制。比较器是可复用的行为策略，不承诺复制任意用户自定义 comparer 的内部可变状态。

## 取值表达式和最终转换器不同

```csharp
CreateMap<Input, Output>()
    .ForMember(d => d.Child, o => o.MapFrom(s => s.Child)); // 取值后递归映射

CreateMap<Input, Output>()
    .ForMember(d => d.Child, o => o
        .MapFrom(s => s.Child)
        .ConvertUsing<SourceChild>(value => CreateFinalChild(value))); // 最终结果
```

旧 `ForMember(d => d.Child, s => s.Child)` 与 options.MapFrom 都是源选择器：只求值一次，复杂结果即使已经是目标类型，也继续映射成独立图。原子目标不做无关重复转换。

显式 whole-type/registry converter、用户转换 operator、成员 `ConvertUsing` 返回最终值：执行一次，返回的实例不再克隆。如果用户主动返回源对象或共享缓存实例，就主动放弃该部分的默认隔离；库不偷偷改写用户方法。

字段顺序：Ignore → PreCondition → 源值一次 → null 规则 → 最终 converter 或普通递归映射 → Condition → 赋值。

- `ConvertUsing<TSourceMember>` 输入必须与所选择的源值静态类型兼容，不猜测额外转换；配置验证按成员路径报错。可显式写泛型参数或 typed lambda。链式 MapFrom 不会自动向后推导 ConvertUsing 的泛型输入。
- null 不传入成员 final converter。NullSubstitute 优先于 IgnoreNullValues；否则按普通 null/default/throw 策略。目标类型的 NullSubstitute **不交给源类型 converter**；原子直接用，复杂替代值每次独立映射，同调用保留替代图内部共享/循环。
- 普通源表达式的 NRE/ArgumentNull 兼容回退仅限取值阶段；converter、setter、Condition、构造器及回调错误不吞掉。Condition 接收完成转换/替代后的目标候选。

## 工厂、更新与用户代码边界

`ConstructUsing` 是构造工厂，不是最终转换器。返回源根对象会在字段写入前拒绝。工厂必须每次提供新的目标；没有全局追踪来检测用户缓存实例。工厂自行初始化且不被映射器覆盖的 readonly/private 图、用户 after-map 代码与最终 converter，均由用户负责其引用所有权。要显式保留身份使用 final converter，不借工厂绕过。

`MapInto` 保留用户明确传入的根目标并更新它；复杂目标子对象可复用，但若它就是对应源子对象，普通映射会分离，避免原位修改源。允许用户显式 source==target 的原地调用；不保证解除源/目标事先存在的任意交叉别名。更新不是事务，集合不按业务键合并。

## 其它后端及验证

运行时图映射、受限查询投影、源生成映射是不同后端。投影只在支持的有限无环形状中独立物化，不承诺 provider 跨路径/行共享引用，也未验证数据库 SQL 翻译；捕获复杂可变 NullSubstitute 明确拒绝。源生成器在支持形状内创建新图，未知动态类型、字典/资源/字段等未支持形状给出诊断或异常，不回退为源引用。详见[投影/context](projection-context.md)及[生成器](../generators/Ling.Mapper.Generator/README.md)。

测试、Native AOT 和性能各自单独验收；历史 identity 快路径性能不能当作新独立复制的等价对照。没有发布、版本号或线上包变更。

### 生成产物升级边界

采用新独立图语义时，需要升级 source generator 并重新编译消费者。旧生成代码中已经编译进去的直接成员赋值，不能仅替换 runtime DLL 就自动变成独立复制。显式 `GeneratedMapperRegistry.Register` 委托视为用户最终映射代码，其返回值不再二次克隆。普通 API 的源码/二进制兼容测试通过，**不等于旧生成产物自动获得新复制语义**。
