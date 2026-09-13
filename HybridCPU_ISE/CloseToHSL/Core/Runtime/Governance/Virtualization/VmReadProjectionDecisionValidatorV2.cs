using System.Collections.Immutable;
using System.Security.Cryptography;
using System.Text;

namespace YAKSys_Hybrid_CPU.Core;

internal enum VmReadProjectionDecisionValidationDecisionV2 : byte
{
    AcceptedPolicyObject = 0,
    DeniedMissingArtifact = 1,
    DeniedCanonicalArtifact = 2,
    DeniedProvenance = 3,
    DeniedIdentity = 4,
    DeniedProfile = 5,
    DeniedOwnerMap = 6,
    DeniedAcceptance = 7,
    DeniedReview = 8,
    DeniedCodeOwners = 9,
    DeniedLineage = 10,
}

internal sealed record VmReadProjectionDecisionValidationResultV2(
    VmReadProjectionDecisionValidationDecisionV2 Decision,
    string Reason,
    AcceptedVmReadProjectionDecisionV2? AcceptedDecision)
{
    internal bool IsAcceptedPolicyObject =>
        Decision == VmReadProjectionDecisionValidationDecisionV2.AcceptedPolicyObject &&
        AcceptedDecision is not null;

    internal bool RuntimeCapabilityGranted => false;
    internal bool ProjectionValueAvailable => false;
    internal bool BackendExecutionAuthorized => false;
    internal bool MutationAuthorized => false;
    internal bool CompletionPublicationAuthorized => false;
    internal bool RetirePublicationAuthorized => false;
}

/// <summary>
/// Fail-closed governance validator for one exact read-only VMREAD profile.
/// It is intentionally separate from the Phase 38 VMCALL validator and exposes
/// no runtime lookup, projection or admission operation.
/// </summary>
internal static class VmReadProjectionDecisionValidatorV2
{
    internal const string ExpectedDecisionId =
        "D2-HV-VMREAD-PROJECTION-V1-GUEST-CR0-CR4-0001";
    internal const string ExpectedOperationNamespace =
        "HybridCPU.VMREAD.Projection.v1";
    internal const string ExpectedOperationId = "READ_GUEST_CR0_CR4_V1";
    internal const ulong ExpectedOwnerId = 0x0048_4350_4553_524FUL; // HCPESRO

    private static readonly ImmutableArray<string> RequiredCodeOwnersScopes =
    [
        "/HybridCPU_ISE/CloseToHSL/Core/Runtime/Governance/Virtualization/",
        "/HybridCPU_ISE/CloseToHSL/Core/Runtime/Domains/SecureCompute/Descriptors/ExecutionState/",
        "/HybridCPU_ISE/CloseToHSL/Core/Runtime/Domains/SecureCompute/Policies/ExecutionState/",
        "/HybridCPU_ISE/CloseToHSL/Core/Virtualization/SecureCompute/Compatibility/Projection/",
        "/HybridCPU_ISE/CloseToHSL/Core/Virtualization/Compatibility/Frontend/Projection/VmcsRead/",
        "/HybridCPU_ISE/docs/ref2/VirtualizationActivationPlan/",
    ];

