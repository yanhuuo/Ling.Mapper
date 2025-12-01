# FluentMapper

FluentMapper 是一个轻量的、可扩展的对象映射库，目标：易用、可靠并对 AOT 场景友好。

本 README 包含：
- 项目简介
- 快速上手（示例）
- 所有主要 API 用法（包含扩展方法与 DI 注册）
- Source Generator 与 AOT 注意事项
- 性能与手动注册指南

---

## 项目简介

FluentMapper 支持：
- 基于 Profile 的映射规则注册（`MapperProfile` + `CreateMap<TSrc,TDest>()`）
- `ForMember` 自定义属性绑定、`Rename` 重命名、`Ignore` 忽略、`ReverseMap` 标记反向映射
- 集合与嵌套对象递归映射
- 注册型类型转换器（`TypeConverterRegistry`）
- Source Generator 生成高性能映射函数，运行时优先使用生成/手动注册委托，回退到表达式树编译
- DI 扩展：`AddFluentMapper` 和全局快捷访问 `MapperProvider`，以及手动注册中心 `MapperRegistry`

目标是：默认高性能（优先使用预生成委托与包装委托），同时在无法生成时提供兼容的运行时回退。

---

## 快速上手

1. 在项目中引用库（项目引用或 NuGet）。
2. 定义 Profile：

```csharp
public class ActivityProfile : MapperProfile
{
    public ActivityProfile()
    {
        CreateMap<ActivityDto, MallActivityEntity>()
            .ForMember(d => d.Name, s => s.FirstName + " " + s.LastName)
            .Rename(d => d.UserId, "Uid")
            .Ignore(d => d.InternalCode)
            .ReverseMap();
    }
}
```

3. 配置并创建 Mapper：

```csharp
var cfg = new MapperConfiguration();
cfg.AddProfile(new ActivityProfile());
cfg.ConfigureConventions(opt => { opt.CaseInsensitiveNameMatch = true; });
var mapper = cfg.CreateMapper();
```

4. 使用映射：

```csharp
var dto = new ActivityDto { FirstName = "Tom", LastName = "Lee" };
var entity = mapper.Map<MallActivityEntity>(dto);
```

或使用扩展方法：

```csharp
// 使用全局默认 mapper
MapperProvider.SetCurrent(mapper);
var e = dto.Adapt<MallActivityEntity, ActivityDto>((src, dest) => dest.Name = src.FirstName + " - Custom");

// 或传入 mapper
var e2 = dto.Adapt<MallActivityEntity, ActivityDto>(mapper, (src, dest) => dest.Name = src.FirstName);
```

---

## 主要 API 详解与示例（所有方法用法）

下面列出库中常用类与方法，并给出示例：

### MapperConfiguration

- `void AddProfile(MapperProfile profile)`：添加映射 Profile。可多次调用。
- `IMapper CreateMapper()`：基于当前配置创建 `IMapper` 实例。
- `void ConfigureConventions(Action<GlobalConventionOptions> convention)`：配置全局约定（如大小写规则）。
- 属性：`bool StrictMode`：开启后未匹配属性时抛异常。

示例：

```csharp
var cfg = new MapperConfiguration();
cfg.AddProfile(new ActivityProfile());
cfg.StrictMode = false;
var mapper = cfg.CreateMapper();
```

### MapperProfile 与 MappingExpression

在 `MapperProfile` 的构造函数中调用：

- `CreateMap<TSource, TDestination>()` 返回 `MappingExpression<TSource, TDestination>` 用于链式配置。
- 在 `MappingExpression` 上常见操作：
  - `.ForMember(d => d.Prop, s => s.Expr)`：自定义绑定
  - `.Rename(d => d.DestProp, "SrcPropName")`：源属性名不同的映射
  - `.Ignore(d => d.Prop)`：忽略该目标属性
  - `.ReverseMap()`：标记需要反向映射（Generator/运行时可选支持）

示例见上文 Profile 部分。

### IMapper

接口方法：
- `TDestination? Map<TDestination>(object? source)`：将 `source` 映射为 `TDestination`。
- `object? Map(object? source, Type sourceType, Type destType)`：通用反射/动态映射调用。

示例：

```csharp
var ent = mapper.Map<MallActivityEntity>(dto);
```

### 扩展方法（`MapperExtensions`）

- `MapTo<TDestination, TSource>(this TSource source, IMapper mapper)`：简单语法糖。
- `Adapt<TDestination, TSource>(this TSource source, IMapper mapper, Action<TSource, TDestination?> custom)`：映射后执行 `custom` 对目标进行二次加工（source-first）。
- `Adapt<TDestination, TSource>(this TSource source, Action<TSource, TDestination?> custom)`：使用全局 `MapperProvider.Current` 的快捷重载。
- `Adapt<TDestination, TSource>(this TSource source, Action<TDestination?, TSource> custom)`：目标先参数（dest, src）形式重载。
- `Adapt<TDestination, TSource>(this TSource source, IMapper mapper)`：无 custom 的映射重载。
- `MapInto<TDestination>(this IMapper mapper, object source, TDestination destination)`：将 source 的值映射到已存在的 destination 实例（不创建新实例）。
- `TryMap<TDestination>(this IMapper mapper, object? source, out TDestination? destination)`：安全映射，失败返回 false。
- `MapOrDefault<TDestination>(this IMapper mapper, object? source, TDestination? defaultValue = default)`：映射失败返回默认值。
- `MapOrThrow<TDestination>(this IMapper mapper, object? source)`：映射结果为 null 则抛出异常。

