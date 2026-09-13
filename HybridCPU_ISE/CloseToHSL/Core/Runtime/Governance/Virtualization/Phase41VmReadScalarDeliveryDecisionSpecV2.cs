using System.Collections.Immutable;

namespace YAKSys_Hybrid_CPU.Core;

/// <summary>
/// Immutable governance specification for exact GuestCr0/GuestCr4 scalar
/// delivery. It does not issue a result receipt or authorize runtime writeback.
/// </summary>
internal static class Phase41VmReadScalarDeliveryDecisionSpecV2
{
    internal const string ExpectedSpecDigest =
        "ccda8698dbeb3f6eef1b4f13e22a3fb7607e939f493138fb7e3373674e234309";

    internal static VirtualizationDecisionSpecV2 Instance { get; } = Create();

    internal static ImmutableArray<byte> CanonicalBytes { get; } =
        VirtualizationDecisionCanonicalEncoderV2.EncodeSpec(Instance);

    internal static bool RuntimeAuthorityGranted => false;
    internal static bool SourceValueAvailable => false;
    internal static bool ResultReceiptIssued => false;
    internal static bool RegisterWritebackAuthorized => false;
    internal static bool RetireCommitAuthorized => false;
    internal static bool BackendExecutionAuthorized => false;
    internal static bool UnderlyingVirtualizationMutationAuthorized => false;
    internal static bool CompletionPublicationAuthorized => false;

    private static VirtualizationDecisionSpecV2 Create()
    {
        VirtualizationDecisionSpecV2 spec = new(
            VmReadScalarDeliveryDecisionValidatorV2.SchemaVersion,
            VmReadScalarDeliveryDecisionValidatorV2.ExpectedDecisionId,
            VmReadScalarDeliveryDecisionValidatorV2.ExpectedOperationNamespace,
            LeafWidth: 0,
            InvalidLeaf: 0,
            NumericLeaf: 0,
            VmReadScalarDeliveryDecisionValidatorV2.ExpectedOperationId,
            VirtualizationDecisionOwnerClassV2.NeutralRuntimeOwner,
            VmReadScalarDeliveryDecisionValidatorV2.ExpectedSourceOwnerId,
            OwnerPolicyVersion: 1,
            OwnerEpoch: 1,
            OperandAbiVersion: 1,
            Rs1Contract: "VmcsFieldSelectorExactFrozenIdFromCanonicalSourceRegister",
            Rs2Contract: "X0ReservedNoAuthority",
            RdContract: "ArchitecturalDestinationRegisterX1ToX31CanonicalRenameIdentity",
            VirtualizationDecisionResultAbiV2.ScalarU64ToDestinationRegister,
            VirtualizationDecisionEffectClassV2.ArchitecturalRegisterResultOnly,
            VirtualizationCapabilityRequirementV2.None,
            CapabilityMask: 0,
            RequiresTypedGrant: false,
            VirtualizationDelegationPolicyV2.NonDelegable,
            VirtualizationRevocationPolicyV2.GovernanceRevocable,
            VirtualizationCapabilityMigrationClassV2.None,
            VirtualizationEvidenceVisibilityV2.GuestVisibleReadOnly,
            VirtualizationProjectionPolicyV2.ExactReadOnlyFieldSet,
            VirtualizationExecutionEvidenceRequirementV2.FieldConformanceProof,
            VirtualizationDomainRequirementV2.ExecutionDomainAndAddressSpaceBound,
            RequireNonZeroDomainTag: true,
            RequiresMemoryDomain: false,
            RequiresIoDomain: false,
            VirtualizationAddressSpaceRequirementV2.ExactNonZeroAddressSpaceTag,
            VirtualizationSecureDomainPolicyV2.Deny,
            VirtualizationCancellationPolicyV2.SquashBeforeRetireZeroArchitecturalEffect,
            VirtualizationReplayPolicyV2.AttemptBoundReceiptNoReplayReuse,
            VirtualizationOperationMigrationPolicyV2.DrainOnly,
            VirtualizationCompletionEvidenceClassV2.None,
            VirtualizationCompletionMigrationClassV2.None,
            VirtualizationProjectionPolicyV2.NeverProject,
            VirtualizationCompletionPolicyV2.None,
            VirtualizationRetirePolicyV2.CanonicalRetireCoordinatorArchitecturalRegisterCommit,
            VirtualizationAdjacentLeafPolicyV2.DenyAllExceptExactFieldSet,
            VirtualizationCrossNamespacePolicyV2.DenyCrossNamespaceReuse,
            CreateOwnerMap(),
            SpecDigest: new string('0', 64),
            VirtualizationDecisionOperationClassV2.ReadOnlyArchitecturalVmReadScalarDelivery,
            VirtualizationDecisionAuthorityPlaneV2.PrivilegedExecutionStateSourceCanonicalRegisterDelivery,
            Phase41VmReadScalarDeliveryE0Contract.ExactFieldIds,
            VirtualizationDecisionMutationClassV2.UnderlyingVirtualizationStateReadOnly,
            DependencyContract: VmReadScalarDeliveryDecisionValidatorV2.ExpectedDependencyContract,
            VmcsMetadataOnly: true,
            RequiresConformanceProof: true);

        spec = spec with
        {
            SpecDigest = VirtualizationDecisionCanonicalEncoderV2.ComputeSpecDigest(spec),
        };
        if (!string.Equals(spec.SpecDigest, ExpectedSpecDigest, StringComparison.Ordinal))
            throw new InvalidOperationException(
                "The VMREAD scalar-delivery SpecV2 canonical digest drifted.");

        return spec;
    }

    private static ImmutableArray<VirtualizationDecisionOwnerMapEntryV2> CreateOwnerMap() =>
    [
        Entry("VmcsField.GuestCr0", "PrivilegedExecutionStateDescriptor.GuestCr0"),
        Entry("VmcsField.GuestCr4", "PrivilegedExecutionStateDescriptor.GuestCr4"),
    ];

    private static VirtualizationDecisionOwnerMapEntryV2 Entry(
        string field,
        string valueSource) =>
        new(
            field,
            "PrivilegedExecutionStateOwnerPolicy",
            valueSource,
            "None",
            "GuestVisibleReadOnlyProjection+FieldConformanceProof+OpaqueAttemptBoundVmReadScalarResultReceipt",
            "DrainOnly",
            "DenyOnAnyMissingOldProjectionD2OwnerDomainAddressSpaceEpochBitsEvidenceConformanceAttemptReplayDestinationOrCanonicalCarrierGate");
}
