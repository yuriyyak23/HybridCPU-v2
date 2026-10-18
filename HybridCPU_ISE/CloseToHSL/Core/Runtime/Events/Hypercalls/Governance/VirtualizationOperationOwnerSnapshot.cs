using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Security.Cryptography;
using System.Text;

namespace YAKSys_Hybrid_CPU.Core;

internal enum VirtualizationOperationOwnerSnapshotLoadDecision : byte
{
    Loaded = 0,
    MissingInput = 1,
    DecisionMismatch = 2,
    DigestMismatch = 3,
    AcceptanceNotCurrent = 4,
    OwnerMismatch = 5,
    PolicyVersionMismatch = 6,
    NamespaceOrLeafMismatch = 7,
    AbiMismatch = 8,
    PolicyMismatch = 9,
    AllocationMismatch = 10,
    NotExactMachineAcceptedSource = 11,
}

internal readonly record struct VirtualizationOperationOwnerSnapshotLoadResult(
    VirtualizationOperationOwnerSnapshotLoadDecision Decision,
    VirtualizationOperationOwnerSnapshot? Snapshot,
    string Reason)
{
    internal bool IsLoaded =>
        Decision == VirtualizationOperationOwnerSnapshotLoadDecision.Loaded &&
        Snapshot is not null;
}

/// <summary>
/// O1 is an immutable runtime-loaded copy of one accepted D2 policy. It is not
/// a capability, live admission, execution token, completion or retire grant.
/// </summary>
internal sealed class VirtualizationOperationOwnerSnapshot
{
    private readonly VirtualizationDecisionSpecV2 _policy;

    private VirtualizationOperationOwnerSnapshot(
        VirtualizationDecisionSpecV2 policy,
        string acceptanceDigest,
        string policyDigest)
    {
        _policy = policy with { };
        AcceptanceDigest = acceptanceDigest;
        PolicyDigest = policyDigest;
    }

    internal uint SchemaVersion => _policy.SchemaVersion;
    internal string DecisionId => _policy.DecisionId;
    internal string SpecDigest => _policy.SpecDigest;
    internal string AcceptanceDigest { get; }
    internal ulong OwnerId => _policy.OwnerId;
    internal uint OwnerPolicyVersion => _policy.OwnerPolicyVersion;
    internal uint OwnerEpoch => _policy.OwnerEpoch;
    internal string OperationNamespace => _policy.OperationNamespace;
    internal string OperationId => _policy.OperationId;
    internal ushort LeafWidth => _policy.LeafWidth;
    internal ushort NumericLeaf => _policy.NumericLeaf;
    internal uint OperandAbiVersion => _policy.OperandAbiVersion;
    internal string Rs1Contract => _policy.Rs1Contract;
    internal string Rs2Contract => _policy.Rs2Contract;
    internal string RdContract => _policy.RdContract;
    internal VirtualizationDecisionResultAbiV2 ResultAbi => _policy.ResultAbi;
    internal VirtualizationDecisionEffectClassV2 EffectClass => _policy.EffectClass;
    internal VirtualizationCapabilityRequirementV2 CapabilityRequirement => _policy.CapabilityRequirement;
    internal ulong CapabilityMask => _policy.CapabilityMask;
    internal bool RequiresTypedGrant => _policy.RequiresTypedGrant;
    internal VirtualizationDelegationPolicyV2 DelegationPolicy => _policy.DelegationPolicy;
    internal VirtualizationRevocationPolicyV2 RevocationPolicy => _policy.RevocationPolicy;
    internal VirtualizationCapabilityMigrationClassV2 CapabilityMigrationClass => _policy.CapabilityMigrationClass;
    internal VirtualizationEvidenceVisibilityV2 EvidenceVisibility => _policy.EvidenceVisibility;
    internal VirtualizationProjectionPolicyV2 FrontendProjectionPolicy => _policy.FrontendProjectionPolicy;
    internal VirtualizationExecutionEvidenceRequirementV2 ExecutionEvidenceRequirement => _policy.ExecutionEvidenceRequirement;
    internal VirtualizationDomainRequirementV2 DomainRequirement => _policy.DomainRequirement;
    internal bool RequireNonZeroDomainTag => _policy.RequireNonZeroDomainTag;
    internal bool RequiresMemoryDomain => _policy.RequiresMemoryDomain;
    internal bool RequiresIoDomain => _policy.RequiresIoDomain;
    internal VirtualizationAddressSpaceRequirementV2 AddressSpaceRequirement => _policy.AddressSpaceRequirement;
    internal VirtualizationSecureDomainPolicyV2 SecureDomainPolicy => _policy.SecureDomainPolicy;
    internal VirtualizationCancellationPolicyV2 CancellationPolicy => _policy.CancellationPolicy;
    internal VirtualizationReplayPolicyV2 ReplayPolicy => _policy.ReplayPolicy;
    internal VirtualizationOperationMigrationPolicyV2 OperationMigrationPolicy => _policy.OperationMigrationPolicy;
    internal VirtualizationCompletionEvidenceClassV2 CompletionEvidenceClass => _policy.CompletionEvidenceClass;
    internal VirtualizationCompletionMigrationClassV2 CompletionMigrationClass => _policy.CompletionMigrationClass;
    internal VirtualizationProjectionPolicyV2 CompletionProjectionPolicy => _policy.CompletionProjectionPolicy;
    internal VirtualizationCompletionPolicyV2 CompletionPolicy => _policy.CompletionPolicy;
    internal VirtualizationRetirePolicyV2 RetirePolicy => _policy.RetirePolicy;
    internal VirtualizationAdjacentLeafPolicyV2 AdjacentLeafPolicy => _policy.AdjacentLeafPolicy;
    internal VirtualizationCrossNamespacePolicyV2 CrossNamespacePolicy => _policy.CrossNamespacePolicy;
    internal ImmutableArray<VirtualizationDecisionOwnerMapEntryV2> OwnerMap => _policy.OwnerMap;
    internal string PolicyDigest { get; }

