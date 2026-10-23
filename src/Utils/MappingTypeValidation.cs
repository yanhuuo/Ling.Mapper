namespace Ling.Mapper.Utils;

/// <summary>Shared runtime/generated validation without reflection-based mapping.</summary>
internal static class MappingTypeValidation
{
    internal static void Validate(Type type, string parameter)
    {
        ArgumentNullException.ThrowIfNull(type, parameter);
#if NET8_0_OR_GREATER
        if (type.IsFunctionPointer)
            throw new ArgumentException("Mapping requires a closed, boxable runtime type.", parameter);
#endif
        if (type == typeof(void) || type.IsByRef || type.IsPointer || type.IsByRefLike || type.ContainsGenericParameters)
            throw new ArgumentException("Mapping requires a closed, boxable runtime type.", parameter);
    }
}
