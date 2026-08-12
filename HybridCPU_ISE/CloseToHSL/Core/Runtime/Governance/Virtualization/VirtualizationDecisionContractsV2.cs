using System.Collections.Immutable;

namespace YAKSys_Hybrid_CPU.Core;

internal enum VirtualizationDecisionOwnerClassV2 : byte
{
    Unspecified = 0,
    NeutralRuntimeOwner = 1,
    CompatibilityFrontend = 2,
}

internal enum VirtualizationDecisionResultAbiV2 : byte
{
    Unspecified = 0,
    NoPayload = 1,
    ArchitecturalScalar64 = 2,
    ScalarU64ToDestinationRegister = 3,
}

internal enum VirtualizationDecisionEffectClassV2 : byte
{
    Unspecified = 0,
    NoStateNoPayload = 1,
    ReadOnlyProjectionNoStateMutation = 2,
    ArchitecturalRegisterResultOnly = 3,
}

internal enum VirtualizationCapabilityRequirementV2 : byte
{
    Unspecified = 0,
    DomainGrantedVmCallProbeNoStateV1 = 1,
    None = 2,
}

internal enum VirtualizationDelegationPolicyV2 : byte
{
    Unspecified = 0,
    NonDelegable = 1,
}

internal enum VirtualizationRevocationPolicyV2 : byte
{
    Unspecified = 0,
    RuntimeRevocable = 1,
    GovernanceRevocable = 2,
}

internal enum VirtualizationCapabilityMigrationClassV2 : byte
{
    Unspecified = 0,
    DomainLocal = 1,
    None = 2,
}

internal enum VirtualizationEvidenceVisibilityV2 : byte
{
    Unspecified = 0,
    HostOnly = 1,
    GuestVisibleReadOnly = 2,
}

internal enum VirtualizationProjectionPolicyV2 : byte
{
    Unspecified = 0,
    NeverProject = 1,
    ExactReadOnlyFieldSet = 2,
}

internal enum VirtualizationExecutionEvidenceRequirementV2 : byte
{
    Unspecified = 0,
    None = 1,
    FieldConformanceProof = 2,
}

internal enum VirtualizationDomainRequirementV2 : byte
{
    Unspecified = 0,
    ExecutionDomainBound = 1,
    ExecutionDomainAndAddressSpaceBound = 2,
}

internal enum VirtualizationAddressSpaceRequirementV2 : byte
{
    Unspecified = 0,
    None = 1,
    ExactNonZeroAddressSpaceTag = 2,
}

internal enum VirtualizationSecureDomainPolicyV2 : byte
{
    Unspecified = 0,
    Deny = 1,
}

internal enum VirtualizationCancellationPolicyV2 : byte
{
    Unspecified = 0,
    DenyBeforeExecution = 1,
    NotApplicableReadOnlyProjection = 2,
    SquashBeforeRetireZeroArchitecturalEffect = 3,
}

internal enum VirtualizationReplayPolicyV2 : byte
{
    Unspecified = 0,
    DenyAttemptReplay = 1,
    NotApplicableReadOnlyProjection = 2,
    AttemptBoundReceiptNoReplayReuse = 3,
}

internal enum VirtualizationOperationMigrationPolicyV2 : byte
{
    Unspecified = 0,
    DrainOnly = 1,
    RevalidatedAfterRestore = 2,
}

internal enum VirtualizationCompletionEvidenceClassV2 : byte
{
    Unspecified = 0,
    HostOwnedRuntimeEvidence = 1,
    None = 2,
}

internal enum VirtualizationCompletionMigrationClassV2 : byte
{
    Unspecified = 0,
    HostOwnedNonMigratable = 1,
    None = 2,
}

internal enum VirtualizationCompletionPolicyV2 : byte
{
    Unspecified = 0,
    AtomicE3ToCompletionRecordAndE5 = 1,
    None = 2,
}

internal enum VirtualizationRetirePolicyV2 : byte
{
    Unspecified = 0,
    PreciseE5BoundNoStateRetire = 1,
    None = 2,
    CanonicalRetireCoordinatorArchitecturalRegisterCommit = 3,
}

