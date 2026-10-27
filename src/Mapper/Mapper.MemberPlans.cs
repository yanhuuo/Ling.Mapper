using Ling.Mapper.Models;

namespace Ling.Mapper.Mapper;

internal sealed partial class Mapper
{
    // One slot per compiled member expression, never per source/result object.
    private sealed class MemberPlanSlot
    {
        internal MappingPlan? Plan;
    }

    private object? MapKnownMember(object? value, Type source, Type destination, AdaptOptions options,
        string name, bool sensitive, MappingContext context, long version, MemberPlanSlot slot)
    {
        context.Push(name, sensitive);
        try
        {
            return MapKnownCollectionValue(value, source, destination, options, null, null, context,
                true, version, ref slot.Plan);
        }
        finally { context.Pop(); }
    }
}
