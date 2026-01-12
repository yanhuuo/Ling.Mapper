# Adapt 扩展方法使用指南

## 概述

`Adapt` 系列扩展方法提供了灵活的对象映射功能，支持在映射完成后通过匿名函数对结果进行二次加工。特别适用于处理分页结果、列表数据和嵌套对象的场景。

## 功能特性

- ? 支持单对象映射与自定义处理
- ? 支持 List 集合批量映射（多种语法）
- ? 支持嵌套对象的递归映射
- ? 支持在映射后对每个元素进行处理
- ? 支持使用全局 Mapper 或指定特定 Mapper 实例
- ? 提供回调函数访问映射结果和原始数据
- ? 支持 `page.Data.AdaptToList<CustomerDto, Customer>()` 简洁语法

---

## 一、单对象映射 (Adapt)

### 1.1 基本用法

#### 不带回调的简单映射

```csharp
// 使用全局 Mapper
var target = source.Adapt<TargetDto>();

// 使用指定 Mapper
var target = source.Adapt<TargetDto>(mapper);
```

#### 带回调的映射（推荐）

```csharp
var target = source.Adapt<TargetDto>((res, src) =>
{
    // res: 映射后的目标对象
    // src: 原始源对象
    res.CustomField = CalculateValue(src);
});
```

### 1.2 处理分页结果

#### 示例 1：循环处理分页结果中的列表项

```csharp
var page = await query
    .ToPageResultAsync(dto.page ?? 1, dto.size ?? 10)
    .Adapt<PageResult<CustomerDto>>((res, dis) =>
    {
        // 处理分页结果中的每一项
        if (res.Items != null)
        {
            foreach (var item in res.Items)
            {
                // 对每个项进行特殊处理
                item.DisplayName = $"{item.FirstName} {item.LastName}";
                item.FullAddress = FormatAddress(item);
            }
        }
        
        // 也可以修改分页信息
        res.Total = res.Items?.Count ?? 0;
    });
```

#### 示例 2：使用 LINQ 批量处理

```csharp
var page = await query
    .ToPageResultAsync(dto.page ?? 1, dto.size ?? 10)
    .Adapt<PageResult<CustomerDto>>((res, dis) =>
    {
        if (res.Items != null)
        {
            res.Items = res.Items
                .Select((item, index) => 
                {
                    item.RowNumber = index + 1;
                    item.IsEven = index % 2 == 0;
                    return item;
                })
                .ToList();
        }
    });
```

### 1.3 处理嵌套对象

```csharp
var order = sourceOrder.Adapt<OrderDto>((res, src) =>
{
    // 嵌套对象会自动映射
    if (res.Customer != null)
    {
        res.Customer.FullName = $"{res.Customer.FirstName} {res.Customer.LastName}";
    }
    
    // 处理嵌套的列表
    if (res.Items != null)
    {
        foreach (var item in res.Items)
        {
            item.ParentOrderId = res.Id;
            item.LineTotal = item.Quantity * item.UnitPrice;
        }
    }
});
```

---

## 二、List 集合映射 - 多种语法支持

### 2.1 方式一：使用 `AdaptToList<TDto, TEntity>()` （推荐 ?）

这是最简洁的语法，直接将集合转换为 List 类型。

#### 基本用法

```csharp
// 最简洁的写法
var customerDtos = page.Data.AdaptToList<CustomerDto, Customer>();

// 等价于
var customerDtos = page.Data.AdaptList<CustomerDto, Customer>();
```

#### 带回调的用法

```csharp
// 转换整个列表，并在回调中处理
var customerDtos = page.Data.AdaptToList<CustomerDto, Customer>((list, source) =>
{
    // list: 映射后的整个列表
    // source: 原始集合
    for (int i = 0; i < list.Count; i++)
    {
        list[i].RowNumber = i + 1;
        list[i].IsFirst = i == 0;
        list[i].IsLast = i == list.Count - 1;
    }
});
```

#### 实际应用示例

```csharp
// 示例 1：API 返回分页数据
public async Task<PageResult<CustomerDto>> GetCustomers(int page, int size)
{
    var result = await _dbContext.Customers
        .Where(c => c.IsActive)
        .ToPageResultAsync(page, size);
    
    // 直接转换 Data 属性
    result.Data = result.Data.AdaptToList<CustomerDto, Customer>((list, src) =>
    {
        for (int i = 0; i < list.Count; i++)
        {
            list[i].RowNumber = (page - 1) * size + i + 1;
        }
    });
    
    return result;
}

// 示例 2：转换并格式化数据
var products = await _dbContext.Products.ToListAsync();
var productDtos = products.AdaptToList<ProductDto, Product>((list, src) =>
{
    foreach (var product in list)
    {
        product.FormattedPrice = $"?{product.Price:N2}";
        product.Status = product.Stock > 0 ? "有货" : "缺货";
    }
});
```

