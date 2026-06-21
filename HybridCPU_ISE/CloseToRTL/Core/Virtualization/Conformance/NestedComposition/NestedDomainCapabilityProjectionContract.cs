using YAKSys_Hybrid_CPU.Core.Nested;

namespace YAKSys_Hybrid_CPU.Core;

public enum NestedDomainCapabilityProjectionViolation : byte
{
    None = 0,
    MissingGenericCapability = 1,
    MissingFrontendProjectionMask = 2,
    MissingOwnerDomain = 3,
    MissingTypedAuthority = 4,
}

public sealed partial class NestedDomainCapabilityProjectionContract
{
    public NestedDomainCapabilityProjectionViolation Evaluate(
        NestedCapabilityPublication publication)
    {
        if (publication.Requirement.Capability == NestedDomainCapability.None)
        {
            return NestedDomainCapabilityProjectionViolation.MissingGenericCapability;
        }

        if (publication.Requirement.CapabilityMask == 0)
        {
            return NestedDomainCapabilityProjectionViolation.MissingFrontendProjectionMask;
        }

        if (publication.Requirement.RequiredGrant.OwnerDomainId == CapabilityGrant.NoOwnerDomainId)
        {
            return NestedDomainCapabilityProjectionViolation.MissingOwnerDomain;
        }

        return publication.Requirement.IsTypedAuthority
            ? NestedDomainCapabilityProjectionViolation.None
            : NestedDomainCapabilityProjectionViolation.MissingTypedAuthority;
    }

    public bool IsSatisfied(NestedCapabilityPublication publication) =>
        Evaluate(publication) == NestedDomainCapabilityProjectionViolation.None;
}
