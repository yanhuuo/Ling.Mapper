using Ling.Mapper;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TestConsole.Test;

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
            .Ignore(d => d.InternalCode);

        // Name is a computed concatenation, not an invertible member path.
        // Define the reverse direction explicitly instead of guessing name splitting.
        CreateMap<MallActivityEntity, ActivityDto>()
            .Rename(d => d.Uid, "UserId")
            .Ignore(d => d.FirstName!)
            .Ignore(d => d.LastName!);
    }
}
