# 异常处理改进 - 快速指南

## ?? 改进目标

澄清异常处理行为，**区分两种不同的场景**：

1. ? **属性不匹配**：只转换匹配的属性，不报错（这是正常的映射行为）
2. ? **无法创建实例**：DTO 没有无参构造函数，抛出异常（这是设计错误）

---

## ?? 重要说明

### Mapper 的实际行为

**Mapper 内部会自动创建目标实例**，使用以下逻辑（在 `Mapper.cs` 中）：

```csharp
// Mapper 内部的实现（简化版）
var bodyExprs = new List<Expression>
{
    // 第一步：创建目标实例
    Expression.Assign(destVar, Expression.New(destType)),
    
    // 第二步：设置匹配的属性
    // ...属性赋值代码...
};
```

**关键点**：
- Mapper 使用 `Expression.New(destType)` 创建实例
- 如果 DTO 没有无参构造函数，会在**编译表达式树时**抛出异常
- 属性不匹配**不会**导致创建实例失败，只是部分属性保持默认值

### 两种不同的场景

| 场景 | 行为 | 是否报错 |
|-----|------|---------|
| **场景 A：属性不匹配** | 只转换匹配的属性，不匹配的保持默认值 | ? 不报错（这是正常的） |
| **场景 B：没有无参构造函数** | 无法创建实例 | ? 抛出异常（这是设计错误） |

---

## ? 改进内容

### 1. `CreateInstance<T>()` 方法的作用

```csharp
private static T CreateInstance<T>()
{
    try
    {
        return (T)System.Activator.CreateInstance(typeof(T))!;
    }
    catch (System.MissingMethodException ex)
    {
        throw new System.MissingMethodException(
            $"无法创建类型 '{typeof(T).FullName}' 的实例：该类型没有无参构造函数。" +
            $"请为 DTO 类型添加无参构造函数，或确保 Mapper 配置正确返回非 null 实例。", ex);
    }
    // ...其他异常处理
}
```

**作用**：作为**备用方案**，仅在极少数情况下（Mapper 返回 null）才会被调用。

**主要异常来源**：**Mapper 内部的 `Expression.New(destType)`**，而不是 `CreateInstance`。

### 2. 异常抛出的时机

| 阶段 | 异常来源 | 说明 |
|-----|---------|------|
| **编译阶段** | `Expression.New(destType)` | Mapper 编译表达式树时发现没有无参构造函数 |
| **运行阶段** | `CreateInstance<T>()` | 仅在 Mapper 返回 null 时作为备用方案 |

### 3. 实际的异常类型

根据 Mapper 的实现，实际抛出的异常是：

```
System.MissingMethodException: Constructor on type 'YourDto' not found.
```

而**不是**我们自定义的异常消息（因为异常发生在 Mapper 内部）。

---

## ?? 使用示例

### 示例 1：正常情况（成功）

```csharp
// 源 DTO
public class SourceDto
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
}

// 目标 DTO（有无参构造函数）
public class DestDto
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
}

var source = new SourceDto { Id = 1, Name = "Test" };
var result = source.Adapt<DestDto>();
// ? 成功：Id=1, Name="Test"
```

### 示例 2：属性部分匹配（成功，不报错）

```csharp
// 源 DTO
public class SourceDto
{
    public int Id { get; set; }
    public string FullName { get; set; } = string.Empty;  // 注意：不是 Name
}

// 目标 DTO
public class DestDto
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
}

var source = new SourceDto { Id = 2, FullName = "John Doe" };
var result = source.Adapt<DestDto>();
// ? 成功：Id=2, Name="" (空字符串，默认值)
// 说明：只转换了匹配的属性 (Id)，不匹配的属性 (Name) 保持默认值
```

### 示例 3：属性完全不匹配（成功，不报错）

```csharp
// 源 DTO
public class SourceDto
{
    public int Value1 { get; set; }
    public string Value2 { get; set; } = string.Empty;
}

// 目标 DTO
public class DestDto
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
}

var source = new SourceDto { Value1 = 3, Value2 = "Test" };
var result = source.Adapt<DestDto>();
// ? 成功：Id=0, Name="" (都是默认值)
// 说明：虽然属性完全不匹配，但仍然创建了实例（所有属性为默认值）
```

### 示例 4：没有无参构造函数（抛出异常）

```csharp
// 目标 DTO（没有无参构造函数）
public class DestDto
{
    public DestDto(int id, string name)
    {
        Id = id;
        Name = name;
    }
    
    public int Id { get; }
    public string Name { get; }
}

var source = new SourceDto { Id = 4, Name = "Test" };
var result = source.Adapt<DestDto>();
// ?? 抛出 MissingMethodException：
// "Constructor on type 'DestDto' not found."
// (这个异常来自 Mapper 内部的 Expression.New)
```

