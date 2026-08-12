using System.Collections.Immutable;

namespace YAKSys_Hybrid_CPU.Core;

/// <summary>
/// Later attributable acceptance of the exact VMREAD SpecV2 bytes already
/// present at SpecCommitSha. The record never names its containing commit and
/// has no runtime projection, capability, admission or execution API.
/// </summary>
internal static class Phase40VmReadProjectionDecisionAcceptanceV2
{
    internal const string SpecCommitSha = "4d3b5b97c22661652c94357319e2e6b16615cceb";
    internal const string ExpectedSpecDigest =
        "52ce040b93f54b36a427c4269f2afff77b2e66f83ceda3ece1b1dc917a58241f";
    internal const string ExpectedAcceptanceDigest =
        "cf99799baba3ce6595fef61b2f53a5ec1a8e1c144d0bccd29df8171f603c34d8";
    internal const string CodeOwnersBlobSha = "f659868c26110e7c0f82778be6dcd3f4f59f43b7";
    internal const string RepositoryPrincipal = "@yaksysdev";
    internal const string OwnerReviewEvidenceId =
        "PHASE40-OWNER-REVIEW-2026-08-11-4D3B5B9";
    internal const string ArchitectureReviewEvidenceId =
        "PHASE40-ARCH-REVIEW-2026-08-11-4D3B5B9";

    internal static VirtualizationDecisionAcceptanceRecordV2 Record { get; } = CreateRecord();

    internal static ImmutableArray<byte> CanonicalBytes { get; } =
        VirtualizationDecisionCanonicalEncoderV2.EncodeAcceptance(Record);

    internal static VirtualizationCodeOwnersEvidenceV2 CodeOwnersEvidence { get; } = new(
        true,
        CodeOwnersBlobSha,
        [
            new("/HybridCPU_ISE/CloseToHSL/Core/Runtime/Governance/Virtualization/", RepositoryPrincipal),
            new("/HybridCPU_ISE/CloseToHSL/Core/Runtime/Domains/SecureCompute/Descriptors/ExecutionState/", RepositoryPrincipal),
            new("/HybridCPU_ISE/CloseToHSL/Core/Runtime/Domains/SecureCompute/Policies/ExecutionState/", RepositoryPrincipal),
            new("/HybridCPU_ISE/CloseToHSL/Core/Virtualization/SecureCompute/Compatibility/Projection/", RepositoryPrincipal),
            new("/HybridCPU_ISE/CloseToHSL/Core/Virtualization/Compatibility/Frontend/Projection/VmcsRead/", RepositoryPrincipal),
            new("/HybridCPU_ISE/docs/ref2/VirtualizationActivationPlan/", RepositoryPrincipal),
        ]);

    internal static bool RuntimeAuthorityGranted => false;
    internal static bool ProjectionValueAvailable => false;
    internal static bool CapabilityGranted => false;
    internal static bool BackendExecutionAuthorized => false;
    internal static bool MutationAuthorized => false;
    internal static bool CompletionPublicationAuthorized => false;
    internal static bool RetirePublicationAuthorized => false;

    internal static VmReadProjectionDecisionValidationResultV2 ValidateRepositoryArtifact(
        string acceptanceContainingCommitSha) =>
        VmReadProjectionDecisionValidatorV2.Validate(
            Phase40VmReadProjectionDecisionSpecV2.Instance,
            Record,
            new(
                Phase40VmReadProjectionDecisionSpecV2.CanonicalBytes,
                CanonicalBytes,
                Phase40VmReadProjectionDecisionSpecV2.CanonicalBytes,
                SpecCommitSha,
                acceptanceContainingCommitSha,
                CodeOwnersEvidence,
                [],
                [],
                []));

    private static VirtualizationDecisionAcceptanceRecordV2 CreateRecord()
    {
        VirtualizationDecisionSpecV2 spec = Phase40VmReadProjectionDecisionSpecV2.Instance;
        if (!string.Equals(spec.SpecDigest, ExpectedSpecDigest, StringComparison.Ordinal))
            throw new InvalidOperationException(
                "The VMREAD SpecV2 digest drifted from the earlier reviewed commit.");

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
        if (!string.Equals(record.AcceptanceDigest, ExpectedAcceptanceDigest, StringComparison.Ordinal))
            throw new InvalidOperationException(
                "The attributable VMREAD AcceptanceRecordV2 digest drifted.");

        return record;
    }
}
