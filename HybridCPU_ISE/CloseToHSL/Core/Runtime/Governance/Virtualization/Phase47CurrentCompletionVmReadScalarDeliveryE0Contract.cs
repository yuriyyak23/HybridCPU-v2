using System.Collections.Immutable;

namespace YAKSys_Hybrid_CPU.Core;

internal enum CurrentCompletionProducerE0Disposition : byte
{
    DeniedNoArchitecturalCommitEvidence = 0,
    DeniedVmCallSpecificAuthority = 1,
    DeniedCompatibilityFactoryIsNotAuthority = 2,
    DeniedNoFieldValidityContract = 3,
}

internal readonly record struct CurrentCompletionProducerE0Entry(
    CompletionRecordClass RecordClass,
    CurrentCompletionProducerE0Disposition Disposition,
    string Reason)
{
    internal bool IsEligible => false;
}

internal readonly record struct CurrentCompletionFieldValidityE0Entry(
    VmcsField Field,
    bool IsValidForAnyProducer,
    string DenialReason);

internal static class Phase47CurrentCompletionVmReadScalarDeliveryE0Contract
{
    internal const string ProposedDecisionId =
        "D2-HV-VMREAD-SCALAR-DELIVERY-V1-CURRENT-COMPLETION-0004";
    internal const string OperationNamespace = "HybridCPU.VMREAD.ScalarDelivery.v1";
    internal const string OperationId = "DELIVER_CURRENT_COMPLETION_FIELDS_SCALAR_V1";
    internal const string BlockerCode =
        "MissingNonforgeableArchitecturallyVisibleCompletionCommitPointAndFieldValidity";

    internal static ImmutableArray<ushort> ExactFieldIds { get; } =
    [
        (ushort)VmcsField.ExitReason,
        (ushort)VmcsField.ExitQualification,
        (ushort)VmcsField.GuestPhysicalAddress,
        (ushort)VmcsField.EptViolationQualification,
    ];

    internal static ImmutableArray<CurrentCompletionProducerE0Entry> ProducerMatrix { get; } =
    [
        new(CompletionRecordClass.None,
            CurrentCompletionProducerE0Disposition.DeniedNoArchitecturalCommitEvidence,
            "Empty records are not an architecturally-visible completion."),
        new(CompletionRecordClass.Trap,
            CurrentCompletionProducerE0Disposition.DeniedNoArchitecturalCommitEvidence,
            "TrapCompletionPublicationFenceResult is a forgeable value result and no canonical retire commit issues nonforgeable current-completion visibility evidence."),
        new(CompletionRecordClass.Event,
            CurrentCompletionProducerE0Disposition.DeniedVmCallSpecificAuthority,
            "DomainHypercallCompletionOwner Event/E5/E6 authority is operation-specific and forbidden as this VMREAD source."),
        new(CompletionRecordClass.MemoryTranslationFault,
            CurrentCompletionProducerE0Disposition.DeniedNoArchitecturalCommitEvidence,
            "No canonical architecturally-visible completion commit binds this class to a domain/context current snapshot."),
        new(CompletionRecordClass.DmaFault,
            CurrentCompletionProducerE0Disposition.DeniedNoArchitecturalCommitEvidence,
            "No canonical architecturally-visible completion commit binds this class to a domain/context current snapshot."),
        new(CompletionRecordClass.VectorStreamFault,
            CurrentCompletionProducerE0Disposition.DeniedNoArchitecturalCommitEvidence,
            "No canonical architecturally-visible completion commit binds this class to a domain/context current snapshot."),
        new(CompletionRecordClass.LaneFault,
            CurrentCompletionProducerE0Disposition.DeniedNoArchitecturalCommitEvidence,
            "No canonical architecturally-visible completion commit binds this class to a domain/context current snapshot."),
        new(CompletionRecordClass.SecurityViolation,
            CurrentCompletionProducerE0Disposition.DeniedNoArchitecturalCommitEvidence,
            "No canonical architecturally-visible completion commit binds this class to a domain/context current snapshot."),
        new(CompletionRecordClass.CompatibilityExit,
            CurrentCompletionProducerE0Disposition.DeniedCompatibilityFactoryIsNotAuthority,
            "CompatibilityExit is publicly constructible through a forgeable fence result and has no production commit caller or owner-issued visibility evidence."),
    ];

