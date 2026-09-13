using System.Collections.Immutable;

namespace YAKSys_Hybrid_CPU.Core;

internal static class Phase45MemoryOwnedVmReadScalarDeliveryDecisionSpecV2
{
    internal const string ExpectedSpecDigest =
        "7cc2ad6bca9cc808aa6d42767dba5c7eaefed1a34180cb6caf3b34384662df21";
    internal static VirtualizationDecisionSpecV2 Instance { get; } = Create();
    internal static ImmutableArray<byte> CanonicalBytes { get; } =
        VirtualizationDecisionCanonicalEncoderV2.EncodeSpec(Instance);
    internal static bool RuntimeAuthorityGranted => false;
    internal static bool SourceValueAvailable => false;
    internal static bool ResultReceiptIssued => false;
    internal static bool ProductionCompositionAuthorized => false;

    private static VirtualizationDecisionSpecV2 Create()
    {
        VirtualizationDecisionSpecV2 spec = new(
            MemoryOwnedVmReadScalarDeliveryDecisionValidatorV2.SchemaVersion,
            MemoryOwnedVmReadScalarDeliveryDecisionValidatorV2.ExpectedDecisionId,
            MemoryOwnedVmReadScalarDeliveryDecisionValidatorV2.ExpectedOperationNamespace,
            0, 0, 0,
            MemoryOwnedVmReadScalarDeliveryDecisionValidatorV2.ExpectedOperationId,
            VirtualizationDecisionOwnerClassV2.NeutralRuntimeOwner,
            MemoryOwnedVmReadScalarDeliveryDecisionValidatorV2.ExpectedSourceOwnerId,
            1, 1, 1,
            "VmcsFieldSelectorExactFrozenIdFromCanonicalSourceRegister",
            "X0ReservedNoAuthority",
            "ArchitecturalDestinationRegisterX1ToX31CanonicalRenameIdentity",
            VirtualizationDecisionResultAbiV2.ScalarU64ToDestinationRegister,
            VirtualizationDecisionEffectClassV2.ArchitecturalRegisterResultOnly,
            VirtualizationCapabilityRequirementV2.None, 0, false,
            VirtualizationDelegationPolicyV2.NonDelegable,
            VirtualizationRevocationPolicyV2.GovernanceRevocable,
            VirtualizationCapabilityMigrationClassV2.None,
            VirtualizationEvidenceVisibilityV2.GuestVisibleReadOnly,
            VirtualizationProjectionPolicyV2.ExactReadOnlyFieldSet,
            VirtualizationExecutionEvidenceRequirementV2.FieldConformanceProof,
            VirtualizationDomainRequirementV2.ExecutionDomainAndAddressSpaceBound,
            true, true, true,
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
            CreateOwnerMap(), new string('0', 64),
            VirtualizationDecisionOperationClassV2.ReadOnlyArchitecturalVmReadScalarDelivery,
            VirtualizationDecisionAuthorityPlaneV2.MemoryAddressSpaceReadProjection,
            Phase45MemoryOwnedVmReadScalarDeliveryE0Contract.ExactFieldIds,
            VirtualizationDecisionMutationClassV2.UnderlyingVirtualizationStateReadOnly,
            MemoryOwnedVmReadScalarDeliveryDecisionValidatorV2.ExpectedDependencyContract,
            true, true);
        spec = spec with { SpecDigest = VirtualizationDecisionCanonicalEncoderV2.ComputeSpecDigest(spec) };
        if (!string.Equals(spec.SpecDigest, ExpectedSpecDigest, StringComparison.Ordinal))
        {
            throw new InvalidOperationException("Phase 45 immutable SpecV2 digest mismatch.");
        }

        return spec;
    }

    private static ImmutableArray<VirtualizationDecisionOwnerMapEntryV2> CreateOwnerMap() =>
    [
        Entry("VmcsField.GuestCr3", "MemoryDomainReadOnlyTranslationView.AddressSpaceRoot", "GuestArchitecturalState"),
        Entry("VmcsField.EptPointer", "MemoryDomainReadOnlyTranslationView.SecondStageRoot", "CompatibilityAlias+OwnedValidSecondStage"),
        Entry("VmcsField.Vpid", "MemoryDomainReadOnlyTranslationView.AddressSpaceTag", "CompatibilityAlias+EnabledNonZeroTag"),
        Entry("VmcsField.Cr3TargetCount", "MemoryDomainReadOnlyTranslationView.AddressSpaceTargetCount", "CompatibilityAlias+CanonicalTargetCount"),
    ];

    private static VirtualizationDecisionOwnerMapEntryV2 Entry(string field, string source, string evidence) =>
        new(field, "MemoryDomainDescriptor", source, "None",
            $"GuestVisibleReadOnly+{evidence}+MaterializedFieldSourceStateProof+RuntimeOwnedAddressSpaceGenerationProof",
            "DrainOnly",
            "DenyOnMissingOwnerMaterializationDomainAddressSpaceCurrentGenerationFieldEvidenceD2AttemptReplayRestoreProfileDestinationOrCanonicalCarrierGate");
}
