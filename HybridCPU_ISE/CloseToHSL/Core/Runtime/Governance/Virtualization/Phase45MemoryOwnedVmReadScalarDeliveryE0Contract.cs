using System.Collections.Immutable;

namespace YAKSys_Hybrid_CPU.Core;

internal static class Phase45MemoryOwnedVmReadScalarDeliveryE0Contract
{
    internal static ImmutableArray<ushort> ExactFieldIds { get; } =
    [
        (ushort)VmcsField.GuestCr3,
        (ushort)VmcsField.EptPointer,
        (ushort)VmcsField.Vpid,
        (ushort)VmcsField.Cr3TargetCount,
    ];

    internal static ImmutableArray<VmReadScalarDeliveryE0FindingV2> Findings { get; } =
    [
        new(1, "CanonicalIngress", "VMREAD->VmxCompatibilityAdmissionService.AdmitVmReadProjection->ReadCompatibilityProjection"),
        new(2, "AuthorityPlane", "MemoryAddressSpaceReadProjection"),
        new(3, "SourceOwner", "MemoryDomainDescriptor+CanonicalMemoryDomainRuntimeOnly"),
        new(4, "ValueSource", "MaterializedMemoryDomainReadOnlyTranslationViewOnly"),
        new(5, "RuntimeBoundary", "FullDomainRuntime+CapabilityNone+FieldSpecificGuestVisibleEvidence"),
        new(6, "GenerationGap", "LegacyAddressSpaceGenerationWasCallerProvidedAndNotCurrentAcrossOwnershipTagTargetReplacementOrRestore"),
        new(7, "GenerationClosure", "MemoryDomainRuntimeNowIssuesNonZeroCurrentGenerationAndAtomicallyCapturesOwnerValueDomainAddressSpaceGenerationField"),
        new(8, "GuestCr3", "AddressSpaceRootOnly"),
        new(9, "EptPointer", "SecondStageRootOnlyWhenTranslationEnabledRootNonZeroAndMemoryDomainOwnsSecondStage"),
        new(10, "Vpid", "AddressSpaceTagOnlyWhenTaggingEnabledAndTagNonZero"),
        new(11, "Cr3TargetCount", "CanonicalAddressSpaceTargetCountWithinExactBound"),
        new(12, "ResultEffect", "ScalarU64ToDestinationRegister+ArchitecturalRegisterResultOnlyCandidate"),
        new(13, "MigrationRollback", "DrainOnly+NoReceiptSealPhysicalDestinationOrOutputSerialization+DefaultDisabled"),
        new(14, "Invalidation", "ReplaySquashRestoreDisableRevocationInvalidateOutstandingReceipt+NoRetireSourceReread"),
        new(15, "NoFallback", "NoVMCSBackingStoreDirectArchitecturalWriteVmxRetireEffectBackendCompletionOrVMCALLReuse"),
        new(16, "AdjacentDenial", "DenyGuestCr0GuestCr4GuestPcGuestSpGuestFlagsCompletionHostCr3ControlsVMWRITENestedSecureComputeTransactionsIommuIoDeviceLaneStreamCompiler"),
    ];

    internal const string GenerationPrerequisiteCommit =
        "3cb896e37fc7b5775099bf34ca9082e488a73dd3";
    internal const string GenerationPrerequisiteTree =
        "840d48603162146de36e51d65bca7d6ebe151d8c";
    internal static bool RuntimeAuthorityGranted => false;
    internal static bool SourceValueAvailable => false;
    internal static bool ResultReceiptIssued => false;
    internal static bool RegisterWritebackAuthorized => false;
    internal static bool RetireCommitAuthorized => false;
    internal static bool BackendExecutionAuthorized => false;
    internal static bool CompletionPublicationAuthorized => false;
    internal static bool ProductionCompositionAuthorized => false;
}
