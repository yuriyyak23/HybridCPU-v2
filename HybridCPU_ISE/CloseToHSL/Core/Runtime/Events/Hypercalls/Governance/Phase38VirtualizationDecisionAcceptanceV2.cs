using System.Collections.Immutable;

namespace YAKSys_Hybrid_CPU.Core;

/// <summary>
/// Attributable acceptance of the exact SpecV2 bytes already present in commit A.
/// The record intentionally does not contain the SHA of its own containing commit.
/// </summary>
internal static class Phase38VirtualizationDecisionAcceptanceV2
{
    internal const string SpecCommitSha = "1061eaa8bc45d598e1fe7b3fead71cf017ad81a6";
    internal const string ExpectedSpecDigest =
        "33076e430fcbc05cf0774d08baadc6d7840f88029fcfb28a458558af82f93ca8";
    internal const string ExpectedAcceptanceDigest =
        "b928ff6254139b9fc0cf9412e192db2f68b1918f01646bea65c85e33791e33e3";
    internal const string CodeOwnersBlobSha = "aafa6f65565345e18621eea0cc890711f64c3dbb";
    internal const string RepositoryPrincipal = "@yaksysdev";
    internal const string OwnerReviewEvidenceId =
        "PRB-OWNER-REVIEW-2026-08-09-1061EAA8";
    internal const string ArchitectureReviewEvidenceId =
        "PRB-ARCH-REVIEW-2026-08-09-1061EAA8";

    internal static VirtualizationDecisionAcceptanceRecordV2 Record { get; } = CreateRecord();

    internal static ImmutableArray<byte> CanonicalBytes { get; } =
        VirtualizationDecisionCanonicalEncoderV2.EncodeAcceptance(Record);

    internal static VirtualizationCodeOwnersEvidenceV2 CodeOwnersEvidence { get; } = new(
        true,
        CodeOwnersBlobSha,
        [
            new("/HybridCPU_ISE/CloseToHSL/Core/Runtime/Events/Hypercalls/", RepositoryPrincipal),
            new("/HybridCPU_ISE/CloseToHSL/Core/Runtime/Capabilities/", RepositoryPrincipal),
            new("/HybridCPU_ISE/CloseToHSL/Core/Pipeline/Safety/", RepositoryPrincipal),
            new("/HybridCPU_ISE/CloseToHSL/Core/Runtime/Completion/", RepositoryPrincipal),
            new("/HybridCPU_ISE/CloseToHSL/Core/Pipeline/Retire/", RepositoryPrincipal),
            new("/HybridCPU_ISE/docs/ref2/VirtualizationActivationPlan/", RepositoryPrincipal),
        ]);

    internal static bool RuntimeAuthorityGranted => false;
    internal static bool BackendExecutionAuthorized => false;
    internal static bool CompletionPublicationAuthorized => false;
    internal static bool RetirePublicationAuthorized => false;

    internal static VirtualizationDecisionValidationResultV2 ValidateRepositoryArtifact(
        string acceptanceContainingCommitSha)
    {
        VirtualizationDecisionSpecV2 spec = Phase38VirtualizationDecisionSpecV2.Instance;
        return VirtualizationDecisionValidatorV2.Validate(
            spec,
            Record,
            new(
                Phase38VirtualizationDecisionSpecV2.CanonicalBytes,
                CanonicalBytes,
                Phase38VirtualizationDecisionSpecV2.CanonicalBytes,
                SpecCommitSha,
                acceptanceContainingCommitSha,
                CodeOwnersEvidence,
                [
                    new(
                        "HybridCPU.VMFUNC.FrozenAbi.v1",
                        16,
                        1,
                        "FROZEN-VMFUNC-CAPABILITY-QUERY",
                        VirtualizationNamespaceClassV2.FrozenCompatibility),
                ],
                [],
                []));
    }

    private static VirtualizationDecisionAcceptanceRecordV2 CreateRecord()
    {
        VirtualizationDecisionSpecV2 spec = Phase38VirtualizationDecisionSpecV2.Instance;
        if (!string.Equals(spec.SpecDigest, ExpectedSpecDigest, StringComparison.Ordinal))
            throw new InvalidOperationException("The materialized SpecV2 digest drifted from reviewed commit A.");

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
            VirtualizationDecisionValidatorV2.CurrentSchemaVersion,
            spec.DecisionId,
            ExpectedSpecDigest,
            SpecCommitSha,
            VirtualizationDecisionAcceptanceStateV2.Accepted,
            RepositoryPrincipal,
            1,
            ownerReview,
            architectureReview,
            CodeOwnersBlobSha,
            null,
            null,
            new string('0', 64));

        record = record with
        {
            AcceptanceDigest =
                VirtualizationDecisionCanonicalEncoderV2.ComputeAcceptanceDigest(record),
        };
        if (!string.Equals(record.AcceptanceDigest, ExpectedAcceptanceDigest, StringComparison.Ordinal))
            throw new InvalidOperationException("The attributable AcceptanceRecordV2 digest drifted.");

        return record;
    }
}
