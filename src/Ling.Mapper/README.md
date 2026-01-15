# Ling.Mapper

[![NuGet](https://img.shields.io/nuget/v/Ling.Mapper.svg)](https://www.nuget.org/packages/Ling.Mapper/)
[![License](https://img.shields.io/badge/license-MIT-blue.svg)](LICENSE)
[![.NET](https://img.shields.io/badge/.NET-10.0-blue)](https://dotnet.microsoft.com/)

🚀 **高性能、零配置的 .NET 对象映射库**

Ling.Mapper 是一个专为 .NET 开发者设计的轻量级、高性能对象映射库。它提供了简洁的 API 和强大的功能，让对象之间的转换变得简单高效。

---

## ✨ 特性

### 🎯 核心功能
- ✅ **零配置映射** - 自动识别同名属性，开箱即用
- ✅ **高性能** - 基于 Expression Tree 编译，接近手写代码的性能
- ✅ **嵌套对象** - 自动处理复杂的嵌套对象映射
- ✅ **集合映射** - 支持 List、Array、IEnumerable 等集合类型
- ✅ **循环引用检测** - 自动处理对象间的循环引用
- ✅ **类型转换** - 智能处理枚举、可空类型等类型转换

### 🔥 高级功能
- ✅ **嵌套属性映射** - 支持 `A.B.C.D` 路径映射
- ✅ **FlexibleOption** - 自动处理驼峰、下划线等命名差异
- ✅ **自定义映射规则** - 通过 Profile 配置复杂映射逻辑
- ✅ **类型转换器** - 注册自定义类型转换器
- ✅ **JSON 转换** - 内置 JSON 属性转换支持
- ✅ **AdaptOptions** - 运行时动态配置映射规则

### ⚡ 性能指标

| 测试场景 | 吞吐量 | 耗时 (1M 次) |
|---------|--------|-------------|
| 简单对象映射 | **975K ops/sec** | 1024 ms |
| 复杂对象映射 | **148K ops/sec** | 674 ms (100K次) |
| 集合映射 | **8.5M elements/sec** | 116 ms |
| 枚举转换 | **2M ops/sec** | 484 ms |
| 可空类型转换 | **1.5M ops/sec** | 659 ms |

*测试环境: .NET 10.0, 8核 CPU*

---

## 📦 安装

### NuGet Package Manager
```bash
Install-Package Ling.Mapper
```

### .NET CLI
```bash
dotnet add package Ling.Mapper
```

### PackageReference
```xml
<PackageReference Include="Ling.Mapper" Version="2.1.4" />
```

---

## 🚀 快速开始

### 1. 基础映射

```csharp
using Ling.Mapper;

// 定义源对象和目标对象
public class UserDto
{
    public int Id { get; set; }
    public string Name { get; set; }
    public string Email { get; set; }
}

public class UserViewModel
{
    public int Id { get; set; }
    public string Name { get; set; }
    public string Email { get; set; }
}

// 使用 Adapt 扩展方法进行映射
var dto = new UserDto { Id = 1, Name = "张三", Email = "zhangsan@example.com" };
var viewModel = dto.Adapt<UserViewModel>();

Console.WriteLine($"{viewModel.Name} - {viewModel.Email}");
// 输出: 张三 - zhangsan@example.com
```

### 2. 集合映射

```csharp
// 自动识别集合类型
var dtoList = new List<UserDto>
{
    new UserDto { Id = 1, Name = "张三", Email = "zhangsan@example.com" },
    new UserDto { Id = 2, Name = "李四", Email = "lisi@example.com" }
};

// 自动映射为 List<UserViewModel>
var viewModels = dtoList.Adapt<List<UserViewModel>>();

Console.WriteLine($"映射了 {viewModels.Count} 个对象");
// 输出: 映射了 2 个对象
```

### 3. 嵌套对象映射

```csharp
public class OrderDto
{
    public int Id { get; set; }
    public CustomerDto Customer { get; set; }
    public List<OrderItemDto> Items { get; set; }
}

public class OrderViewModel
{
    public int Id { get; set; }
    public CustomerViewModel Customer { get; set; }
    public List<OrderItemViewModel> Items { get; set; }
}

var orderDto = new OrderDto
{
    Id = 1,
    Customer = new CustomerDto { Name = "张三", Address = "北京市" },
    Items = new List<OrderItemDto>
    {
        new OrderItemDto { ProductName = "商品A", Quantity = 2 },
        new OrderItemDto { ProductName = "商品B", Quantity = 1 }
    }
};

// 自动处理嵌套对象和集合
var orderViewModel = orderDto.Adapt<OrderViewModel>();
```

### 4. 嵌套属性路径映射 (A.B.C.D)

```csharp
public class SourceModel
{
    public User User { get; set; }
}

public class User
{
    public Profile Profile { get; set; }
}

public class Profile
{
    public string Name { get; set; }
}

public class TargetModel
{
    public string UserName { get; set; }
}

// 配置嵌套属性映射
var config = new MapperConfiguration();
config.CreateMap<SourceModel, TargetModel>(cfg =>
{
    cfg.ForMember(dest => dest.UserName, src => "User.Profile.Name");
});

var mapper = config.CreateMapper();
MapperProvider.SetCurrent(mapper);

var source = new SourceModel
{
    User = new User
    {
        Profile = new Profile { Name = "张三" }
    }
};

var target = source.Adapt<TargetModel>();
Console.WriteLine(target.UserName); // 输出: 张三
```

---

## 🔧 配置与自定义

### 1. 全局配置

```csharp
using Ling.Mapper;

// 创建配置
var config = new MapperConfiguration();

// 配置命名约定
config.ConfigureConventions(opt =>
{
    opt.CaseInsensitiveNameMatch = true;  // 忽略大小写
});

// 创建 Mapper 并设置为全局实例
var mapper = config.CreateMapper();
MapperProvider.SetCurrent(mapper);

// 现在可以直接使用 Adapt 扩展方法
var result = source.Adapt<TargetType>();
```

### 2. 使用 Profile 配置映射规则

```csharp
public class UserProfile : MapperProfile
{
    public UserProfile()
    {
        // 配置 UserDto -> UserViewModel 的映射
        CreateMap<UserDto, UserViewModel>(cfg =>
        {
            // 自定义属性映射
            cfg.ForMember(dest => dest.FullName, 
                src => $"{src.FirstName} {src.LastName}");
            
            // 忽略某些属性
            cfg.Ignore(dest => dest.CreatedAt);
            
            // 重命名映射
            cfg.ForMember(dest => dest.UserName, src => src.Name);
        });
    }
}

// 注册 Profile
var config = new MapperConfiguration();
config.AddProfile(new UserProfile());

var mapper = config.CreateMapper();
MapperProvider.SetCurrent(mapper);
```

### 3. AdaptOptions - 运行时配置

```csharp
using Ling.Mapper;

// 使用 FlexibleOption 处理命名差异
var source = new { user_name = "张三", user_age = 25 };
var target = source.Adapt<UserInfo>(AdaptOptions.FlexibleOption);

Console.WriteLine(target.UserName);  // 输出: 张三
Console.WriteLine(target.UserAge);   // 输出: 25

// 自定义 AdaptOptions
var options = new AdaptOptions
{
    IgnoreCase = true,           // 忽略大小写
    IgnoreUnderscore = true,     // 忽略下划线
    IgnoreNullValues = true,     // 忽略 null 值
    IgnoreProperties = new[] { "Password" }  // 忽略指定属性
};

var result = source.Adapt<TargetType>(options);
```

### 4. 自定义类型转换器

```csharp
using Ling.Mapper;

// 注册 JSON 转换器
TypeConverterRegistry.RegisterJson<ExtraInfoModel>();

// 注册自定义转换器
TypeConverterRegistry.Register<string, DateTime>(str => DateTime.Parse(str));

// 注册双向转换器
TypeConverterRegistry.Register<int, string>(i => i.ToString());
TypeConverterRegistry.Register<string, int>(s => int.Parse(s));
```

---

## 📚 API 参考

### Adapt 扩展方法

| 方法签名 | 说明 |
|---------|------|
| `TDest Adapt<TDest>(this object source)` | 基础映射方法 |
| `TDest Adapt<TDest>(this object source, Action<TDest, object> custom)` | 映射后自定义处理 |
| `TDest Adapt<TDest>(this object source, IMapper mapper)` | 使用指定 Mapper |
| `TDest Adapt<TDest>(this object source, AdaptOptions options)` | 使用运行时选项 |
| `List<TDest> Adapt<TDest>(this IEnumerable source)` | 集合映射 |

### MapperConfiguration

```csharp
var config = new MapperConfiguration();

// 配置命名约定
config.ConfigureConventions(opt =>
{
    opt.CaseInsensitiveNameMatch = true;
});

// 添加 Profile
config.AddProfile(new MyProfile());

// 设置默认 AdaptOptions
config.SetDefaultAdaptOptions(AdaptOptions.FlexibleOption);

// 创建 Mapper
var mapper = config.CreateMapper();
```

### MapperProfile

```csharp
public class MyProfile : MapperProfile
{
    public MyProfile()
    {
        CreateMap<Source, Dest>(cfg =>
        {
            // 自定义属性映射
            cfg.ForMember(dest => dest.FullName, src => $"{src.FirstName} {src.LastName}");
            
            // 忽略属性
            cfg.Ignore(dest => dest.InternalId);
            
            // 嵌套属性路径
            cfg.ForMember(dest => dest.CityName, src => "Address.City.Name");
        });
    }
}
```

---

## 📖 完整文档

详细文档请查看 [docs](./docs) 目录：

### 📘 核心文档
- **[功能概览与快速入门](./docs/功能概览与快速入门.md)** - 完整功能介绍（中文）

### 📗 功能文档
- [嵌套属性映射](./docs/Nested_Property_Mapping.md) - A.B.C.D 路径映射
- [集合自动识别](./docs/Adapt_Collection_Auto_Detection.md) - 集合类型自动检测
- [AdaptOptions 配置](./docs/AdaptOptions_Fix_Final.md) - 运行时配置选项
- [MapperProvider 自动初始化](./docs/MapperProvider_AutoInitialize.md) - 全局 Mapper 配置

### 📕 性能优化文档
- [性能优化 v2.1.4](./docs/Performance_Optimization_v2.1.4.md) - 最新性能优化详解
- [循环引用处理](./docs/v2.1.3_Runtime_Circular_Reference.md) - 运行时循环引用检测
- [StackOverflow 修复](./docs/StackOverflow_Fix.md) - 深度递归问题修复

### 📙 测试文档
- [测试套件概览](./docs/TestSuite_Overview.md) - 完整测试说明
- [循环测试功能](./docs/TestConsole_循环测试功能.md) - 测试控制台使用

---

## 🧪 测试套件

项目包含全面的测试套件，运行测试：

```bash
cd tests/Ling.Mapper.Tests
dotnet run
```

### 测试菜单
```
请选择测试类型：
  1 - 基础功能测试 (Basic Tests)
  2 - 高级功能测试 (Advanced Tests)
  3 - 性能基准测试 (Performance Tests)
  4 - 压力测试 (Stress Tests)
  5 - 自动初始化测试 (Auto Initialize Test) 🆕
  6 - 集合自动识别测试 (Collection Auto Detection) 🆕
  7 - AdaptOptions FlexibleOption 测试 🔥
  8 - 默认 FlexibleOption 测试 ⭐
  9 - 嵌套属性映射测试 (A.B.C.D) 🎯
  0 - 运行所有测试 (Run All Tests)
  q - 退出 (Exit)
```

### 性能测试结果示例
```
--- 性能基准测试 ---

测试配置：
  - CPU: 8 核
  - .NET: 10.0.2

1. 简单对象映射性能（1,000,000 次）
  ⏱ 总耗时: 1024 ms
  📊 平均每次: 0.001024 ms
  🚀 吞吐量: 975,713 ops/sec
  ⚠️ 性能警告: 1024 ms

2. 复杂对象映射性能（100,000 次）
  ⏱ 总耗时: 674 ms
  📊 平均每次: 0.006740 ms
  🚀 吞吐量: 148,267 ops/sec

3. 集合映射性能（10,000 次 x 100 元素）
  ⏱ 总耗时: 116 ms
  📊 总元素数: 1,000,000
  🚀 吞吐量: 8,586,794 elements/sec
  ✅ 性能测试通过 (< 1000ms)
```

---

## 🔄 版本历史

### v2.1.4 (最新) - 2025年1月
- ✅ **性能优化：简单对象映射提升 57%** (975K ops/sec)
- ✅ 优化 ThreadLocal 循环引用检测，跳过简单类型
- ✅ 缓存反射调用和类型检测结果
- ✅ 添加快速路径跳过不必要的检查
- ✅ 测试控制台支持循环测试

### v2.1.3 - 2024年12月
- ✅ 运行时循环引用检测
- ✅ 修复深度嵌套 StackOverflow 问题
- ✅ 优化编译期递归保护
- ✅ 增强错误提示信息

### v2.1.2 - 2024年12月
- ✅ AdaptOptions FlexibleOption 支持
- ✅ 集合自动识别功能
- ✅ MapperProvider 自动初始化
- ✅ 修复命名匹配问题

### v2.1.0 - 2024年11月
- ✅ 嵌套属性映射 (A.B.C.D)
- ✅ JSON 属性转换支持
- ✅ 增强的类型转换器
- ✅ 改进的异常处理

---

## 🎯 高级场景

### 处理循环引用

```csharp
public class Node
{
    public string Name { get; set; }
    public Node Parent { get; set; }
    public List<Node> Children { get; set; }
}

var root = new Node { Name = "Root" };
var child = new Node { Name = "Child", Parent = root };
root.Children = new List<Node> { child };

// 自动检测并处理循环引用
var dto = root.Adapt<NodeDto>();
```

### 枚举与整数互转

```csharp
public enum UserStatus { Inactive = 0, Active = 1 }

public class UserDto
{
    public UserStatus Status { get; set; }
}

public class UserViewModel
{
    public int Status { get; set; }  // 自动转换为整数
}

var dto = new UserDto { Status = UserStatus.Active };
var viewModel = dto.Adapt<UserViewModel>();
Console.WriteLine(viewModel.Status);  // 输出: 1
```

### JSON 属性转换

```csharp
using System.Text.Json;

public class Activity
{
    public string ExtraInfoJson { get; set; }  // JSON 字符串
}

public class ActivityDto
{
    public ExtraInfoModel ExtraInfo { get; set; }  // 对象
}

// 注册 JSON 转换器
TypeConverterRegistry.RegisterJson<ExtraInfoModel>();

var activity = new Activity
{
    ExtraInfoJson = JsonSerializer.Serialize(new ExtraInfoModel { Key = "location", Value = "北京" })
};

var dto = activity.Adapt<ActivityDto>();
Console.WriteLine(dto.ExtraInfo.Value);  // 输出: 北京
```

---

## 🤝 贡献

欢迎提交 Issue 和 Pull Request！

1. Fork 本仓库
2. 创建特性分支 (`git checkout -b feature/AmazingFeature`)
3. 提交更改 (`git commit -m 'Add some AmazingFeature'`)
4. 推送到分支 (`git push origin feature/AmazingFeature`)
5. 开启 Pull Request

---

## 📄 许可证

本项目采用 MIT 许可证 - 详见 [LICENSE](LICENSE) 文件

---

## 🙏 致谢

感谢所有为 Ling.Mapper 做出贡献的开发者！

---

## 📧 联系方式

- **GitHub**: [https://github.com/yanhuuo/Ling.Mapper](https://github.com/yanhuuo/Ling.Mapper)
- **Issues**: [https://github.com/yanhuuo/Ling.Mapper/issues](https://github.com/yanhuuo/Ling.Mapper/issues)

---

<div align="center">

⭐ **如果这个项目对你有帮助，请给我们一个 Star！** ⭐

</div>
