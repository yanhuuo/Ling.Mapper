using Ling.Mapper;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TestConsole;

public class ActivityProfile : MapperProfile
{
    public ActivityProfile()
    {
        CreateMap<ActivityDto, MallActivityEntity>()
            // Name = FirstName + LastName
            .ForMember(d => d.Name, s => s.FirstName + " " + s.LastName)

            // Uid -> UserId
            .Rename(d => d.UserId, "Uid")

            // 忽略 InternalCode
            .Ignore(d => d.InternalCode)
            
            // 生成反向映射
            .ReverseMap();
    }
}
