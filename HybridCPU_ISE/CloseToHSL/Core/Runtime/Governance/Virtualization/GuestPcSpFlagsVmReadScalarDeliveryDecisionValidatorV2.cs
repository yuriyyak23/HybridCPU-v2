using System.Collections.Immutable;
using System.Security.Cryptography;
using System.Text;

namespace YAKSys_Hybrid_CPU.Core;

internal static class GuestPcSpFlagsVmReadScalarDeliveryDecisionValidatorV2
{
    internal const uint SchemaVersion = 2;
    internal const string ExpectedDecisionId =
        "D2-HV-VMREAD-SCALAR-DELIVERY-V1-GUEST-PC-SP-FLAGS-0002";
    internal const string ExpectedOperationNamespace = "HybridCPU.VMREAD.ScalarDelivery.v1";
    internal const string ExpectedOperationId = "DELIVER_GUEST_PC_SP_FLAGS_SCALAR_V1";
    internal const ulong ExpectedSourceOwnerId = 0x0048_4350_4558_444FUL;
    internal const string ExpectedDependencyContract =
        "CanonicalIngress=VmxCompatibilityAdmissionService.AdmitVmReadProjection;RuntimeBoundary=ReadCompatibilityProjection+FullDomainRuntimeUnchanged;Source=ExecutionDomainDescriptor.MaterializedExecutionDomainReadOnlyStateView;Evidence=GuestVisibleCompatibilityAlias+GuestArchitecturalState+MaterializedFieldAndSourceStateProof;Receipt=SingleUseAttemptD2FieldSourceEpochValueDestinationReplayRestoreProfileGenerationBound;Activation=DefaultDisabled;Rollback=DisableBindingAndInvalidateOutstandingReceipts;Transport=CanonicalPRFRenameWriteback;Commit=RetireRecord.RegisterWrite+RetireCoordinator;Migration=DrainOnlyNoReceiptSerialization;NoVMCSFallbackNoVMCALLReuseNoVmxRetireEffectNoTrapBackendCompletionNoDirectArchitecturalWrite";

    private static readonly ImmutableArray<string> RequiredCodeOwnersScopes =
    [
        "/HybridCPU_ISE/CloseToHSL/Core/Runtime/Governance/Virtualization/",
        "/HybridCPU_ISE/CloseToHSL/Core/Runtime/Domains/Descriptors/ExecutionDomain/",
        "/HybridCPU_ISE/CloseToHSL/Core/Virtualization/Compatibility/Frontend/Projection/VmcsRead/",
        "/HybridCPU_ISE/CloseToHSL/Core/Pipeline/Retire/",
        "/HybridCPU_ISE/CloseToHSL/Core/Pipeline/ExecutionFlow/",
        "/HybridCPU_ISE/CloseToHSL/Core/Pipeline/MicroOps/Types/",
        "/HybridCPU_ISE/CloseToHSL/Core/Architecture/Registers/",
        "/HybridCPU_ISE/docs/ref2/VirtualizationActivationPlan/",
    ];

    internal static VmReadScalarDeliveryDecisionValidationResultV2 ValidateSpecShape(
        VirtualizationDecisionSpecV2? spec)
    {
        if (spec is null)
            return Deny(VmReadScalarDeliveryDecisionValidationDecisionV2.DeniedMissingSpec,
                "The GuestPc/GuestSp/GuestFlags scalar-delivery SpecV2 is missing.");
        if (spec.SchemaVersion != SchemaVersion || spec.DecisionId != ExpectedDecisionId ||
            spec.OperationNamespace != ExpectedOperationNamespace || spec.OperationId != ExpectedOperationId)
            return Deny(VmReadScalarDeliveryDecisionValidationDecisionV2.DeniedIdentity,
                "Decision, namespace, operation or schema identity mismatched.");
        if (!ValidateExactProfile(spec))
            return Deny(VmReadScalarDeliveryDecisionValidationDecisionV2.DeniedProfile,
                "The scalar-delivery policy differs from the exact GuestPc/GuestSp/GuestFlags profile.");
        if (!ValidateOwnerMap(spec.OwnerMap))
            return Deny(VmReadScalarDeliveryDecisionValidationDecisionV2.DeniedOwnerMap,
                "Every field must retain ExecutionDomainDescriptor and its materialized read-only view source.");
        if (!VirtualizationDecisionCanonicalEncoderV2.IsCanonicalSha256(spec.SpecDigest) ||
            spec.SpecDigest != VirtualizationDecisionCanonicalEncoderV2.ComputeSpecDigest(spec))
            return Deny(VmReadScalarDeliveryDecisionValidationDecisionV2.DeniedCanonicalDigest,
                "The scalar-delivery spec digest is not canonical.");
        return new(VmReadScalarDeliveryDecisionValidationDecisionV2.ExactPolicyShape,
            "Exact policy shape is canonical; acceptance and runtime authority remain absent.");
    }