    internal static VmReadProjectionDecisionValidationResultV2 Validate(
        VirtualizationDecisionSpecV2? spec,
        VirtualizationDecisionAcceptanceRecordV2? acceptance,
        VirtualizationDecisionValidationEvidenceV2? evidence)
    {
        if (spec is null || acceptance is null || evidence is null || evidence.CodeOwners is null)
            return Deny(VmReadProjectionDecisionValidationDecisionV2.DeniedMissingArtifact,
                "SpecV2, later AcceptanceRecordV2 and repository evidence are required.");

        if (!ValidateProvenance(acceptance, evidence))
            return Deny(VmReadProjectionDecisionValidationDecisionV2.DeniedProvenance,
                "Acceptance must resolve exact earlier spec bytes and cannot name its containing commit.");

        try
        {
            if (spec.SchemaVersion != VirtualizationDecisionValidatorV2.CurrentSchemaVersion ||
                acceptance.SchemaVersion != VirtualizationDecisionValidatorV2.CurrentSchemaVersion ||
                !VirtualizationDecisionCanonicalEncoderV2.IsCanonicalSha256(spec.SpecDigest) ||
                !FixedEquals(spec.SpecDigest, VirtualizationDecisionCanonicalEncoderV2.ComputeSpecDigest(spec)) ||
                !FixedEquals(acceptance.SpecDigest, spec.SpecDigest) ||
                !FixedEquals(acceptance.AcceptanceDigest,
                    VirtualizationDecisionCanonicalEncoderV2.ComputeAcceptanceDigest(acceptance)) ||
                !FixedEquals(evidence.SpecCanonicalBytes,
                    VirtualizationDecisionCanonicalEncoderV2.EncodeSpec(spec)) ||
                !FixedEquals(evidence.SpecBytesAtCommit,
                    VirtualizationDecisionCanonicalEncoderV2.EncodeSpec(spec)) ||
                !FixedEquals(evidence.AcceptanceCanonicalBytes,
                    VirtualizationDecisionCanonicalEncoderV2.EncodeAcceptance(acceptance)))
                return Deny(VmReadProjectionDecisionValidationDecisionV2.DeniedCanonicalArtifact,
                    "Spec or acceptance bytes/digests are not their exact canonical V2 forms.");
        }
        catch (Exception exception) when (
            exception is FormatException or ArgumentException or OverflowException)
        {
            return Deny(VmReadProjectionDecisionValidationDecisionV2.DeniedCanonicalArtifact,
                "Canonical artifact fields are malformed.");
        }

        if (!string.Equals(spec.DecisionId, ExpectedDecisionId, StringComparison.Ordinal) ||
            !string.Equals(acceptance.DecisionId, ExpectedDecisionId, StringComparison.Ordinal) ||
            !string.Equals(spec.OperationNamespace, ExpectedOperationNamespace, StringComparison.Ordinal) ||
            !string.Equals(spec.OperationId, ExpectedOperationId, StringComparison.Ordinal))
            return Deny(VmReadProjectionDecisionValidationDecisionV2.DeniedIdentity,
                "Decision, namespace and operation identity must match the exact VMREAD profile.");

        if (!ValidateExactProfile(spec))
            return Deny(VmReadProjectionDecisionValidationDecisionV2.DeniedProfile,
                "The VMREAD policy is wider than the exact read-only GuestCr0/GuestCr4 profile.");

        if (!ValidateOwnerMap(spec.OwnerMap))
            return Deny(VmReadProjectionDecisionValidationDecisionV2.DeniedOwnerMap,
                "Both fields must bind the same neutral owner/source class and exact field-local value source.");

        if (!ValidateAcceptance(spec, acceptance))
            return Deny(VmReadProjectionDecisionValidationDecisionV2.DeniedAcceptance,
                "Acceptance does not bind the exact immutable spec with accepted governance state.");

        if (!ValidateReviews(spec, acceptance))
            return Deny(VmReadProjectionDecisionValidationDecisionV2.DeniedReview,
                "Neutral-owner and architecture reviews must bind the same earlier spec commit and digest.");

        if (!ValidateCodeOwners(acceptance, evidence.CodeOwners))
            return Deny(VmReadProjectionDecisionValidationDecisionV2.DeniedCodeOwners,
                "Repository attribution is missing for one or more exact owner/projection scopes.");

        if (!evidence.Revocations.IsDefaultOrEmpty || !evidence.Supersessions.IsDefaultOrEmpty ||
            acceptance.SupersedesDecisionId is not null || acceptance.SupersedesAcceptanceDigest is not null)
            return Deny(VmReadProjectionDecisionValidationDecisionV2.DeniedLineage,
                "This first VMREAD D2 acceptance has no revocation or supersession lineage.");

        var accepted = new AcceptedVmReadProjectionDecisionV2(
            spec.DecisionId,
            spec.SpecDigest,
            acceptance.AcceptanceDigest,
            acceptance.SpecCommitSha,
            spec.OperationNamespace,
            spec.ExactFieldIds!.Value,
            spec.OwnerId,
            spec.OwnerPolicyVersion,
            spec.OwnerEpoch,
            spec.MutationClass);

        return new(
            VmReadProjectionDecisionValidationDecisionV2.AcceptedPolicyObject,
            "Exact GuestCr0/GuestCr4 D2 is accepted as immutable governance policy only; no runtime authority is granted.",
            accepted);
    }

    private static bool ValidateProvenance(
        VirtualizationDecisionAcceptanceRecordV2 acceptance,
        VirtualizationDecisionValidationEvidenceV2 evidence) =>
        VirtualizationDecisionCanonicalEncoderV2.IsCanonicalCommitSha(acceptance.SpecCommitSha) &&
        VirtualizationDecisionCanonicalEncoderV2.IsCanonicalCommitSha(acceptance.CodeOwnersBlobSha) &&
        VirtualizationDecisionCanonicalEncoderV2.IsCanonicalCommitSha(evidence.ResolvedSpecCommitSha) &&
        VirtualizationDecisionCanonicalEncoderV2.IsCanonicalCommitSha(evidence.AcceptanceContainingCommitSha) &&
        string.Equals(acceptance.SpecCommitSha, evidence.ResolvedSpecCommitSha, StringComparison.Ordinal) &&
        !string.Equals(acceptance.SpecCommitSha, evidence.AcceptanceContainingCommitSha, StringComparison.Ordinal);

