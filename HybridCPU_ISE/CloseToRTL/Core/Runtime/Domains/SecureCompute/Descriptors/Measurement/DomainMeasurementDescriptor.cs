namespace YAKSys_Hybrid_CPU.Core;

public enum SecureMeasurementState : byte
{
    Missing = 0,
    Pending = 1,
    Materialized = 2,
    Stale = 3,
    Revoked = 4,
}

public enum SecureMeasurementDebugClass : byte
{
    Production = 0,
    MeasuredDebug = 1,
    DebugDenied = 2,
    DebugUnmeasuredDenied = 3,
}

public sealed partial class DomainMeasurementDescriptor
{
    public DomainMeasurementDescriptor()
        : this(
            SecureMeasurementHandle.None,
            SecureMeasurementState.Missing,
            SecureMeasurementDebugClass.Production,
            policyDigest: 0,
            memoryDigest: 0,
            runtimeDigest: 0)
    {
    }

    public DomainMeasurementDescriptor(
        SecureMeasurementHandle handle,
        SecureMeasurementState state,
        SecureMeasurementDebugClass debugClass,
        ulong policyDigest,
        ulong memoryDigest,
        ulong runtimeDigest)
        : this(
            handle,
            state,
            debugClass,
            policyDigest,
            memoryDigest,
            runtimeDigest,
            SecureEvidenceVisibilityClass.HostOwnedQuarantined,
            creatorDomainTag: 0,
            parentMeasurementId: 0,
            policySourceHash: 0)
    {
    }

    public DomainMeasurementDescriptor(
        SecureMeasurementHandle handle,
        SecureMeasurementState state,
        SecureMeasurementDebugClass debugClass,
        ulong policyDigest,
        ulong memoryDigest,
        ulong runtimeDigest,
        SecureEvidenceVisibilityClass attestationEvidenceClass,
        ulong creatorDomainTag,
        ulong parentMeasurementId,
        ulong policySourceHash)
    {
        Handle = handle;
        State = state;
        DebugClass = debugClass;
        PolicyDigest = policyDigest;
        MemoryDigest = memoryDigest;
        RuntimeDigest = runtimeDigest;
        AttestationEvidenceClass = attestationEvidenceClass;
        CreatorDomainTag = creatorDomainTag;
        ParentMeasurementId = parentMeasurementId;
        PolicySourceHash = policySourceHash;
    }

    public static DomainMeasurementDescriptor Missing { get; } = new();

    public SecureMeasurementHandle Handle { get; }

    public SecureMeasurementState State { get; }

    public SecureMeasurementDebugClass DebugClass { get; }

    public ulong PolicyDigest { get; }

    public ulong MemoryDigest { get; }

    public ulong RuntimeDigest { get; }

    public SecureEvidenceVisibilityClass AttestationEvidenceClass { get; }

    public ulong CreatorDomainTag { get; }

    public ulong ParentMeasurementId { get; }

    public ulong PolicySourceHash { get; }

    public ulong MeasurementEpoch => Handle.Epoch;

    public bool IsMaterialized =>
        State == SecureMeasurementState.Materialized &&
        Handle.IsMaterialized &&
        PolicyDigest != 0 &&
        CreatorDomainTag != 0 &&
        PolicySourceHash != 0;

    public bool IsAttestationEvidenceOnly => true;

    public bool IsStale =>
        State is SecureMeasurementState.Stale
            or SecureMeasurementState.Revoked;

    public bool IsCurrentFor(SecureRevocationEpoch policyEpoch) =>
        Handle.MatchesEpoch(policyEpoch);

    public bool BindsDomain(ulong domainTag) =>
        CreatorDomainTag != 0 && CreatorDomainTag == domainTag;

    public bool BindsPolicyDigest(ulong expectedPolicyDigest) =>
        expectedPolicyDigest != 0 && PolicyDigest == expectedPolicyDigest;

    public bool BindsMemoryDigest(ulong expectedMemoryDigest) =>
        expectedMemoryDigest == 0 || MemoryDigest == expectedMemoryDigest;

    public bool MustRevalidateOnRestore(SecureMigrationDescriptor migrationPolicy) =>
        migrationPolicy.RequiresReattestation ||
        AttestationEvidenceClass == SecureEvidenceVisibilityClass.RecomputedAfterRestore;

