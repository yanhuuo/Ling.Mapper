# 异常处理改进说明

## ?? 问题描述

**原问题**：代码中存在大量 `try-catch` 块捕获异常但静默失败，这会导致：

1. ? **隐藏潜在问题**：开发者无法发现实例化失败的原因
2. ? **排查困难**：在使用时遇到问题难以定位
3. ? **潜在风险**：静默失败可能导致运行时的 NullReferenceException
4. ? **违反最佳实践**：空的 catch 块是代码异味（Code Smell）

```csharp
// ? 问题代码示例
try 
{ 
    dest = (TDestination?)System.Activator.CreateInstance(typeof(TDestination)); 
}
catch (System.Exception) 
{
    // 静默失败，没有任何提示！
}
```

---

## ? 改进方案

### 设计原则：**Fail Fast, Fail Loud**

遵循"快速失败，明确失败"的原则，在问题发生时立即抛出清晰的异常，而不是静默失败。

### 1. **提取统一的实例创建方法**

创建 `CreateInstance<T>()` 私有方法，**抛出明确的异常**：

```csharp
/// <summary>
/// 创建目标类型的实例，失败时抛出异常
/// </summary>
/// <typeparam name="T">要创建的类型</typeparam>
/// <returns>创建的实例</returns>
/// <exception cref="System.MissingMethodException">类型没有无参构造函数</exception>
/// <exception cref="System.MemberAccessException">构造函数不可访问（例如私有构造函数）</exception>
/// <exception cref="System.Exception">实例化过程中发生其他异常</exception>
/// <remarks>
/// 此方法要求目标类型必须有一个可访问的无参构造函数。
/// 如果实例化失败，将抛出明确的异常，帮助开发者在调试时快速定位问题。
/// </remarks>
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
    catch (System.MemberAccessException ex)
    {
        throw new System.MemberAccessException(
            $"无法创建类型 '{typeof(T).FullName}' 的实例：构造函数不可访问（可能是私有或受保护的）。" +
            $"请确保目标类型有一个公共的无参构造函数。", ex);
    }
    catch (System.Exception ex)
    {
        throw new System.InvalidOperationException(
            $"创建类型 '{typeof(T).FullName}' 的实例时发生异常：{ex.Message}" +
            $"请检查构造函数是否抛出异常，或目标类型是否可以正常实例化。", ex);
    }
}
```

### 2. **捕获具体的异常类型并提供有用的错误消息**

| 异常类型 | 含义 | 错误消息示例 |
|---------|------|------------|
| `MissingMethodException` | 类型没有无参构造函数 | "无法创建类型 'OrderDto' 的实例：该类型没有无参构造函数。请为 DTO 类型添加无参构造函数..." |
| `MemberAccessException` | 构造函数不可访问（私有等） | "无法创建类型 'SingletonDto' 的实例：构造函数不可访问（可能是私有或受保护的）。请确保目标类型有一个公共的无参构造函数。" |
| `InvalidOperationException` | 其他实例化异常 | "创建类型 'ValidatedDto' 的实例时发生异常：需要先初始化配置。请检查构造函数是否抛出了异常..." |

### 3. **抛出异常 vs 静默失败的对比**

| 对比维度 | 静默失败（旧方案）? | 抛出异常（新方案）? |
|---------|-------------------|-------------------|
| **问题发现时机** | 运行时（可能到生产环境） | 开发/调试时立即发现 |
| **错误信息** | 无任何提示 | 清晰的异常消息 + 修复建议 |
| **排查难度** | 困难，需要猜测和调试 | 容易，异常直接指出问题 |
| **开发体验** | 浪费时间排查隐藏问题 | 快速定位并修复 |
| **代码质量** | 容忍设计缺陷 | 强制正确设计（提供无参构造函数） |
| **生产风险** | 可能导致 NullReferenceException | 开发阶段就暴露问题 |
| **调试效率** | 低（需要单步跟踪） | 高（异常堆栈直接定位） |

### 4. **更新文档注释**