### 2.2 方式二：使用 `AdaptList<TDto, TEntity>()` 

提供对每个元素的单独处理，并包含索引信息。

```csharp
var customerDtos = sourceList.AdaptList<CustomerDto, Customer>((dto, entity, index) =>
{
    // dto: 当前映射后的元素
    // entity: 当前源元素
    // index: 当前元素的索引（从 0 开始）
    dto.RowNumber = index + 1;
    dto.IsFirst = index == 0;
});
```

### 2.3 语法对比

| 语法 | 适用场景 | 回调参数 | 示例 |
|-----|---------|---------|------|
| `.AdaptToList<TDto, TSrc>()` | 整体处理列表 | `(list, source)` | `page.Data.AdaptToList<CustomerDto, Customer>()` |
| `.AdaptList<TDto, TSrc>()` | 单独处理每个元素 | `(item, source, index)` | `list.AdaptList<CustomerDto, Customer>()` |
| `.AdaptList<TDto>()` | 自动推断源类型 | `(item, source, index)` | `list.AdaptList<CustomerDto>()` |

### 2.4 完整示例：电商订单列表

```csharp
// 场景：获取订单列表并处理
public async Task<List<OrderDto>> GetOrders()
{
    var orders = await _dbContext.Orders
        .Include(o => o.OrderItems)
        .Include(o => o.Customer)
        .ToListAsync();
    
    // 方式 1：使用 AdaptToList<TDto, TEntity>，一次性处理整个列表
    return orders.AdaptToList<OrderDto, Order>((list, source) =>
    {
        // 嵌套的 OrderItems 和 Customer 会自动映射
        for (int i = 0; i < list.Count; i++)
        {
            var order = list[i];
            
            // 生成订单号
            order.OrderNumber = $"ORD-{order.Id:D8}";
            
            // 计算订单总金额
            if (order.Items != null)
            {
                order.TotalAmount = order.Items.Sum(item => item.Quantity * item.UnitPrice);
            }
            
            // 格式化客户名称
            if (order.Customer != null)
            {
                order.Customer.FullName = $"{order.Customer.FirstName} {order.Customer.LastName}";
            }
        }
    });
    
    // 方式 2：使用 AdaptList，逐个处理每个订单
    return orders.AdaptList<OrderDto, Order>((order, entity, index) =>
    {
        order.OrderNumber = $"ORD-{order.Id:D8}";
        order.RowNumber = index + 1;
        
        if (order.Items != null)
        {
            order.TotalAmount = order.Items.Sum(item => item.Quantity * item.UnitPrice);
        }
    });
}
```

---

## 三、实际应用场景

### 3.1 分页查询

```csharp
public async Task<PageResult<CustomerDto>> GetCustomerPage(int page, int size)
{
    var result = await _customerRepository.GetPageAsync(page, size);
    
    // 直接使用 Adapt 转换整个分页对象
    return result.Adapt<PageResult<CustomerDto>>((res, src) =>
    {
        // Items 会自动映射为 List<CustomerDto>
        if (res.Items != null)
        {
            for (int i = 0; i < res.Items.Count; i++)
            {
                res.Items[i].RowNumber = (page - 1) * size + i + 1;
                res.Items[i].DisplayName = FormatName(res.Items[i]);
            }
        }
    });
}
```

### 3.2 只转换 Data 部分

```csharp
public async Task<ApiResponse<List<ProductDto>>> GetProducts()
{
    var products = await _dbContext.Products.ToListAsync();
    
    return new ApiResponse<List<ProductDto>>
    {
        Success = true,
        // 直接转换 Data 属性
        Data = products.AdaptToList<ProductDto, Product>((list, src) =>
        {
            foreach (var product in list)
            {
                product.FormattedPrice = $"?{product.Price:N2}";
            }
        }),
        Message = "查询成功"
    };
}
```

### 3.3 树形结构转换（递归嵌套）

```csharp
public class CategoryDto
{
    public int Id { get; set; }
    public string Name { get; set; }
    public List<CategoryDto>? Children { get; set; }
}

// 转换包含子分类的树形结构
var categories = await _dbContext.Categories
    .Include(c => c.Children)
    .Where(c => c.ParentId == null)
    .ToListAsync();

var categoryDtos = categories.AdaptToList<CategoryDto, Category>((list, src) =>
{
    // Children 会自动递归映射
    foreach (var category in list)
    {
        if (category.Children != null && category.Children.Any())
        {
            // 可以对子节点进行额外处理
            foreach (var child in category.Children)
            {
                child.ParentName = category.Name;
            }
        }
    }
});
```

### 3.4 条件处理

