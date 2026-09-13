using System.Collections.Immutable;

namespace YAKSys_Hybrid_CPU.Core;

internal static class Phase43GuestPcSpFlagsVmReadScalarDeliveryDecisionSpecV2
{
    internal const string ExpectedSpecDigest =
        "e67ff2620ff6a1fd193b8303c5b6ae1d532e51241e6b3e405f3c6cedefe2d754";

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
    internal static bool ProductionCompositionAuthorized => false;

    private static VirtualizationDecisionSpecV2 Create()
    {
        VirtualizationDecisionSpecV2 spec = new(
            GuestPcSpFlagsVmReadScalarDeliveryDecisionValidatorV2.SchemaVersion,
            GuestPcSpFlagsVmReadScalarDeliveryDecisionValidatorV2.ExpectedDecisionId,
            GuestPcSpFlagsVmReadScalarDeliveryDecisionValidatorV2.ExpectedOperationNamespace,
            LeafWidth: 0,
            InvalidLeaf: 0,
            NumericLeaf: 0,
            GuestPcSpFlagsVmReadScalarDeliveryDecisionValidatorV2.ExpectedOperationId,
            VirtualizationDecisionOwnerClassV2.NeutralRuntimeOwner,
            GuestPcSpFlagsVmReadScalarDeliveryDecisionValidatorV2.ExpectedSourceOwnerId,
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
            RequiresMemoryDomain: true,
            RequiresIoDomain: true,
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
            VirtualizationDecisionAuthorityPlaneV2.ExecutionDomainReadOnlyStateCanonicalRegisterDelivery,
            Phase43GuestPcSpFlagsVmReadScalarDeliveryE0Contract.ExactFieldIds,
            VirtualizationDecisionMutationClassV2.UnderlyingVirtualizationStateReadOnly,
            DependencyContract: GuestPcSpFlagsVmReadScalarDeliveryDecisionValidatorV2.ExpectedDependencyContract,
            VmcsMetadataOnly: true,
            RequiresConformanceProof: true);

        spec = spec with { SpecDigest = VirtualizationDecisionCanonicalEncoderV2.ComputeSpecDigest(spec) };
        if (spec.SpecDigest != ExpectedSpecDigest)
            throw new InvalidOperationException($"Phase 43 SpecV2 digest is {spec.SpecDigest}.");
        return spec;
    }

    private static ImmutableArray<VirtualizationDecisionOwnerMapEntryV2> CreateOwnerMap() =>
    [
        Entry("VmcsField.GuestPc", "ExecutionDomainReadOnlyStateView.GuestPc"),
        Entry("VmcsField.GuestSp", "ExecutionDomainReadOnlyStateView.GuestSp"),
        Entry("VmcsField.GuestFlags", "ExecutionDomainReadOnlyStateView.GuestFlags"),
    ];

    private static VirtualizationDecisionOwnerMapEntryV2 Entry(string field, string source) =>
        new(field, "ExecutionDomainDescriptor", source, "None",
            "GuestVisibleReadOnlyCompatibilityProjection+MaterializedFieldAndSourceStateProof+OpaqueAttemptBoundReceipt",
            "DrainOnly",
            "DenyOnMissingOwnerMaterializationDomainAddressSpaceSourceEpochEvidenceD2AttemptReplayRestoreProfileDestinationOrCanonicalCarrierGate");
}
