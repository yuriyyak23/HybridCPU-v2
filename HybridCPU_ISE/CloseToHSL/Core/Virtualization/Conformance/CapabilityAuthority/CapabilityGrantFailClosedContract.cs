namespace YAKSys_Hybrid_CPU.Core;

public enum CapabilityGrantFailClosedViolation : byte
{
    None = 0,
    MissingDescriptorSet = 1,
    MissingTypedGrantWasGranted = 2,
    OwnerAwareMissingGrantWasGranted = 3,
}

public sealed partial class CapabilityGrantFailClosedContract
{
    public CapabilityGrantFailClosedViolation Evaluate(
        CapabilityDescriptorSet descriptorSet,
        ulong unmappedCapabilityMask)
    {
        if (descriptorSet is null)
        {
            return CapabilityGrantFailClosedViolation.MissingDescriptorSet;
        }

        CapabilityGrant grant = descriptorSet.CreateGrant(
            unmappedCapabilityMask,
            CapabilityGrantScope.DomainGranted);

        if (grant.IsGranted)
        {
            return CapabilityGrantFailClosedViolation.MissingTypedGrantWasGranted;
        }

        CapabilityGrant ownerAwareGrant = descriptorSet.CreateGrant(
            unmappedCapabilityMask,
            CapabilityGrantScope.DomainGranted,
            CapabilityGrant.DefaultRuntimeOwnerDomainId);

        return ownerAwareGrant.IsGranted
            ? CapabilityGrantFailClosedViolation.OwnerAwareMissingGrantWasGranted
            : CapabilityGrantFailClosedViolation.None;
    }

    public bool IsSatisfied(
        CapabilityDescriptorSet descriptorSet,
        ulong unmappedCapabilityMask) =>
        Evaluate(descriptorSet, unmappedCapabilityMask) == CapabilityGrantFailClosedViolation.None;
}
