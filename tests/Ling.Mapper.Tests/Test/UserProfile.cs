using Ling.Mapper;

namespace TestConsole.Test;

/// <summary>
/// 用户映射配置 Profile
/// </summary>
public class UserProfile : MapperProfile
{
    public UserProfile()
    {
        // 配置 UserSourceDto 到 UserTargetDto 的映射
        CreateMap<UserSourceDto, UserTargetDto>();
    }
}
