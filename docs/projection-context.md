# 查询投影与单次调用上下文

这两项是不同的执行路径：**查询投影只生成表达式树**；**调用上下文用于运行时对象映射**。
不把运行时映射委托塞进查询，不自动调用 `AsEnumerable`、`Compile` 或枚举查询结果。

## 查询投影

```csharp
using Ling.Mapper.Extensions;

// IMapper 上的扩展，返回可重复使用的 Expression<Func<Order, OrderDto>>。
var selector = mapper.GetProjection<Order, OrderDto>();
var query = orders.AsQueryable().Select(selector);

// 同样构造 Queryable.Select，不执行查询。
var projected = orders.AsQueryable().ProjectTo<OrderDto>(mapper);
// 或：orders.AsQueryable().ProjectTo<Order, OrderDto>(mapper);
```

省略 `mapper` 时使用 `MapperProvider.Current`。默认命名选项来自 mapper；可用
`GetProjection<TSource, TDestination>(AdaptOptions)` 或
`ProjectTo<TDestination>(AdaptOptions, mapper)` 显式指定选项。

投影是独立的可选能力 `IProjectionMapper`；旧的第三方 `IMapper` 实现不需要实现新接口。
不支持投影的后端会立即抛出 `NotSupportedException`，不会退化成逐行 `Map`。
运行时 mapper 对类型对与命名选项缓存表达式；注册/替换运行时转换器后，后续请求重新检查。
已经交给调用者的表达式保持原样，并不会在执行查询时被悄悄替换。

### 当前支持范围

- 普通公有属性、`Rename` 源属性路径、`Ignore`、独立的 `MapFrom` 源表达式。
- 开启 `EnableFlattening` 后的属性路径展开；空引用路径返回该源成员类型的默认值。
- 递归嵌套 DTO、nullable 类型、数字/枚举转换；浮点/decimal 转整数保留 `Convert` 的舍入语义。
- 数组、`List<T>`、兼容集合接口及内联元素 `Select`；不注入映射器运行时调用。
- 公有无参构造，或唯一的最高参数数量且必需参数可绑定的公有构造；可选参数、忽略参数、只读 struct。
- `ConstructUsing` 中的 `new` / 简单成员初始化表达式；随后应用可写属性映射。
- 成员 `NullSubstitute` 的原子替代值。`MapFrom` 是源选择器：即使已经返回目标类型，复杂值仍继续构建独立投影。

自定义源表达式通过参数替换直接内联，**没有 `Expression.Invoke`**。
可使用条件表达式及常见 BCL 标量方法。任意用户方法、服务对象、委托和运行时对象捕获被拒绝；
标量闭包值仍由表达式及查询提供程序自身处理，不会强行冻结为创建时的值。

### 明确边界

以下情形抛出带类型与目标路径的 `ProjectionException`，不会在客户端偷偷执行：

- 成员 `ConvertUsing` 最终转换器，以及需要共享或运行时复制的复杂 `NullSubstitute` 常量。
  `NullSubstitute(new Child())` 不会被当作共享的 `Expression.Constant` 返回。查询可使用
  `MapFrom(s => s.Child ?? new Child { Value = 9 })` 明确构造 fallback；每个结果独立构建。

- 需要运行时注册转换器、用户自定义转换/运算符、上下文、服务、任意 helper/factory 方法或 `GetX` 方法约定。
- `Condition` / `PreCondition` / `IgnoreNullValues` 等保留已构造目标状态的赋值规则。
  查询使用 `MapFrom(s => condition ? value : fallback)` 显式表达选择。
- 修改目标嵌套路径（例如 `d => d.Child.Name`）；改为映射 child DTO 或返回 child 的源表达式。
- 字典映射、无法静态表示的集合形状、递归 DTO 环、运行时文本解析/格式化。
- 需要运行时抛错的 null→非 nullable 转换；可选择 nullable 目标或显式源表达式。

运行时自定义表达式的 `NullReferenceException` / `ArgumentNullException` 捕获不会被带进查询。
请在查询用的自定义表达式里显式处理 null，例如
`MapFrom(s => s.Customer == null ? null : s.Customer.Name)`。
投影的数字表达式、构造、集合和 BCL 方法是否能转成 SQL，仍由实际 provider 决定。

**验证范围是 LINQ-to-Objects 和记录表达式但禁止执行/枚举的假 provider。没有实测 SQL 翻译。**
没有增加 EF Core 或 SqlSugar 依赖。对于 SqlSugar 等自己的查询接口，可以取得上述 typed selector，
再传给当前 provider 接受 `Expression<Func<TSource, TDestination>>` 的投影入口；
`ProjectTo` 扩展只适用于真正的 `IQueryable`，不是任何 ORM 查询对象的通用适配层。

