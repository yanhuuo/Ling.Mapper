# Adapt 扩展方法功能总结

## ? 已完成的功能

### 1. 修复中文乱码问题
在 `Ling.Mapper.csproj` 中添加了 UTF-8 编码支持：
```xml
<Utf8Output>true</Utf8Output>
<LangVersion>latest</LangVersion>
```

### 2. 支持多样化的转换方式

#### 方式一：单对象转换（原有功能）
```csharp
// 基本转换
var dto = entity.Adapt<CustomerDto>();

// 带回调处理
var dto = entity.Adapt<CustomerDto>((res, src) =>
{
    res.DisplayName = $"{res.FirstName} {res.LastName}";
});
```

#### 方式二：List 转换 - AdaptToList（新增 ?）
```csharp
// 推荐写法 - 简洁明了
var dtos = page.Data.AdaptToList<CustomerDto, Customer>();

// 带回调处理整个列表
var dtos = page.Data.AdaptToList<CustomerDto, Customer>((list, src) =>
{
    for (int i = 0; i < list.Count; i++)
    {
        list[i].RowNumber = i + 1;
    }
});
```

#### 方式三：List 转换 - AdaptList（原有功能增强）
```csharp
// 对每个元素单独处理，包含索引
var dtos = list.AdaptList<CustomerDto, Customer>((dto, entity, index) =>
{
    dto.RowNumber = index + 1;
    dto.IsFirst = index == 0;
});

// 自动推断源类型
var dtos = list.AdaptList<CustomerDto>((dto, src, index) =>
{
    dto.RowNumber = index + 1;
});
```

### 3. 支持嵌套对象和集合的自动映射

```csharp
// 订单包含订单项和客户信息
var orderDtos = orders.AdaptToList<OrderDto, Order>((list, src) =>
{
    foreach (var order in list)
    {
        // 嵌套的 Items 和 Customer 会自动映射
        if (order.Items != null)
        {
            order.TotalAmount = order.Items.Sum(i => i.Quantity * i.UnitPrice);
        }
        
        if (order.Customer != null)
        {
            order.Customer.FullName = $"{order.Customer.FirstName} {order.Customer.LastName}";
        }
    }
});
```

### 4. 支持层层嵌套的递归映射

```csharp
// 树形结构（分类包含子分类）
var categories = await dbContext.Categories
    .Include(c => c.Children)
    .ToListAsync();

var categoryDtos = categories.AdaptToList<CategoryDto, Category>((list, src) =>
{
    // Children 会自动递归映射
    foreach (var category in list)
    {
        if (category.Children != null)
        {
            foreach (var child in category.Children)
            {
                child.ParentName = category.Name;
            }
        }
    }
});
```

---

## ?? 使用场景对比

### 场景 1：分页数据转换

```csharp
// 转换整个分页对象
var page = await query.ToPageResultAsync(1, 10)
    .Adapt<PageResult<CustomerDto>>((res, src) =>
    {
        // 处理列表中的每一项
        if (res.Items != null)
        {
            foreach (var item in res.Items)
            {
                item.DisplayName = FormatName(item);
            }
        }
    });

// 或者只转换 Data 部分
result.Data = result.Data.AdaptToList<CustomerDto, Customer>((list, src) =>
{
    // 批量处理
    list.ForEach(c => c.DisplayName = FormatName(c));
});
```

### 场景 2：需要索引信息

```csharp
// 使用 AdaptList，回调中包含索引参数
var dtos = list.AdaptList<CustomerDto, Customer>((dto, entity, index) =>
{
    dto.RowNumber = index + 1;
    dto.IsEven = index % 2 == 0;
});
```

### 场景 3：简单批量转换

```csharp
// 使用 AdaptToList，最简洁
var dtos = list.AdaptToList<CustomerDto, Customer>();
```

---

## ?? 新增文件

1. **src\Ling.Mapper\Extensions\CollectionAdaptExtensions.cs**
   - 提供 `AdaptToList<TDestination, TSource>()` 扩展方法
   - 支持整体处理列表的回调函数

2. **docs\Adapt-Usage.md**
   - 详细的使用指南
   - 包含所有使用场景的示例代码
   - 方法签名参考
   - 常见问题解答

3. **tests\Ling.Mapper.Tests\AdaptListDemo.cs**
   - 演示各种 List 转换方式的示例代码
   - 可直接运行查看效果

---

## ?? 方法签名总览

