// See https://aka.ms/new-console-template for more information
using Ling.Mapper;
using TestConsole;

Console.WriteLine("=== Ling.Mapper Test Console ===");

//1. 注册 JSON 转换器：string <-> ExtraInfoModel
TypeConverterRegistry.RegisterJson<ExtraInfoModel>();

//2. 配置 Mapper
var cfg = new MapperConfiguration();
cfg.AddProfile(new ActivityProfile());
cfg.AddProfile(new CustomerDemoProfile());
cfg.AddProfile(new UserProfile()); // 添加用户映射配置
cfg.AddProfile(new NullableTypeProfile()); // 添加可空类型映射配置
cfg.ConfigureConventions(opt =>
{
    //目标属性名称不分大小写
    opt.CaseInsensitiveNameMatch = true;
});

var mapper = cfg.CreateMapper();
// register global default
MapperProvider.SetCurrent(mapper);

//3. 构建 DTO 数据用于测试
var dto = new ActivityDto
{
    FirstName = "Tom",
    LastName = "Lee",
    Uid = 1001,
    ExtraInfoJson = "{\"Level\":3,\"Tag\":\"VIP\"}",
    Items = new List<ActivityItemDto>
    {
        new ActivityItemDto { Key = "A", Price = 99 },
        new ActivityItemDto { Key = "B", Price = 199 }
    },
    User = new ActivityUserDto { NickName = "SuperTom" }
};

//4. 执行映射
var entity = dto.Adapt<MallActivityEntity, ActivityDto>((src, dest) =>
{
    if (dest != null)
    {
        dest.Name = src.FirstName + " - MappedInAction";
    }
});

//5. 输出测试结果
Console.WriteLine("\n=== 映射结果 ===");
Console.WriteLine($"Name: {entity?.Name}");
Console.WriteLine($"UserId: {entity?.UserId}");
Console.WriteLine($"ExtraInfo.Level: {entity?.ExtraInfo?.Level}");
Console.WriteLine($"ExtraInfo.Tag: {entity?.ExtraInfo?.Tag}");
Console.WriteLine($"User.NickName: {entity?.User?.NickName}");
Console.WriteLine("Items:");
if (entity?.Items != null)
{
    foreach (var item in entity.Items)
    {
        Console.WriteLine($" - Key: {item.Key}, Price: {item.Price}");
    }
}
Console.WriteLine("InternalCode (should be null due to Ignore): " + entity?.InternalCode);

//6. 执行映射
var entity2 = dto.Adapt<MallActivityEntity>();


var result = dto.Adapt<MallActivityEntity, ActivityDto>(mapper, (src, dest) =>
{
    if (dest != null)
    {
        // 自定义映射逻辑（覆盖自动映射）
        dest.Name = src.FirstName + " - CustomMap";
        dest.UserId = src.Uid + 999;
    }
});

Console.WriteLine("\n==== 测试 Adapt(Action)结果 ====");
Console.WriteLine("Name: " + result?.Name);
Console.WriteLine("UserId: " + result?.UserId);

// 7. 演示 List 转换功能
AdaptListDemo.Run();

// 8. 演示异常处理行为
ExceptionHandlingTest.Run();

// 9. 演示忽略属性功能
IgnorePropertiesDemo.Run();

// 10. 演示 AdaptOptions 映射规则功能
AdaptOptionsDemo.Run();

// 11. 演示可空类型映射功能
NullableTypeDemo.Run();

Console.WriteLine("\n=== Test Completed ===");
Console.ReadLine();