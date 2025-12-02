# 异常处理策略变更总结

## ?? 核心变更

**从静默失败改为抛出异常** - 遵循 **Fail Fast, Fail Loud** 原则

---

## ?? 变更对比

| 维度 | 旧策略（静默失败）? | 新策略（抛出异常）? |
|-----|-------------------|-------------------|
| **核心行为** | 捕获异常但不处理，返回 null | 抛出明确的异常 |
| **错误消息** | 无 | 详细的错误消息 + 修复建议 |
| **发现时机** | 运行时（可能到生产环境） | 开发/调试时立即发现 |
| **排查难度** | 困难（需要调试器单步跟踪） | 容易（异常堆栈直接定位） |
| **开发体验** | 浪费时间排查 | 快速定位和修复 |
| **代码质量** | 容忍设计缺陷 | 强制正确设计 |

---

## ?? 为什么要改变？

### 旧策略的问题 ?

```csharp
// 旧代码：静默失败
try 
{ 
    dest = (TDestination?)Activator.CreateInstance(typeof(TDestination)); 
}
catch 
{ 
    /* 静默失败，没有任何提示 */ 
}

// 结果：dest 是 null，但不知道为什么
var result = source.Adapt<OrderDto>();
if (result != null)  // 需要额外检查
{
    Console.WriteLine(result.Id);  // 可能永远不会执行
}
```

**问题**：
1. 开发者不知道失败原因
2. 需要花时间调试才能发现是缺少无参构造函数
3. 可能拖到生产环境才暴露问题
4. 容易导致 NullReferenceException

### 新策略的优势 ?

```csharp
// 新代码：抛出异常
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

// 结果：立即抛出清晰的异常
var result = source.Adapt<OrderDto>();
// ?? MissingMethodException: 无法创建类型 'OrderDto' 的实例：
//     该类型没有无参构造函数。请为 DTO 类型添加无参构造函数...
```

**优势**：
1. ? 立即发现问题（开发/调试时）
2. ? 清晰的错误消息
3. ? 包含修复建议
4. ? 强制正确设计 DTO

---

## ?? 实际例子对比

### 场景：DTO 缺少无参构造函数

```csharp
public class OrderDto
{
    public OrderDto(int id) { Id = id; }  // 只有带参数的构造函数
    public int Id { get; }
}
```

#### 旧策略（静默失败）?

```csharp
var result = source.Adapt<OrderDto>();
// result 是 null，但不知道为什么

// 开发者的思考过程：
// "为什么是 null？"
// "是 source 的问题？"
// "是 Mapper 配置的问题？"
// "是 OrderDto 的问题？"
// → 需要调试器单步跟踪才能发现
```

**时间消耗**：可能需要 10-30 分钟排查

#### 新策略（抛出异常）?

```csharp
var result = source.Adapt<OrderDto>();
// ?? 立即抛出异常：
//
// System.MissingMethodException: 无法创建类型 'OrderDto' 的实例：
// 该类型没有无参构造函数。请为 DTO 类型添加无参构造函数，
// 或确保 Mapper 配置正确返回非 null 实例。
//
// at Ling.Mapper.MapperExtensions.CreateInstance[T]()
// at Ling.Mapper.MapperExtensions.Adapt[TDestination,TSource](...)
// at Program.Main() in Program.cs:line 42

// 开发者的思考过程：
// "哦，OrderDto 缺少无参构造函数"
// → 立即知道如何修复
```

**时间消耗**：1-2 分钟（看异常消息 + 添加构造函数）

---

## ?? 如何适应新策略

### 1. 为 DTO 添加无参构造函数（推荐）

```csharp
// ? 推荐做法
public class OrderDto
{
    public OrderDto() { }  // 添加无参构造函数
    public OrderDto(int id) { Id = id; }
    
    public int Id { get; set; }  // 改为可设置（如果需要）
}
```

### 2. 配置 Mapper 使用构造函数

```csharp
public class OrderProfile : MapperProfile
{
    public OrderProfile()
    {
        CreateMap<Order, OrderDto>()
            .ConstructUsing(src => new OrderDto(src.Id));
    }
}
```

### 3. 使用 TryMap（不希望抛出异常）

```csharp
if (mapper.TryMap<OrderDto>(source, out var result))
{
    // 映射成功
    ProcessOrder(result);
}
else
{
    // 映射失败
    _logger.LogWarning("订单映射失败");
}
```

