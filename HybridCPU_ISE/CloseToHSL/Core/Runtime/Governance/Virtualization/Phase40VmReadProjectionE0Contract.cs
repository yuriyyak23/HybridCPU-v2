using System.Collections.Immutable;

namespace YAKSys_Hybrid_CPU.Core;

internal sealed record VmReadProjectionE0FindingV2(
    byte Number,
    string Name,
    string ExactContract);

/// <summary>
/// Audited E0 input for the GuestCr0/GuestCr4 D2 decision. This is immutable
/// governance evidence only and has no projection, capability or admission API.
/// </summary>
internal static class Phase40VmReadProjectionE0Contract
{
    internal static ImmutableArray<ushort> ExactFieldIds { get; } =
    [
        (ushort)VmcsField.GuestCr0,
        (ushort)VmcsField.GuestCr4,
    ];

    internal static ImmutableArray<VmReadProjectionE0FindingV2> Findings { get; } =
    [
        new(1, "CanonicalIngress", "VMREAD->VmxCompatibilityAdmissionService.AdmitVmReadProjection->ReadCompatibilityProjection"),
        new(2, "OwnerAndValueSource", "PrivilegedExecutionStateOwnerPolicy;PrivilegedExecutionStateDescriptor.GuestCr0|GuestCr4"),
        new(3, "Bindings", "MaterializedNonZeroDomainTag+ExactAddressSpaceTag+CurrentPolicyEpoch"),
        new(4, "BitLegalityAndDependencies", "FieldSpecificAllowedAndRequiredMasks+JointGuestCr0GuestCr4DescriptorLegality"),
        new(5, "Capability", "None;NoTypedGrant;NoCapabilityMask"),
        new(6, "Evidence", "GuestVisibleReadOnlyProjection+CompatibilityProjectionEvidence+FieldConformanceProof"),
        new(7, "RestoreMigration", "RevalidatedAfterRestore;PreRestoreEpochNotReusable"),
        new(8, "ArchitecturalVisibility", "ExactSelectedFieldScalar64ReadOnlyProjection"),
        new(9, "GateDenials", "DenyOnAnyMissingOwnerSourceDomainAddressSpaceEpochKindBitsEvidenceVisibilityMigrationConformanceGate"),
        new(10, "NoScalarFallback", "NoVmcsScalarStore+NoBackingStore+NoCr3FlagsPagingOrCompatibilityControlInference"),
        new(11, "NoSideAuthority", "NoWrite+NoBackend+NoMutation+NoCompletion+NoRetire"),
        new(12, "AdjacentDenials", "DenyEveryVmReadFieldOutsideGuestCr0GuestCr4ForThisDecision+AlwaysDenyVmWrite"),
    ];

    internal static bool RuntimeAuthorityGranted => false;
    internal static bool ProjectionValueAvailable => false;
    internal static bool CapabilityGranted => false;
    internal static bool BackendExecutionAuthorized => false;
    internal static bool MutationAuthorized => false;
    internal static bool CompletionPublicationAuthorized => false;
    internal static bool RetirePublicationAuthorized => false;
}
