using System.Collections.Immutable;

namespace YAKSys_Hybrid_CPU.Core;

/// <summary>
/// Byte-exact governance specification for the GuestCr0/GuestCr4 read-only
/// semantic group. It does not register, authorize or execute a VMREAD.
/// </summary>
internal static class Phase40VmReadProjectionDecisionSpecV2
{
    internal static VirtualizationDecisionSpecV2 Instance { get; } = Create();

    internal static ImmutableArray<byte> CanonicalBytes { get; } =
        VirtualizationDecisionCanonicalEncoderV2.EncodeSpec(Instance);

    internal static bool RuntimeAuthorityGranted => false;
    internal static bool ProjectionValueAvailable => false;
    internal static bool CapabilityGranted => false;
    internal static bool BackendExecutionAuthorized => false;
    internal static bool MutationAuthorized => false;
    internal static bool CompletionPublicationAuthorized => false;
    internal static bool RetirePublicationAuthorized => false;

    private static VirtualizationDecisionSpecV2 Create()
    {
        VirtualizationDecisionSpecV2 spec = new(
            VirtualizationDecisionValidatorV2.CurrentSchemaVersion,
            VmReadProjectionDecisionValidatorV2.ExpectedDecisionId,
            VmReadProjectionDecisionValidatorV2.ExpectedOperationNamespace,
            LeafWidth: 0,
            InvalidLeaf: 0,
            NumericLeaf: 0,
            VmReadProjectionDecisionValidatorV2.ExpectedOperationId,
            VirtualizationDecisionOwnerClassV2.NeutralRuntimeOwner,
            VmReadProjectionDecisionValidatorV2.ExpectedOwnerId,
            OwnerPolicyVersion: 1,
            OwnerEpoch: 1,
            OperandAbiVersion: 1,
            Rs1Contract: "VmcsFieldSelectorExactFrozenId",
            Rs2Contract: "X0ReservedNoAuthority",
            RdContract: "ArchitecturalDestinationRegisterScalar64",
            VirtualizationDecisionResultAbiV2.ArchitecturalScalar64,
            VirtualizationDecisionEffectClassV2.ReadOnlyProjectionNoStateMutation,
            VirtualizationCapabilityRequirementV2.None,
            CapabilityMask: 0,
            RequiresTypedGrant: false,
            VirtualizationDelegationPolicyV2.NonDelegable,
            VirtualizationRevocationPolicyV2.GovernanceRevocable,
            VirtualizationCapabilityMigrationClassV2.None,
            VirtualizationEvidenceVisibilityV2.GuestVisibleReadOnly,
            VirtualizationProjectionPolicyV2.ExactReadOnlyFieldSet,
            VirtualizationExecutionEvidenceRequirementV2.FieldConformanceProof,
            VirtualizationDomainRequirementV2.ExecutionDomainAndAddressSpaceBound,
            RequireNonZeroDomainTag: true,
            RequiresMemoryDomain: false,
            RequiresIoDomain: false,
            VirtualizationAddressSpaceRequirementV2.ExactNonZeroAddressSpaceTag,
            VirtualizationSecureDomainPolicyV2.Deny,
            VirtualizationCancellationPolicyV2.NotApplicableReadOnlyProjection,
            VirtualizationReplayPolicyV2.NotApplicableReadOnlyProjection,
            VirtualizationOperationMigrationPolicyV2.RevalidatedAfterRestore,
            VirtualizationCompletionEvidenceClassV2.None,
            VirtualizationCompletionMigrationClassV2.None,
            VirtualizationProjectionPolicyV2.NeverProject,
            VirtualizationCompletionPolicyV2.None,
            VirtualizationRetirePolicyV2.None,
            VirtualizationAdjacentLeafPolicyV2.DenyAllExceptExactFieldSet,
            VirtualizationCrossNamespacePolicyV2.DenyCrossNamespaceReuse,
            CreateOwnerMap(),
            SpecDigest: new string('0', 64),
            VirtualizationDecisionOperationClassV2.ReadOnlyArchitecturalVmReadCompatibilityProjection,
            VirtualizationDecisionAuthorityPlaneV2.PrivilegedExecutionStateReadProjection,
            Phase40VmReadProjectionE0Contract.ExactFieldIds,
            VirtualizationDecisionMutationClassV2.ReadOnly,
            DependencyContract: "JointDescriptorLegalityGuestCr0AndGuestCr4",
            VmcsMetadataOnly: true,
            RequiresConformanceProof: true);

        return spec with
        {
            SpecDigest = VirtualizationDecisionCanonicalEncoderV2.ComputeSpecDigest(spec),
        };
    }

    private static ImmutableArray<VirtualizationDecisionOwnerMapEntryV2> CreateOwnerMap() =>
    [
        Entry("VmcsField.GuestCr0", "PrivilegedExecutionStateDescriptor.GuestCr0"),
        Entry("VmcsField.GuestCr4", "PrivilegedExecutionStateDescriptor.GuestCr4"),
    ];

    private static VirtualizationDecisionOwnerMapEntryV2 Entry(string field, string valueSource) =>
        new(
            field,
            "PrivilegedExecutionStateOwnerPolicy",
            valueSource,
            "None",
            "GuestVisibleReadOnlyProjection+FieldConformanceProof",
            "RevalidatedAfterRestore",
            "DenyOnAnyMissingOwnerDomainAddressSpaceEpochBitsVisibilityMigrationConformanceGate");
}
