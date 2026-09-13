using System.Collections.Immutable;

namespace YAKSys_Hybrid_CPU.Core;

internal static class Phase57CompletionSecondStageVmReadDecisionAcceptanceV2
{
    internal const string SpecCommitSha = "0865929ae2ab5ee95b0912eebd63ae7af2494f4c";
    internal const string SpecTreeSha = "a0108c9e035ff9c57c0167a6efd04788535a1ebd";
    internal const string ExpectedSpecDigest =
        "c68adf194f0452c15f4d11e3a44199b50a0a6168923915e280c62fcbe683c4ab";
    internal const string ExpectedAcceptanceDigest =
        "a6622a79ff3278227a9a39b4e51b20345366ffba24d47cb107b53a0301d3e955";
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

    internal static VmReadScalarDeliveryDecisionValidationResultV2 ValidateRepositoryArtifact(
        string acceptanceContainingCommitSha) =>
        CompletionSecondStageVmReadDecisionValidatorV2.Validate(
            Phase57CompletionSecondStageVmReadDecisionSpecV2.Instance,
            Record,
            new(
                Phase57CompletionSecondStageVmReadDecisionSpecV2.CanonicalBytes,
                CanonicalBytes,
                Phase57CompletionSecondStageVmReadDecisionSpecV2.CanonicalBytes,
                SpecCommitSha,
                acceptanceContainingCommitSha,
                CodeOwnersEvidence,
                [], [], []));

    private static VirtualizationDecisionAcceptanceRecordV2 CreateRecord()
    {
        VirtualizationDecisionSpecV2 spec = Phase57CompletionSecondStageVmReadDecisionSpecV2.Instance;
        if (spec.SpecDigest != ExpectedSpecDigest)
            throw new InvalidOperationException("Phase 57 SpecV2 drifted from its immutable subject commit.");
        VirtualizationDecisionReviewEvidenceV2 ownerReview = new(
            VirtualizationDecisionReviewRoleV2.OwnerReviewRole,
            VirtualizationDecisionReviewAuthorityPlaneV2.NeutralRuntimeOwner,
            VirtualizationDecisionReviewStateV2.Completed,
            RepositoryPrincipal, spec.DecisionId, ExpectedSpecDigest, SpecCommitSha,
            "PHASE57-COMPLETION-OBSERVATION-OWNER-REVIEW-2026-08-13-0865929");
        VirtualizationDecisionReviewEvidenceV2 architectureReview = ownerReview with
        {
            Role = VirtualizationDecisionReviewRoleV2.ArchitectureReviewRole,
            AuthorityPlane = VirtualizationDecisionReviewAuthorityPlaneV2.Architecture,
            EvidenceId = "PHASE57-ARCHITECTURE-REVIEW-2026-08-13-0865929",
        };
        VirtualizationDecisionAcceptanceRecordV2 record = new(
            CompletionSecondStageVmReadDecisionValidatorV2.SchemaVersion,
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
            throw new InvalidOperationException($"Phase 57 AcceptanceRecordV2 digest is {record.AcceptanceDigest}.");
        return record;
    }

    private static VirtualizationCodeOwnersRuleV2 Rule(string scope) => new(scope, RepositoryPrincipal);
}