    internal static ImmutableArray<CurrentCompletionFieldValidityE0Entry> FieldValidityMatrix { get; } =
    [
        DeniedField(VmcsField.ExitReason),
        DeniedField(VmcsField.ExitQualification),
        DeniedField(VmcsField.GuestPhysicalAddress),
        DeniedField(VmcsField.EptViolationQualification),
    ];

    internal static ImmutableArray<VmReadScalarDeliveryE0FindingV2> Findings { get; } =
    [
        new(1, "CanonicalIngress", "VMREAD->VmxCompatibilityAdmissionService.AdmitVmReadProjection->ReadCompatibilityProjection"),
        new(2, "CallerAuthorityGap", "VmxCompatibilityVmReadAdmissionRequest.CompletionIsCallerProvided"),
        new(3, "ConstructibilityGap", "CompletionRecordAndCompatibilityExitFactoryDoNotProveOwnerIssuedCurrentVisibility"),
        new(4, "CommitPointGap", "NoProductionArchitecturallyVisibleNeutralCompletionCommitPointIssuesNonforgeableVisibilityEvidence"),
        new(5, "RegistryGap", "NoDomainContextScopedLiveCurrentCompletionRegistryExists"),
        new(6, "GenerationGap", "NoRuntimeOwnedNonZeroCompletionGenerationExists"),
        new(7, "AtomicCaptureGap", "NoAtomicRecordValueOwnerDomainContextGenerationFieldCaptureExists"),
        new(8, "FieldValidityGap", "CompletionRecordHasScalarZerosButNoPerFieldPresenceOrValidityContract"),
        new(9, "ProducerEligibility", "NoExistingCompletionRecordClassIsEligibleForCurrentCompletionVMREADSource"),
        new(10, "VmCallIsolation", "DomainHypercallCompletionOwnerEventE5E6AreForbiddenAndNotReusable"),
        new(11, "TrapIsolation", "TrapRouteFenceBooleansAndTrapCompletionPublicationAreNotVMREADPermission"),
        new(12, "Migration", "SourceCandidateRecomputedCompletion+RestoreClearsOldSnapshot;ReceiptCandidateDrainOnly"),
        new(13, "ResultEffectCandidate", "ScalarU64ToDestinationRegister+ArchitecturalRegisterResultOnly"),
        new(14, "CapabilityCandidate", "NoneOnlyAfterCanonicalReadOnlyAdmissionAndSourceAuthorityExist"),
        new(15, "OwnerCreation", "DeniedUntilCanonicalCommitEvidenceAndFieldValidityPrerequisitesExist"),
        new(16, "SpecMaterialization", "DeniedBecauseE0OwnerFreshnessAndFieldValidityAreUnresolved"),
        new(17, "AcceptanceMaterialization", "DeniedBecauseNoExactSpecV2CanBeAccepted"),
        new(18, "ProductionComposition", "NotAuthorized"),
        new(19, "NoFallback", "NoCallerCompletionCompatibilityFactoryVmcsBackingStoreVmxRetireEffectBackendCompletionOrDirectArchitecturalWrite"),
        new(20, "Disposition", "BlockedE0WithoutDomainCurrentCompletionOwnerOrVmReadSpecificSurrogateAuthority"),
    ];

    internal static bool CanonicalCommitPointProven => false;
    internal static bool AnyProducerEligible => false;
    internal static bool FieldValidityProven => false;
    internal static bool DomainCurrentCompletionOwnerCreated => false;
    internal static bool SpecV2Materialized => false;
    internal static bool AcceptanceRecordV2Materialized => false;
    internal static bool RuntimeAuthorityGranted => false;
    internal static bool SourceValueAvailable => false;
    internal static bool ResultReceiptIssued => false;
    internal static bool RegisterWritebackAuthorized => false;
    internal static bool RetireCommitAuthorized => false;
    internal static bool BackendExecutionAuthorized => false;
    internal static bool CompletionPublicationAuthorized => false;
    internal static bool ProductionCompositionAuthorized => false;

    private static CurrentCompletionFieldValidityE0Entry DeniedField(VmcsField field) =>
        new(
            field,
            IsValidForAnyProducer: false,
            "No eligible committed producer and CompletionRecord carries no explicit per-field presence bit; zero fallback is forbidden.");
}