```csharp
var result = sourceList.AdaptToList<OrderDto, Order>((list, src) =>
{
    foreach (var order in list)
    {
        // 根据条件进行不同的处理
        if (order.TotalAmount > 1000)
        {
            order.Discount = 0.9m;
            order.Level = "VIP";
        }
        else
        {
            order.Discount = 1.0m;
            order.Level = "普通";
        }
        
        // 格式化日期
        order.OrderDateFormatted = order.OrderDate.ToString("yyyy-MM-dd HH:mm:ss");
    }
});
```

### 3.5 访问外部数据

```csharp
// 从外部数据源补充信息
var productIds = sourceList.Select(x => x.ProductId).ToList();
var productNames = await GetProductNamesAsync(productIds);

var result = sourceList.AdaptToList<OrderItemDto, OrderItem>((list, src) =>
{
    foreach (var item in list)
    {
        // 从外部字典中获取产品名称
        if (productNames.TryGetValue(item.ProductId, out var name))
        {
            item.ProductName = name;
        }
    }
});
```

---

## 四、高级用法

### 4.1 组合使用 Adapt 和 AdaptToList

```csharp
var page = await query
    .ToPageResultAsync(dto.page ?? 1, dto.size ?? 10)
    .Adapt<PageResult<OrderDto>>((res, dis) =>
    {
        // 先映射整个分页对象，然后处理列表
        if (res.Items != null)
        {
            // 可以在这里使用 LINQ 进行二次转换
            res.Items = res.Items
                .Select((order, index) => 
                {
                    // 处理每个订单的嵌套项
                    if (order.Items != null)
                    {
                        foreach (var item in order.Items)
                        {
                            item.LineNumber = index + 1;
                            item.TotalPrice = item.Quantity * item.UnitPrice;
                        }
                    }
                    return order;
                })
                .ToList();
        }
    });
```

### 4.2 链式调用

```csharp
var result = (await _dbContext.Orders
    .Where(o => o.Status == OrderStatus.Completed)
    .ToListAsync())
    .AdaptToList<OrderDto, Order>((list, src) =>
    {
        list.ForEach(o => o.StatusText = "已完成");
    });
```

---

## 五、注意事项

### 5.1 全局 Mapper 配置

使用无参数版本的 `Adapt` 前，需要先配置全局 Mapper：

```csharp
var config = new MapperConfiguration();
config.AddProfile(new MyMapperProfile());
var mapper = config.CreateMapper();

// 设置全局 Mapper
MapperProvider.SetCurrent(mapper);

// 现在可以使用无参数版本
var result = source.Adapt<TargetDto>();
var list = source.AdaptToList<TargetDto, SourceDto>();
```

### 5.2 嵌套对象的映射配置

确保在 MapperProfile 中配置了嵌套对象的映射：

```csharp
public class OrderProfile : MapperProfile
{
    public OrderProfile()
    {
        // 配置 Order 到 OrderDto 的映射
        CreateMap<Order, OrderDto>();
        
        // 配置嵌套对象的映射
        CreateMap<OrderItem, OrderItemDto>();
        CreateMap<Customer, CustomerDto>();
        
        // List 会自动映射，无需额外配置
    }
}
```

### 5.3 性能考虑

- 对于大量数据的转换，建议使用 `AdaptToList<TDto, TSrc>()` 而不是在循环中调用 `Adapt`
- 回调函数中避免进行数据库查询等耗时操作
- 对于深层嵌套的对象，注意控制递归深度

### 5.4 类型推断

```csharp
// ? 推荐：明确指定类型
var list = source.AdaptToList<CustomerDto, Customer>();

// ? 也支持：自动推断源类型
var list = source.AdaptList<CustomerDto>();
```

---

## 六、完整示例

### 示例：电商订单系统

