using Ling.Mapper;

namespace TestConsole;

/// <summary>
/// 可空类型映射配置 Profile
/// </summary>
public class NullableTypeProfile : MapperProfile
{
    public NullableTypeProfile()
    {
        // int? → int
        CreateMap<NullableSource, NonNullableTarget>()
            .Rename(d => d.Id, "NullableId");

        // int → int?
        CreateMap<NonNullableSource, NullableTarget>()
            .Rename(d => d.NullableId, "Id");

        // int? → int?
        CreateMap<NullableSource, NullableTarget>();

        // string? 映射
        CreateMap<StringSource, StringTarget>();

        // 混合场景
        CreateMap<MixedSource, MixedTarget>();
    }
}