在所有相关方法的文档中添加 `<exception>` 标签：

```csharp
/// <exception cref="System.MissingMethodException">目标类型没有无参构造函数</exception>
/// <exception cref="System.MemberAccessException">目标类型的构造函数不可访问</exception>
/// <remarks>
/// 如果映射结果为 null 且目标类型不是值类型，将尝试创建目标类型的实例。
/// 如果实例化失败，将抛出相应的异常，而不是返回 null。
/// </remarks>
```

---

## ?? 改进效果对比

### 改进前（静默失败）?

```csharp
public class OrderDto
{
    public OrderDto(int id) { Id = id; }  // 只有带参数的构造函数
    public int Id { get; }
}

var result = source.Adapt<OrderDto>();
// result 是 null，但不知道为什么
// 后续代码可能导致 NullReferenceException
if (result != null)  // 需要额外的 null 检查
{
    Console.WriteLine(result.Id);
}
```

**问题**：
- ? 没有任何提示，result 是 null
- ? 开发者不知道是映射失败还是实例化失败
- ? 需要花时间调试才能发现是 OrderDto 缺少无参构造函数
- ? 可能会拖到生产环境才发现问题

### 改进后（抛出异常）?

```csharp
public class OrderDto
{
    public OrderDto(int id) { Id = id; }  // 只有带参数的构造函数
    public int Id { get; }
}

var result = source.Adapt<OrderDto>();
// ?? 立即抛出 MissingMethodException：
//
// System.MissingMethodException: 无法创建类型 'OrderDto' 的实例：
// 该类型没有无参构造函数。请为 DTO 类型添加无参构造函数，
// 或确保 Mapper 配置正确返回非 null 实例。
//
//    at Ling.Mapper.MapperExtensions.CreateInstance[T]()
//    at Ling.Mapper.MapperExtensions.Adapt[TDestination,TSource](...)
//    at Program.Main() in Program.cs:line 42
```

**优势**：
- ? 立即发现问题（在开发/调试时）
- ? 清晰的错误消息，直接指出原因
- ? 包含修复建议
- ? 堆栈跟踪清晰，快速定位
- ? 强制开发者正确设计 DTO

---

## ?? 使用场景示例

### 场景 1：正常情况（有无参构造函数）

```csharp
public class CustomerDto
{
    public CustomerDto() { }  // ? 有无参构造函数
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
}

// ? 成功映射，无异常
var result = source.Adapt<CustomerDto>();
Console.WriteLine(result.Name);
```

### 场景 2：没有无参构造函数（立即抛出异常）

```csharp
public class OrderDto
{
    public OrderDto(int id) { Id = id; }  // ? 只有带参数的构造函数
    public int Id { get; }
}

// ?? 抛出 MissingMethodException
var result = source.Adapt<OrderDto>();
```

**异常输出**：
```
System.MissingMethodException: 无法创建类型 'YourNamespace.OrderDto' 的实例：
该类型没有无参构造函数。请为 DTO 类型添加无参构造函数，
或确保 Mapper 配置正确返回非 null 实例。
```

**修复方法 1**：添加无参构造函数
```csharp
public class OrderDto
{
    public OrderDto() { }  // ? 添加无参构造函数
    public OrderDto(int id) { Id = id; }
    public int Id { get; set; }  // 改为可设置
}
```

**修复方法 2**：配置 Mapper 使用构造函数
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

### 场景 3：私有构造函数

```csharp
public class SingletonDto
{
    private SingletonDto() { }  // ? 私有构造函数
    public static SingletonDto Instance { get; } = new SingletonDto();
}

// ?? 抛出 MemberAccessException
var result = source.Adapt<SingletonDto>();
```

**异常输出**：
```
System.MemberAccessException: 无法创建类型 'SingletonDto' 的实例：
构造函数不可访问（可能是私有或受保护的）。
请确保目标类型有一个公共的无参构造函数。
```

**修复方法**：
```csharp
public class SingletonDto
{
    public SingletonDto() { }  // ? 改为 public
    // 或者不使用单例模式
}
```