    private static bool ValidateExactProfile(VirtualizationDecisionSpecV2 spec) =>
        spec.LeafWidth == 0 && spec.InvalidLeaf == 0 && spec.NumericLeaf == 0 &&
        spec.OwnerClass == VirtualizationDecisionOwnerClassV2.NeutralRuntimeOwner &&
        spec.OwnerId == ExpectedOwnerId && spec.OwnerPolicyVersion == 1 && spec.OwnerEpoch == 1 &&
        spec.OperandAbiVersion == 1 &&
        spec.Rs1Contract == "VmcsFieldSelectorExactFrozenId" &&
        spec.Rs2Contract == "X0ReservedNoAuthority" &&
        spec.RdContract == "ArchitecturalDestinationRegisterScalar64" &&
        spec.ResultAbi == VirtualizationDecisionResultAbiV2.ArchitecturalScalar64 &&
        spec.EffectClass == VirtualizationDecisionEffectClassV2.ReadOnlyProjectionNoStateMutation &&
        spec.CapabilityRequirement == VirtualizationCapabilityRequirementV2.None &&
        spec.CapabilityMask == 0 && !spec.RequiresTypedGrant &&
        spec.DelegationPolicy == VirtualizationDelegationPolicyV2.NonDelegable &&
        spec.RevocationPolicy == VirtualizationRevocationPolicyV2.GovernanceRevocable &&
        spec.CapabilityMigrationClass == VirtualizationCapabilityMigrationClassV2.None &&
        spec.EvidenceVisibility == VirtualizationEvidenceVisibilityV2.GuestVisibleReadOnly &&
        spec.FrontendProjectionPolicy == VirtualizationProjectionPolicyV2.ExactReadOnlyFieldSet &&
        spec.ExecutionEvidenceRequirement == VirtualizationExecutionEvidenceRequirementV2.FieldConformanceProof &&
        spec.DomainRequirement == VirtualizationDomainRequirementV2.ExecutionDomainAndAddressSpaceBound &&
        spec.RequireNonZeroDomainTag && !spec.RequiresMemoryDomain && !spec.RequiresIoDomain &&
        spec.AddressSpaceRequirement == VirtualizationAddressSpaceRequirementV2.ExactNonZeroAddressSpaceTag &&
        spec.SecureDomainPolicy == VirtualizationSecureDomainPolicyV2.Deny &&
        spec.CancellationPolicy == VirtualizationCancellationPolicyV2.NotApplicableReadOnlyProjection &&
        spec.ReplayPolicy == VirtualizationReplayPolicyV2.NotApplicableReadOnlyProjection &&
        spec.OperationMigrationPolicy == VirtualizationOperationMigrationPolicyV2.RevalidatedAfterRestore &&
        spec.CompletionEvidenceClass == VirtualizationCompletionEvidenceClassV2.None &&
        spec.CompletionMigrationClass == VirtualizationCompletionMigrationClassV2.None &&
        spec.CompletionProjectionPolicy == VirtualizationProjectionPolicyV2.NeverProject &&
        spec.CompletionPolicy == VirtualizationCompletionPolicyV2.None &&
        spec.RetirePolicy == VirtualizationRetirePolicyV2.None &&
        spec.AdjacentLeafPolicy == VirtualizationAdjacentLeafPolicyV2.DenyAllExceptExactFieldSet &&
        spec.CrossNamespacePolicy == VirtualizationCrossNamespacePolicyV2.DenyCrossNamespaceReuse &&
        spec.OperationClass == VirtualizationDecisionOperationClassV2.ReadOnlyArchitecturalVmReadCompatibilityProjection &&
        spec.AuthorityPlane == VirtualizationDecisionAuthorityPlaneV2.PrivilegedExecutionStateReadProjection &&
        spec.MutationClass == VirtualizationDecisionMutationClassV2.ReadOnly &&
        spec.DependencyContract == "JointDescriptorLegalityGuestCr0AndGuestCr4" &&
        spec.VmcsMetadataOnly && spec.RequiresConformanceProof &&
        spec.ExactFieldIds is { } exactFieldIds &&
        exactFieldIds.SequenceEqual(Phase40VmReadProjectionE0Contract.ExactFieldIds);

