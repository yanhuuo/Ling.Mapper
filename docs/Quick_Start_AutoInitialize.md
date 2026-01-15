## MapperProvider 自动初始化 - 快速开始

### ✨ 零配置使用

```csharp
using Ling.Mapper;

// 定义 DTO
public class UserEntity
{
    public int Id { get; set; }
    public string Name { get; set; }
    public string Email { get; set; }
}

public class UserDto
{
    public int Id { get; set; }
    public string Name { get; set; }
    public string Email { get; set; }
}

// 🎉 直接使用，无需任何配置！
var user = new UserEntity { Id = 1, Name = "张三", Email = "test@example.com" };
var dto = user.Adapt<UserDto>();

Console.WriteLine($"用户: {dto.Name}, 邮箱: {dto.Email}");
```

### 🚀 对比旧版本

#### ❌ 旧版本（繁琐）

```csharp
// 必须先创建和设置 Mapper
var config = new MapperConfiguration();
var mapper = config.CreateMapper();
MapperProvider.SetCurrent(mapper);

// 然后才能使用
var dto = user.Adapt<UserDto>();
```

#### ✅ 新版本（简洁）

```csharp
// 直接使用！
var dto = user.Adapt<UserDto>();
```

### 📖 更多示例

#### 带回调的映射

```csharp
var dto = user.Adapt<UserDto, UserEntity>((dest, src) =>
{
    dest.DisplayName = $"{src.Name} ({src.Email})";
});
```

#### 列表映射

```csharp
var users = new List<UserEntity> { /* ... */ };
var dtos = users.AdaptList<UserDto>();
```

#### 自定义配置（可选）

```csharp
// 如果需要自定义配置，仍然可以手动设置
var config = new MapperConfiguration();
config.ConfigureConventions(opt => opt.CaseInsensitiveNameMatch = true);
MapperProvider.SetCurrent(config.CreateMapper());

// 之后使用的是自定义配置
var dto = user.Adapt<UserDto>();
```

### 🎯 推荐使用场景

✅ **推荐自动初始化**：
- 简单的 DTO 转换
- 快速原型开发
- 单元测试
- 不需要特殊配置

⚠️ **推荐手动设置**：
- 需要自定义映射规则
- 需要注册类型转换器
- 复杂的企业应用

### 📚 完整文档

查看 [MapperProvider_AutoInitialize.md](MapperProvider_AutoInitialize.md) 了解更多技术细节。
