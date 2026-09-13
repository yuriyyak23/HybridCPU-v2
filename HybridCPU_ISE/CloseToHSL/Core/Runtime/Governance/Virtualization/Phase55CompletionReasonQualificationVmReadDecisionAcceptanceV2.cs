using System.Collections.Immutable;

namespace YAKSys_Hybrid_CPU.Core;

internal static class Phase55CompletionReasonQualificationVmReadDecisionAcceptanceV2
{
    internal const string SpecCommitSha = "9497d6152bee14e3743e99edc6c4c451b869ee8a";
    internal const string SpecTreeSha = "60d9d11f8ef783466d7d05e03a52ccdb43ecb007";
    internal const string ExpectedSpecDigest =
        "649f576e09ca0168ba60be3f632a4d8c93f6fabaa8571c22a5445f438dcda6d2";
    internal const string ExpectedAcceptanceDigest =
        "4954b25a5e3c91e2d4c54f0d4e75f83c87e495773889b94e4fd6e98f5617f5be";
    internal const string CodeOwnersBlobSha = "5ee888ca6312af05ac2780bdf288a3d3506d1a63";
    internal const string RepositoryPrincipal = "@yaksysdev";

    internal static VirtualizationDecisionAcceptanceRecordV2 Record { get; } = CreateRecord();
    internal static ImmutableArray<byte> CanonicalBytes { get; } =
        VirtualizationDecisionCanonicalEncoderV2.EncodeAcceptance(Record);
    internal static VirtualizationCodeOwnersEvidenceV2 CodeOwnersEvidence { get; } = new(
        true, CodeOwnersBlobSha,
        [
            Rule("/HybridCPU_ISE/CloseToHSL/Core/Runtime/Governance/Virtualization/"),
            Rule("/HybridCPU_ISE/CloseToHSL/Core/Runtime/Completion/"),
            Rule("/HybridCPU_ISE/CloseToHSL/Core/Virtualization/Compatibility/Frontend/Projection/Completion/"),
            Rule("/HybridCPU_ISE/CloseToHSL/Core/Pipeline/Retire/"),
            Rule("/HybridCPU_ISE/docs/ref2/VirtualizationActivationPlan/"),
        ]);

    internal static bool RuntimeAuthorityGranted => false;
    internal static bool CompletionCreationAuthorized => false;
    internal static bool ProductionCompositionAuthorized => false;
    internal static bool GuestPhysicalAddressAuthorized => false;
    internal static bool EptViolationQualificationAuthorized => false;

    internal static VmReadScalarDeliveryDecisionValidationResultV2 ValidateRepositoryArtifact(
        string acceptanceContainingCommitSha) =>
        CompletionReasonQualificationVmReadDecisionValidatorV2.Validate(
            Phase55CompletionReasonQualificationVmReadDecisionSpecV2.Instance,
            Record,
            new(
                Phase55CompletionReasonQualificationVmReadDecisionSpecV2.CanonicalBytes,
                CanonicalBytes,
                Phase55CompletionReasonQualificationVmReadDecisionSpecV2.CanonicalBytes,
                SpecCommitSha,
                acceptanceContainingCommitSha,
                CodeOwnersEvidence,
                [], [], []));

    private static VirtualizationDecisionAcceptanceRecordV2 CreateRecord()
    {
        VirtualizationDecisionSpecV2 spec = Phase55CompletionReasonQualificationVmReadDecisionSpecV2.Instance;
        if (spec.SpecDigest != ExpectedSpecDigest)
            throw new InvalidOperationException("Phase 55 SpecV2 drifted from its immutable subject commit.");
        VirtualizationDecisionReviewEvidenceV2 ownerReview = new(
            VirtualizationDecisionReviewRoleV2.OwnerReviewRole,
            VirtualizationDecisionReviewAuthorityPlaneV2.NeutralRuntimeOwner,
            VirtualizationDecisionReviewStateV2.Completed,
            RepositoryPrincipal, spec.DecisionId, ExpectedSpecDigest, SpecCommitSha,
            "PHASE55-COMPLETION-OBSERVATION-OWNER-REVIEW-2026-08-13-9497D61");
        VirtualizationDecisionReviewEvidenceV2 architectureReview = ownerReview with
        {
            Role = VirtualizationDecisionReviewRoleV2.ArchitectureReviewRole,
            AuthorityPlane = VirtualizationDecisionReviewAuthorityPlaneV2.Architecture,
            EvidenceId = "PHASE55-ARCHITECTURE-REVIEW-2026-08-13-9497D61",
        };
        VirtualizationDecisionAcceptanceRecordV2 record = new(
            CompletionReasonQualificationVmReadDecisionValidatorV2.SchemaVersion,
            spec.DecisionId, ExpectedSpecDigest, SpecCommitSha,
            VirtualizationDecisionAcceptanceStateV2.Accepted, RepositoryPrincipal,
            AcceptancePolicyVersion: 1, ownerReview, architectureReview, CodeOwnersBlobSha,
            SupersedesDecisionId: null, SupersedesAcceptanceDigest: null,
            AcceptanceDigest: new string('0', 64));
        record = record with
        {
            AcceptanceDigest = VirtualizationDecisionCanonicalEncoderV2.ComputeAcceptanceDigest(record),
        };
        if (record.AcceptanceDigest != ExpectedAcceptanceDigest)
            throw new InvalidOperationException($"Phase 55 AcceptanceRecordV2 digest is {record.AcceptanceDigest}.");
        return record;
    }

    private static VirtualizationCodeOwnersRuleV2 Rule(string scope) => new(scope, RepositoryPrincipal);
}
