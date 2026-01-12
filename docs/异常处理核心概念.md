# 异常处理行为澄清 - 核心要点

## ?? 核心理解

### 两种完全不同的场景

| 场景 | 描述 | 行为 | 是否报错 |
|-----|------|------|---------|
| **场景 A：属性不匹配** | 源和目标的属性名不完全相同 | 只转换匹配的属性，不匹配的保持默认值 | ? **不报错**（这是正常的） |
| **场景 B：无法创建实例** | 目标 DTO 没有无参构造函数 | 无法创建目标实例 | ? **抛出异常**（这是设计错误） |

---

## ?? 关键事实

### 1. Mapper 内部会自动创建实例

```csharp
// Mapper.cs 第 144-146 行（简化版）
var bodyExprs = new List<Expression>
{
    // 第一步：创建目标实例
    Expression.Assign(destVar, Expression.New(destType)),
    
    // 第二步：设置匹配的属性
    // ...属性赋值代码...
};
```

**要点**：
- Mapper 使用 `Expression.New(destType)` 创建实例
- 这发生在**编译表达式树**时
- 如果没有无参构造函数，会在**这里**抛出异常

### 2. `CreateInstance` 几乎不会被调用

```csharp
// MapperExtensions.cs 中的逻辑
var dest = mapper.Map<TDestination>(source);

if (dest == null && !typeof(TDestination).IsValueType)
{
    dest = CreateInstance<TDestination>();  // 备用方案，很少执行
}
```

**要点**：
- `CreateInstance` 只在 `mapper.Map` 返回 null 时调用
- Mapper 几乎总是会返回实例（除非源对象是 null）
- 所以 `CreateInstance` 是一个**安全网**，实际很少被触发

### 3. 异常的真正来源

| 异常来源 | 发生时机 | 频率 | 异常消息 |
|---------|---------|------|---------|
| **Mapper 内部** | 编译表达式树时 | ????? 非常常见 | `Constructor on type 'YourDto' not found.` |
| **CreateInstance** | mapper.Map 返回 null 时 | ? 非常罕见 | `无法创建类型 'YourDto' 的实例：该类型没有无参构造函数...` |

---

## ? 正确的理解

### 属性不匹配是正常的

```csharp
// 源 DTO
public class SourceDto
{
    public int Id { get; set; }
    public string FullName { get; set; } = "";  // 不是 Name
}

// 目标 DTO
public class DestDto
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
}

var source = new SourceDto { Id = 1, FullName = "John" };
var result = source.Adapt<DestDto>();

// 结果：
// - Id = 1 (匹配，已转换)
// - Name = "" (不匹配，保持默认值)
// - ? 不抛出异常，这是正常行为
```

**为什么这样设计？**
1. ? **灵活性**：允许部分属性映射，适用于多种场景
2. ? **向后兼容**：DTO 添加新属性时不会破坏现有代码
3. ? **常见需求**：很多场景只需要映射部分属性

### 没有无参构造函数是错误的

```csharp
// 目标 DTO（错误的设计）
public class DestDto
{
    public DestDto(int id) { Id = id; }  // ? 只有带参数的构造函数
    public int Id { get; }
}

var source = new SourceDto { Id = 1 };
var result = source.Adapt<DestDto>();

// ?? 抛出异常：
// System.MissingMethodException: Constructor on type 'DestDto' not found.
```

**为什么会报错？**
1. ? Mapper 需要无参构造函数来创建实例
2. ? `Expression.New(destType)` 在编译时发现没有无参构造函数
3. ? 这是设计错误，应该在开发阶段修复

---

## ??? 如何区分这两种场景

### 判断依据

```csharp
// 问题：映射失败了，是属性不匹配还是无法创建实例？

try
{
    var result = source.Adapt<DestDto>();
    
    // 如果执行到这里，说明：
    // ? 实例创建成功
    // ? 属性可能部分匹配（这是正常的）
    
    // 检查关键属性是否为默认值
    if (result.Id == 0)
    {
        // 可能是属性不匹配（Id 没有被赋值）
        // 这不是错误，可能需要调整映射配置
    }
}
catch (MissingMethodException ex)
{
    // ?? 无法创建实例
    // 原因：DestDto 没有无参构造函数
    // 解决方案：为 DestDto 添加无参构造函数
    
    _logger.LogError(ex, "DTO 设计错误：缺少无参构造函数");
    throw;
}
```

