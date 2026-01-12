# 枚举类型转换支持文档

## ?? 目录

- [概述](#概述)
- [支持的转换场景](#支持的转换场景)
- [使用示例](#使用示例)
- [转换规则说明](#转换规则说明)
- [注意事项](#注意事项)
- [最佳实践](#最佳实践)

---

## 概述

Ling.Mapper 从 v1.0.6 开始全面支持枚举类型的转换，包括：
- ? 枚举 ? 整数 (enum ? int)
- ? 枚举 ? 字符串 (enum ? string)
- ? 枚举 ? 枚举 (enum ? enum)
- ? 可空枚举的所有转换场景 (enum? ? ...)

所有枚举转换都在运行时自动处理，无需额外配置。

---

## 支持的转换场景

### 1. 枚举 ? 整数

| 源类型 | 目标类型 | 支持 | 说明 |
|--------|----------|------|------|
| `enum` | `int` | ? | 直接转换为枚举的整数值 |
| `int` | `enum` | ? | 整数值转换为对应的枚举 |
| `enum?` | `int` | ? | null 转换为 0 |
| `int` | `enum?` | ? | 包装为可空枚举 |
| `enum?` | `int?` | ? | 保持 null 状态 |
| `int?` | `enum?` | ? | 保持 null 状态 |

### 2. 枚举 ? 字符串

| 源类型 | 目标类型 | 支持 | 说明 |
|--------|----------|------|------|
| `enum` | `string` | ? | 转换为枚举名称字符串 |
| `string` | `enum` | ? | 字符串解析为枚举（不区分大小写） |
| `enum?` | `string` | ? | null 转换为 null |
| `string` | `enum?` | ? | null/空字符串转换为 null |

### 3. 枚举 ? 枚举

| 源类型 | 目标类型 | 支持 | 说明 |
|--------|----------|------|------|
| `EnumA` | `EnumA` | ? | 相同类型直接复制 |
| `EnumA` | `EnumB` | ? | 通过整数值转换（如果值匹配） |
| `EnumA?` | `EnumB` | ? | null 转换为目标枚举的默认值(0) |
| `EnumA?` | `EnumB?` | ? | 保持 null 状态 |

---

## 使用示例

### 示例 1: 枚举转整数

```csharp
public enum UserStatus
{
    Inactive = 0,
    Active = 1,
    Pending = 2
}

public class Source
{
    public UserStatus Status { get; set; }
}

public class Target
{
    public int Status { get; set; }
}

// 使用
var source = new Source { Status = UserStatus.Active };
var target = source.Adapt<Target>();
Console.WriteLine(target.Status); // 输出: 1
```

### 示例 2: 整数转枚举

```csharp
public class Source
{
    public int StatusCode { get; set; }
}

public class Target
{
    public UserStatus StatusCode { get; set; }
}

// 使用
var source = new Source { StatusCode = 1 };
var target = source.Adapt<Target>();
Console.WriteLine(target.StatusCode); // 输出: Active
```

### 示例 3: 枚举转字符串

```csharp
public class Source
{
    public UserStatus Status { get; set; }
}

public class Target
{
    public string Status { get; set; }
}

// 使用
var source = new Source { Status = UserStatus.Active };
var target = source.Adapt<Target>();
Console.WriteLine(target.Status); // 输出: "Active"
```

### 示例 4: 字符串转枚举

```csharp
public class Source
{
    public string Status { get; set; }
}

public class Target
{
    public UserStatus Status { get; set; }
}

// 使用
var source = new Source { Status = "Active" };
var target = source.Adapt<Target>();
Console.WriteLine(target.Status); // 输出: Active

// 不区分大小写
var source2 = new Source { Status = "active" };
var target2 = source2.Adapt<Target>();
Console.WriteLine(target2.Status); // 输出: Active
```

### 示例 5: 可空枚举转整数

```csharp
public class Source
{
    public UserStatus? Status { get; set; }
}

public class Target
{
    public int Status { get; set; }
}

// 有值的情况
var source1 = new Source { Status = UserStatus.Active };
var target1 = source1.Adapt<Target>();
Console.WriteLine(target1.Status); // 输出: 1

// null 的情况
var source2 = new Source { Status = null };
var target2 = source2.Adapt<Target>();
Console.WriteLine(target2.Status); // 输出: 0
```

### 示例 6: 不同枚举类型转换

```csharp
public enum UserStatus
{
    Inactive = 0,
    Active = 1,
    Pending = 2
}

public enum OrderStatus
{
    Pending = 0,
    Completed = 1,
    Cancelled = 2
}

public class Source
{
    public UserStatus Status { get; set; }
}

public class Target
{
    public OrderStatus Status { get; set; }
}

// 使用 - 通过整数值转换
var source = new Source { Status = UserStatus.Active }; // 值为 1
var target = source.Adapt<Target>();
Console.WriteLine(target.Status); // 输出: Completed (值也是 1)
```

### 示例 7: API 集成场景

```csharp
// API 返回的 DTO（使用整数）
public class ApiUserDto
{
    public int Status { get; set; }  // 0, 1, 2
    public string Name { get; set; }
}

// 领域模型（使用枚举）
public class User
{
    public UserStatus Status { get; set; }
    public string Name { get; set; }
}

// 自动转换
var apiResponse = new ApiUserDto 
{ 
    Status = 1, 
    Name = "John" 
};

var user = apiResponse.Adapt<User>();
Console.WriteLine(user.Status); // 输出: Active
```

---

## 转换规则说明

### 1. 枚举 → 整数

- **原理**: 直接获取枚举的底层整数值
- **性能**: 极快，编译时生成转换代码
- **null 处理**: 
  - `enum?` → `int`: null 转换为 `0`
  - `enum?` → `int?`: null 保持为 null

### 2. 整数 → 枚举

- **原理**: 将整数值转换为对应的枚举成员
- **验证**: 不会验证整数是否为有效的枚举值
- **未定义值**: 如果整数值在枚举中未定义，仍然会转换（类似于 `(UserStatus)999`）
- **null 处理**:
  - `int` → `enum?`: 包装为可空枚举
  - `int?` → `enum`: null 转换为枚举的默认值（通常是 0）

### 3. 枚举 → 字符串

- **原理**: 调用 `ToString()` 方法获取枚举名称
- **输出**: 枚举成员的名称字符串（如 "Active"）
- **null 处理**: `enum?` → `string`: null 转换为 null

### 4. 字符串 → 枚举

- **原理**: 使用 `Enum.Parse()` 解析字符串
- **大小写**: 不区分大小写（ignoreCase: true）
- **异常**: 如果字符串不是有效的枚举名称，会抛出异常
- **null 处理**: 
  - `string` → `enum?`: null/空字符串转换为 null
  - `string` → `enum`: null 会抛出异常

### 5. 枚举 → 枚举

- **同类型**: 直接复制
- **不同类型**: 通过整数值作为中间类型转换
  ```
  EnumA → int → EnumB
  ```
- **注意**: 只有当两个枚举的成员值相同时，转换才有意义

---

## 注意事项

### ?? 字符串转枚举的异常

```csharp
public class Source
{
    public string Status { get; set; }
}

public class Target
{
    public UserStatus Status { get; set; }
}

// ? 会抛出异常
var source = new Source { Status = "InvalidValue" };
var target = source.Adapt<Target>(); // ArgumentException

// ? 建议使用可空枚举
public class SafeTarget
{
    public UserStatus? Status { get; set; }
}

// 或使用 try-catch
try
{
    var target = source.Adapt<Target>();
}
catch (ArgumentException)
{
    // 处理无效的枚举字符串
}
```

### ?? 整数转枚举的未定义值

```csharp
public class Source
{
    public int Status { get; set; }
}

public class Target
{
    public UserStatus Status { get; set; }
}

// 整数值 999 在 UserStatus 中未定义
var source = new Source { Status = 999 };
var target = source.Adapt<Target>();
Console.WriteLine(target.Status); // 输出: 999 (未定义的枚举值)
Console.WriteLine(Enum.IsDefined(typeof(UserStatus), target.Status)); // False
```

### ?? 不同枚举类型转换

```csharp
public enum UserStatus
{
    Active = 1,
    Inactive = 2
}

public enum OrderStatus
{
    Pending = 1,
    Completed = 2
}

var source = new Source { Status = UserStatus.Active }; // 值为 1
var target = source.Adapt<Target>();
Console.WriteLine(target.Status); // 输出: Pending (也是 1)

// ?? 虽然值相同，但语义完全不同！
```

---

## 最佳实践

### 1. API 集成时使用枚举

```csharp
// ? 推荐：在领域模型中使用枚举
public class User
{
    public UserStatus Status { get; set; }
}

// DTO 使用整数，自动转换
public class UserDto
{
    public int Status { get; set; }
}

var dto = new UserDto { Status = 1 };
var user = dto.Adapt<User>(); // 自动转换
```

### 2. 可空枚举用于可选状态

```csharp
// ? 使用可空枚举表示"未设置"状态
public class User
{
    public UserStatus? Status { get; set; } // null 表示未设置
}

// 从 API 映射
public class ApiUser
{
    public int? StatusCode { get; set; }
}

var apiUser = new ApiUser { StatusCode = null };
var user = apiUser.Adapt<User>();
Console.WriteLine(user.Status == null); // True
```

### 3. 字符串转枚举时的安全处理

```csharp
// ? 方法 1：使用可空枚举
public class Target
{
    public UserStatus? Status { get; set; }
}

// ? 方法 2：使用自定义转换
CreateMap<Source, Target>()
    .ForMember(d => d.Status, s => 
        Enum.TryParse<UserStatus>(s.StatusText, true, out var status) 
            ? status 
            : UserStatus.Inactive);
```

### 4. 不同枚举转换时添加明确映射

```csharp
// ? 避免：隐式转换不同语义的枚举
var user = new User { Status = UserStatus.Active };
var order = user.Adapt<Order>(); // Status 语义不同！

// ? 推荐：显式映射
CreateMap<User, Order>()
    .ForMember(d => d.Status, s => 
        s.Status == UserStatus.Active 
            ? OrderStatus.Pending 
            : OrderStatus.Cancelled);
```

### 5. 使用枚举提高代码可读性

```csharp
// ? 使用魔术数字
if (user.Status == 1)
{
    // ...
}

// ? 使用枚举
if (user.Status == UserStatus.Active)
{
    // ...
}
```

---

## 性能说明

枚举转换的性能：

| 转换类型 | 性能 | 说明 |
|---------|------|------|
| enum ? int | ??? 极快 | 编译时生成直接转换代码 |
| enum → string | ?? 快 | 调用 ToString() |
| string → enum | ? 一般 | 运行时解析字符串 |
| enum ? enum | ??? 极快 | 通过整数中转 |

**建议**：
- 优先使用 `enum` ? `int` 转换（最快）
- 避免频繁使用 `string` → `enum` 转换（相对较慢）
- 如果性能关键，考虑缓存字符串到枚举的映射

---

## 相关文档

- [README.md](../README.md) - 项目主文档
- [可空类型支持文档](NullableTypes_Support.md) - 可空类型转换规则
- [API 参考文档](API_Reference.md) - 完整 API 列表

---

## 更新日志

- **v1.0.6** (2024): 添加完整的枚举类型转换支持

---

<div align="center">

**枚举转换让 API 集成更简单！** ?

Made with ?? by [yanhuuo](https://github.com/yanhuuo)

</div>
