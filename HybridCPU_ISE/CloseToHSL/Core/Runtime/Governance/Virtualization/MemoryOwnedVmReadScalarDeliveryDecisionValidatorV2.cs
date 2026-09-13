using System.Collections.Immutable;
using System.Security.Cryptography;
using System.Text;

namespace YAKSys_Hybrid_CPU.Core;

internal static class MemoryOwnedVmReadScalarDeliveryDecisionValidatorV2
{
    internal const uint SchemaVersion = 2;
    internal const string ExpectedDecisionId =
        "D2-HV-VMREAD-SCALAR-DELIVERY-V1-GUEST-CR3-EPTP-VPID-CR3TC-0003";
    internal const string ExpectedOperationNamespace = "HybridCPU.VMREAD.ScalarDelivery.v1";
    internal const string ExpectedOperationId = "DELIVER_GUEST_CR3_EPTP_VPID_CR3TC_SCALAR_V1";
    internal const ulong ExpectedSourceOwnerId = 0x004D_454D_4144_4452UL;
    internal const string ExpectedDependencyContract =
        "CanonicalIngress=VmxCompatibilityAdmissionService.AdmitVmReadProjection;RuntimeBoundary=ReadCompatibilityProjection+FullDomainRuntimeUnchanged;AuthorityPlane=MemoryAddressSpaceReadProjection;Source=MemoryDomainDescriptor.MaterializedMemoryDomainReadOnlyTranslationView;Freshness=CanonicalMemoryDomainRuntimeNonZeroCurrentAddressSpaceGenerationAtomicOwnerValueDomainAddressSpaceGenerationFieldCapture;Fields=GuestCr3AddressSpaceRoot+EptPointerOwnedValidSecondStageRoot+VpidEnabledNonZeroTag+Cr3TargetCountCanonicalBound;Capability=None;Evidence=GuestArchitecturalStateOrCompatibilityAlias+MaterializedFieldSourceGenerationProof;Result=ScalarU64ToDestinationRegister;Effect=ArchitecturalRegisterResultOnly;Transport=ExistingScalarReceiptCanonicalPRFRenameWriteback;Commit=RetireRecord.RegisterWrite+RetireCoordinator;Migration=DrainOnlyNoReceiptSealPhysicalDestinationOrOutputSerialization;Activation=DefaultDisabled;NoProductionCompositionNoVMCSFallbackNoVMCALLReuseNoVmxRetireEffectNoBackendCompletionNoDirectArchitecturalWrite";

    private static readonly ImmutableArray<string> RequiredCodeOwnersScopes =
    [
        "/HybridCPU_ISE/CloseToHSL/Core/Runtime/Governance/Virtualization/",
        "/HybridCPU_ISE/CloseToHSL/Core/Runtime/Domains/Descriptors/MemoryDomain/",
        "/HybridCPU_ISE/CloseToHSL/Core/Runtime/Domains/Admission/Memory/",
        "/HybridCPU_ISE/CloseToHSL/Core/Runtime/Memory/Translation/",
        "/HybridCPU_ISE/CloseToHSL/Core/Virtualization/Compatibility/Frontend/Projection/VmcsRead/",
        "/HybridCPU_ISE/CloseToHSL/Core/Pipeline/Retire/",
        "/HybridCPU_ISE/docs/ref2/VirtualizationActivationPlan/",
    ];

