using System;
using System.Linq.Expressions;
using Ling.Mapper;
using Ling.Mapper.Configuration;

var configuration = new MapperConfiguration();
configuration.AddProfile(new Profile());
var mapped = configuration.CreateMapper().Map<Destination>(new Source())!;
if (mapped.Value != 41 || mapped.Text != "legacy" || mapped.Ignored != "initial")
    throw new Exception("Legacy ForMember behavior changed.");
try
{
    new MappingExpression<Source, Destination>().ForMember(d => d.Value, (Expression<Func<Source, int>>)null!);
    throw new Exception("Typed null source selector should be rejected.");
}
catch (ArgumentNullException) { }
try
{
    new MappingExpression<Source, Destination>().ForMember(d => d.Value, null!);
    throw new Exception("Untyped null source selector should be rejected.");
}
catch (ArgumentNullException error) when (error.ParamName == "srcExpr") { }
Console.WriteLine("Legacy ForMember consumer: ordinary expressions, method-name collision, typed/untyped null passed.");

public sealed class Source
{
    public int Ignore() => 41;
    public string Text => "legacy";
    public string Ignored => "source";
}
public sealed class Destination
{
    public int Value { get; set; }
    public string? Text { get; set; }
    public string? Ignored { get; set; } = "initial";
}
public sealed class Profile : MapperProfile
{
    public Profile() => CreateMap<Source, Destination>()
        .ForMember(d => d.Value, s => s.Ignore())
        .ForMember(destMember: d => d.Text, srcExpr: s => s.Text)
        .Ignore(d => d.Ignored!);
}