### 场景 4：构造函数抛出异常

```csharp
public class ValidatedDto
{
    public ValidatedDto()
    {
        // ? 构造函数中执行可能失败的操作
        if (!IsConfigured)
            throw new InvalidOperationException("需要先初始化配置");
    }
    
    private static bool IsConfigured = false;
}

// ?? 抛出 InvalidOperationException
var result = source.Adapt<ValidatedDto>();
```

**异常输出**：
```
System.InvalidOperationException: 创建类型 'ValidatedDto' 的实例时发生异常：
需要先初始化配置。请检查构造函数是否抛出了异常，
或目标类型是否可以正常实例化。
```

**修复方法**：
```csharp
public class ValidatedDto
{
    public ValidatedDto()
    {
        // ? 不在构造函数中执行可能失败的操作
    }
    
    public bool IsInitialized { get; set; }
    
    // 使用初始化方法
    public void Initialize()
    {
        if (!IsConfigured)
            throw new InvalidOperationException("需要先初始化配置");
        IsInitialized = true;
    }
}
```

---

## ??? 推荐的错误处理策略

### 策略 1：直接使用 Adapt（推荐，最简单）

```csharp
// 如果 DTO 设计正确（有无参构造函数），直接使用
var customer = source.Adapt<CustomerDto>();
// 开发阶段会立即发现设计问题，不会拖到生产环境
```

**适用场景**：
- ? DTO 设计正确（有无参构造函数）
- ? 希望在开发阶段就发现问题
- ? 不需要特殊的错误处理

### 策略 2：使用 try-catch 捕获异常

```csharp
try
{
    var customer = source.Adapt<CustomerDto>();
    ProcessCustomer(customer);
}
catch (MissingMethodException ex)
{
    // DTO 设计问题，应该在开发阶段修复
    _logger.LogError(ex, "DTO 缺少无参构造函数，这是设计错误");
    throw;  // 重新抛出，因为这是设计错误，不应该发生
}
catch (Exception ex)
{
    // 其他映射异常
    _logger.LogError(ex, "映射失败");
    // 根据业务需求决定是否继续
}
```

**适用场景**：
- ? 需要记录日志
- ? 需要根据不同异常类型做不同处理
- ? 需要向用户显示友好的错误消息

### 策略 3：使用 TryMap（不希望抛出异常）

```csharp
if (mapper.TryMap<CustomerDto>(source, out var result))
{
    // 映射成功
    ProcessCustomer(result);
}
else
{
    // 映射失败，记录日志或使用默认值
    _logger.LogWarning("客户数据映射失败");
    result = GetDefaultCustomer();
}
```

**适用场景**：
- ? 不确定映射是否会成功
- ? 不希望抛出异常
- ? 有默认值或备用方案

### 策略 4：使用 MapOrDefault（提供默认值）

```csharp
var customer = mapper.MapOrDefault<CustomerDto>(source, new CustomerDto
{
    Id = 0,
    Name = "Unknown"
});
```

**适用场景**：
- ? 映射失败时有明确的默认值
- ? 不需要知道失败原因
- ? 简化代码逻辑

---

## ?? 最佳实践建议

### 1. **明确的 DTO 设计**

```csharp
// ? 推荐：提供无参构造函数
public class CustomerDto
{
    public CustomerDto() { }  // 必须提供
    
    // 可选：也可以提供带参数的构造函数
    public CustomerDto(int id, string name)
    {
        Id = id;
        Name = name;
    }
    
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
}

// ? 避免：只有带参数的构造函数
public class CustomerDto
{
    public CustomerDto(int id, string name)  // 缺少无参构造函数
    {
        Id = id;
        Name = name;
    }
    
    public int Id { get; }
    public string Name { get; }
}
```

### 2. **使用 Record 类型（C# 9+）**