### 4. 使用 MapOrDefault（提供默认值）

```csharp
var result = mapper.MapOrDefault<OrderDto>(source, new OrderDto { Id = 0 });
```

---

## ?? 设计原则：Fail Fast, Fail Loud

### Fail Fast（快速失败）

**定义**：在问题发生时立即失败，不要拖到后面

**优势**：
- ? 问题更容易定位（堆栈跟踪更短）
- ? 避免错误状态传播
- ? 减少调试时间

**示例**：
```csharp
// ? Fail Fast
public void ProcessOrder(Order order)
{
    if (order == null)
        throw new ArgumentNullException(nameof(order));  // 立即失败
    
    // 继续处理...
}

// ? 不是 Fail Fast
public void ProcessOrder(Order order)
{
    // 静默接受 null，后面可能导致 NullReferenceException
    var total = order?.Items?.Sum(x => x.Price) ?? 0;  // 掩盖问题
}
```

### Fail Loud（明确失败）

**定义**：失败时要明确，提供清晰的错误消息

**优势**：
- ? 快速理解问题
- ? 包含修复建议
- ? 提高开发效率

**示例**：
```csharp
// ? Fail Loud
throw new MissingMethodException(
    $"无法创建类型 '{typeof(T).FullName}' 的实例：该类型没有无参构造函数。" +
    $"请为 DTO 类型添加无参构造函数，或确保 Mapper 配置正确返回非 null 实例。");

// ? 不是 Fail Loud
throw new Exception("Error");  // 不清楚是什么错误
```

---

## ?? 迁移检查清单

如果你从旧版本升级，请检查以下内容：

### ? 第 1 步：运行测试

```bash
dotnet test
```

如果有测试失败，查看是否是因为 DTO 缺少无参构造函数。

### ? 第 2 步：检查所有 DTO

查找所有使用 Adapt 的 DTO 类型，确保它们有无参构造函数：

```csharp
// ? 需要修复
public class OrderDto
{
    public OrderDto(int id) { Id = id; }
}

// ? 已修复
public class OrderDto
{
    public OrderDto() { }  // 添加
    public OrderDto(int id) { Id = id; }
}
```

### ? 第 3 步：更新 Mapper 配置

对于必须使用构造函数参数的 DTO，更新 Mapper 配置：

```csharp
CreateMap<Order, OrderDto>()
    .ConstructUsing(src => new OrderDto(src.Id));
```

### ? 第 4 步：使用 TryMap 替代（可选）

如果不希望抛出异常，改用 TryMap：

```csharp
// 改前
var result = source.Adapt<OrderDto>();
if (result != null) { /* ... */ }

// 改后
if (mapper.TryMap<OrderDto>(source, out var result))
{
    // 映射成功
}
```

---

## ?? 相关文档

| 文档 | 描述 |
|-----|------|
| [Exception-Handling-Quick-Guide.md](./Exception-Handling-Quick-Guide.md) | 快速指南（推荐先看） |
| [Exception-Handling-Improvements.md](./Exception-Handling-Improvements.md) | 详细说明（深入理解） |
| [Adapt-Usage.md](./Adapt-Usage.md) | Adapt 使用指南 |

---

## ?? 总结

### 核心变更

**从静默失败改为抛出异常**

### 设计原则

**Fail Fast, Fail Loud**

### 主要优势

1. ? **早期发现问题**：开发/调试阶段立即发现
2. ? **清晰的错误消息**：包含详细信息和修复建议
3. ? **更快的调试**：不需要单步跟踪
4. ? **更高的代码质量**：强制正确设计 DTO
5. ? **更好的开发体验**：节省大量时间

### 适应方法

1. 为 DTO 添加无参构造函数（推荐）
2. 配置 Mapper 使用构造函数
3. 使用 TryMap（不希望抛出异常）
4. 使用 MapOrDefault（提供默认值）

### 破坏性更改

**是**，但这是积极的改变，有助于：
- 在开发阶段就发现问题
- 强制开发者遵循最佳实践
- 提高整体代码质量

---

**变更日期**: 2025-01-10  
**变更类型**: 异常处理策略  
**影响范围**: 所有 Adapt 方法  
**推荐操作**: 为所有 DTO 添加无参构造函数