    internal static VmReadScalarDeliveryDecisionValidationResultV2 ValidateSpecShape(
        VirtualizationDecisionSpecV2? spec)
    {
        if (spec is null) return Deny(VmReadScalarDeliveryDecisionValidationDecisionV2.DeniedMissingSpec, "Memory-owned SpecV2 is missing.");
        if (spec.SchemaVersion != SchemaVersion || spec.DecisionId != ExpectedDecisionId ||
            spec.OperationNamespace != ExpectedOperationNamespace || spec.OperationId != ExpectedOperationId)
            return Deny(VmReadScalarDeliveryDecisionValidationDecisionV2.DeniedIdentity, "Decision identity mismatched.");
        if (!ValidateExactProfile(spec))
            return Deny(VmReadScalarDeliveryDecisionValidationDecisionV2.DeniedProfile, "Memory-owned scalar profile mismatched.");
        if (!ValidateOwnerMap(spec.OwnerMap))
            return Deny(VmReadScalarDeliveryDecisionValidationDecisionV2.DeniedOwnerMap, "Memory owner/value map mismatched.");
        if (!VirtualizationDecisionCanonicalEncoderV2.IsCanonicalSha256(spec.SpecDigest) ||
            spec.SpecDigest != VirtualizationDecisionCanonicalEncoderV2.ComputeSpecDigest(spec))
            return Deny(VmReadScalarDeliveryDecisionValidationDecisionV2.DeniedCanonicalDigest, "Spec digest is not canonical.");
        return new(VmReadScalarDeliveryDecisionValidationDecisionV2.ExactPolicyShape,
            "Exact memory-owned policy shape is canonical; acceptance and runtime authority remain absent.");
    }

    internal static VmReadScalarDeliveryDecisionValidationResultV2 Validate(
        VirtualizationDecisionSpecV2? spec,
        VirtualizationDecisionAcceptanceRecordV2? acceptance,
        VirtualizationDecisionValidationEvidenceV2? evidence)
    {
        if (spec is null || acceptance is null || evidence is null || evidence.CodeOwners is null)
            return Deny(VmReadScalarDeliveryDecisionValidationDecisionV2.DeniedMissingArtifact, "Spec, later acceptance and evidence are required.");
        VmReadScalarDeliveryDecisionValidationResultV2 shape = ValidateSpecShape(spec);
        if (!shape.IsExactPolicyShape) return shape;
        if (acceptance.SpecCommitSha != evidence.ResolvedSpecCommitSha ||
            acceptance.SpecCommitSha == evidence.AcceptanceContainingCommitSha ||
            !VirtualizationDecisionCanonicalEncoderV2.IsCanonicalCommitSha(acceptance.SpecCommitSha) ||
            !VirtualizationDecisionCanonicalEncoderV2.IsCanonicalCommitSha(evidence.AcceptanceContainingCommitSha))
            return Deny(VmReadScalarDeliveryDecisionValidationDecisionV2.DeniedProvenance, "Acceptance must bind an earlier immutable spec.");
        if (acceptance.SchemaVersion != SchemaVersion || acceptance.DecisionId != ExpectedDecisionId ||
            !FixedEquals(acceptance.SpecDigest, spec.SpecDigest) ||
            !FixedEquals(acceptance.AcceptanceDigest, VirtualizationDecisionCanonicalEncoderV2.ComputeAcceptanceDigest(acceptance)) ||
            !FixedEquals(evidence.SpecCanonicalBytes, VirtualizationDecisionCanonicalEncoderV2.EncodeSpec(spec)) ||
            !FixedEquals(evidence.SpecBytesAtCommit, VirtualizationDecisionCanonicalEncoderV2.EncodeSpec(spec)) ||
            !FixedEquals(evidence.AcceptanceCanonicalBytes, VirtualizationDecisionCanonicalEncoderV2.EncodeAcceptance(acceptance)))
            return Deny(VmReadScalarDeliveryDecisionValidationDecisionV2.DeniedAcceptance, "Acceptance bytes or digests mismatched.");
        if (acceptance.AcceptanceState != VirtualizationDecisionAcceptanceStateV2.Accepted ||
            acceptance.AcceptancePolicyVersion != 1 || acceptance.AcceptedBy != "@yaksysdev")
            return Deny(VmReadScalarDeliveryDecisionValidationDecisionV2.DeniedAcceptance, "Acceptance principal or state mismatched.");
        if (!Review(acceptance.OwnerReviewEvidence, VirtualizationDecisionReviewRoleV2.OwnerReviewRole,
                VirtualizationDecisionReviewAuthorityPlaneV2.NeutralRuntimeOwner, spec, acceptance) ||
            !Review(acceptance.ArchitectureReviewEvidence, VirtualizationDecisionReviewRoleV2.ArchitectureReviewRole,
                VirtualizationDecisionReviewAuthorityPlaneV2.Architecture, spec, acceptance))
            return Deny(VmReadScalarDeliveryDecisionValidationDecisionV2.DeniedReview, "Required reviews mismatched.");
        if (!evidence.CodeOwners.FilePresent || evidence.CodeOwners.BlobSha != acceptance.CodeOwnersBlobSha ||
            !RequiredCodeOwnersScopes.All(scope => evidence.CodeOwners.Rules.Any(rule =>
                rule.Scope == scope && rule.Principal == acceptance.AcceptedBy)))
            return Deny(VmReadScalarDeliveryDecisionValidationDecisionV2.DeniedCodeOwners, "Required CODEOWNERS coverage is missing.");
        if (!evidence.Revocations.IsDefaultOrEmpty || !evidence.Supersessions.IsDefaultOrEmpty ||
            acceptance.SupersedesDecisionId is not null || acceptance.SupersedesAcceptanceDigest is not null)
            return Deny(VmReadScalarDeliveryDecisionValidationDecisionV2.DeniedLineage, "Earlier VMREAD decisions must remain unchanged.");
        return new(VmReadScalarDeliveryDecisionValidationDecisionV2.AcceptedPolicyObject,
            "Exact memory-owned D2 is governance policy only; production composition remains unauthorized.",
            new AcceptedVmReadScalarDeliveryDecisionV2(
                spec.DecisionId, spec.SpecDigest, acceptance.AcceptanceDigest, acceptance.SpecCommitSha,
                spec.OperationNamespace, spec.ExactFieldIds!.Value, spec.OwnerId, spec.OwnerPolicyVersion,
                spec.OwnerEpoch, spec.ResultAbi, spec.EffectClass, spec.OperationMigrationPolicy, spec.RetirePolicy));
    }

