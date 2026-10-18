using System.Collections.Immutable;

namespace YAKSys_Hybrid_CPU.Core;

internal static class Phase55CompletionReasonQualificationVmReadE0Contract
{
    internal static ImmutableArray<ushort> ExactFieldIds { get; } =
    [
        (ushort)VmcsField.ExitReason,
        (ushort)VmcsField.ExitQualification,
    ];

    internal static ImmutableArray<ushort> ExplicitlyDeniedFieldIds { get; } =
    [
        (ushort)VmcsField.GuestPhysicalAddress,
        (ushort)VmcsField.EptViolationQualification,
    ];

    internal static ImmutableArray<VmReadScalarDeliveryE0FindingV2> Findings { get; } =
    [
        new(1, "Authority", "DomainCompletionObservationOwnerReadOnlySnapshotOnly"),
        new(2, "Producer", "ExactCanonicalCpuInstructionTranslationFaultProducerOnly"),
        new(3, "MappedReasons", "AccessDeniedOrOwnerScopeMismatchToFrozenSecurityPolicyViolation"),
        new(4, "Qualification", "ExactReasonAccessKindAccessSizeEncodingReturnedUnchanged"),
        new(5, "PairBinding", "ExitReasonAndExitQualificationRequireTheSameOwnerApprovedSemanticTuple"),
        new(6, "AbsentVersusZero", "PresentBitsRequiredAndZeroQualificationCannotBeSynthesized"),
        new(7, "GuestPhysicalAddress", "DeniedVirtualAddressIsNotGuestPhysicalAddress"),
        new(8, "EptViolationQualification", "DeniedTranslationFaultAuxiliaryIsNotSecondStageTranslationViolation"),
        new(9, "Lifecycle", "SnapshotGenerationRestoreOwnerScopeAttemptEventAndDigestBoundReceipt"),
        new(10, "Delivery", "ExistingE1CarrierReceiptPRFWritebackRetireRegisterPathOnly"),
        new(11, "NoFallback", "NoInferenceZeroIommuNestedExceptionVmcsBackingOrVmxAuthority"),
        new(12, "Activation", "ExactPolicyOptInDefaultDisabledAndNoAdjacentFieldActivation"),
    ];

    internal static bool RuntimeAuthorityGranted => false;
    internal static bool CompletionCreationAuthorized => false;
    internal static bool VmxAuthorityGranted => false;
    internal static bool GuestPhysicalAddressAuthorized => false;
    internal static bool EptViolationQualificationAuthorized => false;
    internal static bool ProductionCompositionAuthorized => false;
}