    internal bool IsCapability => false;
    internal bool RuntimeAuthorityGranted => false;
    internal bool BackendExecutionAuthorized => false;
    internal bool CompletionPublicationAuthorized => false;
    internal bool RetirePublicationAuthorized => false;

    internal static VirtualizationOperationOwnerSnapshot CreateValidated(
        VirtualizationDecisionSpecV2 policy,
        string acceptanceDigest) =>
        new(
            policy,
            acceptanceDigest,
            VirtualizationOperationOwnerSnapshotDigest.Compute(
                policy.SpecDigest,
                acceptanceDigest,
                policy.OwnerId,
                policy.OwnerPolicyVersion,
                policy.OwnerEpoch));
}

internal static class VirtualizationOperationOwnerSnapshotDigest
{
    private static readonly byte[] Envelope = Encoding.ASCII.GetBytes("HCPUO1\0");

    internal static string Compute(
        string specDigest,
        string acceptanceDigest,
        ulong ownerId,
        uint ownerPolicyVersion,
        uint ownerEpoch)
    {
        byte[] bytes = new byte[Envelope.Length + 32 + 32 + 8 + 4 + 4];
        Envelope.CopyTo(bytes, 0);
        Convert.FromHexString(specDigest).CopyTo(bytes, Envelope.Length);
        Convert.FromHexString(acceptanceDigest).CopyTo(bytes, Envelope.Length + 32);
        int offset = Envelope.Length + 64;
        BinaryPrimitives.WriteUInt64BigEndian(bytes.AsSpan(offset, 8), ownerId);
        BinaryPrimitives.WriteUInt32BigEndian(bytes.AsSpan(offset + 8, 4), ownerPolicyVersion);
        BinaryPrimitives.WriteUInt32BigEndian(bytes.AsSpan(offset + 12, 4), ownerEpoch);
        return Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
    }
}

