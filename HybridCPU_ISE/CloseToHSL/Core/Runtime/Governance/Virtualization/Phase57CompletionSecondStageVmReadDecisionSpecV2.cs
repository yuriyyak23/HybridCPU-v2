using System.Collections.Immutable;

namespace YAKSys_Hybrid_CPU.Core;

internal static class Phase57CompletionSecondStageVmReadDecisionSpecV2
{
    internal const string ExpectedSpecDigest =
        "c68adf194f0452c15f4d11e3a44199b50a0a6168923915e280c62fcbe683c4ab";
    internal static VirtualizationDecisionSpecV2 Instance { get; } = Create();
    internal static ImmutableArray<byte> CanonicalBytes { get; } =
        VirtualizationDecisionCanonicalEncoderV2.EncodeSpec(Instance);
    internal static bool RuntimeAuthorityGranted => false;
    internal static bool ProductionCompositionAuthorized => false;

    private static VirtualizationDecisionSpecV2 Create()
    {
        VirtualizationDecisionSpecV2 spec = new(
            CompletionSecondStageVmReadDecisionValidatorV2.SchemaVersion,
            CompletionSecondStageVmReadDecisionValidatorV2.ExpectedDecisionId,
            CompletionSecondStageVmReadDecisionValidatorV2.ExpectedOperationNamespace,
            0, 0, 0,
            CompletionSecondStageVmReadDecisionValidatorV2.ExpectedOperationId,
            VirtualizationDecisionOwnerClassV2.NeutralRuntimeOwner,
            CompletionSecondStageVmReadDecisionValidatorV2.ExpectedSourceOwnerId,
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
            VirtualizationDomainRequirementV2.ExecutionDomainBound,
            true, false, false,
            VirtualizationAddressSpaceRequirementV2.None,
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
            VirtualizationDecisionAuthorityPlaneV2.CompletionObservationReadProjection,
            Phase57CompletionSecondStageVmReadE0Contract.ExactFieldIds,
            VirtualizationDecisionMutationClassV2.UnderlyingVirtualizationStateReadOnly,
            CompletionSecondStageVmReadDecisionValidatorV2.ExpectedDependencyContract,
            true, true);
        spec = spec with { SpecDigest = VirtualizationDecisionCanonicalEncoderV2.ComputeSpecDigest(spec) };
        if (!string.Equals(spec.SpecDigest, ExpectedSpecDigest, StringComparison.Ordinal))
            throw new InvalidOperationException($"Phase 57 immutable SpecV2 digest mismatch: {spec.SpecDigest}");
        return spec;
    }

    private static ImmutableArray<VirtualizationDecisionOwnerMapEntryV2> CreateOwnerMap() =>
    [
        Entry("VmcsField.GuestPhysicalAddress", "NeutralCompletionObservationSnapshot.Facts.FaultAddress", "ExactPresentGuestPhysicalAddressSemantic"),
        Entry("VmcsField.EptViolationQualification", "NeutralCompletionObservationSnapshot.Facts.FaultAuxiliary", "ExactReasonQualificationAuxiliaryAndProvenanceMapping"),
    ];

    private static VirtualizationDecisionOwnerMapEntryV2 Entry(string field, string source, string evidence) =>
        new(field, "DomainCompletionObservationOwner", source, "None",
            $"GuestVisibleReadOnly+ExactCpuSecondStageProducerSemanticTuple+{evidence}+CompletionGenerationRestoreOwnerScopeAttemptEventDigestTranslationProvenanceProof",
            "DrainOnly",
            "DenyOnMissingOrForeignProducerAbsentOrIncompatibleReasonQualificationAddressAuxiliaryProvenanceStaleGenerationRestoreScopeAttemptEventDigestReplayDestinationOrCanonicalCarrierGate");
}
