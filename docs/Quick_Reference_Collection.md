# 快速参考：集合自动识别与类型不匹配处理

## 🎯 集合自动识别 (v2.3)

### 基本用法

```csharp
var entities = GetEntities();

// ✅ 自动识别 List<T>
var dtos = entities.Adapt<List<UserDto>>();

// ✅ 自动识别 IEnumerable<T>
var dtos = entities.Adapt<IEnumerable<UserDto>>();

// ✅ 自动识别数组
var dtos = entities.Adapt<UserDto[]>();
```

### 对比

| 方式 | 代码 | 回调参数 | 适用场景 |
|------|------|----------|---------|
| **Adapt** | `entities.Adapt<List<UserDto>>()` | 整个列表 | 通用场景 |
| **AdaptList** | `entities.AdaptList<UserDto>()` | 每个元素 + 索引 | 需要元素级控制 |

### 示例

```csharp
// 场景 1: 简单映射
var dtos = entities.Adapt<List<UserDto>>();

// 场景 2: 列表级后处理
var dtos = entities.Adapt<List<UserDto>>((list, source) =>
{
    if (list != null)
    {
        foreach (var dto in list)
        {
            dto.DisplayName = $"用户: {dto.Name}";
        }
    }
});

// 场景 3: 元素级控制（使用 AdaptList）
var dtos = entities.AdaptList<UserDto>((dto, entity, index) =>
{
    dto.RowNumber = index + 1;
    dto.DisplayName = $"[{index}] {entity.Name}";
});
```

---

## 🔍 类型不匹配处理机制

### 处理策略

| 场景 | 如何处理 | 失败后果 |
|------|---------|----------|
| **枚举转换** | 编译时表达式 | 返回 null，优雅降级 |
| **可空类型** | 编译时表达式 | 返回 null，优雅降级 |
| **简单类型** | Expression.Convert | 返回 null，优雅降级 |
| **属性匹配** | Try-Catch | 静默跳过不兼容属性 |
| **实例化失败** | 抛出详细异常 | 中断映射，提示错误 |
| **循环引用** | 返回缓存对象 | 防止崩溃，可能不完整 |

### 支持的类型转换

#### ✅ 枚举转换
```csharp
enum Status { Active = 1, Inactive = 2 }

// enum → int
int value = Status.Active.Adapt<int>(); // 1

// int → enum
Status status = 1.Adapt<Status>(); // Status.Active

// enum → string
string text = Status.Active.Adapt<string>(); // "Active"

// string → enum
Status status = "Active".Adapt<Status>(); // Status.Active
```

#### ✅ 可空类型转换
```csharp
// int → int?
int? nullable = 42.Adapt<int?>(); // 42

// int? → int
int value = nullable.Adapt<int>(); // 42

// int? → long?
long? longValue = nullable.Adapt<long?>(); // 42L
```

#### ✅ 数值类型转换
```csharp
// int → long
long longValue = 42.Adapt<long>(); // 42L

// float → double
double doubleValue = 3.14f.Adapt<double>(); // 3.14
```

#### ❌ 不支持的转换（静默失败）
```csharp
// 字符串 → 复杂对象（无法自动转换）
// 属性会被跳过，不会抛异常
```

---

## 🛠️ 调试技巧

### 1. 检查映射结果

```csharp
var dto = entity.Adapt<UserDto>();

if (dto == null)
{
    Console.WriteLine("映射失败：结果为 null");
}
else
{
    // 检查关键属性
    if (dto.Id == 0)
    {
        Console.WriteLine("警告：Id 未映射");
    }
}
```

### 2. 使用显式映射检查类型

```csharp
// 对于可能失败的映射，使用 TryMap
if (mapper.TryMap<UserDto>(entity, out var dto))
{
    Console.WriteLine($"映射成功: {dto.Name}");
}
else
{
    Console.WriteLine("映射失败");
}
```

### 3. 日志记录（未来版本）

```csharp
// 未来版本将支持
var options = new AdaptOptions
{
    OnTypeMismatch = (prop, srcType, destType) => 
    {
        Console.WriteLine($"⚠️ 属性 {prop} 类型不匹配");
        Console.WriteLine($"   源: {srcType.Name}");
        Console.WriteLine($"   目标: {destType.Name}");
    },
    OnPropertyMapFailed = (prop, ex) => 
    {
        Console.WriteLine($"❌ 属性 {prop} 映射失败: {ex.Message}");
    }
};

var dto = entity.Adapt<UserDto>(options);
```

---

## ⚡ 性能建议

### 1. 大批量数据

```csharp
// ✅ 使用显式 AdaptList，避免重复类型检查
var dtos = entities.AdaptList<UserDto>();

// ⚠️ 自动检测有轻微开销
var dtos = entities.Adapt<List<UserDto>>();
```

### 2. 性能关键场景

```csharp
// ✅ 使用 MapTo 跳过集合检测
var dto = entity.MapTo<UserDto>(mapper);

// ⚠️ Adapt 会进行类型检查
var dto = entity.Adapt<UserDto>();
```

### 3. 预编译映射

```csharp
// ✅ 提前配置映射规则
var config = new MapperConfiguration();
config.CreateMap<UserEntity, UserDto>();
var mapper = config.CreateMapper();

// 后续映射会更快
var dto = entity.MapTo<UserDto>(mapper);
```

---

## 🎓 最佳实践

### ✅ 推荐做法

```csharp
// 1. 统一使用 Adapt
var dto = entity.Adapt<UserDto>();
var dtos = entities.Adapt<List<UserDto>>();

// 2. 需要元素级控制时使用 AdaptList
var dtos = entities.AdaptList<UserDto>((dto, entity, index) =>
{
    dto.Index = index;
});

// 3. DTO 类型添加无参构造函数
public class UserDto
{
    public UserDto() { } // 推荐
    public int Id { get; set; }
    public string Name { get; set; }
}

// 4. 使用相同属性名（或配置映射规则）
public class UserEntity
{
    public int Id { get; set; }      // 与 UserDto.Id 匹配
    public string Name { get; set; }  // 与 UserDto.Name 匹配
}
```

### ❌ 避免做法

```csharp
// ❌ 不要依赖静默失败的属性映射
// 如果属性类型不兼容，应该显式配置转换

// ❌ 不要在 DTO 使用私有构造函数
public class UserDto
{
    private UserDto() { } // ❌ 会导致实例化失败
}

// ❌ 不要在热路径上频繁创建 Mapper
// 应该复用 Mapper 实例
for (int i = 0; i < 10000; i++)
{
    var config = new MapperConfiguration(); // ❌ 性能差
    var mapper = config.CreateMapper();
    var dto = entity.MapTo<UserDto>(mapper);
}
```

---

## 📚 相关文档

- [完整文档](Adapt_Collection_Auto_Detection.md)
- [完成总结](Collection_Auto_Detection_Complete.md)
- [Adapt使用指南](Adapt使用指南.md)

---

**快速测试**

```bash
dotnet run --project tests/Ling.Mapper.Tests

# 选择选项 6 - 集合自动识别测试
```
