using System.Collections.Immutable;

namespace YAKSys_Hybrid_CPU.Core;

internal static class Phase57CompletionSecondStageVmReadE0Contract
{
    internal static ImmutableArray<ushort> ExactFieldIds { get; } =
    [
        (ushort)VmcsField.GuestPhysicalAddress,
        (ushort)VmcsField.EptViolationQualification,
    ];

    internal static ImmutableArray<VmReadScalarDeliveryE0FindingV2> Findings { get; } =
    [
        new(1, "Authority", "DomainCompletionObservationOwnerReadOnlyCommittedSnapshotOnly"),
        new(2, "Producer", "ExactCanonicalCpuSecondStageTranslationFaultProducerOnly"),
        new(3, "Gpa", "PresentGuestPhysicalAddressSemanticCopiedExactlyIncludingZero"),
        new(4, "Auxiliary", "PresentSecondStageTranslationViolationSemanticDecodedExactly"),
        new(5, "Reason", "SecondStageViolationOrSecondStageMisconfigurationMustMatchAuxiliaryKind"),
        new(6, "Qualification", "ReasonAccessKindAccessSizeMustMatchAuxiliaryTuple"),
        new(7, "CompatibilityMapping", "AccessBitMisconfigurationBitAndPageWalkLevelMappedExplicitly"),
        new(8, "Provenance", "MemoryOperationTranslationOwnerMemoryDomainAddressSpaceGenerationAndIdentityRequired"),
        new(9, "Lifecycle", "SnapshotGenerationRestoreOwnerScopeAttemptEventDigestAndProvenanceBoundReceipt"),
        new(10, "Delivery", "ExistingE1CarrierReceiptPRFWritebackRetireRegisterPathOnly"),
        new(11, "NoFallback", "NoInferenceZeroFallbackIommuNestedExceptionVmcsBackingOrNeutralEncodingPassThrough"),
        new(12, "Activation", "ExactPolicyOptInDefaultDisabledAndNoAdjacentFieldActivation"),
    ];

    internal static bool RuntimeAuthorityGranted => false;
    internal static bool CompletionCreationAuthorized => false;
    internal static bool VmxAuthorityGranted => false;
    internal static bool ProductionCompositionAuthorized => false;
}
