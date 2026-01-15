# Adapt 集合自动识别 & 类型不匹配处理机制

## 📋 改进需求

### 1. AdaptList 合并到 Adapt
让 `Adapt` 方法自动识别集合类型并处理

### 2. 类型不匹配处理分析
梳理当前的类型不匹配处理机制

---

## 🔍 当前类型不匹配处理机制

### 1. **TypeConversionHelper** - 编译时类型转换

位置：`src\Ling.Mapper\Mapper\TypeConversionHelper.cs`

支持的转换类型：

#### ✅ 枚举转换
- `enum → int` / `int → enum`
- `enum → string` / `string → enum`
- `enum → enum` (不同枚举类型)
- 支持可空枚举转换

#### ✅ 可空类型转换
- `T → T?` (非可空到可空)
- `T? → T` (可空到非可空)
- `T? → U?` (可空到可空，不同类型)
- `T → U?` (非可空到不同类型的可空)
- `T? → U` (可空到不同类型的非可空)

#### ✅ 简单类型转换
- 数值类型互转（int, long, double, decimal 等）
- 使用 `Expression.Convert` 实现

**处理方式**：
```csharp
// 生成表达式树，编译时转换
var converted = Expression.Convert(srcAccess, destType);
```

**失败策略**：
- 返回 `null`，由上层决定如何处理
- 不抛出异常，保持优雅降级

---

### 2. **ApplyAdaptOptions** - 运行时属性匹配

位置：`src\Ling.Mapper\Extensions\MapperExtensions.cs` (Line 762)

```csharp
private static void ApplyAdaptOptions<TDestination, TSource>(...)
{
    // 4. 如果启用了特殊匹配规则，需要手动赋值
    if ((options.IgnoreCase || options.IgnoreUnderscore) && srcValue != null)
    {
        try
        {
            // 尝试类型转换
            if (destProp.PropertyType.IsAssignableFrom(srcProp.PropertyType))
            {
                destProp.SetValue(dest, srcValue);
            }
            else if (destProp.PropertyType == srcProp.PropertyType)
            {
                destProp.SetValue(dest, srcValue);
            }
        }
        catch
        {
            // 类型不兼容，跳过 - 静默失败
        }
    }
}
```

**处理方式**：
- 使用 `IsAssignableFrom` 检查类型兼容性
- 使用 `try-catch` 捕获运行时异常
- **静默失败** - 跳过不兼容的属性

**问题**：
- ❌ 没有日志记录
- ❌ 开发者无法感知失败
- ❌ 调试困难

---

### 3. **CreateInstance** - 对象实例化

位置：`src\Ling.Mapper\Extensions\MapperExtensions.cs` (Line 598)

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
            $"无法创建类型 '{typeof(T).FullName}' 的实例：该类型没有无参构造函数。...", ex);
    }
    catch (System.MemberAccessException ex)
    {
        throw new System.MemberAccessException(
            $"无法创建类型 '{typeof(T).FullName}' 的实例：构造函数不可访问...", ex);
    }
    catch (System.Exception ex)
    {
        throw new System.InvalidOperationException(
            $"创建类型 '{typeof(T).FullName}' 的实例时发生异常：{ex.Message}...", ex);
    }
}
```

**处理方式**：
- **立即失败** - 抛出详细异常
- 提供清晰的错误消息
- 帮助开发者快速定位问题

---

### 4. **Mapper.Map** - 核心映射逻辑

位置：`src\Ling.Mapper\Mapper\Mapper.cs`

#### 4.1 Null 值处理

```csharp
public object? Map(object? source, Type sourceType, Type destType)
{
    if (source == null)
    {
        // 如果目标类型是非可空值类型，返回默认值
        if (destType.IsValueType && Nullable.GetUnderlyingType(destType) == null)
        {
            return Activator.CreateInstance(destType);
        }
        return null;
    }
    // ...
}
```

#### 4.2 循环引用处理

```csharp
// v2.1.3: 运行时循环引用检测
if (!sourceType.IsValueType && !TypeUtils.IsSimple(sourceType))
{
    var context = _mappingContext.Value;
    
    // 检查是否已经在映射这个对象实例
    if (context.TryGetValue(source, out var existingResult))
    {
        return existingResult;  // 返回已映射的对象，打破循环
    }
    
    // 标记正在映射
    context[source] = null!;
    // ...
}
```

**处理方式**：
- **优雅降级** - 返回已映射对象
- 防止 StackOverflow
- 使用字典缓存已映射对象

---

## 🎯 改进方案

### 方案 1: Adapt 自动识别集合

让 `Adapt` 方法检测目标类型是否为集合，自动选择集合映射逻辑。

#### 实现思路

```csharp
public static TDestination? Adapt<TDestination>(
    this object source, 
    System.Action<TDestination?, object>? custom = null)
{
    var mapper = MapperProvider.Current;
    var destType = typeof(TDestination);
    
    // 🆕 检测是否为集合类型
    if (IsCollectionType(destType) && source is IEnumerable sourceEnumerable)
    {
        return (TDestination?)AdaptCollection(sourceEnumerable, destType, mapper, custom);
    }
    
    // 原有的单对象映射逻辑
    var dest = mapper.Map<TDestination>(source);
    // ...
}