internal static class VirtualizationOperationOwnerSnapshotLoader
{
    internal static VirtualizationOperationOwnerSnapshotLoadResult LoadExactAcceptedPolicy() =>
        TryLoad(
            Phase38AcceptedVirtualizationDecisionRegistry.ExactEntry.Policy,
            Phase38VirtualizationDecisionSpecV2.Instance,
            Phase38VirtualizationDecisionAcceptanceV2.Record,
            HypercallRuntimeOwnerRegistry.Phase38ProbeOwner);

    internal static VirtualizationOperationOwnerSnapshotLoadResult TryLoad(
        AcceptedVirtualizationDecision? accepted,
        VirtualizationDecisionSpecV2? spec,
        VirtualizationDecisionAcceptanceRecordV2? acceptance,
        HypercallRuntimeOwnerAllocation? allocation)
    {
        if (accepted is null || spec is null || acceptance is null || allocation is null)
            return Deny(VirtualizationOperationOwnerSnapshotLoadDecision.MissingInput, "O1 requires D2 policy, spec, acceptance and owner allocation inputs.");

        if (!string.Equals(accepted.DecisionId, spec.DecisionId, StringComparison.Ordinal) ||
            !string.Equals(acceptance.DecisionId, spec.DecisionId, StringComparison.Ordinal))
        {
            return Deny(VirtualizationOperationOwnerSnapshotLoadDecision.DecisionMismatch, "O1 decision identity does not match D2.");
        }

        if (!string.Equals(accepted.SpecDigest, spec.SpecDigest, StringComparison.Ordinal) ||
            !string.Equals(acceptance.SpecDigest, spec.SpecDigest, StringComparison.Ordinal) ||
            !string.Equals(accepted.AcceptanceDigest, acceptance.AcceptanceDigest, StringComparison.Ordinal))
        {
            return Deny(VirtualizationOperationOwnerSnapshotLoadDecision.DigestMismatch, "O1 digest lineage does not match D2.");
        }

        if (acceptance.AcceptanceState != VirtualizationDecisionAcceptanceStateV2.Accepted ||
            acceptance.SupersedesDecisionId is not null ||
            acceptance.SupersedesAcceptanceDigest is not null)
        {
            return Deny(VirtualizationOperationOwnerSnapshotLoadDecision.AcceptanceNotCurrent, "O1 requires one current non-superseding accepted record.");
        }

        if (accepted.OwnerId == 0 || accepted.OwnerId != spec.OwnerId || allocation.OwnerId != spec.OwnerId)
            return Deny(VirtualizationOperationOwnerSnapshotLoadDecision.OwnerMismatch, "O1 owner identity is missing or mismatched.");

        if (accepted.OwnerPolicyVersion != spec.OwnerPolicyVersion ||
            accepted.OwnerEpoch != spec.OwnerEpoch ||
            allocation.OwnerPolicyVersion != spec.OwnerPolicyVersion ||
            allocation.OwnerEpoch != spec.OwnerEpoch)
        {
            return Deny(VirtualizationOperationOwnerSnapshotLoadDecision.PolicyVersionMismatch, "O1 owner policy version or epoch is stale.");
        }

        if (!string.Equals(accepted.OperationNamespace, spec.OperationNamespace, StringComparison.Ordinal) ||
            accepted.NumericLeaf != spec.NumericLeaf ||
            spec.LeafWidth != 16 ||
            spec.NumericLeaf == 0)
        {
            return Deny(VirtualizationOperationOwnerSnapshotLoadDecision.NamespaceOrLeafMismatch, "O1 namespace or exact leaf does not match D2.");
        }

        if (spec.OperandAbiVersion != 1 ||
            !string.Equals(spec.Rs1Contract, "ArchitecturalRegisterFullNumericLeafValue", StringComparison.Ordinal) ||
            !string.Equals(spec.Rs2Contract, "X0", StringComparison.Ordinal) ||
            !string.Equals(spec.RdContract, "X0NoResult", StringComparison.Ordinal) ||
            spec.ResultAbi != VirtualizationDecisionResultAbiV2.NoPayload ||
            spec.EffectClass != VirtualizationDecisionEffectClassV2.NoStateNoPayload)
        {
            return Deny(VirtualizationOperationOwnerSnapshotLoadDecision.AbiMismatch, "O1 ABI or effect policy does not match the exact first slice.");
        }

        if (spec.CapabilityMask != RuntimeCapabilityIds.VmCallProbeNoStateV1Mask ||
            !spec.RequiresTypedGrant ||
            spec.DomainRequirement != VirtualizationDomainRequirementV2.ExecutionDomainBound ||
            !spec.RequireNonZeroDomainTag ||
            spec.RequiresMemoryDomain ||
            spec.RequiresIoDomain ||
            spec.SecureDomainPolicy != VirtualizationSecureDomainPolicyV2.Deny ||
            spec.FrontendProjectionPolicy != VirtualizationProjectionPolicyV2.NeverProject ||
            spec.AdjacentLeafPolicy != VirtualizationAdjacentLeafPolicyV2.DenyAllExceptExact)
        {
            return Deny(VirtualizationOperationOwnerSnapshotLoadDecision.PolicyMismatch, "O1 runtime policy does not match the exact accepted profile.");
        }

        if (!string.Equals(allocation.DecisionId, spec.DecisionId, StringComparison.Ordinal) ||
            !string.Equals(allocation.SpecDigest, spec.SpecDigest, StringComparison.Ordinal) ||
            !string.Equals(allocation.OwnerRole, "DomainHypercallRuntimeOwner", StringComparison.Ordinal))
        {
            return Deny(VirtualizationOperationOwnerSnapshotLoadDecision.AllocationMismatch, "O1 owner allocation is not the accepted HCOWNR allocation.");
        }

        if (!ReferenceEquals(accepted, Phase38AcceptedVirtualizationDecisionRegistry.ExactEntry.Policy) ||
            !ReferenceEquals(spec, Phase38VirtualizationDecisionSpecV2.Instance) ||
            !ReferenceEquals(acceptance, Phase38VirtualizationDecisionAcceptanceV2.Record) ||
            !ReferenceEquals(allocation, HypercallRuntimeOwnerRegistry.Phase38ProbeOwner))
        {
            return Deny(VirtualizationOperationOwnerSnapshotLoadDecision.NotExactMachineAcceptedSource, "O1 cannot be loaded from a cloned, caller-built or unregistered policy object.");
        }

        VirtualizationOperationOwnerSnapshot snapshot =
            VirtualizationOperationOwnerSnapshot.CreateValidated(spec, acceptance.AcceptanceDigest);
        return new(VirtualizationOperationOwnerSnapshotLoadDecision.Loaded, snapshot, "Exact accepted D2 policy loaded as immutable O1.");
    }

