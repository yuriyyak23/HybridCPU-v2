namespace YAKSys_Hybrid_CPU.Core;

public enum CapabilityNegotiationDecision : byte
{
    Granted = 0,
    DeniedEmptyMask = 1,
    DeniedMissingDescriptorSet = 2,
    DeniedMissingHardwareCapability = 3,
    DeniedRuntimeDisabled = 4,
    DeniedDomainNotGranted = 5,
}

public readonly record struct CapabilityNegotiationResult(
    CapabilityNegotiationDecision Decision,
    CapabilityGrant Grant)
{
    public bool IsGranted => Decision == CapabilityNegotiationDecision.Granted && Grant.IsGranted;

    public static CapabilityNegotiationResult Denied(CapabilityNegotiationDecision decision) =>
        new(decision, CapabilityGrant.Denied);
}

public sealed partial class CapabilityNegotiationService
{
    public CapabilityNegotiationResult Negotiate(
        CapabilityDescriptorSet? descriptorSet,
        ulong capabilityMask,
        CapabilityGrantScope scope = CapabilityGrantScope.DomainGranted) =>
        Negotiate(descriptorSet, capabilityMask, CapabilityGrant.DefaultRuntimeOwnerDomainId, scope);

    public CapabilityNegotiationResult Negotiate(
        CapabilityDescriptorSet? descriptorSet,
        ulong capabilityMask,
        ulong ownerDomainId,
        CapabilityGrantScope scope = CapabilityGrantScope.DomainGranted)
    {
        if (capabilityMask == 0)
        {
            return CapabilityNegotiationResult.Denied(CapabilityNegotiationDecision.DeniedEmptyMask);
        }

        if (descriptorSet is null)
        {
            return CapabilityNegotiationResult.Denied(CapabilityNegotiationDecision.DeniedMissingDescriptorSet);
        }

        if (!descriptorSet.HasHardwareCapability(capabilityMask))
        {
            return CapabilityNegotiationResult.Denied(CapabilityNegotiationDecision.DeniedMissingHardwareCapability);
        }

        if (!descriptorSet.IsRuntimeEnabled(capabilityMask))
        {
            return CapabilityNegotiationResult.Denied(CapabilityNegotiationDecision.DeniedRuntimeDisabled);
        }

        if (!descriptorSet.IsDomainGranted(capabilityMask))
        {
            return CapabilityNegotiationResult.Denied(CapabilityNegotiationDecision.DeniedDomainNotGranted);
        }

        return new CapabilityNegotiationResult(
            CapabilityNegotiationDecision.Granted,
            new CapabilityGrant(
                capabilityMask,
                scope,
                isGranted: true,
                ownerDomainId,
                CapabilityDelegationPolicy.NonDelegable,
                CapabilityRevocationPolicy.RuntimeRevocable,
                CapabilityMigrationClass.DomainLocal,
                CapabilityEvidenceVisibility.HostOnly,
                CapabilityFrontendProjectionPolicy.ProjectIfCompatible));
    }

    public CapabilityGrant NegotiateGrant(
        CapabilityDescriptorSet? descriptorSet,
        ulong capabilityMask,
        CapabilityGrantScope scope = CapabilityGrantScope.DomainGranted) =>
        Negotiate(descriptorSet, capabilityMask, scope).Grant;

    public CapabilityGrant NegotiateGrant(
        CapabilityDescriptorSet? descriptorSet,
        ulong capabilityMask,
        ulong ownerDomainId,
        CapabilityGrantScope scope = CapabilityGrantScope.DomainGranted) =>
        Negotiate(descriptorSet, capabilityMask, ownerDomainId, scope).Grant;

    public ulong ProjectEffectiveCompatibilityCaps(CapabilityDescriptorSet? descriptorSet) =>
        descriptorSet?.CompatibilityCapsProjection ?? 0UL;
}