    private static bool ValidateExactProfile(VirtualizationDecisionSpecV2 spec) =>
        spec.LeafWidth == 0 && spec.InvalidLeaf == 0 && spec.NumericLeaf == 0 &&
        spec.OwnerClass == VirtualizationDecisionOwnerClassV2.NeutralRuntimeOwner &&
        spec.OwnerId == ExpectedSourceOwnerId && spec.OwnerPolicyVersion == 1 && spec.OwnerEpoch == 1 &&
        spec.OperandAbiVersion == 1 &&
        spec.Rs1Contract == "VmcsFieldSelectorExactFrozenIdFromCanonicalSourceRegister" &&
        spec.Rs2Contract == "X0ReservedNoAuthority" &&
        spec.RdContract == "ArchitecturalDestinationRegisterX1ToX31CanonicalRenameIdentity" &&
        spec.ResultAbi == VirtualizationDecisionResultAbiV2.ScalarU64ToDestinationRegister &&
        spec.EffectClass == VirtualizationDecisionEffectClassV2.ArchitecturalRegisterResultOnly &&
        spec.CapabilityRequirement == VirtualizationCapabilityRequirementV2.None && spec.CapabilityMask == 0 && !spec.RequiresTypedGrant &&
        spec.DelegationPolicy == VirtualizationDelegationPolicyV2.NonDelegable &&
        spec.RevocationPolicy == VirtualizationRevocationPolicyV2.GovernanceRevocable &&
        spec.CapabilityMigrationClass == VirtualizationCapabilityMigrationClassV2.None &&
        spec.EvidenceVisibility == VirtualizationEvidenceVisibilityV2.GuestVisibleReadOnly &&
        spec.FrontendProjectionPolicy == VirtualizationProjectionPolicyV2.ExactReadOnlyFieldSet &&
        spec.ExecutionEvidenceRequirement == VirtualizationExecutionEvidenceRequirementV2.FieldConformanceProof &&
        spec.DomainRequirement == VirtualizationDomainRequirementV2.ExecutionDomainAndAddressSpaceBound &&
        spec.RequireNonZeroDomainTag && spec.RequiresMemoryDomain && spec.RequiresIoDomain &&
        spec.AddressSpaceRequirement == VirtualizationAddressSpaceRequirementV2.ExactNonZeroAddressSpaceTag &&
        spec.SecureDomainPolicy == VirtualizationSecureDomainPolicyV2.Deny &&
        spec.CancellationPolicy == VirtualizationCancellationPolicyV2.SquashBeforeRetireZeroArchitecturalEffect &&
        spec.ReplayPolicy == VirtualizationReplayPolicyV2.AttemptBoundReceiptNoReplayReuse &&
        spec.OperationMigrationPolicy == VirtualizationOperationMigrationPolicyV2.DrainOnly &&
        spec.CompletionEvidenceClass == VirtualizationCompletionEvidenceClassV2.None &&
        spec.CompletionMigrationClass == VirtualizationCompletionMigrationClassV2.None &&
        spec.CompletionProjectionPolicy == VirtualizationProjectionPolicyV2.NeverProject &&
        spec.CompletionPolicy == VirtualizationCompletionPolicyV2.None &&
        spec.RetirePolicy == VirtualizationRetirePolicyV2.CanonicalRetireCoordinatorArchitecturalRegisterCommit &&
        spec.AdjacentLeafPolicy == VirtualizationAdjacentLeafPolicyV2.DenyAllExceptExactFieldSet &&
        spec.CrossNamespacePolicy == VirtualizationCrossNamespacePolicyV2.DenyCrossNamespaceReuse &&
        spec.OperationClass == VirtualizationDecisionOperationClassV2.ReadOnlyArchitecturalVmReadScalarDelivery &&
        spec.AuthorityPlane == VirtualizationDecisionAuthorityPlaneV2.MemoryAddressSpaceReadProjection &&
        spec.MutationClass == VirtualizationDecisionMutationClassV2.UnderlyingVirtualizationStateReadOnly &&
        spec.DependencyContract == ExpectedDependencyContract && spec.VmcsMetadataOnly && spec.RequiresConformanceProof &&
        spec.ExactFieldIds is { } fields && fields.SequenceEqual(Phase45MemoryOwnedVmReadScalarDeliveryE0Contract.ExactFieldIds);

