namespace Ling.Mapper.Configuration;

/// <summary>Generated lookup boundary retained for internal compatibility.</summary>
internal static class GeneratedMapperFactoryProxy
{
    public static bool TryGetMapper(Type source, Type destination, out Func<object, object?>? mapper)
    {
        if (!System.Runtime.CompilerServices.RuntimeFeature.IsDynamicCodeSupported && GeneratedMapperRegistry.TryGet(source, destination, out _))
        {
            var generated = GeneratedMapperRegistry.CreateMapper();
            mapper = value => generated.Map(value, source, destination);
            return true;
        }
        mapper = null;
        return false;
    }
}
