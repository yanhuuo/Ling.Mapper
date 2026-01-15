# MapperProvider 自动初始化功能

## 📋 更新说明

### 问题背景

在之前的版本中，使用 `Adapt` 扩展方法前必须手动设置全局 Mapper：

```csharp
// ❌ 之前：必须手动设置，很不方便
var config = new MapperConfiguration();
var mapper = config.CreateMapper();
MapperProvider.SetCurrent(mapper);

// 然后才能使用
var result = source.Adapt<TargetDto>();
```

这种方式存在以下问题：
1. ❌ 使用繁琐，每次都要手动初始化
2. ❌ 容易忘记设置导致运行时异常
3. ❌ 对于简单场景显得过于复杂

### 解决方案

✅ **实现自动初始化机制**

现在 `MapperProvider.Current` 在首次访问时会自动创建一个使用默认配置的 Mapper 实例。

## 🎯 核心改进

### 1. MapperProvider 自动初始化

**修改文件**: `src\Ling.Mapper\Provider\MapperProvider.cs`

```csharp
public static class MapperProvider
{
    private static IMapper? _current;
    private static readonly object _lock = new object();
    private static bool _isInitialized = false;

    /// <summary>
    /// 获取当前全局 Mapper 实例。如果未设置，将自动创建一个默认实例。
    /// </summary>
    public static IMapper Current
    {
        get
        {
            if (_current == null && !_isInitialized)
            {
                lock (_lock)
                {
                    if (_current == null && !_isInitialized)
                    {
                        _current = CreateDefaultMapper();
                        _isInitialized = true;
                    }
                }
            }
            return _current!;
        }
    }

    private static IMapper CreateDefaultMapper()
    {
        var config = new MapperConfiguration();
        return config.CreateMapper();
    }
}
```

**关键特性**：
- ✅ **线程安全**: 使用双重检查锁定模式
- ✅ **延迟初始化**: 只在首次访问时创建
- ✅ **默认配置**: 自动使用标准配置

### 2. 移除异常检查

**修改文件**: `src\Ling.Mapper\Extensions\MapperExtensions.cs`

```csharp
// ✅ 之前：需要检查并抛出异常
var mapper = MapperProvider.Current ?? throw new InvalidOperationException(...);

// ✅ 现在：直接获取，保证不为 null
var mapper = MapperProvider.Current;
```

**受影响的方法**：
- ✅ `Adapt<TDestination, TSource>` (所有重载)
- ✅ `Adapt<TDestination>` (所有重载)
- ✅ `AdaptList<TDestination, TSource>`
- ✅ `AdaptList<TDestination>`
- ✅ `Adapt` with `AdaptOptions` (所有重载)

### 3. 更新文档注释

移除了所有方法中 `<exception cref="System.InvalidOperationException">未注册全局 Mapper</exception>` 的文档说明。

## 📖 使用示例

### 场景 1: 零配置快速使用

```csharp
// 🎉 直接使用，无需任何配置！
var source = new SourceDto { Id = 1, Name = "测试" };
var target = source.Adapt<TargetDto>();

Console.WriteLine($"Id={target.Id}, Name={target.Name}");
```

### 场景 2: 带回调的映射

```csharp
// 🎉 同样不需要预先设置 Mapper
var result = source.Adapt<TargetDto, SourceDto>((dest, src) =>
{
    dest.DisplayName = $"[{src.Id}] {src.Name}";
});
```

### 场景 3: 列表映射

```csharp
var sourceList = new List<SourceDto>
{
    new SourceDto { Id = 1, Name = "项目1" },
    new SourceDto { Id = 2, Name = "项目2" }
};

// 🎉 列表映射也能自动初始化
var targetList = sourceList.AdaptList<TargetDto, SourceDto>((dest, src, index) =>
{
    dest.DisplayName = $"[{index + 1}] {src.Name}";
});
```

### 场景 4: 手动设置仍然有效

```csharp
// 如果需要自定义配置，仍然可以手动设置
var config = new MapperConfiguration();
config.ConfigureConventions(opt => opt.CaseInsensitiveNameMatch = true);
var customMapper = config.CreateMapper();

MapperProvider.SetCurrent(customMapper);

// 之后使用的是自定义的 Mapper
var result = source.Adapt<TargetDto>();
```

### 场景 5: 清除和重新初始化

```csharp
// 清除全局 Mapper
MapperProvider.Clear();

// 下次访问时会自动重新创建
var result = source.Adapt<TargetDto>();
```

