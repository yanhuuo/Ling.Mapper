# Ling.Mapper

[![NuGet](https://img.shields.io/nuget/v/Ling.Mapper.svg)](https://www.nuget.org/packages/Ling.Mapper/)
[![License](https://img.shields.io/github/license/yanhuuo/Ling.Mapper)](LICENSE)

**简单、高效的 .NET 对象映射库**

轻量级 Fluent 风格对象映射器，支持多层级映射、链式 API、AOT、DI 注入、运行时映射规则配置。

---

## ? 特性

- ?? **简单易用**：链式 API，直观的映射配置
- ? **高性能**：表达式树编译，支持 Source Generator 优化
- ?? **类型安全**：完整的泛型支持和编译时检查
- ?? **灵活配置**：Profile 配置、运行时规则、自定义转换器
- ?? **可空类型支持**：完整支持 `int?`、`string?` 等可空类型转换
- ??? **AOT 友好**：支持原生 AOT 编译场景
- ?? **DI 集成**：内置依赖注入扩展

---

## ?? 安装

```bash
dotnet add package Ling.Mapper
```

## ?? 快速开始

### 1. 基本映射

```csharp
// 定义映射规则
public class UserProfile : MapperProfile
{
    public UserProfile()
    {
        CreateMap<UserDto, User>()
            .ForMember(dest => dest.FullName, src => src.FirstName + " " + src.LastName)
            .Ignore(dest => dest.Password);
    }
}

// 配置并使用
var config = new MapperConfiguration();
config.AddProfile(new UserProfile());
var mapper = config.CreateMapper();

var user = mapper.Map<User>(userDto);
```

### 2. 扩展方法（推荐）

```csharp
// 设置全局 Mapper
MapperProvider.SetCurrent(mapper);

// 简洁的映射语法
var user = userDto.Adapt<User>();

// 带回调的映射
var user = userDto.Adapt<User>((dest, src) => 
{
    dest.UpdatedAt = DateTime.Now;
});
```

### 3. DI 集成

```csharp
// Startup.cs 或 Program.cs
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

## ?? 核心功能

### Profile 配置

```csharp
public class OrderProfile : MapperProfile
{
    public OrderProfile()
    {
        CreateMap<OrderDto, Order>()
            // 自定义属性映射
            .ForMember(d => d.TotalAmount, s => s.Price * s.Quantity)
            
            // 重命名映射
            .Rename(d => d.CustomerId, "UserId")
            
            // 忽略属性
            .Ignore(d => d.InternalCode)
            
            // 反向映射
            .ReverseMap();
    }
}
```

### 运行时映射规则（新功能）

```csharp
// 忽略大小写匹配
var user = apiResponse.Adapt<User>(AdaptOptions.IgnoreCaseOption);

// 忽略下划线匹配（snake_case <-> PascalCase）
var user = dbRow.Adapt<User>(AdaptOptions.IgnoreUnderscoreOption);

// 灵活匹配（大小写 + 下划线）
var user = data.Adapt<User>(AdaptOptions.FlexibleOption);

// 组合多个规则
var user = source.Adapt<User>(new AdaptOptions
{
    IgnoreCase = true,
    IgnoreUnderscore = true,
    IgnoreNullValues = true,
    IgnoreProperties = new[] { "Password", "CreditCard" }
}, (dest, src) => 
{
    dest.UpdatedAt = DateTime.Now;
});
```

### 集合映射

```csharp
// List 映射
var users = userDtos.AdaptList<User, UserDto>();

// 带索引的处理
var users = userDtos.AdaptList<User, UserDto>((dest, src, index) => 
{
    dest.RowNumber = index + 1;
});
```

### 类型转换器

```csharp
// 注册 JSON 转换器
TypeConverterRegistry.RegisterJson<ExtraInfo>();

// 自定义转换器
TypeConverterRegistry.Register(
    typeof(string), 
    typeof(DateTime), 
    new Func<string, DateTime>(s => DateTime.Parse(s))
);
```

### 高性能手动注册

```csharp
// 手动注册高性能委托（优先使用）
MapperRegistry.Register<UserDto, User>(dto => new User
{
    Id = dto.Id,
    Name = dto.FirstName + " " + dto.LastName
});
```

---

## ?? Adapt 方法完整指南

### 基础用法

```csharp
// 1. 使用全局 Mapper
var user = userDto.Adapt<User>();

// 2. 指定 Mapper 实例
var user = userDto.Adapt<User>(mapper);

// 3. 显式指定源类型
var user = userDto.Adapt<User, UserDto>();

// 4. 带回调的映射
var user = userDto.Adapt<User>((dest, src) => 
{
    dest.CreatedAt = DateTime.Now;
});
```

### 运行时规则

```csharp
// 1. 忽略大小写（API 响应映射）
var user = apiResponse.Adapt<User>(AdaptOptions.IgnoreCaseOption);
// username -> UserName

// 2. 忽略下划线（数据库映射）
var user = dbRow.Adapt<User>(AdaptOptions.IgnoreUnderscoreOption);
// user_name -> UserName

// 3. 灵活匹配（第三方数据）
var user = data.Adapt<User>(AdaptOptions.FlexibleOption);
// User_Name -> UserName

// 4. 忽略敏感字段
var user = source.Adapt<User>(new AdaptOptions
{
    IgnoreProperties = new[] { "Password", "CreditCard" }
});

// 5. 部分更新（只映射非 null 值）
var updated = dto.Adapt<User>(new AdaptOptions
{
    IgnoreNullValues = true
});
```

### 实际场景示例

```csharp
// 场景 1：API 响应处理
[HttpGet]
public async Task<IActionResult> GetUsers()
{
    var users = await _repository.GetAllAsync();
    return Ok(users.AdaptList<UserDto>());
}

// 场景 2：分页结果映射
var page = await query
    .ToPageResultAsync(pageIndex, pageSize)
    .Adapt<PageResult<UserDto>>((dest, src) => 
    {
        // 对每个项进行处理
        if (dest.Items != null)
        {
            foreach (var item in dest.Items)
            {
                item.Avatar = GetAvatarUrl(item.Id);
            }
        }
    });

// 场景 3：第三方 API 集成
var externalData = await _httpClient.GetAsync<ExternalApiResponse>();
var user = externalData.Adapt<User>(
    AdaptOptions.IgnoreCaseOption,
    (dest, src) => dest.Source = "ExternalAPI"
);

// 场景 4：数据库映射
var dbResult = await _connection.QueryAsync<DatabaseRow>(sql);
var users = dbResult.Select(row => 
    row.Adapt<User>(AdaptOptions.IgnoreUnderscoreOption)
).ToList();
```

---

## ?? 高级特性

### 全局约定配置

```csharp
config.ConfigureConventions(opt => 
{
    opt.CaseInsensitiveNameMatch = true;
    opt.IgnoreSpecialCharacters = true;
});
```

### 严格模式

```csharp
config.StrictMode = true; // 未匹配属性时抛出异常
```

### 映射到已有实例

```csharp
var existingUser = _repository.GetById(id);
mapper.MapInto(existingUser, dto);
```

### 安全映射

```csharp
if (mapper.TryMap<User>(dto, out var user))
{
    // 映射成功
}

var user = mapper.MapOrDefault<User>(dto, defaultUser);
var user = mapper.MapOrThrow<User>(dto);
```

---

## ?? 性能

### 优化建议

1. **使用 Source Generator**：编译时生成映射代码
2. **手动注册委托**：通过 `MapperRegistry.Register` 注册高性能委托
3. **缓存 Mapper 实例**：Mapper 实例是线程安全的，建议单例使用
4. **避免频繁配置**：在应用启动时配置一次

### 执行顺序

1. MapperRegistry 注册的委托（最快）
2. Source Generator 生成的代码
3. 表达式树编译（运行时回退）

---

## ??? 项目结构

```
Ling.Mapper/
├── src/
│   └── Ling.Mapper/           # 核心库
│       ├── Configuration/     # 配置类
│       ├── Extensions/        # 扩展方法
│       ├── Models/            # 模型类
│       ├── Mapper/            # 映射器实现
│       ├── Provider/          # 全局提供者
│       ├── Registry/          # 注册中心
│       └── TypeConverter/     # 类型转换器
├── tests/
│   └── Ling.Mapper.Tests/    # 测试项目
└── docs/                      # 文档
    ├── QuickStart_AdaptOptions.md
    └── AdaptOptions_Usage.md
```

---

## ?? 更新日志

### v1.0.4 (最新)
- ? 新增运行时映射规则支持（AdaptOptions）
- ? 支持忽略大小写、下划线等灵活匹配
- ?? 修复所有 XML 注释警告
- ?? 修复空引用警告
- ?? 优化 DependencyInjection 包版本兼容性

### v1.0.3
- ? 新增 Adapt 扩展方法
- ? 新增 List 映射支持
- ?? 修复类型转换器问题

### v1.0.2
- ? 初始版本发布

---

## ?? 贡献

欢迎提交 Issue 和 Pull Request！

---

## ?? 许可证

[MIT License](LICENSE)

---

## ?? 相关链接

- [GitHub 仓库](https://github.com/yanhuuo/Ling.Mapper)
- [NuGet 包](https://www.nuget.org/packages/Ling.Mapper/)
- [详细文档](docs/QuickStart_AdaptOptions.md)

---

## ?? 为什么选择 Ling.Mapper？

| 特性 | Ling.Mapper | AutoMapper | Mapster |
|------|------------|------------|---------|
| 简单易用 | ? | ? | ? |
| 高性能 | ? | ?? | ? |
| AOT 支持 | ? | ? | ?? |
| 运行时规则 | ? | ? | ? |
| 轻量级 | ? | ? | ? |
| DI 集成 | ? | ? | ?? |

---

**让对象映射变得简单高效！** ??