```csharp
// 实体类
public class Order
{
    public int Id { get; set; }
    public DateTime OrderDate { get; set; }
    public Customer Customer { get; set; }
    public List<OrderItem> Items { get; set; }
}

public class OrderItem
{
    public int Id { get; set; }
    public int ProductId { get; set; }
    public int Quantity { get; set; }
    public decimal UnitPrice { get; set; }
}

// DTO 类
public class OrderDto
{
    public int Id { get; set; }
    public string OrderNumber { get; set; }
    public CustomerDto Customer { get; set; }
    public List<OrderItemDto> Items { get; set; }
    public decimal TotalAmount { get; set; }
}

// 使用示例
public async Task<PageResult<OrderDto>> GetOrders(int page, int size)
{
    var query = _dbContext.Orders
        .Include(o => o.Customer)
        .Include(o => o.Items)
        .OrderByDescending(o => o.OrderDate);
    
    var pageResult = await query.ToPageResultAsync(page, size);
    
    // 方式 1：转换整个分页对象
    return pageResult.Adapt<PageResult<OrderDto>>((res, src) =>
    {
        if (res.Items != null)
        {
            foreach (var order in res.Items)
            {
                // 生成订单号
                order.OrderNumber = $"ORD-{order.Id:D8}";
                
                // 计算订单总金额
                if (order.Items != null)
                {
                    order.TotalAmount = order.Items.Sum(item => 
                        item.Quantity * item.UnitPrice);
                    
                    // 为每个订单项设置行号
                    for (int i = 0; i < order.Items.Count; i++)
                    {
                        order.Items[i].LineNumber = i + 1;
                    }
                }
                
                // 格式化客户名称
                if (order.Customer != null)
                {
                    order.Customer.FullName = 
                        $"{order.Customer.FirstName} {order.Customer.LastName}";
                }
            }
        }
    });
    
    // 方式 2：只转换 Data 部分
    var result = new PageResult<OrderDto>
    {
        Page = pageResult.Page,
        Size = pageResult.Size,
        Total = pageResult.Total,
        Data = pageResult.Data.AdaptToList<OrderDto, Order>((list, src) =>
        {
            for (int i = 0; i < list.Count; i++)
            {
                var order = list[i];
                order.OrderNumber = $"ORD-{order.Id:D8}";
                order.RowNumber = i + 1;
                
                if (order.Items != null)
                {
                    order.TotalAmount = order.Items.Sum(item => 
                        item.Quantity * item.UnitPrice);
                }
            }
        })
    };
    
    return result;
}
```

---

## 七、方法签名参考

### Adapt 方法重载

```csharp
// 1. 基本映射（使用全局 Mapper）
TDestination? Adapt<TDestination>(this object source, Action<TDestination?, object>? custom = null)

// 2. 基本映射（指定 Mapper）
TDestination? Adapt<TDestination>(this object source, IMapper mapper, Action<TDestination?, object>? custom = null)

// 3. 指定源类型（使用全局 Mapper）
TDestination? Adapt<TDestination, TSource>(this TSource source, Action<TDestination?, TSource>? custom)

// 4. 指定源类型（指定 Mapper）
TDestination? Adapt<TDestination, TSource>(this TSource source, IMapper mapper, Action<TDestination?, TSource>? custom)
```

### AdaptToList 方法重载 ? 新增

```csharp
// 1. List 映射（指定源和目标类型，使用全局 Mapper）
List<TDestination>? AdaptToList<TDestination, TSource>(
    this IEnumerable<TSource>? source, 
    Action<List<TDestination>?, IEnumerable<TSource>>? custom = null)

// 2. List 映射（指定源和目标类型，指定 Mapper）
List<TDestination>? AdaptToList<TDestination, TSource>(
    this IEnumerable<TSource>? source, 
    IMapper mapper,
    Action<List<TDestination>?, IEnumerable<TSource>>? custom = null)
```

### AdaptList 方法重载

```csharp
// 1. List 映射（指定源和目标类型，使用全局 Mapper）
List<TDestination>? AdaptList<TDestination, TSource>(
    this IEnumerable<TSource>? source, 
    Action<TDestination?, TSource, int>? custom = null)

// 2. List 映射（指定源和目标类型，指定 Mapper）
List<TDestination>? AdaptList<TDestination, TSource>(
    this IEnumerable<TSource>? source, 
    IMapper mapper,
    Action<TDestination?, TSource, int>? custom = null)

// 3. List 映射（自动推断源类型，使用全局 Mapper）
List<TDestination>? AdaptList<TDestination>(
    this IEnumerable? source, 
    Action<TDestination?, object, int>? custom = null)
```

---

## 八、常见问题

### Q1: `AdaptToList<TDto, TSrc>()` 和 `AdaptList<TDto, TSrc>()` 有什么区别？

- `AdaptToList<TDto, TSrc>()`: 回调函数接收整个列表，适合批量处理，语法更简洁
- `AdaptList<TDto, TSrc>()`: 回调函数对每个元素单独调用，适合逐个处理并包含索引信息

### Q2: 如何选择使用哪种方式？

- 需要访问索引或对每个元素单独处理 → 使用 `AdaptList<TDto, TSrc>()`
- 需要批量处理或简洁语法 → 使用 `AdaptToList<TDto, TSrc>()`

### Q3: 是否支持异步映射？

当前版本不直接支持异步回调。如需异步操作，建议先同步映射，再使用 `async/await` 处理。

### Q4: 如何处理 null 值？

所有方法都会自动处理 null 值，返回 null 而不会抛出异常。

---

## 九、相关文档

- [MapperProfile 配置指南](./MapperProfile-Guide.md)
- [类型转换器注册](./TypeConverter-Guide.md)
- [依赖注入集成](./DI-Integration.md)

---

**最后更新**: 2025-01-10  
**版本**: 1.0.4  
**新增**: 支持 `page.Data.AdaptToList<TDto, TEntity>()` 简洁语法
