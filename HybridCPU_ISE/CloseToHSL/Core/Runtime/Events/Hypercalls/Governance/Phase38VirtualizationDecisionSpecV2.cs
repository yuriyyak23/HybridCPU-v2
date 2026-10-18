using System.Collections.Immutable;

namespace YAKSys_Hybrid_CPU.Core;

/// <summary>
/// The byte-exact Phase 38 D2 specification artifact. This is immutable policy
/// input only: it is not an owner snapshot, capability, admission certificate,
/// executor, completion record or retire authorization.
/// </summary>
internal static class Phase38VirtualizationDecisionSpecV2
{
    internal static VirtualizationDecisionSpecV2 Instance { get; } = Create();

    internal static ImmutableArray<byte> CanonicalBytes { get; } =
        VirtualizationDecisionCanonicalEncoderV2.EncodeSpec(Instance);

    internal static bool RuntimeAuthorityGranted => false;
    internal static bool BackendExecutionAuthorized => false;
    internal static bool CompletionPublicationAuthorized => false;
    internal static bool RetirePublicationAuthorized => false;

    private static VirtualizationDecisionSpecV2 Create()
    {
        VirtualizationDecisionSpecV2 spec = new(
            VirtualizationDecisionValidatorV2.CurrentSchemaVersion,
            VirtualizationDecisionValidatorV2.ExpectedDecisionId,
            VirtualizationDecisionValidatorV2.ExpectedOperationNamespace,
            16,
            0,
            1,
            VirtualizationDecisionValidatorV2.ExpectedOperationId,
            VirtualizationDecisionOwnerClassV2.NeutralRuntimeOwner,
            VirtualizationDecisionValidatorV2.ExpectedOwnerId,
            1,
            1,
            1,
            "ArchitecturalRegisterFullNumericLeafValue",
            "X0",
            "X0NoResult",
            VirtualizationDecisionResultAbiV2.NoPayload,
            VirtualizationDecisionEffectClassV2.NoStateNoPayload,
            VirtualizationCapabilityRequirementV2.DomainGrantedVmCallProbeNoStateV1,
            VirtualizationDecisionValidatorV2.ExpectedCapabilityMask,
            true,
            VirtualizationDelegationPolicyV2.NonDelegable,
            VirtualizationRevocationPolicyV2.RuntimeRevocable,
            VirtualizationCapabilityMigrationClassV2.DomainLocal,
            VirtualizationEvidenceVisibilityV2.HostOnly,
            VirtualizationProjectionPolicyV2.NeverProject,
            VirtualizationExecutionEvidenceRequirementV2.None,
            VirtualizationDomainRequirementV2.ExecutionDomainBound,
            true,
            false,
            false,
            VirtualizationAddressSpaceRequirementV2.None,
            VirtualizationSecureDomainPolicyV2.Deny,
            VirtualizationCancellationPolicyV2.DenyBeforeExecution,
            VirtualizationReplayPolicyV2.DenyAttemptReplay,
            VirtualizationOperationMigrationPolicyV2.DrainOnly,
            VirtualizationCompletionEvidenceClassV2.HostOwnedRuntimeEvidence,
            VirtualizationCompletionMigrationClassV2.HostOwnedNonMigratable,
            VirtualizationProjectionPolicyV2.NeverProject,
            VirtualizationCompletionPolicyV2.AtomicE3ToCompletionRecordAndE5,
            VirtualizationRetirePolicyV2.PreciseE5BoundNoStateRetire,
            VirtualizationAdjacentLeafPolicyV2.DenyAllExceptExact,
            VirtualizationCrossNamespacePolicyV2.AllowDistinctFrozenCompatibilityNamespaceOnly,
            CreateOwnerMap(),
            new string('0', 64));

        return spec with
        {
            SpecDigest = VirtualizationDecisionCanonicalEncoderV2.ComputeSpecDigest(spec),
        };
    }

    private static ImmutableArray<VirtualizationDecisionOwnerMapEntryV2> CreateOwnerMap() =>
    [
        Entry("Operation", "DomainHypercallRuntimeOwner"),
        Entry("OperandAbi", "CanonicalOperandSnapshotOwner"),
        Entry("CapabilityAdmission", "RuntimeCapabilityOwner"),
        Entry("DomainAdmission", "RuntimeDomainOwner"),
        Entry("ExecutionAdmission", "SafetyVerifier"),
        Entry("CancellationReplay", "DomainHypercallRuntimeOwner"),
        Entry("CompletionPublication", "NeutralCompletionOwner"),
        Entry("RetirePublication", "CanonicalRetireOwner"),
        Entry("MigrationRestore", "CheckpointRestoreOwner"),
        Entry("AdjacentLeafDenial", "VirtualizationDecisionValidatorV2"),
    ];

    private static VirtualizationDecisionOwnerMapEntryV2 Entry(string field, string owner) =>
        new(
            field,
            owner,
            "Phase38ExactValueSource",
            "ExactTypedGrantOrNone",
            "HostOnlyOrNone",
            "DrainOnlyOrHostOwnedNonMigratable",
            "DenyOnMissingOrMismatch");
}
