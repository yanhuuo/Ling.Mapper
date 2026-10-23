namespace Ling.Mapper;

/// <summary>Explicitly requests a source-generated mapping pair. Add the generator
/// as an analyzer; this attribute alone does not generate code. Runtime Profiles
/// and converters are not interpreted by the source generator.</summary>
[AttributeUsage(AttributeTargets.Assembly, AllowMultiple = true)]
public sealed class GeneratedMapAttribute : Attribute
{
    /// <summary>Source type to generate.</summary>
    public Type SourceType { get; }
    /// <summary>Destination type to generate.</summary>
    public Type DestinationType { get; }
    /// <summary>Declare an explicit generated pair.</summary>
    public GeneratedMapAttribute(Type sourceType, Type destinationType)
    {
        SourceType = sourceType;
        DestinationType = destinationType;
    }
}