internal enum VirtualizationAdjacentLeafPolicyV2 : byte
{
    Unspecified = 0,
    DenyAllExceptExact = 1,
    DenyAllExceptExactFieldSet = 2,
}

internal enum VirtualizationDecisionOperationClassV2 : byte
{
    Unspecified = 0,
    ReadOnlyArchitecturalVmReadCompatibilityProjection = 1,
    ReadOnlyArchitecturalVmReadScalarDelivery = 2,
}

internal enum VirtualizationDecisionAuthorityPlaneV2 : byte
{
    Unspecified = 0,
    PrivilegedExecutionStateReadProjection = 1,
    PrivilegedExecutionStateSourceCanonicalRegisterDelivery = 2,
    ExecutionDomainReadOnlyStateCanonicalRegisterDelivery = 3,
    MemoryAddressSpaceReadProjection = 4,
}

internal enum VirtualizationDecisionMutationClassV2 : byte
{
    Unspecified = 0,
    ReadOnly = 1,
    UnderlyingVirtualizationStateReadOnly = 2,
}

internal enum VirtualizationCrossNamespacePolicyV2 : byte
{
    Unspecified = 0,
    AllowDistinctFrozenCompatibilityNamespaceOnly = 1,
    DenyCrossNamespaceReuse = 2,
}

internal enum VirtualizationDecisionAcceptanceStateV2 : byte
{
    Draft = 0,
    Accepted = 1,
    Revoked = 2,
    Superseded = 3,
}

internal enum VirtualizationDecisionReviewRoleV2 : byte
{
    Unspecified = 0,
    OwnerReviewRole = 1,
    ArchitectureReviewRole = 2,
}

internal enum VirtualizationDecisionReviewAuthorityPlaneV2 : byte
{
    Unspecified = 0,
    NeutralRuntimeOwner = 1,
    Architecture = 2,
    CompatibilityFrontend = 3,
}

internal enum VirtualizationDecisionReviewStateV2 : byte
{
    Missing = 0,
    Completed = 1,
}

internal enum VirtualizationDecisionRevocationStateV2 : byte
{
    Draft = 0,
    Effective = 1,
}

internal enum VirtualizationDecisionSupersessionStateV2 : byte
{
    Draft = 0,
    Effective = 1,
}

internal enum VirtualizationNamespaceClassV2 : byte
{
    RuntimeAuthority = 0,
    FrozenCompatibility = 1,
}

internal sealed record VirtualizationDecisionOwnerMapEntryV2(
    string FieldOrOperation,
    string Owner,
    string ValueSource,
    string CapabilityPolicy,
    string EvidenceClass,
    string MigrationClass,
    string DenialReason);

