using System.Collections.Immutable;

namespace YAKSys_Hybrid_CPU.Core;

internal static class Phase43GuestPcSpFlagsVmReadScalarDeliveryE0Contract
{
    internal static ImmutableArray<ushort> ExactFieldIds { get; } =
    [
        (ushort)VmcsField.GuestPc,
        (ushort)VmcsField.GuestSp,
        (ushort)VmcsField.GuestFlags,
    ];

    internal static ImmutableArray<VmReadScalarDeliveryE0FindingV2> Findings { get; } =
    [
        new(1, "CanonicalIngress", "VMREAD->VmxCompatibilityAdmissionService.AdmitVmReadProjection->ReadCompatibilityProjection"),
        new(2, "SourceOwner", "ExecutionDomainDescriptorOnly"),
        new(3, "ValueSource", "MaterializedExecutionDomainReadOnlyStateView.GuestPc|GuestSp|GuestFlagsOnly"),
        new(4, "RuntimeBoundary", "PreserveFullDomainRuntimeReadCompatibilityProjection+CapabilityNone+GuestVisibleCompatibilityAliasEvidence"),
        new(5, "Materialization", "ExactFieldMaterialized+GuestArchitecturalStateEvidence+ImmutableDescriptorViewSnapshotRequired"),
        new(6, "EpochGap", "CurrentProjectionCarriesStateEpochButDoesNotValidateCurrentEpoch;FutureProductionMustAtomicallyBindAndRevalidateSourceEpoch"),
        new(7, "ResultAbi", "ScalarU64ToDestinationRegister+ArchitecturalRegisterResultOnly"),
        new(8, "Receipt", "OpaqueAttemptD2FieldSourceEpochValueDestinationReplayRestoreProfileGenerationBoundSingleUse"),
        new(9, "Transport", "CanonicalPRFRenameWritebackOnly+RetireRecord.RegisterWrite+RetireCoordinatorOnly"),
        new(10, "MigrationRollback", "DrainOnly+NoReceiptSerialization+DisableBindingAndInvalidateOutstandingReceipts"),
        new(11, "SquashReplayRestore", "ZeroArchitecturalEffectBeforeRetire+InvalidateOnReplaySquashRestoreOrProfileGenerationChange"),
        new(12, "NoFallback", "NoVMCSScalarOrBackingStore+NoDirectArchitecturalWrite+NoVmxRetireEffectAuthority"),
        new(13, "NoReuse", "NoVMCALLCapabilityD2O1E2ToE7E5E6+NoTrapOrBackendCompletion"),
        new(14, "AdjacentDenial", "DenyVMWRITEGuestCrFieldsMemoryOwnedFieldsHostAliasesHostCr3CompatibilityControlsAndAllOtherFields"),
        new(15, "Activation", "DefaultDisabled+GovernancePolicyIsNotCapabilityAdmissionSourceAuthorityReceiptOrRetireGrant"),
    ];

    internal static bool RuntimeAuthorityGranted => false;
    internal static bool SourceValueAvailable => false;
    internal static bool ResultReceiptIssued => false;
    internal static bool RegisterWritebackAuthorized => false;
    internal static bool RetireCommitAuthorized => false;
    internal static bool BackendExecutionAuthorized => false;
    internal static bool UnderlyingVirtualizationMutationAuthorized => false;
    internal static bool CompletionPublicationAuthorized => false;
    internal static bool ProductionCompositionAuthorized => false;
}
