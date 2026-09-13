namespace YAKSys_Hybrid_CPU.Core;

public enum SecureGrantMaterializationSource : byte
{
    None = 0,
    NeutralRuntimeOwner = 1,
    MigrationRestore = 2,
    GuestArchitecturalState = 3,
    CompatibilityProjection = 4,
}

public enum SecureGrantAuthorityDecision : byte
{
    Allowed = 0,
    DeniedMissingRuntimeOwner = 1,
    DeniedGuestScalarMaterialization = 2,
    DeniedCompatibilityProjectionAuthority = 3,
    DeniedMissingProvenance = 4,
    DeniedStaleEpoch = 5,
    DeniedAuthorityBounds = 6,
    DeniedMissingTypedGrant = 7,
    DeniedRevokedGrant = 8,
}

public readonly record struct SecureGrantEpochSet(
    SecureRevocationEpoch DomainPolicyEpoch,
    SecureRevocationEpoch MemoryPolicyEpoch,
    SecureRevocationEpoch GrantCollectionEpoch,
    SecureRevocationEpoch ProvenanceEpoch)
{
    public static SecureGrantEpochSet Single(SecureRevocationEpoch epoch) =>
        new(epoch, epoch, epoch, epoch);

    public bool AllCurrentFor(SecureGrantHandle handle) =>
        DomainPolicyEpoch.IsCurrent(handle.Epoch) &&
        MemoryPolicyEpoch.IsCurrent(handle.Epoch) &&
        GrantCollectionEpoch.IsCurrent(handle.Epoch) &&
        ProvenanceEpoch.IsCurrent(handle.Epoch);
}

public readonly record struct SecureGrantAuthorityResult(
    SecureGrantAuthorityDecision Decision,
    string Reason)
{
    public bool IsAllowed =>
        Decision == SecureGrantAuthorityDecision.Allowed;

    public static SecureGrantAuthorityResult Allowed { get; } =
        new(SecureGrantAuthorityDecision.Allowed, string.Empty);

    public static SecureGrantAuthorityResult Denied(
        SecureGrantAuthorityDecision decision,
        string reason) =>
        new(decision, reason);
}

public sealed partial class SecureGrantAuthorityPolicy
{
    public static SecureGrantAuthorityPolicy Default { get; } = new();

    public SecureGrantAuthorityResult Validate(
        SecureGrantHandle handle,
        SecureGrantMaterializationSource source,
        SecureAuthorityBounds grantedBounds,
        SecureAuthorityBounds requestedBounds,
        SecureGrantEpochSet epochs,
        bool runtimeOwnerMaterialized,
        CapabilityDescriptorSet? capabilities = null,
        CapabilityGrantScope requiredScope = CapabilityGrantScope.None,
        bool grantRevoked = false)
    {
        if (source == SecureGrantMaterializationSource.GuestArchitecturalState)
        {
            return Deny(
                SecureGrantAuthorityDecision.DeniedGuestScalarMaterialization,
                "Guest-visible scalar values cannot materialize secure grant authority.");
        }

        if (source == SecureGrantMaterializationSource.CompatibilityProjection ||
            requiredScope == CapabilityGrantScope.CompatibilityProjection)
        {
            return Deny(
                SecureGrantAuthorityDecision.DeniedCompatibilityProjectionAuthority,
                "Compatibility projection scope cannot satisfy secure grant authority.");
        }

        if (!runtimeOwnerMaterialized)
        {
            return Deny(
                SecureGrantAuthorityDecision.DeniedMissingRuntimeOwner,
                "Secure grant validation requires a neutral runtime owner.");
        }

        if (!handle.HasScalarShape || !handle.HasProvenance)
        {
            return Deny(
                SecureGrantAuthorityDecision.DeniedMissingProvenance,
                "Secure grant handle requires runtime provenance.");
        }

        if (grantRevoked)
        {
            return Deny(
                SecureGrantAuthorityDecision.DeniedRevokedGrant,
                "Revoked secure grants cannot satisfy admission.");
        }

        if (!epochs.AllCurrentFor(handle))
        {
            return Deny(
                SecureGrantAuthorityDecision.DeniedStaleEpoch,
                "Secure grant handle must match current domain, memory, grant and provenance epochs.");
        }

        if (!requestedBounds.IsSubsetOf(grantedBounds))
        {
            return Deny(
                SecureGrantAuthorityDecision.DeniedAuthorityBounds,
                "Secure grant requested authority exceeds granted bounds.");
        }

        if (requiredScope != CapabilityGrantScope.None &&
            CapabilityBoundaryRequirement.TypedGrant(handle.LocalId, requiredScope)
                .IsSatisfiedBy(capabilities ?? CapabilityDescriptorSet.Empty) != true)
        {
            return Deny(
                SecureGrantAuthorityDecision.DeniedMissingTypedGrant,
                "Secure grant handle requires a matching neutral typed grant.");
        }

        return SecureGrantAuthorityResult.Allowed;
    }

    private static SecureGrantAuthorityResult Deny(
        SecureGrantAuthorityDecision decision,
        string reason) =>
        SecureGrantAuthorityResult.Denied(decision, reason);
}
