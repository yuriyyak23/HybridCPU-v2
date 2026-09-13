using System.Collections.Immutable;

namespace YAKSys_Hybrid_CPU.Core;

internal static class Phase55CompletionReasonQualificationVmReadDecisionSpecV2
{
    internal const string ExpectedSpecDigest =
        "649f576e09ca0168ba60be3f632a4d8c93f6fabaa8571c22a5445f438dcda6d2";
    internal static VirtualizationDecisionSpecV2 Instance { get; } = Create();
    internal static ImmutableArray<byte> CanonicalBytes { get; } =
        VirtualizationDecisionCanonicalEncoderV2.EncodeSpec(Instance);
    internal static bool RuntimeAuthorityGranted => false;
    internal static bool ProductionCompositionAuthorized => false;

    private static VirtualizationDecisionSpecV2 Create()
    {
        VirtualizationDecisionSpecV2 spec = new(
            CompletionReasonQualificationVmReadDecisionValidatorV2.SchemaVersion,
            CompletionReasonQualificationVmReadDecisionValidatorV2.ExpectedDecisionId,
            CompletionReasonQualificationVmReadDecisionValidatorV2.ExpectedOperationNamespace,
            0, 0, 0,
            CompletionReasonQualificationVmReadDecisionValidatorV2.ExpectedOperationId,
            VirtualizationDecisionOwnerClassV2.NeutralRuntimeOwner,
            CompletionReasonQualificationVmReadDecisionValidatorV2.ExpectedSourceOwnerId,
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
            Phase55CompletionReasonQualificationVmReadE0Contract.ExactFieldIds,
            VirtualizationDecisionMutationClassV2.UnderlyingVirtualizationStateReadOnly,
            CompletionReasonQualificationVmReadDecisionValidatorV2.ExpectedDependencyContract,
            true, true);
        spec = spec with { SpecDigest = VirtualizationDecisionCanonicalEncoderV2.ComputeSpecDigest(spec) };
        if (!string.Equals(spec.SpecDigest, ExpectedSpecDigest, StringComparison.Ordinal))
            throw new InvalidOperationException($"Phase 55 immutable SpecV2 digest mismatch: {spec.SpecDigest}");
        return spec;
    }

    private static ImmutableArray<VirtualizationDecisionOwnerMapEntryV2> CreateOwnerMap() =>
    [
        Entry("VmcsField.ExitReason", "NeutralCompletionObservationSnapshot.Facts.Reason", "OwnerApprovedReasonMapping"),
        Entry("VmcsField.ExitQualification", "NeutralCompletionObservationSnapshot.Facts.Qualification", "ExactReasonBoundQualification"),
    ];

    private static VirtualizationDecisionOwnerMapEntryV2 Entry(string field, string source, string evidence) =>
        new(field, "DomainCompletionObservationOwner", source, "None",
            $"GuestVisibleReadOnly+ExactCpuTranslationProducerSemanticTuple+{evidence}+CompletionGenerationRestoreOwnerScopeAttemptEventDigestProof",
            "DrainOnly",
            "DenyOnMissingOrForeignProducerAbsentOrIncompatibleReasonQualificationStaleGenerationRestoreScopeAttemptEventDigestReplayDestinationOrCanonicalCarrierGate");
}