**修复方法**：
```csharp
public class DestDto
{
    public DestDto() { }  // ? 添加无参构造函数
    
    public DestDto(int id, string name)
    {
        Id = id;
        Name = name;
    }
    
    public int Id { get; set; }  // 改为可设置
    public string Name { get; set; } = string.Empty;
}
```

---

## ??? 推荐的错误处理方式

### 1. 使用 TryMap（不希望抛出异常）

```csharp
if (mapper.TryMap<DestDto>(source, out var result))
{
    // 映射成功
    ProcessData(result);
}
else
{
    // 映射失败（通常是因为没有无参构造函数）
    _logger.LogWarning("映射失败");
}
```

### 2. 使用 MapOrDefault（提供默认值）

```csharp
// 映射失败时使用默认值
var result = mapper.MapOrDefault<DestDto>(source, new DestDto
{
    Id = 0,
    Name = "Unknown"
});
```

### 3. 使用 Adapt 并捕获异常（推荐）

```csharp
try
{
    var result = source.Adapt<DestDto>();
    ProcessData(result);
}
catch (MissingMethodException ex)
{
    // DTO 缺少无参构造函数，这是设计错误
    _logger.LogError(ex, "DTO 设计错误：缺少无参构造函数");
    throw;  // 重新抛出，因为这是设计错误
}
catch (Exception ex)
{
    // 其他映射异常
    _logger.LogError(ex, "映射失败");
}
```

### 4. 直接使用 Adapt（最简单）

```csharp
// 如果 DTO 设计正确（有无参构造函数），直接使用即可
var result = source.Adapt<DestDto>();
// 如果有问题，开发/调试时就会报错，不会拖到生产环境
```

---

## ? 最佳实践

### ? 推荐：DTO 提供无参构造函数

```csharp
public class CustomerDto
{
    public CustomerDto() { }  // ? 提供无参构造函数
    
    // 可选：也可以提供带参数的构造函数
    public CustomerDto(int id, string name)
    {
        Id = id;
        Name = name;
    }
    
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
}
```

### ? 避免：只有带参数的构造函数

```csharp
// ? 这样会导致 Mapper 抛出异常
public class CustomerDto
{
    public CustomerDto(int id, string name)  // 没有无参构造函数
    {
        Id = id;
        Name = name;
    }
    
    public int Id { get; }
    public string Name { get; }
}
```

### ?? 特殊场景：映射规则配置

如果你配置了特殊的映射规则（忽略特殊字符、忽略大小写等），这些规则会正常工作：

```csharp
var cfg = new MapperConfiguration();
cfg.ConfigureConventions(opt =>
{
    opt.CaseInsensitiveNameMatch = true;      // 忽略大小写
    opt.IgnoreSpecialCharacters = true;       // 忽略特殊字符（如下划线）
});

var mapper = cfg.CreateMapper();

// 这些配置会影响属性匹配，但不会影响实例创建
```

**示例**：
```csharp
// 源 DTO
public class SourceDto
{
    public int user_id { get; set; }  // 小写 + 下划线
}

// 目标 DTO
public class DestDto
{
    public int UserId { get; set; }   // 大写驼峰
}

// 配置了 CaseInsensitiveNameMatch 和 IgnoreSpecialCharacters 后
var source = new SourceDto { user_id = 123 };
var result = source.Adapt<DestDto>();
// ? 成功：UserId=123 (自动匹配了 user_id -> UserId)
```

---

## ?? 异常来源总结

| 异常来源 | 发生时机 | 异常类型 | 解决方案 |
|---------|---------|---------|---------|
| **Mapper 内部** | 编译表达式树时 | `MissingMethodException` | 为 DTO 添加无参构造函数 |
| **CreateInstance** | Mapper 返回 null 时 | `MissingMethodException` (增强版) | 为 DTO 添加无参构造函数 |
| **属性不匹配** | 不会抛出异常 | 无 | 不是错误，是正常行为 |

---

## ?? 常见问题

### Q1: 我不想让程序在这里抛出异常怎么办？

**A**: 使用 `TryMap` 或 `MapOrDefault` 方法：

```csharp
// 方式 1: TryMap
if (mapper.TryMap<DestDto>(source, out var result))
{
    // 成功
}
else
{
    // 失败，不会抛出异常
}

// 方式 2: MapOrDefault
var result = mapper.MapOrDefault<DestDto>(source, defaultValue);