    private static bool ValidateOwnerMap(
        ImmutableArray<VirtualizationDecisionOwnerMapEntryV2> ownerMap)
    {
        if (ownerMap.IsDefaultOrEmpty || ownerMap.Length != 2)
            return false;

        VirtualizationDecisionOwnerMapEntryV2? cr0 = ownerMap.FirstOrDefault(
            item => item.FieldOrOperation == "VmcsField.GuestCr0");
        VirtualizationDecisionOwnerMapEntryV2? cr4 = ownerMap.FirstOrDefault(
            item => item.FieldOrOperation == "VmcsField.GuestCr4");
        return cr0 is not null && cr4 is not null &&
            cr0.Owner == "PrivilegedExecutionStateOwnerPolicy" && cr4.Owner == cr0.Owner &&
            cr0.ValueSource == "PrivilegedExecutionStateDescriptor.GuestCr0" &&
            cr4.ValueSource == "PrivilegedExecutionStateDescriptor.GuestCr4" &&
            cr0.CapabilityPolicy == "None" && cr4.CapabilityPolicy == cr0.CapabilityPolicy &&
            cr0.EvidenceClass == cr4.EvidenceClass &&
            cr0.MigrationClass == "RevalidatedAfterRestore" && cr4.MigrationClass == cr0.MigrationClass &&
            cr0.DenialReason == cr4.DenialReason;
    }

    private static bool ValidateAcceptance(
        VirtualizationDecisionSpecV2 spec,
        VirtualizationDecisionAcceptanceRecordV2 acceptance) =>
        acceptance.AcceptanceState == VirtualizationDecisionAcceptanceStateV2.Accepted &&
        acceptance.AcceptancePolicyVersion == 1 &&
        acceptance.AcceptedBy == "@yaksysdev" &&
        FixedEquals(acceptance.SpecDigest, spec.SpecDigest) &&
        VirtualizationDecisionCanonicalEncoderV2.IsCanonicalSha256(acceptance.AcceptanceDigest);

    private static bool ValidateReviews(
        VirtualizationDecisionSpecV2 spec,
        VirtualizationDecisionAcceptanceRecordV2 acceptance) =>
        ValidateReview(acceptance.OwnerReviewEvidence,
            VirtualizationDecisionReviewRoleV2.OwnerReviewRole,
            VirtualizationDecisionReviewAuthorityPlaneV2.NeutralRuntimeOwner,
            spec, acceptance) &&
        ValidateReview(acceptance.ArchitectureReviewEvidence,
            VirtualizationDecisionReviewRoleV2.ArchitectureReviewRole,
            VirtualizationDecisionReviewAuthorityPlaneV2.Architecture,
            spec, acceptance);

    private static bool ValidateReview(
        VirtualizationDecisionReviewEvidenceV2 review,
        VirtualizationDecisionReviewRoleV2 role,
        VirtualizationDecisionReviewAuthorityPlaneV2 plane,
        VirtualizationDecisionSpecV2 spec,
        VirtualizationDecisionAcceptanceRecordV2 acceptance) =>
        review is not null && review.Role == role && review.AuthorityPlane == plane &&
        review.State == VirtualizationDecisionReviewStateV2.Completed &&
        review.Principal == acceptance.AcceptedBy &&
        review.ReviewedDecisionId == spec.DecisionId &&
        FixedEquals(review.ReviewedSpecDigest, spec.SpecDigest) &&
        review.ReviewedSpecCommitSha == acceptance.SpecCommitSha &&
        !string.IsNullOrWhiteSpace(review.EvidenceId);

    private static bool ValidateCodeOwners(
        VirtualizationDecisionAcceptanceRecordV2 acceptance,
        VirtualizationCodeOwnersEvidenceV2 codeOwners) =>
        codeOwners.FilePresent && codeOwners.BlobSha == acceptance.CodeOwnersBlobSha &&
        RequiredCodeOwnersScopes.All(scope => codeOwners.Rules.Any(rule =>
            rule.Scope == scope && rule.Principal == acceptance.AcceptedBy));

    private static bool FixedEquals(string? left, string? right) =>
        left is not null && right is not null &&
        CryptographicOperations.FixedTimeEquals(
            Encoding.ASCII.GetBytes(left), Encoding.ASCII.GetBytes(right));

    private static bool FixedEquals(ImmutableArray<byte> left, ImmutableArray<byte> right) =>
        !left.IsDefault && !right.IsDefault &&
        CryptographicOperations.FixedTimeEquals(left.AsSpan(), right.AsSpan());

    private static VmReadProjectionDecisionValidationResultV2 Deny(
        VmReadProjectionDecisionValidationDecisionV2 decision,
        string reason) => new(decision, reason, null);
}
