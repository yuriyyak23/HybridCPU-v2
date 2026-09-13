// Description: Neutral capability-boundary requirement that requires typed grants; compatibility bit masks are projection-only.
namespace YAKSys_Hybrid_CPU.Core;

public readonly record struct CapabilityBoundaryRequirement(
    ulong CapabilityMask,
    CapabilityGrantScope Scope,
    bool RequiresTypedGrant)
{
    public static CapabilityBoundaryRequirement None { get; } =
        new(
            CapabilityMask: 0,
            Scope: CapabilityGrantScope.None,
            RequiresTypedGrant: false);

    public static CapabilityBoundaryRequirement TypedGrant(
        ulong capabilityMask,
        CapabilityGrantScope scope) =>
        new(
            capabilityMask,
            scope,
            RequiresTypedGrant: true);

    public bool IsSatisfiedBy(CapabilityDescriptorSet descriptorSet)
    {
        if (CapabilityMask == 0)
        {
            return !RequiresTypedGrant;
        }

        if (!RequiresTypedGrant)
        {
            return false;
        }

        return descriptorSet.TypedGrants.TryGetGrant(CapabilityMask, Scope, out CapabilityGrant grant) &&
               grant.HasTypedAuthority;
    }
}