```csharp
// ? 自动生成构造函数映射
public record CustomerDto(int Id, string Name);

// Mapper 配置会自动识别构造函数参数
public class CustomerProfile : MapperProfile
{
    public CustomerProfile()
    {
        CreateMap<Customer, CustomerDto>();  // 自动映射到构造函数
    }
}
```

### 3. **配置 Mapper 使用构造函数**

```csharp
public class OrderProfile : MapperProfile
{
    public OrderProfile()
    {
        CreateMap<Order, OrderDto>()
            .ConstructUsing(src => new OrderDto(src.Id, src.CustomerName));
        // 确保 Mapper 返回非 null 实例，不会触发 CreateInstance
    }
}
```

### 4. **单元测试覆盖**

```csharp
[Fact]
public void Adapt_Should_Throw_When_NoParameterlessConstructor()
{
    // Arrange
    var source = new Order { Id = 1 };
    
    // Act & Assert
    var exception = Assert.Throws<MissingMethodException>(
        () => source.Adapt<OrderDto>()
    );
    
    Assert.Contains("没有无参构造函数", exception.Message);
}
```

---

## ? 性能影响

### 正常路径（无异常）

```csharp
public class CustomerDto
{
    public CustomerDto() { }  // 有无参构造函数
    public int Id { get; set; }
}

// Mapper 正确配置，返回非 null 实例
var result = source.Adapt<CustomerDto>();
// ? 不会调用 CreateInstance，无性能影响
```

### 异常路径（抛出异常）

```csharp
public class OrderDto
{
    public OrderDto(int id) { Id = id; }  // 没有无参构造函数
}

// ?? 抛出异常
var result = source.Adapt<OrderDto>();
// ?? 这是设计错误，应该在开发阶段就修复
// 不应该出现在生产环境，所以性能不是问题
```

**结论**：
- ? 正确使用（有无参构造函数）：零性能影响
- ? 错误使用（没有无参构造函数）：应该在开发阶段修复，不应该关心性能

---

## ?? 设计原则总结

### Fail Fast, Fail Loud

**快速失败**：在问题发生时立即失败，不要拖到后面
**明确失败**：抛出清晰的异常，包含详细的错误消息和修复建议

### 优势

1. ? **早期发现问题**：在开发/调试阶段就暴露设计缺陷
2. ? **清晰的错误信息**：直接指出问题和解决方案
3. ? **强制正确设计**：促使开发者遵循最佳实践
4. ? **提高代码质量**：减少运行时错误
5. ? **更好的开发体验**：节省排查时间，提高生产力
6. ? **降低生产风险**：问题不会拖到生产环境

---

## ?? 迁移指南

### 从旧版本升级

如果你的代码依赖于旧的"静默失败"行为：

#### 步骤 1：检查 DTO 设计

运行你的单元测试或手动测试，看是否有异常抛出：

```csharp
// 如果抛出 MissingMethodException，说明 DTO 缺少无参构造函数
var result = source.Adapt<YourDto>();
```

#### 步骤 2：修复 DTO

为所有 DTO 添加无参构造函数：

```csharp
public class YourDto
{
    public YourDto() { }  // 添加这行
    // ...existing code...
}
```

#### 步骤 3：或者使用 TryMap

如果确实不想抛出异常，改用 `TryMap`：

```csharp
// 改前
var result = source.Adapt<YourDto>();
if (result != null) { /* ... */ }

// 改后
if (mapper.TryMap<YourDto>(source, out var result))
{
    // 映射成功
}
```

---

## ?? 相关文档

- **快速指南**：[Exception-Handling-Quick-Guide.md](./Exception-Handling-Quick-Guide.md)
- **使用指南**：[Adapt-Usage.md](./Adapt-Usage.md)
- **功能总结**：[Feature-Summary.md](./Feature-Summary.md)

---

**改进日期**: 2025-01-10  
**改进内容**: 从静默失败改为抛出明确的异常  
**影响范围**: 所有 Adapt 相关方法  
**破坏性更改**: 是（但是是积极的改变，有助于发现和修复设计问题）  
**设计原则**: Fail Fast, Fail Loud
