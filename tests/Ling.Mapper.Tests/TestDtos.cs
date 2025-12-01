using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TestConsole;

public class ActivityDto
{
    public string? FirstName { get; set; }
    public string? LastName { get; set; }
    public int Uid { get; set; }
    public string? ExtraInfoJson { get; set; }
    public List<ActivityItemDto>? Items { get; set; }
    public ActivityUserDto? User { get; set; }
}

public class ActivityItemDto
{
    public string? Key { get; set; }
    public decimal Price { get; set; }
}

public class ActivityUserDto
{
    public string? NickName { get; set; }
}

public class MallActivityEntity
{
    public string? Name { get; set; }
    public int UserId { get; set; }
    public ExtraInfoModel? ExtraInfo { get; set; }
    public List<MallItemEntity>? Items { get; set; }
    public MallUserEntity? User { get; set; }
    public string? InternalCode { get; set; } // ignored
}

public class MallItemEntity
{
    /// <summary>
    /// 
    /// </summary>
    public string? Key { get; set; }
    /// <summary>
    /// 
    /// </summary>
    public decimal Price { get; set; }
}

public class MallUserEntity
{
    /// <summary>
    /// 
    /// </summary>
    public string? NickName { get; set; }
}

public class ExtraInfoModel
{
    public int Level { get; set; }
    public string? Tag { get; set; }
}