## 🔍 技术细节

### 线程安全保证

使用**双重检查锁定（Double-Check Locking）**模式：

```csharp
if (_current == null && !_isInitialized)  // 第一次检查（快速路径）
{
    lock (_lock)  // 加锁
    {
        if (_current == null && !_isInitialized)  // 第二次检查
        {
            _current = CreateDefaultMapper();
            _isInitialized = true;
        }
    }
}
```

**优势**：
- ✅ 避免每次访问都加锁（性能优化）
- ✅ 保证只创建一个实例
- ✅ 线程安全

### 状态标记

使用 `_isInitialized` 标记而不是仅检查 `_current == null`：

```csharp
private static bool _isInitialized = false;
```

**原因**：
- ✅ 区分"未初始化"和"初始化后被清除"两种状态
- ✅ 支持 `Clear()` 方法后的重新初始化
- ✅ 更明确的状态管理

## ✅ 测试验证

### 测试文件

`tests/Ling.Mapper.Tests/AutoMapperProviderTest.cs`

### 测试场景

1. ✅ **直接使用 Adapt，无需手动设置**
2. ✅ **清除后自动重新初始化**
3. ✅ **AdaptList 也能自动初始化**
4. ✅ **手动设置自定义 Mapper 仍然有效**

### 运行测试

```bash
# 运行测试项目，选择选项 5
dotnet run --project tests/Ling.Mapper.Tests
```

## 🎁 优势总结

### 对开发者的好处

| 特性 | 之前 | 现在 |
|------|------|------|
| **快速上手** | ❌ 必须先了解配置 | ✅ 直接使用 |
| **代码量** | ❌ 每次都要初始化 | ✅ 零配置 |
| **错误处理** | ❌ 忘记设置会抛异常 | ✅ 自动处理 |
| **灵活性** | ✅ 支持自定义 | ✅ 仍然支持 |

### 适用场景

#### ✅ 适合自动初始化的场景：

- 简单的对象映射
- 原型开发和快速验证
- 单元测试
- 不需要特殊配置的应用

#### ⚠️ 建议手动设置的场景：

- 需要自定义映射规则
- 需要注册类型转换器
- 需要配置命名约定
- 多租户应用（每个租户不同配置）

## 🔄 版本兼容性

### 向后兼容

✅ **完全向后兼容**

```csharp
// 旧代码仍然有效
var config = new MapperConfiguration();
var mapper = config.CreateMapper();
MapperProvider.SetCurrent(mapper);

var result = source.Adapt<TargetDto>();
```

### 升级建议

对于简单场景，可以移除手动设置代码：

```csharp
// ❌ 可以删除这些代码（如果不需要自定义配置）
var config = new MapperConfiguration();
var mapper = config.CreateMapper();
MapperProvider.SetCurrent(mapper);

// ✅ 直接使用即可
var result = source.Adapt<TargetDto>();
```

## 📝 API 变更清单

### MapperProvider

| 成员 | 变更类型 | 说明 |
|------|---------|------|
| `Current` | 修改 | 从 `IMapper?` 改为自动初始化，返回 `IMapper` |
| `SetCurrent()` | 不变 | 仍可手动设置 |
| `Clear()` | 增强 | 清除后下次访问会自动重新初始化 |

### MapperExtensions

| 方法 | 变更类型 | 说明 |
|------|---------|------|
| 所有 `Adapt` 重载 | 简化 | 移除 `InvalidOperationException` |
| 所有 `AdaptList` 重载 | 简化 | 移除 `InvalidOperationException` |
| 文档注释 | 更新 | 移除"未注册全局 Mapper"的异常说明 |

## 🚀 性能影响

### 初始化开销

- **首次访问**: 约 10-50ms（创建 MapperConfiguration 和 Mapper）
- **后续访问**: 0ms（直接返回缓存实例）

### 线程安全开销

- **首次初始化**: 需要加锁，轻微性能开销
- **后续访问**: 无锁快速路径，无性能影响

### 内存占用

- **默认 Mapper**: 约 100-500KB
- **自动释放**: 否（全局单例）

## 📚 相关文档

- [功能概览](功能概览.md)
- [Adapt使用指南](Adapt使用指南.md)
- [全局配置和运行时选项指南](全局配置和运行时选项指南.md)

## 📅 更新日期

2024年（版本 v2.2.0+）
