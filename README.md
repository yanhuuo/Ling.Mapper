# Ling.Mapper

[![NuGet](https://img.shields.io/nuget/v/Ling.Mapper.svg)](https://www.nuget.org/packages/Ling.Mapper/)
[![License](https://img.shields.io/badge/license-MIT-blue.svg)](LICENSE)
[![.NET](https://img.shields.io/badge/.NET-6.0%2b%20-8.0%20-9.0%20-10.0-blue)](https://dotnet.microsoft.com/)

🚀 **轻量级、高性能的 .NET 对象映射库**

Ling.Mapper 是一个基于 Expression Tree 的对象映射库，提供简洁的 API 和高效的性能。支持复杂对象映射、集合转换、循环引用检测，以及灵活的配置选项。

---

## ✨ 核心特性

### 🎯 基础功能
- ✅ **简洁的 Adapt API** - 一行代码完成对象映射：`source.Adapt<Target>()`
- ✅ **高性能编译** - 基于 Expression Tree 编译，接近手写代码性能
- ✅ **自动属性匹配** - 智能识别同名属性（支持忽略大小写/下划线）
- ✅ **集合映射** - 支持 List、Array、IEnumerable 等所有集合类型
- ✅ **嵌套对象映射** - 自动处理对象图中的嵌套对象和集合

### 🔧 高级功能
- ✅ **Profile 配置** - 支持 ForMember、Ignore、Rename、ReverseMap
- ✅ **AdaptOptions** - 运行时动态控制映射行为（IgnoreCase、IgnoreUnderscore、IgnoreNullValues）
- ✅ **运行时忽略字段** - 支持在 Adapt 时动态指定要忽略的字段名
- ✅ **映射回调** - 单次循环 1 对 1 回调，性能优化的映射后处理
- ✅ **循环引用保护** - 运行时自动检测和打破循环引用
- ✅ **类型转换** - 内置枚举、可空类型等常见类型转换
- ✅ **自定义转换器** - 通过 TypeConverterRegistry 注册自定义转换逻辑

### ⚡ 性能表现

| 场景 | 吞吐量 | 说明 |
|------|--------|------|
| 简单对象映射 | **975K ops/sec** | 属性较少的对象 |
| 复杂对象映射 | **148K ops/sec** | 嵌套对象、集合 |
| 集合映射 | **8.5M elements/sec** | 集合元素处理速度 |
| 枚举转换 | **2M ops/sec** | 枚举类型互转 |
| 可空类型转换 | **1.5M ops/sec** | 可空类型处理 |

*测试环境: .NET 10.0, 8核 CPU, 支持 .NET 6.0/8.0/9.0/10.0*

---

## 📦 快速安装

### NuGet CLI
```bash
dotnet add package Ling.Mapper
```

### Package Manager
```bash
Install-Package Ling.Mapper
```

### PackageReference
```xml
<PackageReference Include="Ling.Mapper" Version="1.1.2" />
```

---

## 🚀 五分钟快速开始

### 1. 最简单的映射

```csharp
using Ling.Mapper.Extensions;

public class UserDto
{
    public int Id { get; set; }
    public string Name { get; set; }
    public string Email { get; set; }
}

public class UserEntity
{
    public int Id { get; set; }
    public string Name { get; set; }
    public string Email { get; set; }
}

// 直接使用 Adapt 扩展方法
var dto = new UserDto { Id = 1, Name = "张三", Email = "zhangsan@example.com" };
var entity = dto.Adapt<UserEntity>();

Console.WriteLine($"{entity.Name} - {entity.Email}");
// 输出: 张三 - zhangsan@example.com
```

### 2. 集合映射

```csharp
// List 自动映射
var dtoList = new List<UserDto>
{
    new UserDto { Id = 1, Name = "张三" },
    new UserDto { Id = 2, Name = "李四" }
};

var entityList = dtoList.Adapt<List<UserEntity>>();
Console.WriteLine($"映射了 {entityList.Count} 条记录");
// 输出: 映射了 2 条记录

// Array 也支持
UserDto[] dtoArray = dtoList.ToArray();
var entityArray = dtoArray.Adapt<UserEntity[]>();
```

### 3. 嵌套对象与集合映射

```csharp
public class OrderDto
{
    public int Id { get; set; }
    public UserDto User { get; set; }
    public List<ProductDto> Products { get; set; }
}

public class OrderEntity
{
    public int Id { get; set; }
    public UserEntity User { get; set; }
    public List<ProductEntity> Products { get; set; }
}

// 自动处理嵌套对象和集合
var order = new OrderDto
{
    Id = 101,
    User = new UserDto { Id = 1, Name = "张三" },
    Products = new List<ProductDto>
    {
        new ProductDto { Id = 1, Name = "笔记本" },
        new ProductDto { Id = 2, Name = "鼠标" }
    }
};

var orderEntity = order.Adapt<OrderEntity>();
// 整个对象图都被映射了
```

**支持的集合类型**：
- `List<T>`、`T[]` 数组
- `IEnumerable<T>`、`IList<T>` 接口
- `ICollection<T>`、`IReadOnlyCollection<T>`
- 简单类型集合：`List<string>`、`List<int>`
- 可空类型集合：`List<int?>`、`List<DateTime?>`

### 4. 配置全局 Mapper（可选）

```csharp
using Ling.Mapper;

// 应用启动时配置（例如 Program.cs）
var config = new MapperConfiguration();
config.AddProfile(new UserProfile());
config.AddProfile(new OrderProfile());

var mapper = config.CreateMapper();
MapperProvider.SetCurrent(mapper);

// 现在可以在任何地方使用 Adapt
var entity = dto.Adapt<UserEntity>();
```

---

## 🔧 高级用法

### 1. 使用 Profile 配置映射

```csharp
public class UserProfile : MapperProfile
{
    public UserProfile()
    {
        // 创建映射配置
        CreateMap<UserDto, UserEntity>()
            // 自定义属性映射
            .ForMember(d => d.FullName, s => s.FirstName + " " + s.LastName)
            
            // 重命名属性
            .Rename(d => d.UserId, "Uid")
            
            // 忽略属性
            .Ignore(d => d.Password)
            
            // 生成反向映射
            .ReverseMap();
    }
}
```

### 2. 运行时映射选项

```csharp
// 处理命名差异：下划线 -> 驼峰
public class ApiDto
{
    public string user_name { get; set; }
    public int user_id { get; set; }
}

public class UserEntity
{
    public string UserName { get; set; }
    public int UserId { get; set; }
}

// 使用 FlexibleOption 自动处理
var entity = apiDto.Adapt<UserEntity>(AdaptOptions.FlexibleOption);
```

可用的选项：

```csharp
AdaptOptions.Strict              // 严格匹配（默认）
AdaptOptions.IgnoreCase          // 忽略大小写
AdaptOptions.IgnoreUnderscore    // 忽略下划线
AdaptOptions.IgnoreNullValues    // null 值不覆盖
AdaptOptions.Default             // 忽略大小写 + 下划线
AdaptOptions.FlexibleOption      // 别名，同 Default

// 组合使用
var result = source.Adapt<Target>(
    AdaptOptions.IgnoreCase | AdaptOptions.IgnoreNullValues
);
```

### 3. 运行时忽略字段映射

```csharp
// 在 Adapt 时动态指定要忽略的字段
public class UserSource
{
    public string Name { get; set; }
    public string Password { get; set; }
    public string CreditCard { get; set; }
    public int Age { get; set; }
}

public class UserTarget
{
    public string Name { get; set; }
    public string Password { get; set; }
    public string CreditCard { get; set; }
    public int Age { get; set; }
}

var source = new UserSource
{
    Name = "张三",
    Password = "secret123",
    CreditCard = "1111-2222-3333-4444",
    Age = 30
};

// 忽略 Password 和 CreditCard 字段
var target = source.Adapt<UserTarget>("Password", "CreditCard");

Console.WriteLine($"Name: {target.Name}");         // 张三
Console.WriteLine($"Password: {target.Password}");  // null
Console.WriteLine($"CreditCard: {target.CreditCard}"); // null
Console.WriteLine($"Age: {target.Age}");            // 30
```

**特性说明**：
- 支持忽略不存在的字段（安全处理，不会抛异常）
- 可以同时忽略多个字段
- 灵活的运行时控制，无需预先配置 Profile

### 4. 映射后回调（性能优化版）

```csharp
// 强类型回调（编译时类型检查）
var entity = dto.Adapt<UserEntity, UserDto>((dest, src) =>
{
    dest.FullName = $"{src.FirstName} {src.LastName}";
    dest.CreatedAt = DateTime.Now;
});

// 集合项级回调（每个元素映射后执行）
var entityList = dtoList.Adapt<List<UserEntity>, UserDto>((dest, src) =>
{
    dest.CreatedAt = DateTime.Now;
    dest.UpdatedBy = "system";
});

// 集合整体回调（整个集合映射完成后执行）
var entityList = dtoList.Adapt<List<UserEntity>>((destList, srcList) =>
{
    Console.WriteLine($"映射了 {destList?.Count} 条记录");
});
```

**性能优化**：
- 回调逻辑已深入到 Mapper 层
- List 映射采用单次循环 1 对 1 关系
- 避免了重复遍历，性能更优

### 5. 嵌套属性映射

```csharp
public class OrderSource
{
    public int Id { get; set; }
    public UserInfo User { get; set; }
}

public class UserInfo
{
    public string Name { get; set; }
    public int Age { get; set; }
}

public class OrderDto
{
    public int Id { get; set; }
    public string UserName { get; set; }
    public int UserAge { get; set; }
}

// 使用 Rename 配置嵌套属性映射
public class OrderProfile : MapperProfile
{
    public OrderProfile()
    {
        CreateMap<OrderSource, OrderDto>()
            .Rename(d => d.UserName, "User.Name")
            .Rename(d => d.UserAge, "User.Age");
    }
}

// 使用示例
var source = new OrderSource
{
    Id = 1,
    User = new UserInfo { Name = "张三", Age = 30 }
};

var dest = source.Adapt<OrderDto>();
Console.WriteLine($"{dest.UserName} - {dest.UserAge}");
// 输出: 张三 - 30
```

**支持深层嵌套**：
- 支持任意深度的属性路径（如 `Company.Address.City`）
- 自动处理空引用（null-safe）
- 支持 ForMember 自定义深层嵌套表达式

### 6. 内置类型转换

```csharp
// 枚举与字符串互转
public enum UserStatus
{
    Active = 1,
    Inactive = 2
}

var status = UserStatus.Active;
var statusStr = status.Adapt<string>();      // "Active"
var statusBack = statusStr.Adapt<UserStatus>(); // UserStatus.Active

// 可空类型转换
int? nullableInt = 42;
var regularInt = nullableInt.Adapt<int>();   // 42

var regularInt2 = 100;
var nullableInt2 = regularInt2.Adapt<int?>(); // 100

// 数字类型转换
var doubleValue = 3.14;
var intValue = doubleValue.Adapt<int>();     // 3

// long <-> DateTime 转换（时间戳）
var dateTime = DateTime.Now;
var timestamp = dateTime.Adapt<long>();      // 转为时间戳
var backToDateTime = timestamp.Adapt<DateTime>(); // 转回 DateTime
```

### 7. 自定义类型转换

```csharp
using Ling.Mapper.TypeConverter;

// 注册单向转换
TypeConverterRegistry.Register<string, DateTime>(
    str => DateTime.Parse(str)
);

// 注册双向转换
TypeConverterRegistry.Register<int, string>(i => i.ToString());
TypeConverterRegistry.Register<string, int>(s => int.Parse(s));

// 注册 JSON 转换
TypeConverterRegistry.RegisterJson<ExtraInfoModel>();
```

### 8. 循环引用处理

```csharp
public class Node
{
    public string Name { get; set; }
    public Node? Parent { get; set; }
    public List<Node>? Children { get; set; }
}

// 创建循环引用
var root = new Node { Name = "Root" };
var child = new Node { Name = "Child", Parent = root };
root.Children = new List<Node> { child };

// Mapper 自动检测并打破循环，不会 StackOverflow
var target = root.Adapt<Node>();
```

---

## 📚 关键类和接口

### IMapper 接口

```csharp
public interface IMapper
{
    // 泛型映射（使用默认选项）
    TDestination? Map<TDestination>(object? source);
    
    // 泛型映射（使用自定义选项）
    TDestination? Map<TDestination>(object source, AdaptOptions options);

    // 非泛型映射
    object? Map(object? source, Type sourceType, Type destType);
    object? Map(object? source, Type sourceType, Type destType, AdaptOptions options);
}
```

### MapperConfiguration

```csharp
var config = new MapperConfiguration();

// 添加 Profile
config.AddProfile(new UserProfile());
config.AddProfiles(new UserProfile(), new OrderProfile());

// 设置默认选项
config.DefaultAdaptOptions = AdaptOptions.FlexibleOption;

// 启用严格模式（未匹配属性会抛异常）
config.StrictMode = true;

// 全局约定配置
config.ConfigureConventions(opt =>
{
    opt.IgnoreCase = true;
    opt.IgnoreUnderscore = true;
});

// 创建 Mapper
var mapper = config.CreateMapper();
```

### MapperProfile

```csharp
public class OrderProfile : MapperProfile
{
    public OrderProfile()
    {
        CreateMap<OrderDto, OrderEntity>()
            // 计算属性
            .ForMember(d => d.Total, s => s.Items.Sum(i => i.Price * i.Qty))
            
            // 条件映射
            .ForMember(d => d.Status, s => s.IsPaid ? "已支付" : "未支付")
            
            // 嵌套属性
            .ForMember(d => d.CustomerName, s => s.Customer.Name)
            
            // 忽略属性
            .Ignore(d => d.InternalId)
            
            // 重命名
            .Rename(d => d.OrderNo, "Id");
    }
}
```

### AdaptOptions

```csharp
[Flags]
public enum AdaptOptions
{
    Strict = 0,                                    // 严格匹配
    IgnoreCase = 1 << 0,                          // 忽略大小写
    IgnoreUnderscore = 1 << 1,                    // 忽略下划线
    IgnoreNullValues = 1 << 2,                    // 忽略 null 值
    Default = IgnoreCase | IgnoreUnderscore,      // 默认选项
    FlexibleOption = IgnoreCase | IgnoreUnderscore // 灵活选项（别名）
}
```

### MapperProvider

```csharp
// 自动初始化（首次访问时）
var mapper = MapperProvider.Current;

// 手动设置全局 Mapper
MapperProvider.SetCurrent(mapper);

// 清除全局 Mapper
MapperProvider.Clear();
```

---

## 💡 最佳实践

### 1. 应用启动时统一配置

```csharp
// Program.cs
public static void Main(string[] args)
{
    ConfigureMapper();
    // ... 其他初始化
}

private static void ConfigureMapper()
{
    var config = new MapperConfiguration();
    
    // 批量注册 Profile
    config.AddProfiles(
        new UserProfile(),
        new OrderProfile(),
        new ProductProfile()
    );
    
    // 设置全局选项
    config.DefaultAdaptOptions = AdaptOptions.FlexibleOption;
    
    // 创建并设置
    MapperProvider.SetCurrent(config.CreateMapper());
}
```

### 2. Profile 按业务模块组织

```
Profiles/
├── UserProfile.cs
├── OrderProfile.cs
├── ProductProfile.cs
└── PaymentProfile.cs
```

### 3. 简单场景直接使用 Adapt

```csharp
// ✅ 推荐：属性完全一致，直接映射
var entity = dto.Adapt<UserEntity>();

// ❌ 不推荐：为简单映射添加 Profile 增加复杂度
CreateMap<UserDto, UserEntity>();  // 没必要
```

### 4. 复杂映射使用 Profile

```csharp
// ✅ 推荐：需要自定义时使用 Profile
CreateMap<OrderDto, OrderEntity>()
    .ForMember(d => d.Total, s => s.Items.Sum(i => i.Price * i.Qty))
    .Ignore(d => d.InternalCode);
```

### 5. 使用后处理而不是分散逻辑

```csharp
// ✅ 推荐：集中处理映射后逻辑
var entity = dto.Adapt<UserEntity>((dest, src) =>
{
    dest.CreatedAt = DateTime.Now;
    dest.CreatedBy = CurrentUser.Id;
});

// ❌ 不推荐：映射后再处理
var entity = dto.Adapt<UserEntity>();
entity.CreatedAt = DateTime.Now;
entity.CreatedBy = CurrentUser.Id;
```

---

## 🧪 测试

项目包含全面的测试套件：

```bash
cd tests/Ling.Mapper.Tests
dotnet run
```

测试涵盖：
- 基础对象映射
- 集合映射（List、Array、IEnumerable）
- 嵌套对象映射
- 深层嵌套属性（A.B.C.D）
- 循环引用检测
- 类型转换（枚举、可空类型、数字类型）
- 运行时忽略字段
- 映射回调（单个元素、集合、整体回调）
- Profile 配置（ForMember、Ignore、Rename、ReverseMap）
- AdaptOptions 灵活选项（IgnoreCase、IgnoreUnderscore、IgnoreNullValues）
- 性能基准测试
- 压力测试

---

## 📖 完整文档

详见 [docs/使用文档.md](./docs/使用文档.md) 了解：
- 详细的功能说明
- 完整的 API 参考
- 高级场景和最佳实践
- 常见问题解答
- 性能优化建议

---


## 🤝 贡献

欢迎提交 Issue 和 Pull Request！

1. Fork 本仓库
2. 创建特性分支：`git checkout -b feature/amazing-feature`
3. 提交更改：`git commit -m 'Add amazing feature'`
4. 推送分支：`git push origin feature/amazing-feature`
5. 发起 Pull Request

---

## 📄 许可证

本项目采用 MIT 许可证 - 详见 [LICENSE](LICENSE) 文件

---

## 📝 更新日志

### v1.1.2 (最新)
- ✨ 新增运行时忽略字段功能：`source.Adapt<Target>("Field1", "Field2")`
- 🚀 优化映射回调性能：回调深入到 Mapper 层，避免重复遍历
- 🔧 修复 List 映射回调：采用单次循环 1 对 1 关系
- 📚 完善测试覆盖：新增忽略字段、嵌套属性等测试用例

### v1.1.1
- ✨ 支持深层嵌套属性映射（A.B.C.D）
- ✨ 新增 IgnoreNullValues 选项
- 🔧 优化表达式树编译性能
- 🐛 修复循环引用 StackOverflow 问题

---

## 📧 链接

- **GitHub**: [Ling.Mapper](https://github.com/yanhuuo/Ling.Mapper)
- **NuGet**: [Ling.Mapper](https://www.nuget.org/packages/Ling.Mapper/)
- **Issues**: [报告问题](https://github.com/yanhuuo/Ling.Mapper/issues)

---

<div align="center">

**如果这个项目对你有帮助，请给个 ⭐ Star！**

</div>
