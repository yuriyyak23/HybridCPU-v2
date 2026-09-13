using System.Collections.Immutable;

namespace YAKSys_Hybrid_CPU.Core;

/// <summary>
/// Later attributable acceptance of the exact scalar-delivery SpecV2 bytes at
/// SpecCommitSha. It is governance policy only and never names its own commit.
/// </summary>
internal static class Phase41VmReadScalarDeliveryDecisionAcceptanceV2
{
    internal const string SpecCommitSha =
        "bb2125226425eb341fd94cc67cc6bb26abf918fe";
    internal const string ExpectedSpecDigest =
        "ccda8698dbeb3f6eef1b4f13e22a3fb7607e939f493138fb7e3373674e234309";
    internal const string ExpectedAcceptanceDigest =
        "465a887c2918a762af6bf2039f98c294b2f601b1bb7b71512df3f95a9141b5d5";
    internal const string CodeOwnersBlobSha =
        "6a9aef1c554340818860242cdb571b4278b5735c";
    internal const string RepositoryPrincipal = "@yaksysdev";
    internal const string OwnerReviewEvidenceId =
        "PHASE41-SCALAR-DELIVERY-OWNER-REVIEW-2026-08-11-BB21252";
    internal const string ArchitectureReviewEvidenceId =
        "PHASE41-SCALAR-DELIVERY-ARCH-REVIEW-2026-08-11-BB21252";

    internal static VirtualizationDecisionAcceptanceRecordV2 Record { get; } =
        CreateRecord();

    internal static ImmutableArray<byte> CanonicalBytes { get; } =
        VirtualizationDecisionCanonicalEncoderV2.EncodeAcceptance(Record);

    internal static VirtualizationCodeOwnersEvidenceV2 CodeOwnersEvidence { get; } = new(
        true,
        CodeOwnersBlobSha,
        [
            Rule("/HybridCPU_ISE/CloseToHSL/Core/Runtime/Governance/Virtualization/"),
            Rule("/HybridCPU_ISE/CloseToHSL/Core/Runtime/Domains/SecureCompute/Descriptors/ExecutionState/"),
            Rule("/HybridCPU_ISE/CloseToHSL/Core/Runtime/Domains/SecureCompute/Policies/ExecutionState/"),
            Rule("/HybridCPU_ISE/CloseToHSL/Core/Virtualization/SecureCompute/Compatibility/Projection/"),
            Rule("/HybridCPU_ISE/CloseToHSL/Core/Virtualization/Compatibility/Frontend/Projection/VmcsRead/"),
            Rule("/HybridCPU_ISE/CloseToHSL/Core/Pipeline/Retire/"),
            Rule("/HybridCPU_ISE/CloseToHSL/Core/Pipeline/ExecutionFlow/"),
            Rule("/HybridCPU_ISE/CloseToHSL/Core/Pipeline/MicroOps/Types/"),
            Rule("/HybridCPU_ISE/CloseToHSL/Core/Architecture/Registers/"),
            Rule("/HybridCPU_ISE/docs/ref2/VirtualizationActivationPlan/"),
        ]);

    internal static bool RuntimeAuthorityGranted => false;
    internal static bool SourceValueAvailable => false;
    internal static bool ResultReceiptIssued => false;
    internal static bool RegisterWritebackAuthorized => false;
    internal static bool RetireCommitAuthorized => false;
    internal static bool BackendExecutionAuthorized => false;
    internal static bool UnderlyingVirtualizationMutationAuthorized => false;
    internal static bool CompletionPublicationAuthorized => false;

    internal static VmReadScalarDeliveryDecisionValidationResultV2
        ValidateRepositoryArtifact(string acceptanceContainingCommitSha) =>
        VmReadScalarDeliveryDecisionValidatorV2.Validate(
            Phase41VmReadScalarDeliveryDecisionSpecV2.Instance,
            Record,
            new(
                Phase41VmReadScalarDeliveryDecisionSpecV2.CanonicalBytes,
                CanonicalBytes,
                Phase41VmReadScalarDeliveryDecisionSpecV2.CanonicalBytes,
                SpecCommitSha,
                acceptanceContainingCommitSha,
                CodeOwnersEvidence,
                [],
                [],
                []));

    private static VirtualizationDecisionAcceptanceRecordV2 CreateRecord()
    {
        VirtualizationDecisionSpecV2 spec =
            Phase41VmReadScalarDeliveryDecisionSpecV2.Instance;
        if (spec.SpecDigest != ExpectedSpecDigest)
            throw new InvalidOperationException(
                "The scalar-delivery SpecV2 digest drifted from the earlier commit.");

        VirtualizationDecisionReviewEvidenceV2 ownerReview = new(
            VirtualizationDecisionReviewRoleV2.OwnerReviewRole,
            VirtualizationDecisionReviewAuthorityPlaneV2.NeutralRuntimeOwner,
            VirtualizationDecisionReviewStateV2.Completed,
            RepositoryPrincipal,
            spec.DecisionId,
            ExpectedSpecDigest,
            SpecCommitSha,
            OwnerReviewEvidenceId);
        VirtualizationDecisionReviewEvidenceV2 architectureReview = ownerReview with
        {
            Role = VirtualizationDecisionReviewRoleV2.ArchitectureReviewRole,
            AuthorityPlane = VirtualizationDecisionReviewAuthorityPlaneV2.Architecture,
            EvidenceId = ArchitectureReviewEvidenceId,
        };
        VirtualizationDecisionAcceptanceRecordV2 record = new(
            VmReadScalarDeliveryDecisionValidatorV2.SchemaVersion,
            spec.DecisionId,
            ExpectedSpecDigest,
            SpecCommitSha,
            VirtualizationDecisionAcceptanceStateV2.Accepted,
            RepositoryPrincipal,
            AcceptancePolicyVersion: 1,
            ownerReview,
            architectureReview,
            CodeOwnersBlobSha,
            SupersedesDecisionId: null,
            SupersedesAcceptanceDigest: null,
            AcceptanceDigest: new string('0', 64));

        record = record with
        {
            AcceptanceDigest =
                VirtualizationDecisionCanonicalEncoderV2.ComputeAcceptanceDigest(record),
        };
        if (record.AcceptanceDigest != ExpectedAcceptanceDigest)
            throw new InvalidOperationException(
                "The scalar-delivery AcceptanceRecordV2 canonical digest drifted.");

        return record;
    }

    private static VirtualizationCodeOwnersRuleV2 Rule(string scope) =>
        new(scope, RepositoryPrincipal);
}