示例：

```csharp
var dest = dto.Adapt<MallActivityEntity, ActivityDto>(mapper, (src, d) => { d.UserId = src.Uid; });
mapper.MapInto(existingEntity, dto);
if (mapper.TryMap<MallActivityEntity>(dto, out var maybe)) { /* use maybe */ }
```

### DI 扩展

- `IServiceCollection AddFluentMapper(this IServiceCollection services, Action<MapperConfiguration>? configAction = null, params Assembly[] scanAssemblies)`：
  - 会创建 `MapperConfiguration`，执行 `configAction`，扫描 `scanAssemblies`（默认 EntryAssembly）中继承 `MapperProfile` 的类型并添加到配置，创建并注册 `IMapper` 与 `MapperConfiguration`。
  - 同时会调用 `MapperProvider.SetCurrent(mapper)` 注册全局默认映射器（便捷）。

示例：

```csharp
services.AddFluentMapper(cfg => { cfg.AddProfile(new ActivityProfile()); });
```

### 类型转换器（TypeConverterRegistry）

- `void TypeConverterRegistry.Register(Type src, Type dest, Delegate converter)`：手动注册转换器。
- `void TypeConverterRegistry.RegisterJson<T>()`：注册 `string <-> T` 的 JSON 转换（使用 `System.Text.Json`）。

示例：

```csharp
TypeConverterRegistry.RegisterJson<ExtraInfoModel>();
```

### 手动注册高性能委托（MapperRegistry）

- `MapperRegistry.Register<TSrc,TDest>(Func<TSrc,TDest> func)`：手动注册强类型委托，运行时优先使用，完全避免 DynamicInvoke。
- `MapperRegistry.Register(Type src, Type dest, Delegate func)`：非泛型版本。

示例：

```csharp
MapperRegistry.Register<ActivityDto, MallActivityEntity>(dto => new MallActivityEntity { Name = dto.FirstName + dto.LastName });
```

---

## Source Generator 与 AOT 支持

- 本库包含一个 Roslyn Source Generator（项目 `MapperService.Generator`），用于在编译期扫描 `CreateMap<TSrc,TDest>()` 并生成映射方法，Generator 会将映射函数注册到 `GeneratedMapperFactory` 并同时通过 `MapperRegistry.Register<TSrc,TDest>(...)` 注册强类型委托，以便运行时直接使用这些高性能函数。
- 运行时查找顺序：`MapperRegistry` wrapper/typed -> `GeneratedMapperFactory` -> 表达式树编译回退。
- 为 AOT 场景推荐：
  - 启用生成器并确保所有映射对在编译期可见（不要用运行时动态类型）；
  - 在发布配置中禁用运行时表达式编译（若将来添加配置开关 `AllowRuntimeCompile=false`）；
  - 对于无法生成的类型，手动通过 `MapperRegistry.Register` 注册委托以确保没有运行时 code-gen。

---

## 性能建议

- 优先使用 Source Generator 或 `MapperRegistry.Register<TSrc,TDest>` 手动注册强类型委托。
- 避免在热路径中频繁使用反射或 DynamicInvoke；库默认会缓存 wrapper 委托以减少开销。
- 对于大量元素映射，建议批量映射方法（可使用 `MapMany` 扩展自定义实现返回 List<T>）。

---

## 示例：完整使用流程（Console 示例）

见 `samples/TestConsole/Program.cs`。核心步骤：

1. 注册转换器：
```csharp
TypeConverterRegistry.RegisterJson<ExtraInfoModel>();
```
2. 配置并创建 mapper：
```csharp
var cfg = new MapperConfiguration();
cfg.AddProfile(new ActivityProfile());
var mapper = cfg.CreateMapper();
MapperProvider.SetCurrent(mapper); // 注册全局
```
3. 映射：
```csharp
var entity = dto.Adapt<MallActivityEntity, ActivityDto>((src, dest) => dest.Name = src.FirstName);
```

---

## 构建与测试

- 使用 `dotnet build` 构建整个解决方案（Generator 在编译时运行）。
- 运行 samples：
  - 切换到 `samples/TestConsole` 并执行 `dotnet run`。

---

## 常见问题（FAQ）

Q: 如何在 AOT 环境完全避免运行时代码生成？
A: 使用 Source Generator 覆盖所有需要的映射对或手动通过 `MapperRegistry.Register<TSrc,TDest>` 注册映射委托。并在配置中（未来）关闭运行时编译回退。

Q: 如果我的映射表达式很复杂（调用方法/外部状态）怎么办？
A: 复杂的表达式在 Generator 中解析可能有限，建议手动注册 `Func<TSrc,TDest>` 到 `MapperRegistry`，或在 Profile 中使用 `ForMember` 并允许运行时回退（如果安全）或改写为可编译表达式。

---

如果你希望，我可以：
- 继续把 README 中的每个示例补充更多边缘情况示例；
- 生成 API 文档（从 XML 注释）；
- 在 README 中加入基准测试（BenchmarkDotNet）示例与结果。