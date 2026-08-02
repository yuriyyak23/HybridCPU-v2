namespace YAKSys_Hybrid_CPU.Core;

public enum PrivilegedExecutionStateOwnerDecision : byte
{
    AllowedOwnerMaterializedProjectionClosed = 0,
    DeniedMissingDescriptor = 1,
    DeniedUnmaterializedDescriptor = 2,
    DeniedDomainTagMismatch = 3,
    DeniedAddressSpaceTagMismatch = 4,
    DeniedStaleEpoch = 5,
    DeniedRegisterKindMismatch = 6,
    DeniedEvidenceClass = 7,
    DeniedMigrationClass = 8,
    DeniedGuestCr0ReservedBits = 9,
    DeniedGuestCr0RequiredBits = 10,
    DeniedGuestCr4ReservedBits = 11,
    DeniedGuestCr4RequiredBits = 12,
}

public readonly record struct PrivilegedExecutionStateOwnerRequest(
    PrivilegedExecutionStateDescriptor? Descriptor,
    ulong RuntimeDomainTag,
    ulong RuntimeAddressSpaceTag,
    PrivilegedExecutionStateEpoch CurrentEpoch);

public readonly record struct PrivilegedExecutionStateOwnerResult(
    PrivilegedExecutionStateOwnerDecision Decision,
    bool OwnerAccepted,
    bool ReadOnlyProjectionAuthorized,
    bool MutationAuthorized,
    bool BackendExecutionAuthorized,
    bool CompletionPublicationAuthorized,
    bool RetirePublicationAuthorized,
    string Reason)
{
    public bool IsAllowed =>
        Decision == PrivilegedExecutionStateOwnerDecision.AllowedOwnerMaterializedProjectionClosed &&
        OwnerAccepted;

    public static PrivilegedExecutionStateOwnerResult AllowedOwnerOnly { get; } =
        new(
            PrivilegedExecutionStateOwnerDecision.AllowedOwnerMaterializedProjectionClosed,
            OwnerAccepted: true,
            ReadOnlyProjectionAuthorized: false,
            MutationAuthorized: false,
            BackendExecutionAuthorized: false,
            CompletionPublicationAuthorized: false,
            RetirePublicationAuthorized: false,
            Reason: "Neutral privileged execution-state ownership is accepted; projection, mutation, execution and publication remain closed.");

    public static PrivilegedExecutionStateOwnerResult Denied(
        PrivilegedExecutionStateOwnerDecision decision,
        string reason) =>
        new(
            decision,
            OwnerAccepted: false,
            ReadOnlyProjectionAuthorized: false,
            MutationAuthorized: false,
            BackendExecutionAuthorized: false,
            CompletionPublicationAuthorized: false,
            RetirePublicationAuthorized: false,
            reason);
}

public sealed class PrivilegedExecutionStateOwnerPolicy
{
    public static PrivilegedExecutionStateOwnerPolicy Default { get; } = new();

    public PrivilegedExecutionStateOwnerResult Admit(
        PrivilegedExecutionStateOwnerRequest request)
    {
        if (request.Descriptor is null)
        {
            return Deny(
                PrivilegedExecutionStateOwnerDecision.DeniedMissingDescriptor,
                "Privileged execution-state ownership requires a descriptor.");
        }

        PrivilegedExecutionStateDescriptor descriptor = request.Descriptor.Value;
        if (!descriptor.IsMaterialized)
        {
            return Deny(
                PrivilegedExecutionStateOwnerDecision.DeniedUnmaterializedDescriptor,
                "Privileged execution-state ownership requires materialized identity, epoch and legality policy.");
        }

        if (descriptor.DomainTag != request.RuntimeDomainTag)
        {
            return Deny(
                PrivilegedExecutionStateOwnerDecision.DeniedDomainTagMismatch,
                "Privileged execution-state owner domain binding does not match the runtime domain.");
        }

        if (descriptor.AddressSpaceTag != request.RuntimeAddressSpaceTag)
        {
            return Deny(
                PrivilegedExecutionStateOwnerDecision.DeniedAddressSpaceTagMismatch,
                "Privileged execution-state owner address-space binding does not match the runtime address space.");
        }

        if (!request.CurrentEpoch.IsCurrent(descriptor.PolicyEpoch))
        {
            return Deny(
                PrivilegedExecutionStateOwnerDecision.DeniedStaleEpoch,
                "Privileged execution-state owner epoch is stale or unmaterialized.");
        }

        if (!descriptor.HasCanonicalRegisterKinds)
        {
            return Deny(
                PrivilegedExecutionStateOwnerDecision.DeniedRegisterKindMismatch,
                "Privileged execution-state values must use canonical GuestCr0 and GuestCr4 kinds.");
        }

        if (descriptor.EvidenceClass !=
            PrivilegedExecutionStateEvidenceClass.GuestVisibleReadOnlyProjection)
        {
            return Deny(
                PrivilegedExecutionStateOwnerDecision.DeniedEvidenceClass,
                "Privileged execution-state owner requires an explicit guest-visible read-only evidence classification.");
        }

        if (descriptor.MigrationClass !=
            PrivilegedExecutionStateMigrationClass.RevalidatedAfterRestore)
        {
            return Deny(
                PrivilegedExecutionStateOwnerDecision.DeniedMigrationClass,
                "Privileged execution-state owner must be revalidated after restore.");
        }

        if (descriptor.LegalityPolicy.HasReservedBits(descriptor.GuestCr0))
        {
            return Deny(
                PrivilegedExecutionStateOwnerDecision.DeniedGuestCr0ReservedBits,
                "GuestCr0 contains bits outside the descriptor-owned legality mask.");
        }

        if (!descriptor.LegalityPolicy.HasAllRequiredBits(descriptor.GuestCr0))
        {
            return Deny(
                PrivilegedExecutionStateOwnerDecision.DeniedGuestCr0RequiredBits,
                "GuestCr0 is missing descriptor-required bits.");
        }

        if (descriptor.LegalityPolicy.HasReservedBits(descriptor.GuestCr4))
        {
            return Deny(
                PrivilegedExecutionStateOwnerDecision.DeniedGuestCr4ReservedBits,
                "GuestCr4 contains bits outside the descriptor-owned legality mask.");
        }

        if (!descriptor.LegalityPolicy.HasAllRequiredBits(descriptor.GuestCr4))
        {
            return Deny(
                PrivilegedExecutionStateOwnerDecision.DeniedGuestCr4RequiredBits,
                "GuestCr4 is missing descriptor-required bits.");
        }

        return PrivilegedExecutionStateOwnerResult.AllowedOwnerOnly;
    }

    private static PrivilegedExecutionStateOwnerResult Deny(
        PrivilegedExecutionStateOwnerDecision decision,
        string reason) =>
        PrivilegedExecutionStateOwnerResult.Denied(decision, reason);
}
