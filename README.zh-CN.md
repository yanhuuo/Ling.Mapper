# FluentMapper - 中文使用说明

FluentMapper 是一个轻量、可扩展的对象映射库，目标是提供易用的 API、良好的性能并兼顾 AOT 环境的兼容性。

本文档为 README 的中文完整翻译与扩展，包含快速上手、API 用法示例、Source Generator 与 AOT 指南，以及性能建议。

## 快速上手

1. 引用库（项目引用或 NuGet）。
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

3. 创建配置并构建映射器：

```csharp
var cfg = new MapperConfiguration();
cfg.AddProfile(new ActivityProfile());
cfg.ConfigureConventions(opt => { opt.CaseInsensitiveNameMatch = true; });
var mapper = cfg.CreateMapper();
```

4. 执行映射：

```csharp
var dto = new ActivityDto { FirstName = "Tom", LastName = "Lee" };
var entity = mapper.Map<MallActivityEntity>(dto);
```

或使用扩展方法：

```csharp
MapperProvider.SetCurrent(mapper);
var e = dto.Adapt<MallActivityEntity, ActivityDto>((src, dest) => dest.Name = src.FirstName + " - Custom");
```

## 单元测试示例

仓库中已添加一个 xUnit 测试项目 `tests/MapperService.Tests`，包含基础示例：

- `Map_SimpleDto_To_Entity_Works`：测试简单 DTO 到实体的映射。
- `Adapt_With_CustomAction_Works`：测试 `Adapt` 与自定义回调。

运行测试：

```bash
dotnet test
```

## 生成 API 文档（基于 XML 注释）

项目已启用 XML 文档输出（在 `MapperService/FluentMapper.csproj` 中设置），生成规则如下：

- 构建项目将输出 XML 文档文件，例如：`bin/Debug/net6.0/FluentMapper.xml`。
- 推荐使用 DocFX 生成完整 API 文档：
  1. 安装 DocFX（https://dotnet.github.io/docfx/）。
  2. 在仓库根目录创建 `docs/docfx.json`（已提供示例）。
  3. 运行 `docfx docs/docfx.json` 生成 HTML 文档。

示例配置文件位于 `docs/docfx.json`，并在 `docs/articles` 中放置了 README 的转换副本。

## Source Generator 与 AOT 建议

- Generator 会在编译期生成强类型映射函数并通过 `MapperRegistry` 注册，运行时优先使用这些高性能委托。
- 对于 AOT 或不允许运行时代码生成的环境，确保：
  - 所有映射对在编译期可见，以便 Generator 生成对应方法；
  - 或者手动使用 `MapperRegistry.Register<TSrc,TDest>` 注册委托以避免运行时编译。

## 性能提示

- 优先使用 Source Generator 或 `MapperRegistry.Register` 提前注册强类型委托。
- 避免在热路径中使用 DynamicInvoke 或过多反射操作。
- 对集合映射考虑批量操作（可以编写 `MapMany` 扩展以返回 `List<T>`）。

---

如果你需要，我可以：
- 继续完善 DocFX 配置并生成静态 HTML 文档放到 `docs/_site`；
- 扩展测试覆盖更多场景；
- 将 README 示例转为更多单元测试或集成测试（例如集合、嵌套对象、类型转换器等）。