    public static ulong ComputePolicyDigest(SecureComputeDomainDescriptor descriptor)
    {
        ulong digest = Mix(0x5EC0D1AUL, descriptor.DomainTag);
        digest = Mix(digest, (ulong)descriptor.SecurityLevel);
        digest = Mix(digest, descriptor.MeasurementRequired ? 1UL : 0UL);
        digest = Mix(digest, descriptor.PrivateMemoryRequired ? 1UL : 0UL);
        digest = Mix(digest, (ulong)descriptor.HostInspectionPolicy.Mode);
        digest = Mix(digest, descriptor.HostInspectionPolicy.AllowPrivateMemoryInspection ? 1UL : 0UL);
        digest = Mix(digest, descriptor.EvidenceVisibilityPolicy.AllowGuestVisibleEvidence ? 1UL : 0UL);
        digest = Mix(digest, descriptor.EvidenceVisibilityPolicy.AllowMigrationSerializableEvidence ? 1UL : 0UL);
        digest = Mix(digest, descriptor.EvidenceVisibilityPolicy.AllowCompatibilityAliasEvidence ? 1UL : 0UL);
        digest = Mix(digest, descriptor.EvidenceVisibilityPolicy.AllowDebugEvidence ? 1UL : 0UL);
        digest = Mix(digest, (ulong)descriptor.MigrationPolicy.Mode);
        digest = Mix(digest, (ulong)descriptor.MigrationPolicy.PrivateMemoryPolicy);
        digest = Mix(digest, descriptor.MigrationPolicy.PolicyEpoch.Current);
        digest = Mix(digest, (ulong)descriptor.MigrationPolicy.MeasurementRestorePolicy);
        digest = Mix(digest, (ulong)descriptor.MigrationPolicy.GrantRestorePolicy);
        digest = MixIoPolicy(digest, descriptor.IoPolicy);
        digest = MixHypercallPolicy(digest, descriptor.HypercallPolicy);
        digest = Mix(digest, (ulong)descriptor.DebugPolicy.Mode);
        digest = Mix(digest, descriptor.DebugPolicy.ChangesMeasurementClass ? 1UL : 0UL);
        digest = Mix(digest, descriptor.CompatibilityProjectionPolicy.AllowReadOnlyAliases ? 1UL : 0UL);
        digest = Mix(digest, descriptor.CompatibilityProjectionPolicy.AllowedAliasMask);
        return NonZero(digest);
    }

    public static ulong ComputeMemoryDigest(SecureMemoryDomainDescriptor? memory)
    {
        if (memory is null || !memory.IsMaterialized)
        {
            return 0;
        }

        ulong digest = Mix(0x5EC0D2AUL, memory.DomainTag);
        digest = Mix(digest, memory.AddressSpaceTag);
        digest = Mix(digest, memory.PolicyEpoch.Current);
        digest = Mix(digest, (ulong)memory.DmaPolicy);

        foreach (SecureMemoryRegionDescriptor region in memory.Regions)
        {
            digest = Mix(digest, (ulong)region.RegionClass);
            digest = Mix(digest, region.Start);
            digest = Mix(digest, region.Length);
            digest = Mix(digest, (ulong)region.HostVisibility);
            digest = Mix(digest, region.PolicyEpoch);
            digest = Mix(digest, (ulong)region.RuntimeDirtyPolicy);
            digest = Mix(digest, (ulong)region.RuntimeMigrationClass);
        }

        return NonZero(digest);
    }

    private static ulong Mix(ulong digest, ulong value) =>
        ((digest << 7) | (digest >> 57)) ^ value ^ 0x9E3779B97F4A7C15UL;

    private static ulong MixIoPolicy(ulong digest, SecureIoDomainDescriptor policy)
    {
        digest = Mix(digest, (ulong)policy.DmaPolicy);
        digest = Mix(digest, policy.RequireCompletionFence ? 1UL : 0UL);
        digest = Mix(digest, policy.NeutralIoOwnerMaterialized ? 1UL : 0UL);

        foreach (SecureSharedBufferDescriptor buffer in policy.SharedBuffers)
        {
            digest = Mix(digest, buffer.BufferId);
            digest = Mix(digest, buffer.Start);
            digest = Mix(digest, buffer.Length);
            digest = Mix(digest, (ulong)buffer.Direction);
            digest = MixGrantHandle(digest, buffer.Grant);
            digest = Mix(digest, (ulong)buffer.EvidenceClass);
            digest = Mix(digest, buffer.OwnerDomainTag);
            digest = Mix(digest, buffer.LifetimeEpoch);
        }

        return digest;
    }

    private static ulong MixHypercallPolicy(ulong digest, SecureHypercallDescriptor policy)
    {
        digest = Mix(digest, policy.NeutralBackendOwnerRequired ? 1UL : 0UL);
        digest = Mix(digest, policy.AllowBackendExecution ? 1UL : 0UL);
        digest = MixGrantHandle(digest, policy.RequiredGrant);
        digest = Mix(digest, policy.RequireEvidenceApproval ? 1UL : 0UL);
        digest = Mix(digest, policy.RequireCompletionFence ? 1UL : 0UL);
        digest = Mix(digest, policy.RequireRetirePublicationRule ? 1UL : 0UL);

        foreach (ulong hypercallId in policy.AllowedHypercallIds)
        {
            digest = Mix(digest, hypercallId);
        }

        foreach (SecureHypercallArgumentDescriptor argument in policy.Arguments)
        {
            digest = Mix(digest, argument.Index);
            digest = Mix(digest, (ulong)argument.ArgumentClass);
            digest = Mix(digest, argument.SharedBufferId);
            digest = MixGrantHandle(digest, argument.Grant);
        }

        return digest;
    }

    private static ulong MixGrantHandle(ulong digest, SecureGrantHandle grant)
    {
        digest = Mix(digest, (ulong)grant.Kind);
        digest = Mix(digest, grant.LocalId);
        digest = Mix(digest, grant.ProvenanceHash);
        return Mix(digest, grant.Epoch);
    }

    private static ulong NonZero(ulong value) =>
        value == 0 ? 1UL : value;
}
