using System.Collections.Generic;

namespace YAKSys_Hybrid_CPU.Core;

public readonly record struct SecureHypercallTransportOpcode(ushort Value);

public readonly record struct SecureHypercallDecodedLeaf(ulong Value)
{
    public bool IsMaterialized => Value != 0;
}

public readonly record struct SecureComputeServiceId(ulong Value)
{
    public bool IsMaterialized => Value != 0;
}

public readonly record struct SecureBackendOwnerId(ulong Value)
{
    public bool IsMaterialized => Value != 0;
}

public readonly record struct SecureHypercallContractVersion(ushort Major, ushort Minor)
{
    public bool IsMaterialized => Major != 0;
}

public enum SecureHypercallArgumentOwnership : byte
{
    GuestImmediate = 0,
    ExplicitSharedBuffer = 1,
    OpaqueRuntimeHandle = 2,
    RawHostPointerDenied = 3,
}

public enum SecureHypercallReplayPolicy : byte
{
    DenyReplay = 0,
    IdempotentRetryWithMatchingToken = 1,
}

public enum SecureHypercallCancellationPolicy : byte
{
    DenyBeforeExecution = 0,
}

public enum SecureHypercallRequestMigrationClass : byte
{
    NonMigratableInFlight = 0,
    DescriptorOnlyRevalidatedAfterRestore = 1,
}

public enum SecureHypercallResultMigrationClass : byte
{
    NoResultBeforeExecution = 0,
    RecomputedAfterRestore = 1,
}

public readonly record struct SecureHypercallBackendArgument(
    byte Index,
    SecureHypercallArgumentOwnership Ownership,
    ulong Value,
    ulong Length,
    SecureGrantHandle Grant)
{
    public bool IsRawPointer =>
        Ownership == SecureHypercallArgumentOwnership.RawHostPointerDenied;

    public bool IsSharedBuffer =>
        Ownership == SecureHypercallArgumentOwnership.ExplicitSharedBuffer;

    public bool IsOpaqueHandle =>
        Ownership == SecureHypercallArgumentOwnership.OpaqueRuntimeHandle;
}

public sealed class SecureHypercallBackendContractDescriptor
{
    public SecureHypercallBackendContractDescriptor(
        SecureHypercallDecodedLeaf decodedLeaf,
        SecureComputeServiceId serviceId,
        SecureBackendOwnerId ownerId,
        SecureRevocationEpoch ownerEpoch,
        SecureHypercallContractVersion version,
        SecureGrantHandle requiredGrant,
        SecureHypercallReplayPolicy replayPolicy,
        SecureHypercallCancellationPolicy cancellationPolicy,
        SecureHypercallRequestMigrationClass requestMigrationClass,
        SecureHypercallResultMigrationClass resultMigrationClass)
    {
        DecodedLeaf = decodedLeaf;
        ServiceId = serviceId;
        OwnerId = ownerId;
        OwnerEpoch = ownerEpoch;
        Version = version;
        RequiredGrant = requiredGrant;
        ReplayPolicy = replayPolicy;
        CancellationPolicy = cancellationPolicy;
        RequestMigrationClass = requestMigrationClass;
        ResultMigrationClass = resultMigrationClass;
    }

    public static SecureHypercallBackendContractDescriptor Unresolved { get; } =
        new(
            default,
            default,
            default,
            SecureRevocationEpoch.Unmaterialized,
            default,
            SecureGrantHandle.None,
            SecureHypercallReplayPolicy.DenyReplay,
            SecureHypercallCancellationPolicy.DenyBeforeExecution,
            SecureHypercallRequestMigrationClass.NonMigratableInFlight,
            SecureHypercallResultMigrationClass.NoResultBeforeExecution);

    public SecureHypercallDecodedLeaf DecodedLeaf { get; }

    public SecureComputeServiceId ServiceId { get; }

    public SecureBackendOwnerId OwnerId { get; }

    public SecureRevocationEpoch OwnerEpoch { get; }

    public SecureHypercallContractVersion Version { get; }

    public SecureGrantHandle RequiredGrant { get; }

    public SecureHypercallReplayPolicy ReplayPolicy { get; }

    public SecureHypercallCancellationPolicy CancellationPolicy { get; }

    public SecureHypercallRequestMigrationClass RequestMigrationClass { get; }

    public SecureHypercallResultMigrationClass ResultMigrationClass { get; }

    public bool IsMaterialized =>
        DecodedLeaf.IsMaterialized &&
        ServiceId.IsMaterialized &&
        OwnerId.IsMaterialized &&
        OwnerEpoch.IsMaterialized &&
        Version.IsMaterialized &&
        RequiredGrant.IsMaterialized;
}

public readonly record struct SecureHypercallBackendContractRequest(
    SecureHypercallTransportOpcode TransportOpcode,
    SecureHypercallDecodedLeaf DecodedLeaf,
    SecureComputeServiceId ServiceId,
    SecureHypercallContractVersion ContractVersion,
    SecureBackendOwnerDescriptor? Owner,
    SecureRevocationEpoch CurrentEpoch,
    SecureGrantHandle PresentedGrant,
    bool EvidenceValidated,
    SecureRevocationEpoch EvidenceEpoch,
    SecureIoDomainDescriptor? IoPolicy,
    ulong ValidatedDomainTag,
    IReadOnlyList<SecureHypercallBackendArgument> Arguments,
    bool IsReplay,
    bool IdempotentRetry,
    bool ReplayTokenMatches,
    bool CancellationRequested);
