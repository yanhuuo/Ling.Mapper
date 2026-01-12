using Ling.Mapper;

namespace TestConsole;

/// <summary>
/// ”√ªß”≥…‰≈‰÷√ Profile
/// </summary>
public class UserProfile : MapperProfile
{
    public UserProfile()
    {
        // ≈‰÷√ UserSourceDto µΩ UserTargetDto µƒ”≥…‰
        CreateMap<UserSourceDto, UserTargetDto>();
    }
}
