using System.Collections.Immutable;

namespace YAKSys_Hybrid_CPU.Core;

internal enum CurrentCompletionFieldE0Disposition : byte
{
    BlockedMissingOwnerApprovedProjectionMapping = 0,
    BlockedAbsentForExactProducer = 1,
    BlockedSemanticMismatch = 2,
    BlockedNoEligibleProducer = 3,
}

internal readonly record struct CurrentCompletionFieldE0Entry(
    VmcsField Field,
    CurrentCompletionFieldE0Disposition Disposition,
    string NeutralSource,
    string DenialReason)
{
    internal bool IsD2Eligible => false;
}

internal static class Phase49CurrentCompletionVmReadScalarDeliveryE0Contract
{
    internal const string ProposedDecisionId =
        "D2-HV-VMREAD-SCALAR-DELIVERY-V1-CURRENT-COMPLETION-0004";
    internal const string OperationNamespace = "HybridCPU.VMREAD.ScalarDelivery.v1";
    internal const string OperationId = "DELIVER_CURRENT_COMPLETION_FIELDS_SCALAR_V1";
    internal const string BlockerCode =
        "IncompleteExactProducerFieldSemanticCoverageAndProjectionMapping";

    internal static ImmutableArray<ushort> ExactFieldIds { get; } =
    [
        (ushort)VmcsField.ExitReason,
        (ushort)VmcsField.ExitQualification,
        (ushort)VmcsField.GuestPhysicalAddress,
        (ushort)VmcsField.EptViolationQualification,
    ];

    internal static ImmutableArray<CurrentCompletionFieldE0Entry> FieldMatrix { get; } =
    [
        new(
            VmcsField.ExitReason,
            CurrentCompletionFieldE0Disposition.BlockedMissingOwnerApprovedProjectionMapping,
            "Present neutral TrapEntry reason from CanonicalPipelineTrapEntryProducer",
            "A neutral cause code is not an owner-approved VMX ExitReason mapping."),
        new(
            VmcsField.ExitQualification,
            CurrentCompletionFieldE0Disposition.BlockedAbsentForExactProducer,
            "Qualification is explicitly absent and producer policy disallows it",
            "The only registered production producer cannot provide qualification."),
        new(
            VmcsField.GuestPhysicalAddress,
            CurrentCompletionFieldE0Disposition.BlockedSemanticMismatch,
            "TrapEntry permits only VirtualAddress semantic",
            "GuestPhysicalAddress requires a present GuestPhysicalAddress semantic fact."),
        new(
            VmcsField.EptViolationQualification,
            CurrentCompletionFieldE0Disposition.BlockedNoEligibleProducer,
            "Auxiliary is explicitly absent and producer policy permits None only",
            "No registered production producer supplies second-stage translation-violation auxiliary data."),
    ];

    internal static ImmutableArray<VmReadScalarDeliveryE0FindingV2> Findings { get; } =
    [
        new(1, "P48Foundation", "CanonicalCommitReceiptObservationGenerationPresenceAndSemanticDenialAreProvenNeutralFoundations"),
        new(2, "ExactProducer", "CanonicalPipelineTrapEntryProducerIsTheOnlyRegisteredProductionProducer"),
        new(3, "ReasonCoverage", "PresentNeutralTrapCauseExistsButNoOwnerApprovedExitReasonMappingExists"),
        new(4, "QualificationCoverage", "ExactProducerPolicyDisallowsQualificationAndProductionFactIsAbsent"),
        new(5, "AddressCoverage", "ExactProducerAllowsVirtualAddressOnlyGuestPhysicalSemanticIsUnavailable"),
        new(6, "AuxiliaryCoverage", "NoSecondStageTranslationViolationAuxiliaryProducerIsRegistered"),
        new(7, "GroupAtomicity", "ExactFourFieldOperationCannotBePartiallyAuthorized"),
        new(8, "CallerIsolation", "CallerProvidedCompletionAndGenerationRemainForbidden"),
        new(9, "CompatibilityIsolation", "CompletionRecordCompatibilityFactoryAndVmcsBackingStoreAreNotAuthority"),
        new(10, "VmCallIsolation", "DomainHypercallCompletionOwnerE5E6CannotBeReused"),
        new(11, "TrapIsolation", "TrapRouteAndFenceResultsCannotAuthorizeProjection"),
        new(12, "NoFallback", "AbsentOrSemanticMismatchMustDenyAndSuccessfulZeroFallbackIsForbidden"),
        new(13, "SpecMaterialization", "DeniedBecauseExactProducerFieldCoverageAndMappingAreIncomplete"),
        new(14, "AcceptanceMaterialization", "DeniedBecauseNoExactSpecV2Exists"),
        new(15, "ProductionComposition", "NotAuthorized"),
        new(16, "Disposition", "BlockedE0WithoutVmReadSpecificSurrogateAuthority"),
    ];

    internal static bool CanonicalCommitPointProven => true;
    internal static bool NeutralObservationOwnerProven => true;
    internal static bool ExplicitPresenceAndSemanticContractProven => true;
    internal static bool ExactProductionProducerRegistered => true;
    internal static bool ExactFourFieldCoverageProven => false;
    internal static bool OwnerApprovedProjectionMappingProven => false;
    internal static bool SpecV2Materialized => false;
    internal static bool AcceptanceRecordV2Materialized => false;
    internal static bool RuntimeAuthorityGranted => false;
    internal static bool ProductionCompositionAuthorized => false;
}