## 独立物化与运行时图映射的区别

本轮默认行为不再把可变同类型对象、数组或 List 直接返回为原引用。投影在支持的有限形状内
构造新节点；struct 的可变引用属性也递归处理。子 `IEnumerable<T>` 被物化为 List，避免 DTO
里保留延迟 Select 迭代器，导致查询执行后仍跟随源集合变化。最外层 `ProjectTo` 查询本身仍然延迟执行。

投影只能描述静态、有限的 DTO 形状。object/interface 运行时多态、字典、循环类型、系统资源、
field-only/opaque private state 等不能偷偷复用源对象，而是明确拒绝。不自动反射克隆资源。

**查询物化不是运行时图映射：不承诺 provider 跨路径、跨行或集合重复元素的引用身份。**
同一源 child 出现在两个选择位置时，投影可产生两个独立 child；两者都不共享源的可变节点。
需要保持图内共享引用与循环时，应使用运行时 `Map`/`Adapt`，而非要求 SQL 表达式携带运行时引用表。
上述约束同样适用于投影在 LINQ-to-Objects 上执行；没有 SQL/SqlSugar 翻译验证声明。

## 单次调用参数与服务

```csharp
// Profile 中；第二个参数在每次调用时取得，不是在计划编译时捕获。
CreateMap<Order, OrderDto>()
    .ForMember(d => d.Label, o => o.MapFrom((s, context) =>
        context.GetParameter<string>("prefix") + s.Id));

var context = new MappingCallContext(
    new Dictionary<string, object?> { ["prefix"] = "ORD-" },
    services: serviceProvider);

var dto = mapper.MapWithContext<OrderDto>(order, context);
var sameTarget = mapper.MapIntoWithContext(order, existingDto, context);
// 使用当前 mapper：order.AdaptWithContext<OrderDto>(context)
// 或 order.AdaptIntoWithContext(existingDto, context)
```

- `Parameters` 是 ordinal key 的只读**字典快照**；构造后修改原字典不影响它。
  字典中的对象值不是深拷贝，仍由调用者管理。
- `GetParameter<T>` 不做隐式转换：缺少参数抛 `KeyNotFoundException`，类型错误抛 `InvalidCastException`。
  可选参数使用 `TryGetParameter<T>`。
- `GetRequiredService<T>` 每次向本次的 `IServiceProvider` 请求服务；没有全局服务缓存，
  mapper 和 scope **不负责 Dispose provider 或 service**。
- 普通运行时自定义表达式也能读取 `MappingCallContext.Current`；应在表达式执行时读取它，
  不要在启动配置阶段把某次请求的 context/service 保存进闭包。
- `MapWithContext` 有泛型与显式 Type 入口，以及 options / per-item callback / ignored names 组合。
  callbacks 在 scope 内执行；异常时恢复之前的上下文。已有目标映射仍遵守其非事务性修改契约。

也可以显式包围多次调用：

```csharp
using (context.BeginScope())
{
    var first = mapper.Map<OrderDto>(order);
    await DoOtherWorkAsync();
    var second = mapper.Map<OrderDto>(anotherOrder);
}
```

scope 使用 `AsyncLocal`，跨 await 传递、嵌套后恢复；子异步流程覆盖 scope 不会改变父流程。
按创建顺序的逆序 Dispose；违反顺序会报错。`using` / `finally` 可确保异常恢复。
在 scope 外，`Current` 返回一个无参数、无服务的空上下文。

## 生成后端边界

Source Generator 后端不解释运行时 Profile、上下文参数或服务配置。
非空上下文通过 `MapWithContext` 或显式 scope 调用生成 mapper 均会明确拒绝；
空上下文不改变原映射行为。生成后端也不支持 `GetProjection`。
这不是把这些运行时能力扩展到了 NativeAOT；生成 guard 的独立 smoke 是 **JIT 验证**。
实际 NativeAOT 发布/执行仍需独立验证。

## 复现功能检查

```sh
dotnet build tests/Ling.Mapper.Tests/TestConsole.csproj -c Release -p:TargetFrameworks=net10.0
dotnet tests/Ling.Mapper.Tests/bin/Release/net10.0/TestConsole.dll --suite ProjectionRegressionTests
dotnet tests/Ling.Mapper.Tests/bin/Release/net10.0/TestConsole.dll --suite MappingCallContextRegressionTests
dotnet run --project tests/Ling.Mapper.ProjectionContextSmoke/Ling.Mapper.ProjectionContextSmoke.csproj -c Release
```

普通 regression runner 会预注册 JSON 转换器，生成 mapper 会按既有规则拒绝该运行时注册状态，
因此生成 guard 放在无此初始化的独立进程。它不修改生成器或模拟 NativeAOT。