private static bool IsCollectionType(Type type)
{
    // 检查是否为 List<T>, IList<T>, IEnumerable<T> 等
    if (!type.IsGenericType) return false;
    
    var genericDef = type.GetGenericTypeDefinition();
    return genericDef == typeof(List<>) ||
           genericDef == typeof(IList<>) ||
           genericDef == typeof(ICollection<>) ||
           genericDef == typeof(IEnumerable<>);
}
```

#### 使用示例

```csharp
// ✅ 之前：需要明确调用 AdaptList
var dtos = entities.AdaptList<UserDto>();

// ✅ 现在：自动识别
var dtos = entities.Adapt<List<UserDto>>();
var dtos = entities.Adapt<IEnumerable<UserDto>>();
var dtos = entities.Adapt<IList<UserDto>>();

// ✅ 带回调
var dtos = entities.Adapt<List<UserDto>>((dto, entity) =>
{
    // 处理整个列表
    if (dto != null && dto is List<UserDto> list)
    {
        foreach (var item in list)
        {
            // 处理每一项
        }
    }
});
```

#### 优缺点

**优点**：
- ✅ API 统一，只需记住 `Adapt`
- ✅ 自动识别，减少认知负担
- ✅ 保留 `AdaptList` 作为显式 API（向后兼容）

**缺点**：
- ⚠️ 运行时类型检测，轻微性能开销
- ⚠️ 回调参数变为整个列表（与 AdaptList 的元素级回调不同）

---

### 方案 2: 增强类型不匹配处理

#### 2.1 添加可选的日志回调

```csharp
public class AdaptOptions
{
    // 🆕 类型不匹配时的回调
    public Action<string, Type, Type>? OnTypeMismatch { get; set; }
    
    // 🆕 属性映射失败时的回调
    public Action<string, Exception>? OnPropertyMapFailed { get; set; }
}
```

#### 2.2 改进 ApplyAdaptOptions

```csharp
private static void ApplyAdaptOptions<TDestination, TSource>(...)
{
    try
    {
        if (destProp.PropertyType.IsAssignableFrom(srcProp.PropertyType))
        {
            destProp.SetValue(dest, srcValue);
        }
        else if (destProp.PropertyType == srcProp.PropertyType)
        {
            destProp.SetValue(dest, srcValue);
        }
        else
        {
            // 🆕 类型不匹配，通知调用者
            options.OnTypeMismatch?.Invoke(
                destProp.Name,
                srcProp.PropertyType,
                destProp.PropertyType
            );
        }
    }
    catch (Exception ex)
    {
        // 🆕 属性映射失败，通知调用者
        options.OnPropertyMapFailed?.Invoke(destProp.Name, ex);
    }
}
```

#### 2.3 使用示例

```csharp
var options = new AdaptOptions
{
    IgnoreCase = true,
    OnTypeMismatch = (propName, srcType, destType) =>
    {
        Console.WriteLine($"⚠️ 属性 '{propName}' 类型不匹配: {srcType.Name} -> {destType.Name}");
    },
    OnPropertyMapFailed = (propName, ex) =>
    {
        Console.WriteLine($"❌ 属性 '{propName}' 映射失败: {ex.Message}");
    }
};

