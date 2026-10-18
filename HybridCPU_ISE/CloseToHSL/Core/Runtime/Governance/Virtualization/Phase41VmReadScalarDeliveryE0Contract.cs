using System.Collections.Immutable;

namespace YAKSys_Hybrid_CPU.Core;

internal sealed record VmReadScalarDeliveryE0FindingV2(
    byte Number,
    string Name,
    string ExactContract);

/// <summary>
/// Audited input for the separate GuestCr0/GuestCr4 scalar-delivery D2. This
/// artifact records constraints only; it issues no receipt or runtime authority.
/// </summary>
internal static class Phase41VmReadScalarDeliveryE0Contract
{
    internal static ImmutableArray<ushort> ExactFieldIds { get; } =
    [
        (ushort)VmcsField.GuestCr0,
        (ushort)VmcsField.GuestCr4,
    ];

    internal static ImmutableArray<VmReadScalarDeliveryE0FindingV2> Findings { get; } =
    [
        new(1, "SeparateDecision", "NeverModifyOrBroadenD2-HV-VMREAD-PROJECTION-V1-GUEST-CR0-CR4-0001"),
        new(2, "SourceOwner", "PrivilegedExecutionStateOwnerPolicy+PrivilegedExecutionStateDescriptor.GuestCr0|GuestCr4Only"),
        new(3, "SourcePrerequisite", "AcceptedExactPhase40ReadOnlyProjectionD2+FreshOwnerAdmission+FieldConformanceProof"),
        new(4, "ResultAbi", "ScalarU64ToDestinationRegister+ArchitecturalRegisterResultOnly"),
        new(5, "AttemptIdentity", "OpaqueVmReadScalarResultReceiptBoundToLiveAttemptReplayEpochBundleDomainAddressSpaceDescriptorEpochFieldAndRd"),
        new(6, "SpeculativeTransport", "CanonicalPublishedDestinationDependency+PRF+Rename+EXMEMWBScalarCarrierOnly"),
        new(7, "ArchitecturalCommit", "WBLocalRetireRecord.RegisterWrite+CanonicalRetireCoordinatorOnly"),
        new(8, "Squash", "AnySquashBeforePreciseRetireConsumesOrInvalidatesReceiptAndHasZeroArchitecturalEffect"),
        new(9, "Replay", "ReceiptIsSingleUseAndCannotCrossAttemptReplayEpochSquashRestoreOrIssuerGeneration"),
        new(10, "Migration", "DrainOnly+NoReceiptCheckpointOrRestore"),
        new(11, "NoSideAuthority", "NoDirectArchitecturalWrite+NoVMCSWriteback+NoUnderlyingVirtualizationMutation+NoTrapCompletion"),
        new(12, "NoReuse", "NoVMCALLE5E6+NoProbeD2O1E1ToE7+NoVmxRetireEffect.VmcsReadAuthority"),
        new(13, "AdjacentDenial", "DenyX0MissingDestinationVMWRITEGuestCr3HostAliasesCompatibilityControlsAndAllOtherFields"),
        new(14, "ReceiptRole", "ReceiptAttestsOneScalarResultForOneCanonicalCarrierAndIsNotCapabilityAdmissionSourceOwnerOrRetireGrant"),
    ];

    internal static bool RuntimeAuthorityGranted => false;
    internal static bool SourceValueAvailable => false;
    internal static bool ResultReceiptIssued => false;
    internal static bool RegisterWritebackAuthorized => false;
    internal static bool RetireCommitAuthorized => false;
    internal static bool BackendExecutionAuthorized => false;
    internal static bool UnderlyingVirtualizationMutationAuthorized => false;
    internal static bool CompletionPublicationAuthorized => false;
}
