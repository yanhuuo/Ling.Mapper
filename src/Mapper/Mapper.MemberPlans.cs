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
            // This member call always has exact-pair eligibility, no callback and
            // no ignores. Keep its runtime/update/version guards and complete plan.
            ref var cachedPlan = ref slot.Plan;
            if (context.IsUpdating || value == null || value.GetType() != source ||
                TypeConverterRegistry.Version != version)
                return MapValue(value, source, destination, options, null, null, context);
            // The slow MapValue branch deliberately stays outside this catch,
            // matching the original collection helper's exception boundary.
            try
            {
                var plan = System.Threading.Volatile.Read(ref cachedPlan);
                if (plan == null)
                {
                    plan = GetOrCompile(source, destination, options, null);
                    System.Threading.Volatile.Write(ref cachedPlan, plan);
                }
                return plan(value, null, context);
            }
            catch (MappingException error) when (ReferenceEquals(error.Origin, context.Origin)) { throw; }
            catch (Exception error) { throw MappingException.Create(source, destination, context.GetPath(), value, error, _config, context.IsSensitive, context.Origin); }
        }
        finally { context.Pop(); }
    }
}
