namespace YAKSys_Hybrid_CPU.Core;

public sealed partial class CapabilityDescriptorSet
{
    public CapabilityDescriptorSet()
        : this(CapabilityGrantCollection.Empty)
    {
    }

    public CapabilityDescriptorSet(CapabilityGrantCollection typedGrants)
    {
        TypedGrants = typedGrants ?? CapabilityGrantCollection.Empty;
    }

    public static CapabilityDescriptorSet Empty { get; } = new();

    public ulong GlobalHardwareCaps =>
        TypedGrants.ProjectMask(CapabilityGrantScope.HardwareAvailable);

    public ulong RuntimeEnabledCaps =>
        TypedGrants.ProjectMask(CapabilityGrantScope.RuntimeEnabled);

    public ulong DomainGrantedCaps =>
        TypedGrants.ProjectMask(CapabilityGrantScope.DomainGranted);

    public CapabilityGrantCollection TypedGrants { get; }

    public ulong CompatibilityCapsProjection =>
        TypedGrants.EffectiveCompatibilityMask;

    public ulong EffectiveCaps => CompatibilityCapsProjection;

    public bool HasHardwareCapability(ulong capabilityMask) =>
        HasTypedGrant(capabilityMask, CapabilityGrantScope.HardwareAvailable);

    public bool IsRuntimeEnabled(ulong capabilityMask) =>
        HasTypedGrant(capabilityMask, CapabilityGrantScope.RuntimeEnabled);

    public bool IsDomainGranted(ulong capabilityMask) =>
        HasTypedGrant(capabilityMask, CapabilityGrantScope.DomainGranted);

    public bool HasEffectiveCapability(ulong capabilityMask) =>
        HasTypedGrant(capabilityMask, CapabilityGrantScope.CompatibilityProjection);

    public CapabilityGrant CreateGrant(
        ulong capabilityMask,
        CapabilityGrantScope scope) =>
        TypedGrants.TryGetGrant(capabilityMask, scope, out CapabilityGrant grant)
            ? grant
            : CapabilityGrant.Denied;

    public CapabilityGrant CreateGrant(
        ulong capabilityMask,
        CapabilityGrantScope scope,
        ulong ownerDomainId,
        CapabilityDelegationPolicy delegationPolicy = CapabilityDelegationPolicy.NonDelegable,
        CapabilityRevocationPolicy revocationPolicy = CapabilityRevocationPolicy.RuntimeRevocable,
        CapabilityMigrationClass migrationClass = CapabilityMigrationClass.DomainLocal,
        CapabilityEvidenceVisibility evidenceVisibility = CapabilityEvidenceVisibility.HostOnly,
        CapabilityFrontendProjectionPolicy frontendProjectionPolicy = CapabilityFrontendProjectionPolicy.ProjectIfCompatible) =>
        TypedGrants.TryGetGrant(capabilityMask, scope, out CapabilityGrant grant)
            ? new(
                grant.CapabilityMask,
                grant.Scope,
                grant.IsGranted,
                ownerDomainId,
                delegationPolicy,
                revocationPolicy,
                migrationClass,
                evidenceVisibility,
                frontendProjectionPolicy)
            : new(
                capabilityMask,
                scope,
                isGranted: false,
                ownerDomainId,
                delegationPolicy,
                revocationPolicy,
                migrationClass,
                evidenceVisibility,
                frontendProjectionPolicy);

    private bool HasTypedGrant(ulong capabilityMask, CapabilityGrantScope scope) =>
        TypedGrants.TryGetGrant(capabilityMask, scope, out CapabilityGrant grant) &&
        grant.HasTypedAuthority;
}