---

## ?? 解决方案

### 场景 A：属性不匹配（调整配置）

```csharp
// 方案 1：配置映射规则
var cfg = new MapperConfiguration();
cfg.ConfigureConventions(opt =>
{
    opt.CaseInsensitiveNameMatch = true;      // 忽略大小写
    opt.IgnoreSpecialCharacters = true;       // 忽略特殊字符
});

// 方案 2：使用 Rename 显式指定映射
cfg.AddProfile(new MyProfile());

public class MyProfile : MapperProfile
{
    public MyProfile()
    {
        CreateMap<SourceDto, DestDto>()
            .Rename(d => d.Name, "FullName");  // FullName -> Name
    }
}

// 方案 3：在回调中手动设置
var result = source.Adapt<DestDto>((dest, src) =>
{
    dest.Name = src.FullName;  // 手动设置不匹配的属性
});
```

### 场景 B：无法创建实例（修复设计）

```csharp
// ? 错误的设计
public class DestDto
{
    public DestDto(int id) { Id = id; }
    public int Id { get; }
}

// ? 正确的设计
public class DestDto
{
    public DestDto() { }  // 添加无参构造函数
    
    public DestDto(int id) { Id = id; }  // 保留带参数的构造函数
    
    public int Id { get; set; }  // 改为可设置
}
```

---

## ?? 示例对比

### 示例 1：属性完全不匹配（不报错）

```csharp
public class SourceDto
{
    public int Value1 { get; set; }
    public string Value2 { get; set; } = "";
}

public class DestDto
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
}

var source = new SourceDto { Value1 = 1, Value2 = "Test" };
var result = source.Adapt<DestDto>();

// 结果：Id=0, Name="" (都是默认值)
// ? 不报错，虽然属性完全不匹配，但实例创建成功了
```

### 示例 2：有无参构造函数但属性不匹配（不报错）

```csharp
public class DestDto
{
    public DestDto() { }  // ? 有无参构造函数
    
    public int Id { get; set; }
    public string Name { get; set; } = "";
}

var source = new SourceDto { Value1 = 1, Value2 = "Test" };
var result = source.Adapt<DestDto>();

// 结果：Id=0, Name="" (都是默认值)
// ? 不报错，实例创建成功
```

### 示例 3：属性匹配但没有无参构造函数（报错）

```csharp
public class DestDto
{
    public DestDto(int id, string name)  // ? 没有无参构造函数
    {
        Id = id;
        Name = name;
    }
    
    public int Id { get; }
    public string Name { get; }
}

var source = new SourceDto { Id = 1, Name = "Test" };
var result = source.Adapt<DestDto>();

// ?? 报错：MissingMethodException
// 原因：虽然属性匹配，但无法创建实例
```

---

## ?? 总结

### 核心要点

1. **属性不匹配 ≠ 错误**：这是 Mapper 的正常行为，提供灵活性
2. **无法创建实例 = 错误**：这是设计缺陷，必须修复
3. **异常主要来自 Mapper 内部**：`Expression.New(destType)`
4. **`CreateInstance` 是安全网**：实际很少被调用

### 判断标准

| 问题 | 判断方法 | 解决方案 |
|-----|---------|---------|
| 属性不匹配？ | 实例创建成功，但某些属性是默认值 | 配置映射规则或手动设置 |
| 无法创建实例？ | 抛出 MissingMethodException | 添加无参构造函数 |

### 设计原则

- ? **为所有 DTO 提供无参构造函数**（避免场景 B）
- ? **配置映射规则**（处理场景 A）
- ? **使用回调函数**（灵活处理特殊情况）

---

**文档日期**: 2025-01-10  
**核心结论**: 属性不匹配是正常的，没有无参构造函数才是错误
