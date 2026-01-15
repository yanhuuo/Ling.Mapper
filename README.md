# Ling.Mapper

<div align="center">

[![NuGet](https://img.shields.io/nuget/v/Ling.Mapper.svg?style=flat-square&logo=nuget)](https://www.nuget.org/packages/Ling.Mapper/)
[![NuGet Downloads](https://img.shields.io/nuget/dt/Ling.Mapper.svg?style=flat-square&logo=nuget)](https://www.nuget.org/packages/Ling.Mapper/)
[![License](https://img.shields.io/github/license/yanhuuo/Ling.Mapper?style=flat-square)](LICENSE)
[![.NET](https://img.shields.io/badge/.NET-6%20%7C%208%20%7C%209%20%7C%2010-512BD4?style=flat-square&logo=dotnet)](https://dotnet.microsoft.com/)

**?? 简单、高效、类型安全的 .NET 对象映射库**

[快速开始](#-快速开始) ? [特性](#-特性) ? [文档](#-文档) ? [示例](#-示例) ? [性能](#-性能)

</div>

---

## ?? 简介

Ling.Mapper 是一个轻量级、高性能的 .NET 对象映射库，专注于提供**简单易用**和**高效执行**的映射体验。

### 为什么选择 Ling.Mapper？

| 特点 | 说明 |
|------|------|
| ?? **简单直观** | Fluent API 设计，链式调用，5 分钟上手 |
| ? **性能卓越** | 表达式树编译 + 委托缓存，接近手写代码的性能 |
| ??? **类型安全** | 完整的泛型支持和可空类型处理 |
| ?? **灵活强大** | Profile 配置 + 运行时规则 + 自定义转换器 |
| ?? **AOT 友好** | 支持原生 AOT 编译和 Source Generator |
| ?? **DI 集成** | 开箱即用的依赖注入支持 |

---

## ? 特性

### 核心功能

<table>
<tr>
<td width="50%">

**基础映射**
- ? 自动映射同名属性
- ? Profile 配置高级规则
- ? ForMember 自定义表达式
- ? Rename 属性重命名
- ? Ignore 忽略属性

</td>
<td width="50%">

**类型支持**
- ? 可空类型（`int?`, `string?`）
- ? 枚举转换（enum ? int/string）
- ? 集合映射（List, Array, IEnumerable）
- ? 嵌套对象递归映射
- ? 自定义类型转换器

</td>
</tr>
</table>

### 高级特性

- ?? **运行时规则**：`AdaptOptions` 灵活配置映射行为
- ?? **扩展方法**：`Adapt<T>()` 简洁的映射语法
- ?? **手动注册**：`MapperRegistry` 注册高性能委托
- ?? **反向映射**：`ReverseMap()` 自动生成反向配置
- ?? **严格模式**：开发时检测未映射属性

---

## ?? 安装

### NuGet 包管理器

```bash
dotnet add package Ling.Mapper
```

### Package Manager Console

```powershell
Install-Package Ling.Mapper
```

### .csproj 文件

```xml
<PackageReference Include="Ling.Mapper" Version="1.0.5" />
```

---

## ?? 快速开始

### 1. 定义映射规则

```csharp
using Ling.Mapper;

public class UserProfile : MapperProfile
{
    public UserProfile()
    {
        CreateMap<UserDto, User>()
            .ForMember(dest => dest.FullName, 
                       src => src.FirstName + " " + src.LastName)
            .Rename(dest => dest.UserId, "Id")
            .Ignore(dest => dest.Password);
    }
}
```

### 2. 配置 Mapper

```csharp
var config = new MapperConfiguration();
config.AddProfile(new UserProfile());
config.ConfigureConventions(opt => 
{
    opt.CaseInsensitiveNameMatch = true;
});

var mapper = config.CreateMapper();
MapperProvider.SetCurrent(mapper);  // 设置全局 Mapper
```

### 3. 执行映射

```csharp
// 基础映射
var user = mapper.Map<User>(userDto);

// 使用扩展方法
var user = userDto.Adapt<User>();

// 带回调的映射
var user = userDto.Adapt<User>((src, dest) => 
{
    dest.UpdatedAt = DateTime.Now;
});
```

---

## ?? 示例

### 基本映射

```csharp
// 定义模型
public class UserDto
{
    public int Id { get; set; }
    public string? FirstName { get; set; }
    public string? LastName { get; set; }
}

public class User
{
    public int UserId { get; set; }
    public string? FullName { get; set; }
}

// 配置映射
public class UserProfile : MapperProfile
{
    public UserProfile()
    {
        CreateMap<UserDto, User>()
            .ForMember(d => d.FullName, 
                       s => s.FirstName + " " + s.LastName)
            .Rename(d => d.UserId, "Id");
    }
}

// 执行映射
var user = userDto.Adapt<User>();
```

### 可空类型映射

```csharp
public class Source
{
    public int? NullableId { get; set; }
    public string? Name { get; set; }
}

public class Target
{
    public int Id { get; set; }        // null → 0
    public string? Name { get; set; }
}

// 自动处理
var target = source.Adapt<Target>();

// 自定义处理
CreateMap<Source, Target>()
    .ForMember(d => d.Id, s => s.NullableId ?? -1);
```

### 枚举类型映射

```csharp
public enum UserStatus { Inactive = 0, Active = 1, Pending = 2 }

// enum → int
var source = new { Status = UserStatus.Active };
var target = source.Adapt<IntTarget>();  // Status = 1

// int → enum
var source2 = new { StatusCode = 1 };
var target2 = source2.Adapt<EnumTarget>();  // Status = Active

// enum → string
var target3 = source.Adapt<StringTarget>();  // Status = "Active"

// string → enum (不区分大小写)
var source3 = new { Status = "active" };
var target4 = source3.Adapt<EnumTarget>();  // Status = Active
```

### 运行时映射规则

```csharp
// 忽略大小写
var user = apiResponse.Adapt<User>(AdaptOptions.IgnoreCaseOption);

// 忽略下划线
var user = dbRow.Adapt<User>(AdaptOptions.IgnoreUnderscoreOption);

// 灵活匹配
var user = data.Adapt<User>(AdaptOptions.FlexibleOption);

// 组合规则
var user = source.Adapt<User>(new AdaptOptions
{
    IgnoreCase = true,
    IgnoreNullValues = true,
    IgnoreProperties = new[] { "Password", "CreditCard" }
});
```

### 集合映射

```csharp
// List 映射
var users = userDtos.AdaptList<User>();

// 带索引处理
var users = userDtos.AdaptList<User>((src, dest, index) => 
{
    dest.RowNumber = index + 1;
});
```

### DI 集成

```csharp
// Program.cs
services.AddFluentMapper(config => 
{
    config.AddProfile(new UserProfile());
    config.ConfigureConventions(opt => 
    {
        opt.CaseInsensitiveNameMatch = true;
    });
});

// 在服务中使用
public class UserService
{
    private readonly IMapper _mapper;
    
    public UserService(IMapper mapper)
    {
        _mapper = mapper;
    }
    
    public UserDto GetUser(int id)
    {
        var user = _repository.GetById(id);
        return user.Adapt<UserDto>(_mapper);
    }
}
```

---

## ?? 文档

### 核心文档

| 文档 | 说明 |
|------|------|
| [?? 功能概览](docs/Feature-Summary.md) | 完整的功能列表 |
| [?? Adapt 使用](docs/Adapt-Usage.md) | Adapt 方法详解 |
| [?? 枚举转换](docs/EnumConversion_Support.md) | enum ? int/string |
| [?? 异常处理](docs/Exception-Handling-Quick-Guide.md) | 异常处理策略 |

### 更新日志

| 版本 | 说明 |
|------|------|
| v1.0.5 | 可空类型支持 |
| v1.0.4 | 警告修复 |
| v1.0.3 | 运行时规则 |

---

## ? 性能

### 性能特点

<table>
<tr>
<td width="50%">

**优化机制**
1. 表达式树编译（一次编译，多次复用）
2. 委托缓存（避免重复编译）
3. Source Generator（编译时生成代码）
4. 手动注册优先（最高性能路径）

</td>
<td width="50%">

**执行顺序**
```
MapperRegistry 注册的委托（最快）
    ↓
Source Generator 生成的代码
    ↓
表达式树编译（运行时回退）
```

</td>
</tr>
</table>

### 性能建议

| 建议 | 说明 |
|------|------|
| ? 手动注册 | 使用 `MapperRegistry.Register` 注册高频映射 |
| ? Source Generator | 启用 Source Generator（如果可用） |
| ? 缓存实例 | Mapper 线程安全，建议单例 |
| ? 一次配置 | 在启动时配置，避免频繁配置 |

---

## ?? 实际应用场景

### API 响应映射

```csharp
[HttpGet]
public async Task<IActionResult> GetUsers()
{
    var users = await _repository.GetAllAsync();
    return Ok(users.AdaptList<UserDto>());
}
```

### 数据库映射

```csharp
var dbResult = await _connection.QueryAsync<DatabaseRow>(sql);
var users = dbResult.Select(row => 
    row.Adapt<User>(AdaptOptions.IgnoreUnderscoreOption)
).ToList();
```

### 第三方 API 集成

```csharp
var externalData = await _httpClient.GetAsync<ExternalApiResponse>();
var user = externalData.Adapt<User>(
    AdaptOptions.IgnoreCaseOption,
    (src, dest) => dest.Source = "ExternalAPI"
);
```

---

## ?? 与其他库对比

| 特性 | Ling.Mapper | AutoMapper | Mapster |
|------|------------|------------|---------|
| 简单易用 | ? | ? | ? |
| 高性能 | ? | ?? | ? |
| AOT 支持 | ? | ? | ?? |
| 可空类型 | ? | ?? | ?? |
| 运行时规则 | ? | ? | ? |
| 轻量级 | ? (< 100KB) | ? | ? |
| DI 集成 | ? | ? | ?? |
| 链式 API | ? | ? | ? |

---

## ?? 更新日志

### v1.0.5 (当前) - 可空类型支持

- ? **新增**：完整的可空类型支持（`int?`、`string?` 等）
- ? **增强**：`ConvertSimpleType` 方法新增 7 种可空类型转换场景
- ?? **文档**：添加可空类型详细使用文档
- ? **测试**：添加 6+ 个可空类型测试场景

### v1.0.4 - 警告修复

- ??? **修复**：所有 XML 注释警告（27 → 2）
- ??? **修复**：空引用警告和 CS8603 警告
- ??? **优化**：DependencyInjection 包版本兼容性
- ?? **文档**：完善 README 和 API 参考文档

### v1.0.3 - 运行时规则

- ? **新增**：运行时映射规则（AdaptOptions）
- ? **新增**：AdaptList 集合映射扩展
- ?? **修复**：类型转换器问题

---

## ?? 贡献

欢迎提交 Issue 和 Pull Request！

### 贡献流程

1. Fork 项目
2. 创建特性分支 (`git checkout -b feature/AmazingFeature`)
3. 提交更改 (`git commit -m 'Add some AmazingFeature'`)
4. 推送到分支 (`git push origin feature/AmazingFeature`)
5. 开启 Pull Request

### 贡献指南

- 遵循现有代码风格
- 添加适当的测试
- 更新相关文档
- 确保所有测试通过

---

## ?? 许可证

本项目采用 [MIT License](LICENSE) 许可证。

---

## ?? 相关链接

<div align="center">

| 链接 | 说明 |
|------|------|
| ?? [NuGet 包](https://www.nuget.org/packages/Ling.Mapper/) | 下载最新版本 |
| ?? [GitHub 仓库](https://github.com/yanhuuo/Ling.Mapper) | 源代码和问题追踪 |
| ?? [完整文档](docs/README.md) | 详细的技术文档 |
| ?? [问题反馈](https://github.com/yanhuuo/Ling.Mapper/issues) | 报告 Bug |
| ?? [功能建议](https://github.com/yanhuuo/Ling.Mapper/issues) | 提出新想法 |

</div>

---

## ? Star 历史

如果这个项目对您有帮助，请给一个 ? Star 支持一下！

[![Stargazers over time](https://starchart.cc/yanhuuo/Ling.Mapper.svg)](https://starchart.cc/yanhuuo/Ling.Mapper)

---

<div align="center">

### ?? 让对象映射变得简单高效！

**Ling.Mapper** - 轻量、高效、易用的对象映射库

Made with ?? by [yanhuuo](https://github.com/yanhuuo)

---

[![GitHub stars](https://img.shields.io/github/stars/yanhuuo/Ling.Mapper?style=social)](https://github.com/yanhuuo/Ling.Mapper)
[![GitHub forks](https://img.shields.io/github/forks/yanhuuo/Ling.Mapper?style=social)](https://github.com/yanhuuo/Ling.Mapper)
[![GitHub watchers](https://img.shields.io/github/watchers/yanhuuo/Ling.Mapper?style=social)](https://github.com/yanhuuo/Ling.Mapper)

</div>