    private static VirtualizationOperationOwnerSnapshotLoadResult Deny(
        VirtualizationOperationOwnerSnapshotLoadDecision decision,
        string reason) =>
        new(decision, null, reason);
}

internal static class Phase38VirtualizationOperationOwnerSnapshotRegistry
{
    private static readonly VirtualizationOperationOwnerSnapshotLoadResult LoadResult =
        VirtualizationOperationOwnerSnapshotLoader.LoadExactAcceptedPolicy();

    internal static VirtualizationOperationOwnerSnapshot ExactSnapshot =>
        LoadResult.Snapshot ?? throw new InvalidOperationException(LoadResult.Reason);

    internal static bool TryResolve(
        string operationNamespace,
        ushort numericLeaf,
        out VirtualizationOperationOwnerSnapshot snapshot)
    {
        if (LoadResult.IsLoaded &&
            string.Equals(operationNamespace, ExactSnapshot.OperationNamespace, StringComparison.Ordinal) &&
            numericLeaf == ExactSnapshot.NumericLeaf)
        {
            snapshot = ExactSnapshot;
            return true;
        }

        snapshot = null!;
        return false;
    }

    internal static bool RuntimeAuthorityGranted => false;
    internal static bool BackendExecutionAuthorized => false;
    internal static bool CompletionPublicationAuthorized => false;
    internal static bool RetirePublicationAuthorized => false;
}