var dto = entity.Adapt<UserDto>(options);
```

---

## 📊 类型不匹配处理策略总结

| 场景 | 策略 | 位置 | 优点 | 缺点 |
|------|------|------|------|------|
| **枚举转换** | 编译时生成表达式 | TypeConversionHelper | 性能高，类型安全 | 仅支持常见转换 |
| **可空类型** | 编译时生成表达式 | TypeConversionHelper | 性能高，类型安全 | 仅支持可空 ↔ 非可空 |
| **简单类型** | Expression.Convert | TypeConversionHelper | 性能高 | 不兼容类型会失败 |
| **属性匹配** | Try-Catch 静默失败 | ApplyAdaptOptions | 不会中断映射 | 无法感知失败 |
| **实例化** | 立即抛出详细异常 | CreateInstance | 错误清晰 | 不够优雅 |
| **循环引用** | 返回已映射对象 | Mapper.Map | 防止崩溃 | 可能返回不完整对象 |

---

## 🚀 推荐实施顺序

### Phase 1: 集合自动识别 (高优先级)
1. 实现 `IsCollectionType` 辅助方法
2. 修改 `Adapt<TDestination>` 方法添加集合检测
3. 保留 `AdaptList` 方法（向后兼容）
4. 编写单元测试
5. 更新文档

### Phase 2: 类型不匹配回调 (中优先级)
1. 在 `AdaptOptions` 添加回调属性
2. 修改 `ApplyAdaptOptions` 调用回调
3. 添加使用示例
4. 更新文档

### Phase 3: 增强错误报告 (低优先级)
1. 添加可选的详细日志模式
2. 创建 `MappingDiagnostics` 类收集错误
3. 提供诊断工具

---

## 💡 最佳实践建议

### 对于开发者

1. **使用 Adapt 即可**
   ```csharp
   // 推荐：统一使用 Adapt
   var dto = entity.Adapt<UserDto>();
   var dtos = entities.Adapt<List<UserDto>>();
   ```

2. **需要元素级控制时使用 AdaptList**
   ```csharp
   // 需要对每个元素单独处理
   var dtos = entities.AdaptList<UserDto>((dto, entity, index) =>
   {
       dto.RowNumber = index + 1;
   });
   ```

3. **类型不匹配时启用回调**
   ```csharp
   var options = new AdaptOptions
   {
       OnTypeMismatch = LogTypeMismatch,
       OnPropertyMapFailed = LogMappingError
   };
   var dto = entity.Adapt<UserDto>(options);
   ```

### 对于库维护者

1. **优先编译时转换** - 性能最优
2. **运行时转换使用 try-catch** - 防止崩溃
3. **提供回调机制** - 让使用者决定错误处理
4. **保持向后兼容** - 不移除现有 API

---

## 📝 相关文件

- `src\Ling.Mapper\Extensions\MapperExtensions.cs` - 扩展方法
- `src\Ling.Mapper\Mapper\TypeConversionHelper.cs` - 类型转换
- `src\Ling.Mapper\Mapper\Mapper.cs` - 核心映射逻辑
- `src\Ling.Mapper\Models\AdaptOptions.cs` - 映射选项
- `src\Ling.Mapper\Utils\TypeUtils.cs` - 类型工具

---

## 📅 更新日期

2024年（版本 v2.3.0+）
