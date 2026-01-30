# Ling.Mapper

[![NuGet](https://img.shields.io/nuget/v/Ling.Mapper.svg)](https://www.nuget.org/packages/Ling.Mapper/)
[![License](https://img.shields.io/badge/license-MIT-blue.svg)](LICENSE)
[![.NET](https://img.shields.io/badge/.NET-6.0%2b%20-8.0%20-9.0%20-10.0-blue)](https://dotnet.microsoft.com/)

轻量级、高性能的 .NET 对象映射库

Ling.Mapper 是一个基于 Expression Tree 的对象映射库，提供简洁的 Adapt API 和高效的性能。支持复杂对象映射、集合转换、循环引用检测，以及灵活的运行时配置选项。

---

## 核心特性

- **简洁的 Adapt API** - 一行代码完成对象映射：`source.Adapt<Target>()`
- **高性能编译** - 基于 Expression Tree 编译，接近手写代码性能
- **自动属性匹配** - 智能识别同名属性（支持忽略大小写/下划线）
- **集合映射** - 支持 List、Array、IEnumerable 等所有集合类型
- **嵌套对象映射** - 自动处理对象图中的嵌套对象和集合
- **运行时映射选项** - AdaptOptions 动态控制映射行为
- **运行时忽略字段** - Adapt 时动态指定要忽略的字段名
- **映射回调** - 支持单个对象和集合的回调处理

### 性能表现

| 场景 | 吞吐量 | 说明 |
|------|--------|------|
| 简单对象映射 | **975K ops/sec** | 属性较少的对象 |
| 复杂对象映射 | **148K ops/sec** | 嵌套对象、集合 |
| 集合映射 | **8.5M elements/sec** | 集合元素处理速度 |

*测试环境: .NET 10.0, 8核 CPU, 支持 .NET 6.0/8.0/9.0/10.0*

---

## 快速安装

```bash
dotnet add package Ling.Mapper
```

或

```bash
Install-Package Ling.Mapper
```

---

## 快速开始

### 1. 最简单的映射

```csharp
using Ling.Mapper.Extensions;

public class UserSource
{
    public int Id { get; set; }
    public string Name { get; set; }
}

public class UserTarget
{
    public int Id { get; set; }
    public string Name { get; set; }
}

// 基础映射
var source = new UserSource { Id = 1, Name = "张三" };
var target = source.Adapt<UserTarget>();

Console.WriteLine($"{target.Id} - {target.Name}");
// 输出: 1 - 张三
```

### 2. 集合映射

```csharp
// List 映射
var sourceList = new List<UserSource>
{
    new UserSource { Id = 1, Name = "张三" },
    new UserSource { Id = 2, Name = "李四" }
};

var targetList = sourceList.Adapt<List<UserTarget>>();

Console.WriteLine($"映射了 {targetList.Count} 条记录");
// 输出: 映射了 2 条记录
```

---

## 高级用法

### 1. 单个对象映射带回调

```csharp
public class UserSource
{
    public int Id { get; set; }
    public string FirstName { get; set; }
    public string LastName { get; set; }
    public int Age { get; set; }
}

public class UserTarget
{
    public int Id { get; set; }
    public string FirstName { get; set; }
    public string LastName { get; set; }
    public string FullName { get; set; }
    public bool IsAdult { get; set; }
}

// 使用回调处理映射后的逻辑
var source = new UserSource
{
    Id = 1,
    FirstName = "John",
    LastName = "Doe",
    Age = 30
};

var target = source.Adapt<UserTarget, UserSource>((dest, src) =>
{
    // dest: 映射后的目标对象
    // src: 原始源对象
    dest.FullName = $"{src.FirstName} {src.LastName}";
    dest.IsAdult = src.Age >= 18;
});

Console.WriteLine($"{target.FullName}, IsAdult: {target.IsAdult}");
// 输出: John Doe, IsAdult: True
```

### 2. 集合映射带项级回调（推荐）

```csharp
var sourceList = new List<UserSource>
{
    new UserSource { Id = 1, FirstName = "John", LastName = "Doe", Age = 30 },
    new UserSource { Id = 2, FirstName = "Jane", LastName = "Smith", Age = 25 }
};

// 推荐：使用项级回调，在一次循环内完成映射和回调
int rowNumber = 0;
var targetList = sourceList.Adapt<UserTarget, UserSource>((dest, src) =>
{
    // 每个元素映射完成后立即执行
    dest.FullName = $"{src.FirstName} {src.LastName}";
    dest.IsAdult = src.Age >= 18;
    dest.RowNumber = ++rowNumber;
});

// 输出结果
foreach (var item in targetList)
{
    Console.WriteLine($"{item.RowNumber}. {item.FullName}, IsAdult: {item.IsAdult}");
}
// 输出:
// 1. John Doe, IsAdult: True
// 2. Jane Smith, IsAdult: True
```

### 3. 集合映射带整体回调

```csharp
var sourceList = new List<UserSource>
{
    new UserSource { Id = 1, FirstName = "John", LastName = "Doe" },
    new UserSource { Id = 2, FirstName = "Jane", LastName = "Smith" }
};

// 使用整体回调，在整个集合映射完成后执行
var targetList = sourceList.Adapt<List<UserTarget>>(list =>
{
    // list: 已经映射完成的列表
    for (int i = 0; i < list.Count; i++)
    {
        list[i].FullName += " (Verified)";
    }
});

foreach (var item in targetList)
{
    Console.WriteLine(item.FullName);
}
// 输出:
// John Doe (Verified)
// Jane Smith (Verified)
```

### 4. 运行时映射选项

```csharp
// 处理命名差异
var source = new
{
    id = 1,
    user_name = "张三",
    AGE = 30
};

// 使用 FlexibleOption（忽略大小写 + 忽略下划线）
var target = source.Adapt<UserTarget>(AdaptOptions.FlexibleOption);
```

**可用的选项**：

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

### 5. 运行时忽略字段

```csharp
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

Console.WriteLine($"Name: {target.Name}");
Console.WriteLine($"Password: {target.Password}");  // null
Console.WriteLine($"CreditCard: {target.CreditCard}"); // null
Console.WriteLine($"Age: {target.Age}");            // 30
```

**特性说明**：
- 支持忽略不存在的字段（安全处理，不会抛异常）
- 可以同时忽略多个字段
- 灵活的运行时控制

### 6. 嵌套对象映射

```csharp
public class OrderSource
{
    public int Id { get; set; }
    public UserSource User { get; set; }
}

public class OrderTarget
{
    public int Id { get; set; }
    public UserTarget User { get; set; }
}

// 自动处理嵌套对象
var order = new OrderSource
{
    Id = 101,
    User = new UserSource { Id = 1, Name = "张三" }
};

var orderTarget = order.Adapt<OrderTarget>();

Console.WriteLine($"Order: {orderTarget.Id}, User: {orderTarget.User.Name}");
// 输出: Order: 101, User: 张三
```

### 7. 集合属性映射

```csharp
public class OrderSource
{
    public int Id { get; set; }
    public List<UserSource> Users { get; set; }
}

public class OrderTarget
{
    public int Id { get; set; }
    public List<UserTarget> Users { get; set; }
}

// 自动处理集合属性
var order = new OrderSource
{
    Id = 101,
    Users = new List<UserSource>
    {
        new UserSource { Id = 1, Name = "张三" },
        new UserSource { Id = 2, Name = "李四" }
    }
};

var orderTarget = order.Adapt<OrderTarget>();

Console.WriteLine($"Order: {orderTarget.Id}, Users: {orderTarget.Users.Count}");
// 输出: Order: 101, Users: 2
```

### 8. 循环引用处理

```csharp
public class NodeSource
{
    public int Id { get; set; }
    public string Name { get; set; }
    public NodeSource Related { get; set; }
}

public class NodeTarget
{
    public int Id { get; set; }
    public string Name { get; set; }
    public NodeTarget Related { get; set; }
}

// 创建循环引用
var nodeA = new NodeSource { Id = 1, Name = "Node A" };
var nodeB = new NodeSource { Id = 2, Name = "Node B" };
nodeA.Related = nodeB;
nodeB.Related = nodeA;

// Mapper 自动检测并处理循环引用
var targetA = nodeA.Adapt<NodeTarget>();

Console.WriteLine($"{targetA.Name} -> {targetA.Related.Name}");
// 输出: Node A -> Node B
```

### 9. 忽略 Null 值

```csharp
public class UserSource
{
    public int Id { get; set; }
    public string Name { get; set; }
    public string Description { get; set; }
}

public class UserTarget
{
    public int Id { get; set; }
    public string Name { get; set; } = "Default";
    public string Description { get; set; }
}

var source = new UserSource
{
    Id = 1,
    Name = null,  // null 值
    Description = "Test"
};

// 使用 IgnoreNullValues 选项
var target = source.Adapt<UserTarget>(AdaptOptions.IgnoreNullValues);

Console.WriteLine($"Id: {target.Id}");
Console.WriteLine($"Name: {target.Name}");  // 输出: Default (保留原值)
Console.WriteLine($"Description: {target.Description}");  // 输出: Test
```

---

## API 参考

### AdaptExtensions 扩展方法

```csharp
// 基础映射
public static TDestination Adapt<TDestination>(this object? source)

// 带选项映射
public static TDestination Adapt<TDestination>(this object? source, AdaptOptions options)

// 忽略字段映射
public static TDestination Adapt<TDestination>(this object? source, string firstIgnore, params string[] otherIgnores)

// 单个对象带回调
public static TDestination Adapt<TDestination, TSource>(this TSource source, Action<TDestination, TSource> afterMapItem)

// 集合项级回调（推荐）
public static List<TTargetItem> Adapt<TTargetItem, TSourceItem>(this IEnumerable<TSourceItem> source, Action<TTargetItem, TSourceItem> afterMapItem)
    where TTargetItem : class, new()

// 集合整体回调
public static TDestination Adapt<TDestination>(this object? source, Action<TDestination> afterMap)
```

### AdaptOptions 枚举

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

---

## 测试

运行完整测试套件：

```bash
cd tests/Ling.Mapper.Tests
dotnet run
```

测试涵盖：
- 基础对象映射
- 集合映射（List、Array、IEnumerable）
- 嵌套对象映射
- 循环引用检测
- 运行时忽略字段
- 映射回调（单个对象、集合项级、集合整体）
- AdaptOptions 各种选项
- Null 值处理
- 性能基准测试

---

## 更新日志

### v1.1.2 (最新)
- 新增运行时忽略字段功能：`source.Adapt<Target>("Field1", "Field2")`
- 优化映射回调性能：回调深入到 Mapper 层，避免重复遍历
- 优化 List 映射回调：采用单次循环 1 对 1 关系
- 完善测试覆盖：新增忽略字段、嵌套属性等测试用例

### v1.1.1
- 支持深层嵌套属性映射（A.B.C.D）
- 新增 IgnoreNullValues 选项
- 优化表达式树编译性能
- 修复循环引用 StackOverflow 问题

---

## 链接

- **GitHub**: [Ling.Mapper](https://github.com/yanhuuo/Ling.Mapper)
- **NuGet**: [Ling.Mapper](https://www.nuget.org/packages/Ling.Mapper/)
- **Issues**: [报告问题](https://github.com/yanhuuo/Ling.Mapper/issues)

---

## 许可证

本项目采用 MIT 许可证 - 详见 [LICENSE](LICENSE) 文件

---

<div align="center">

**如果这个项目对你有帮助，请给个 Star！**

</div>
