# Ling.Mapper 功能概览

<div align="center">

**一个简单、快速、类型安全的 .NET 对象映射库**

</div>

---

## ?? 核心特性

### 1. 零配置映射
```csharp
// 同名属性自动映射
var dto = entity.Adapt<UserDto>();
```

### 2. 类型安全
```csharp
// 编译时类型检查，避免运行时错误
var user = userDto.Adapt<User, UserDto>();
```

### 3. 高性能
- ? 使用表达式树编译，接近手写代码的性能
- ? 缓存已编译的映射器
- ? 避免反射开销

### 4. 丰富的配置选项
```csharp
// Profile 配置
CreateMap<User, UserDto>()
    .ForMember(d => d.FullName, s => s.FirstName + " " + s.LastName)
    .Ignore(d => d.Password)
    .Rename(d => d.Id, "UserId");

// 运行时选项
var dto = entity.Adapt<UserDto>(options => 
{
    options.IgnoreCase = true;
    options.IgnoreNullValues = true;
});
```

---

## ?? 支持的类型转换

### 基础类型映射
| 源类型 | 目标类型 | 支持 |
|--------|----------|------|
| 相同类型 | 相同类型 | ? |
| 基础类型 | 基础类型 | ? |
| string | string | ? |
| DateTime | DateTime | ? |
| Guid | Guid | ? |

### 可空类型支持
| 源类型 | 目标类型 | 支持 | 说明 |
|--------|----------|------|------|
| `T` | `T?` | ? | 包装为可空 |
| `T?` | `T` | ? | null → default(T) |
| `T?` | `U?` | ? | 类型转换 + 保持 null |
| `T?` | `U` | ? | 类型转换 + null → default(U) |

### 枚举类型支持
| 源类型 | 目标类型 | 支持 | 说明 |
|--------|----------|------|------|
| `enum` | `int` | ? | 获取整数值 |
| `int` | `enum` | ? | 转换为枚举 |
| `enum` | `string` | ? | 转换为名称 |
| `string` | `enum` | ? | 解析枚举（不区分大小写） |
| `enum?` | `int` | ? | null → 0 |
| `enum?` | `int?` | ? | 保持 null |
| `EnumA` | `EnumB` | ? | 通过整数值转换 |

### 集合类型支持
| 源类型 | 目标类型 | 支持 |
|--------|----------|------|
| `List<T>` | `List<U>` | ? |
| `T[]` | `U[]` | ? |
| `IEnumerable<T>` | `List<U>` | ? |
| `List<T>` | `U[]` | ? |

### 复杂类型支持
- ? 嵌套对象自动递归映射
- ? 集合内元素自动映射
- ? 多层嵌套结构

---

## ?? 配置方式

### 1. Profile 配置（推荐用于复杂映射）
```csharp
public class UserProfile : MapperProfile
{
    public UserProfile()
    {
        CreateMap<User, UserDto>()
            .ForMember(d => d.FullName, s => s.FirstName + " " + s.LastName)
            .Ignore(d => d.Password)
            .Rename(d => d.Id, "UserId");
    }
}

// 注册
var config = new MapperConfiguration();
config.AddProfile(new UserProfile());
var mapper = config.CreateMapper();
```

### 2. 运行时 Adapt 选项（推荐用于简单场景）
```csharp
var dto = entity.Adapt<UserDto>(options => 
{
    options.IgnoreCase = true;           // 忽略大小写
    options.IgnoreUnderscores = true;    // 忽略下划线
    options.IgnoreNullValues = true;     // 忽略 null 值
    options.IgnoreProperties("Password", "Secret"); // 忽略属性
});
```

### 3. 手动注册（推荐用于性能关键场景）
```csharp
MapperRegistry.Register<UserDto, User>(dto => new User
{
    Id = dto.Id,
    Name = dto.Name,
    Email = dto.Email
});
```

---

## ?? Adapt 选项详解

### IgnoreCase - 忽略大小写
```csharp
// Source: UserName → Target: username
var target = source.Adapt<Target>(opt => opt.IgnoreCase = true);
```

### IgnoreUnderscores - 忽略下划线
```csharp
// Source: user_name → Target: UserName
var target = source.Adapt<Target>(opt => opt.IgnoreUnderscores = true);
```

### IgnoreNullValues - 忽略 null 值
```csharp
// 只映射非 null 的属性（部分更新）
var target = source.Adapt<Target>(opt => opt.IgnoreNullValues = true);
```

### IgnoreProperties - 忽略指定属性
```csharp
// 不映射 Password 和 Secret 属性
var target = source.Adapt<Target>(opt => 
    opt.IgnoreProperties("Password", "Secret"));
```

### 组合使用
```csharp
var target = source.Adapt<Target>(options => 
{
    options.IgnoreCase = true;
    options.IgnoreUnderscores = true;
    options.IgnoreNullValues = true;
    options.IgnoreProperties("Password");
});
```

---

## ?? 使用场景