### AdaptToList (新增)
```csharp
// 使用全局 Mapper
List<TDestination>? AdaptToList<TDestination, TSource>(
    this IEnumerable<TSource>? source, 
    Action<List<TDestination>?, IEnumerable<TSource>>? custom = null)

// 使用指定 Mapper
List<TDestination>? AdaptToList<TDestination, TSource>(
    this IEnumerable<TSource>? source, 
    IMapper mapper,
    Action<List<TDestination>?, IEnumerable<TSource>>? custom = null)
```

### AdaptList (原有 + 增强)
```csharp
// 指定源和目标类型
List<TDestination>? AdaptList<TDestination, TSource>(
    this IEnumerable<TSource>? source, 
    Action<TDestination?, TSource, int>? custom = null)

// 自动推断源类型
List<TDestination>? AdaptList<TDestination>(
    this IEnumerable? source, 
    Action<TDestination?, object, int>? custom = null)
```

### Adapt (原有)
```csharp
// 单对象转换
TDestination? Adapt<TDestination>(
    this object source, 
    Action<TDestination?, object>? custom = null)

// 指定源类型
TDestination? Adapt<TDestination, TSource>(
    this TSource source, 
    Action<TDestination?, TSource>? custom)
```

---

## ?? 最佳实践

### 1. 选择合适的方法

- **简单转换** → 使用 `AdaptToList<TDto, TEntity>()`
- **需要索引** → 使用 `AdaptList<TDto, TEntity>()`
- **复杂对象** → 使用 `Adapt<TDto>()` 然后处理嵌套

### 2. 性能优化

```csharp
// ? 推荐：一次性转换
var dtos = list.AdaptToList<CustomerDto, Customer>();

// ? 避免：循环中多次调用
var dtos = new List<CustomerDto>();
foreach (var item in list)
{
    dtos.Add(item.Adapt<CustomerDto>()); // 不推荐
}
```

### 3. 类型安全

```csharp
// ? 推荐：明确指定类型
var dtos = list.AdaptToList<CustomerDto, Customer>();

// ?? 可用但不够清晰
var dtos = list.AdaptList<CustomerDto>();
```

---

## ?? 与其他 Mapper 库对比

### AutoMapper
```csharp
// AutoMapper 的写法
var dtos = _mapper.Map<List<CustomerDto>>(customers);
```

### Ling.Mapper
```csharp
// Ling.Mapper 的写法 - 更灵活
var dtos = customers.AdaptToList<CustomerDto, Customer>((list, src) =>
{
    // 可以在映射后进行额外处理
    list.ForEach(c => c.DisplayName = FormatName(c));
});
```

**优势**：
- ? 支持映射后的回调处理
- ? 类型安全且明确
- ? 支持嵌套对象自动映射
- ? 支持递归结构（树形数据）
- ? 轻量级，性能优秀

---

## ?? 注意事项

1. **全局 Mapper 配置**
   ```csharp
   var config = new MapperConfiguration();
   config.AddProfile(new MyProfile());
   MapperProvider.SetCurrent(config.CreateMapper());
   ```

2. **嵌套对象映射配置**
   ```csharp
   public class OrderProfile : MapperProfile
   {
       public OrderProfile()
       {
           CreateMap<Order, OrderDto>();
           CreateMap<OrderItem, OrderItemDto>(); // 配置嵌套类型
           CreateMap<Customer, CustomerDto>();
       }
   }
   ```

3. **UTF-8 编码**
   - 已在项目文件中配置 `<Utf8Output>true</Utf8Output>`
   - 打包后的 XML 文档不会出现中文乱码

---

## ?? 快速开始

```csharp
// 1. 配置 Mapper
var config = new MapperConfiguration();
config.AddProfile(new CustomerProfile());
MapperProvider.SetCurrent(config.CreateMapper());

// 2. 使用 AdaptToList 转换列表
var customers = await dbContext.Customers.ToListAsync();
var customerDtos = customers.AdaptToList<CustomerDto, Customer>((list, src) =>
{
    for (int i = 0; i < list.Count; i++)
    {
        list[i].RowNumber = i + 1;
        list[i].DisplayName = $"{list[i].FirstName} {list[i].LastName}";
    }
});

// 3. 使用 Adapt 转换单个对象
var customer = await dbContext.Customers.FirstAsync();
var customerDto = customer.Adapt<CustomerDto>((dto, src) =>
{
    dto.DisplayName = $"{dto.FirstName} {dto.LastName}";
});
```

---

**版本**: 1.0.4  
**最后更新**: 2025-01-10  
**主要改进**: 
- 支持 `AdaptToList<TDto, TEntity>()` 语法
- 修复中文编码问题
- 支持嵌套和递归映射
- 完善文档和示例