/// <summary>
/// Immutable governance specification. It describes policy, never a runtime grant.
/// </summary>
internal sealed record VirtualizationDecisionSpecV2(
    uint SchemaVersion,
    string DecisionId,
    string OperationNamespace,
    ushort LeafWidth,
    ushort InvalidLeaf,
    ushort NumericLeaf,
    string OperationId,
    VirtualizationDecisionOwnerClassV2 OwnerClass,
    ulong OwnerId,
    uint OwnerPolicyVersion,
    uint OwnerEpoch,
    uint OperandAbiVersion,
    string Rs1Contract,
    string Rs2Contract,
    string RdContract,
    VirtualizationDecisionResultAbiV2 ResultAbi,
    VirtualizationDecisionEffectClassV2 EffectClass,
    VirtualizationCapabilityRequirementV2 CapabilityRequirement,
    ulong CapabilityMask,
    bool RequiresTypedGrant,
    VirtualizationDelegationPolicyV2 DelegationPolicy,
    VirtualizationRevocationPolicyV2 RevocationPolicy,
    VirtualizationCapabilityMigrationClassV2 CapabilityMigrationClass,
    VirtualizationEvidenceVisibilityV2 EvidenceVisibility,
    VirtualizationProjectionPolicyV2 FrontendProjectionPolicy,
    VirtualizationExecutionEvidenceRequirementV2 ExecutionEvidenceRequirement,
    VirtualizationDomainRequirementV2 DomainRequirement,
    bool RequireNonZeroDomainTag,
    bool RequiresMemoryDomain,
    bool RequiresIoDomain,
    VirtualizationAddressSpaceRequirementV2 AddressSpaceRequirement,
    VirtualizationSecureDomainPolicyV2 SecureDomainPolicy,
    VirtualizationCancellationPolicyV2 CancellationPolicy,
    VirtualizationReplayPolicyV2 ReplayPolicy,
    VirtualizationOperationMigrationPolicyV2 OperationMigrationPolicy,
    VirtualizationCompletionEvidenceClassV2 CompletionEvidenceClass,
    VirtualizationCompletionMigrationClassV2 CompletionMigrationClass,
    VirtualizationProjectionPolicyV2 CompletionProjectionPolicy,
    VirtualizationCompletionPolicyV2 CompletionPolicy,
    VirtualizationRetirePolicyV2 RetirePolicy,
    VirtualizationAdjacentLeafPolicyV2 AdjacentLeafPolicy,
    VirtualizationCrossNamespacePolicyV2 CrossNamespacePolicy,
    ImmutableArray<VirtualizationDecisionOwnerMapEntryV2> OwnerMap,
    string SpecDigest,
    VirtualizationDecisionOperationClassV2 OperationClass =
        VirtualizationDecisionOperationClassV2.Unspecified,
    VirtualizationDecisionAuthorityPlaneV2 AuthorityPlane =
        VirtualizationDecisionAuthorityPlaneV2.Unspecified,
    ImmutableArray<ushort>? ExactFieldIds = null,
    VirtualizationDecisionMutationClassV2 MutationClass =
        VirtualizationDecisionMutationClassV2.Unspecified,
    string DependencyContract = "",
    bool VmcsMetadataOnly = false,
    bool RequiresConformanceProof = false);

internal sealed record VirtualizationDecisionReviewEvidenceV2(
    VirtualizationDecisionReviewRoleV2 Role,
    VirtualizationDecisionReviewAuthorityPlaneV2 AuthorityPlane,
    VirtualizationDecisionReviewStateV2 State,
    string Principal,
    string ReviewedDecisionId,
    string ReviewedSpecDigest,
    string ReviewedSpecCommitSha,
    string EvidenceId);

/// <summary>
/// Immutable later artifact referring to spec bytes already present at SpecCommitSha.
/// It never records the SHA of its own future containing commit.
/// </summary>
internal sealed record VirtualizationDecisionAcceptanceRecordV2(
    uint SchemaVersion,
    string DecisionId,
    string SpecDigest,
    string SpecCommitSha,
    VirtualizationDecisionAcceptanceStateV2 AcceptanceState,
    string AcceptedBy,
    uint AcceptancePolicyVersion,
    VirtualizationDecisionReviewEvidenceV2 OwnerReviewEvidence,
    VirtualizationDecisionReviewEvidenceV2 ArchitectureReviewEvidence,
    string CodeOwnersBlobSha,
    string? SupersedesDecisionId,
    string? SupersedesAcceptanceDigest,
    string AcceptanceDigest);

/// <summary>
/// Revocation is append-only governance lineage; an accepted record is never edited.
/// </summary>
internal sealed record VirtualizationDecisionRevocationRecordV2(
    uint SchemaVersion,
    string RevocationId,
    string DecisionId,
    string AcceptanceDigest,
    VirtualizationDecisionRevocationStateV2 State,
    string RevokedBy,
    string Reason,
    ulong Sequence,
    string RevocationDigest);

/// <summary>
/// Supersession is append-only governance lineage; both referenced decisions remain immutable.
/// </summary>
internal sealed record VirtualizationDecisionSupersessionRecordV2(
    uint SchemaVersion,
    string SupersessionId,
    string SupersededDecisionId,
    string SupersededAcceptanceDigest,
    string SupersedingDecisionId,
    string SupersedingAcceptanceDigest,
    VirtualizationDecisionSupersessionStateV2 State,
    string SupersededBy,
    ulong Sequence,
    string SupersessionDigest);