    internal static VmReadScalarDeliveryDecisionValidationResultV2 Validate(
        VirtualizationDecisionSpecV2? spec,
        VirtualizationDecisionAcceptanceRecordV2? acceptance,
        VirtualizationDecisionValidationEvidenceV2? evidence)
    {
        if (spec is null || acceptance is null || evidence is null || evidence.CodeOwners is null)
            return Deny(VmReadScalarDeliveryDecisionValidationDecisionV2.DeniedMissingArtifact,
                "SpecV2, later AcceptanceRecordV2 and repository evidence are required.");
        VmReadScalarDeliveryDecisionValidationResultV2 shape = ValidateSpecShape(spec);
        if (!shape.IsExactPolicyShape) return shape;
        if (!VirtualizationDecisionCanonicalEncoderV2.IsCanonicalCommitSha(acceptance.SpecCommitSha) ||
            !VirtualizationDecisionCanonicalEncoderV2.IsCanonicalCommitSha(evidence.ResolvedSpecCommitSha) ||
            !VirtualizationDecisionCanonicalEncoderV2.IsCanonicalCommitSha(evidence.AcceptanceContainingCommitSha) ||
            acceptance.SpecCommitSha != evidence.ResolvedSpecCommitSha ||
            acceptance.SpecCommitSha == evidence.AcceptanceContainingCommitSha)
            return Deny(VmReadScalarDeliveryDecisionValidationDecisionV2.DeniedProvenance,
                "Acceptance must bind an exact earlier spec and cannot name its own containing commit.");
        try
        {
            if (acceptance.SchemaVersion != SchemaVersion || acceptance.DecisionId != ExpectedDecisionId ||
                !FixedEquals(acceptance.SpecDigest, spec.SpecDigest) ||
                !FixedEquals(acceptance.AcceptanceDigest, VirtualizationDecisionCanonicalEncoderV2.ComputeAcceptanceDigest(acceptance)) ||
                !FixedEquals(evidence.SpecCanonicalBytes, VirtualizationDecisionCanonicalEncoderV2.EncodeSpec(spec)) ||
                !FixedEquals(evidence.SpecBytesAtCommit, VirtualizationDecisionCanonicalEncoderV2.EncodeSpec(spec)) ||
                !FixedEquals(evidence.AcceptanceCanonicalBytes, VirtualizationDecisionCanonicalEncoderV2.EncodeAcceptance(acceptance)))
                return Deny(VmReadScalarDeliveryDecisionValidationDecisionV2.DeniedAcceptance,
                    "Acceptance bytes or digests do not bind the exact spec.");
        }
        catch (Exception exception) when (exception is FormatException or ArgumentException or OverflowException)
        {
            return Deny(VmReadScalarDeliveryDecisionValidationDecisionV2.DeniedAcceptance,
                "Acceptance canonical fields are malformed.");
        }
        if (acceptance.AcceptanceState != VirtualizationDecisionAcceptanceStateV2.Accepted ||
            acceptance.AcceptancePolicyVersion != 1 || acceptance.AcceptedBy != "@yaksysdev")
            return Deny(VmReadScalarDeliveryDecisionValidationDecisionV2.DeniedAcceptance,
                "Acceptance state, policy version or principal is not exact.");
        if (!ValidateReview(acceptance.OwnerReviewEvidence, VirtualizationDecisionReviewRoleV2.OwnerReviewRole,
                VirtualizationDecisionReviewAuthorityPlaneV2.NeutralRuntimeOwner, spec, acceptance) ||
            !ValidateReview(acceptance.ArchitectureReviewEvidence, VirtualizationDecisionReviewRoleV2.ArchitectureReviewRole,
                VirtualizationDecisionReviewAuthorityPlaneV2.Architecture, spec, acceptance))
            return Deny(VmReadScalarDeliveryDecisionValidationDecisionV2.DeniedReview,
                "Neutral source-owner and architecture reviews must bind the earlier spec.");
        if (!evidence.CodeOwners.FilePresent || evidence.CodeOwners.BlobSha != acceptance.CodeOwnersBlobSha ||
            !RequiredCodeOwnersScopes.All(scope => evidence.CodeOwners.Rules.Any(rule =>
                rule.Scope == scope && rule.Principal == acceptance.AcceptedBy)))
            return Deny(VmReadScalarDeliveryDecisionValidationDecisionV2.DeniedCodeOwners,
                "Required source, carrier, retire or governance ownership is missing.");
        if (!evidence.Revocations.IsDefaultOrEmpty || !evidence.Supersessions.IsDefaultOrEmpty ||
            acceptance.SupersedesDecisionId is not null || acceptance.SupersedesAcceptanceDigest is not null)
            return Deny(VmReadScalarDeliveryDecisionValidationDecisionV2.DeniedLineage,
                "This decision must not modify or supersede an earlier VMREAD decision.");

        var accepted = new AcceptedVmReadScalarDeliveryDecisionV2(
            spec.DecisionId, spec.SpecDigest, acceptance.AcceptanceDigest, acceptance.SpecCommitSha,
            spec.OperationNamespace, spec.ExactFieldIds!.Value, spec.OwnerId, spec.OwnerPolicyVersion,
            spec.OwnerEpoch, spec.ResultAbi, spec.EffectClass, spec.OperationMigrationPolicy, spec.RetirePolicy);
        return new(VmReadScalarDeliveryDecisionValidationDecisionV2.AcceptedPolicyObject,
            "Exact GuestPc/GuestSp/GuestFlags D2 is governance policy only; no receipt or runtime authority is issued.", accepted);
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
        spec.AuthorityPlane == VirtualizationDecisionAuthorityPlaneV2.ExecutionDomainReadOnlyStateCanonicalRegisterDelivery &&
        spec.MutationClass == VirtualizationDecisionMutationClassV2.UnderlyingVirtualizationStateReadOnly &&
        spec.DependencyContract == ExpectedDependencyContract && spec.VmcsMetadataOnly && spec.RequiresConformanceProof &&
        spec.ExactFieldIds is { } fields && fields.SequenceEqual(Phase43GuestPcSpFlagsVmReadScalarDeliveryE0Contract.ExactFieldIds);

    private static bool ValidateOwnerMap(ImmutableArray<VirtualizationDecisionOwnerMapEntryV2> map)
    {
        if (map.IsDefaultOrEmpty || map.Length != 3) return false;
        string[] fields = ["GuestPc", "GuestSp", "GuestFlags"];
        return fields.All(name => map.Any(entry =>
            entry.FieldOrOperation == $"VmcsField.{name}" && entry.Owner == "ExecutionDomainDescriptor" &&
            entry.ValueSource == $"ExecutionDomainReadOnlyStateView.{name}" && entry.CapabilityPolicy == "None" &&
            entry.MigrationClass == "DrainOnly" && entry.EvidenceClass.Contains("MaterializedFieldAndSourceStateProof", StringComparison.Ordinal)));
    }

    private static bool ValidateReview(VirtualizationDecisionReviewEvidenceV2 review,
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