    private static bool ValidateOwnerMap(ImmutableArray<VirtualizationDecisionOwnerMapEntryV2> map)
    {
        if (map.IsDefaultOrEmpty || map.Length != 4) return false;
        string[] fields = ["GuestCr3", "EptPointer", "Vpid", "Cr3TargetCount"];
        return fields.All(name => map.Any(entry => entry.FieldOrOperation == $"VmcsField.{name}" &&
            entry.Owner == "MemoryDomainDescriptor" &&
            entry.ValueSource.StartsWith("MemoryDomainReadOnlyTranslationView.", StringComparison.Ordinal) &&
            entry.CapabilityPolicy == "None" && entry.MigrationClass == "DrainOnly" &&
            entry.EvidenceClass.Contains("RuntimeOwnedAddressSpaceGenerationProof", StringComparison.Ordinal)));
    }

    private static bool Review(VirtualizationDecisionReviewEvidenceV2 review,
        VirtualizationDecisionReviewRoleV2 role, VirtualizationDecisionReviewAuthorityPlaneV2 plane,
        VirtualizationDecisionSpecV2 spec, VirtualizationDecisionAcceptanceRecordV2 acceptance) =>
        review is not null && review.Role == role && review.AuthorityPlane == plane &&
        review.State == VirtualizationDecisionReviewStateV2.Completed && review.Principal == acceptance.AcceptedBy &&
        review.ReviewedDecisionId == spec.DecisionId && FixedEquals(review.ReviewedSpecDigest, spec.SpecDigest) &&
        review.ReviewedSpecCommitSha == acceptance.SpecCommitSha && !string.IsNullOrWhiteSpace(review.EvidenceId);
    private static bool FixedEquals(string? left, string? right) => left is not null && right is not null &&
        CryptographicOperations.FixedTimeEquals(Encoding.ASCII.GetBytes(left), Encoding.ASCII.GetBytes(right));
    private static bool FixedEquals(ImmutableArray<byte> left, ImmutableArray<byte> right) =>
        !left.IsDefault && !right.IsDefault && CryptographicOperations.FixedTimeEquals(left.AsSpan(), right.AsSpan());
    private static VmReadScalarDeliveryDecisionValidationResultV2 Deny(
        VmReadScalarDeliveryDecisionValidationDecisionV2 decision, string reason) => new(decision, reason);
}