internal sealed record VirtualizationCodeOwnersRuleV2(string Scope, string Principal);

internal sealed record VirtualizationCodeOwnersEvidenceV2(
    bool FilePresent,
    string BlobSha,
    ImmutableArray<VirtualizationCodeOwnersRuleV2> Rules);

internal sealed record VirtualizationNamespaceAllocationV2(
    string OperationNamespace,
    ushort LeafWidth,
    ushort NumericLeaf,
    string DecisionId,
    VirtualizationNamespaceClassV2 NamespaceClass);

internal sealed record VirtualizationDecisionRevocationEvidenceV2(
    VirtualizationDecisionRevocationRecordV2 Record,
    ImmutableArray<byte> CanonicalBytes);

internal sealed record VirtualizationDecisionSupersessionEvidenceV2(
    VirtualizationDecisionSupersessionRecordV2 Record,
    ImmutableArray<byte> CanonicalBytes);

internal sealed record VirtualizationDecisionValidationEvidenceV2(
    ImmutableArray<byte> SpecCanonicalBytes,
    ImmutableArray<byte> AcceptanceCanonicalBytes,
    ImmutableArray<byte> SpecBytesAtCommit,
    string ResolvedSpecCommitSha,
    string AcceptanceContainingCommitSha,
    VirtualizationCodeOwnersEvidenceV2 CodeOwners,
    ImmutableArray<VirtualizationNamespaceAllocationV2> ExistingAllocations,
    ImmutableArray<VirtualizationDecisionRevocationEvidenceV2> Revocations,
    ImmutableArray<VirtualizationDecisionSupersessionEvidenceV2> Supersessions);

/// <summary>
/// Positive validator output is immutable policy metadata. It is deliberately not a
/// capability, grant, owner registry entry, operation lookup, admission or execution token.
/// </summary>
internal sealed record AcceptedVirtualizationDecision(
    string DecisionId,
    string SpecDigest,
    string AcceptanceDigest,
    string SpecCommitSha,
    string OperationNamespace,
    ushort NumericLeaf,
    ulong OwnerId,
    uint OwnerPolicyVersion,
    uint OwnerEpoch,
    VirtualizationDecisionEffectClassV2 EffectClass,
    VirtualizationDecisionAdjacentPolicySnapshotV2 AdjacentPolicy);

/// <summary>
/// Machine-accepted read-projection policy metadata. It deliberately has no
/// projection value, capability, admission, execution or publication method.
/// </summary>
internal sealed record AcceptedVmReadProjectionDecisionV2(
    string DecisionId,
    string SpecDigest,
    string AcceptanceDigest,
    string SpecCommitSha,
    string OperationNamespace,
    ImmutableArray<ushort> ExactFieldIds,
    ulong OwnerId,
    uint OwnerPolicyVersion,
    uint OwnerEpoch,
    VirtualizationDecisionMutationClassV2 MutationClass);

/// <summary>
/// Machine-accepted scalar-delivery policy metadata. It is not a source owner,
/// result receipt, execution token, writeback packet or retire authorization.
/// </summary>
internal sealed record AcceptedVmReadScalarDeliveryDecisionV2(
    string DecisionId,
    string SpecDigest,
    string AcceptanceDigest,
    string SpecCommitSha,
    string OperationNamespace,
    ImmutableArray<ushort> ExactFieldIds,
    ulong SourceOwnerId,
    uint SourceOwnerPolicyVersion,
    uint SourceOwnerEpoch,
    VirtualizationDecisionResultAbiV2 ResultAbi,
    VirtualizationDecisionEffectClassV2 EffectClass,
    VirtualizationOperationMigrationPolicyV2 MigrationPolicy,
    VirtualizationRetirePolicyV2 RetirePolicy);

internal sealed record VirtualizationDecisionAdjacentPolicySnapshotV2(
    VirtualizationAdjacentLeafPolicyV2 AdjacentLeafPolicy,
    VirtualizationCrossNamespacePolicyV2 CrossNamespacePolicy);