### 1. API 集成
```csharp
// 第三方 API 返回的 JSON 数据
public class ApiResponse
{
    public int status_code { get; set; }
    public string user_name { get; set; }
}

// 领域模型
public class User
{
    public int StatusCode { get; set; }
    public string UserName { get; set; }
}

// 自动映射（忽略下划线）
var user = apiResponse.Adapt<User>(opt => opt.IgnoreUnderscores = true);
```

### 2. 数据库实体映射
```csharp
// Entity → DTO
var userDto = userEntity.Adapt<UserDto>();

// DTO → Entity
var userEntity = userDto.Adapt<UserEntity>();
```

### 3. 部分更新
```csharp
// 只更新非 null 的字段
var updateDto = new UpdateUserDto 
{ 
    Name = "New Name", 
    Email = null // 不更新 Email
};

var user = existingUser.Adapt<User>(opt => opt.IgnoreNullValues = true);
```

### 4. 集合映射
```csharp
// List 映射
var userDtos = users.Adapt<List<UserDto>>();

// Array 映射
var userArray = users.ToArray().Adapt<UserDto[]>();

// 带回调的列表映射
var dtos = users.AdaptToList<UserDto, User>((list, source) =>
{
    for (int i = 0; i < list.Count; i++)
    {
        list[i].RowNumber = i + 1;
    }
});
```

---

## ?? 性能优化

### 1. 使用手动注册
```csharp
// 最快 - 手动注册
MapperRegistry.Register<UserDto, User>(dto => new User
{
    Id = dto.Id,
    Name = dto.Name
});

var user = dto.Adapt<User>();
```

### 2. 使用 Profile 预编译
```csharp
// 快 - Profile 在启动时编译
var config = new MapperConfiguration();
config.AddProfile(new UserProfile());
var mapper = config.CreateMapper();
```

### 3. 避免频繁的运行时配置
```csharp
// ? 慢 - 每次都重新编译
for (int i = 0; i < 1000; i++)
{
    var dto = entity.Adapt<UserDto>(opt => opt.IgnoreCase = true);
}

// ? 快 - 使用预定义的 Profile
for (int i = 0; i < 1000; i++)
{
    var dto = entity.Adapt<UserDto>();
}
```

---

## ?? DI 集成

### ASP.NET Core
```csharp
// Startup.cs / Program.cs
services.AddFluentMapper(config =>
{
    config.AddProfile(new UserProfile());
    config.AddProfile(new OrderProfile());
});

// Controller
public class UserController : ControllerBase
{
    private readonly IMapper _mapper;
    
    public UserController(IMapper mapper)
    {
        _mapper = mapper;
    }
    
    public IActionResult Get()
    {
        var user = _userService.GetUser();
        var dto = _mapper.Map<UserDto>(user);
        return Ok(dto);
    }
}
```

---

## ?? 与其他库的对比

| 特性 | Ling.Mapper | AutoMapper | Mapster |
|------|-------------|------------|---------|
| 零配置映射 | ? | ? | ? |
| 类型安全 | ? | ?? | ? |
| 运行时配置 | ? | ? | ?? |
| 可空类型支持 | ? | ?? | ? |
| 枚举转换 | ? | ?? | ? |
| 性能 | ??? | ?? | ??? |
| 学习曲线 | 简单 | 中等 | 简单 |

---

## ?? 更多文档

- [主页 README.md](../README.md) - 安装和快速开始
- [Adapt 方法使用](Adapt-Usage.md) - Adapt 方法详细指南
- [可空类型支持](../NULLABLE_TYPES_UPDATE.md) - 可空类型转换规则
- [枚举类型支持](EnumConversion_Support.md) - 枚举转换完整指南
- [异常处理](Exception-Handling-Quick-Guide.md) - 异常处理策略

---

## ?? 快速开始

### 1. 安装
```bash
dotnet add package Ling.Mapper
```

### 2. 基本使用
```csharp
// 零配置映射
var dto = entity.Adapt<UserDto>();

// 带选项映射
var dto = entity.Adapt<UserDto>(opt => 
{
    opt.IgnoreCase = true;
    opt.IgnoreNullValues = true;
});

// 列表映射
var dtos = entities.Adapt<List<UserDto>>();
```

### 3. Profile 配置
```csharp
public class UserProfile : MapperProfile
{
    public UserProfile()
    {
        CreateMap<User, UserDto>()
            .ForMember(d => d.FullName, s => s.FirstName + " " + s.LastName);
    }
}

var config = new MapperConfiguration();
config.AddProfile(new UserProfile());
var mapper = config.CreateMapper();
```

---

## ?? 支持与反馈

- ?? [NuGet 包](https://www.nuget.org/packages/Ling.Mapper/)
- ?? [GitHub 仓库](https://github.com/yanhuuo/Ling.Mapper)
- ?? [问题反馈](https://github.com/yanhuuo/Ling.Mapper/issues)

---

<div align="center">

**让对象映射变得简单高效！** ?

Made with ?? by [yanhuuo](https://github.com/yanhuuo)

</div>
