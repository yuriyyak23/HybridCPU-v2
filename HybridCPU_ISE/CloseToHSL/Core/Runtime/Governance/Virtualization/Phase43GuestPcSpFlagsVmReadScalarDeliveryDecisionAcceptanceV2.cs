using System.Collections.Immutable;

namespace YAKSys_Hybrid_CPU.Core;

internal static class Phase43GuestPcSpFlagsVmReadScalarDeliveryDecisionAcceptanceV2
{
    internal const string SpecCommitSha = "46fc02c26e54aea5d5a905e953c490340b1e338a";
    internal const string SpecTreeSha = "3df2dd90c043e44d30bf4d5ed42113d41814703f";
    internal const string ExpectedSpecDigest =
        "e67ff2620ff6a1fd193b8303c5b6ae1d532e51241e6b3e405f3c6cedefe2d754";
    internal const string ExpectedAcceptanceDigest =
        "1bc7ccf0df814538ff531572fa9e8872e61f081dd0fae25dfc4337d50d94b96a";
    internal const string CodeOwnersBlobSha = "8e7843a9e4f2f604df08509eff9f6bcf2365fc5d";
    internal const string RepositoryPrincipal = "@yaksysdev";

    internal static VirtualizationDecisionAcceptanceRecordV2 Record { get; } = CreateRecord();
    internal static ImmutableArray<byte> CanonicalBytes { get; } =
        VirtualizationDecisionCanonicalEncoderV2.EncodeAcceptance(Record);

    internal static VirtualizationCodeOwnersEvidenceV2 CodeOwnersEvidence { get; } = new(
        true, CodeOwnersBlobSha,
        [
            Rule("/HybridCPU_ISE/CloseToHSL/Core/Runtime/Governance/Virtualization/"),
            Rule("/HybridCPU_ISE/CloseToHSL/Core/Runtime/Domains/Descriptors/ExecutionDomain/"),
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
    internal static bool ProductionCompositionAuthorized => false;

    internal static VmReadScalarDeliveryDecisionValidationResultV2 ValidateRepositoryArtifact(
        string acceptanceContainingCommitSha) =>
        GuestPcSpFlagsVmReadScalarDeliveryDecisionValidatorV2.Validate(
            Phase43GuestPcSpFlagsVmReadScalarDeliveryDecisionSpecV2.Instance,
            Record,
            new(
                Phase43GuestPcSpFlagsVmReadScalarDeliveryDecisionSpecV2.CanonicalBytes,
                CanonicalBytes,
                Phase43GuestPcSpFlagsVmReadScalarDeliveryDecisionSpecV2.CanonicalBytes,
                SpecCommitSha,
                acceptanceContainingCommitSha,
                CodeOwnersEvidence,
                [], [], []));

    private static VirtualizationDecisionAcceptanceRecordV2 CreateRecord()
    {
        VirtualizationDecisionSpecV2 spec = Phase43GuestPcSpFlagsVmReadScalarDeliveryDecisionSpecV2.Instance;
        if (spec.SpecDigest != ExpectedSpecDigest)
            throw new InvalidOperationException("Phase 43 SpecV2 drifted from its immutable subject commit.");
        VirtualizationDecisionReviewEvidenceV2 ownerReview = new(
            VirtualizationDecisionReviewRoleV2.OwnerReviewRole,
            VirtualizationDecisionReviewAuthorityPlaneV2.NeutralRuntimeOwner,
            VirtualizationDecisionReviewStateV2.Completed,
            RepositoryPrincipal, spec.DecisionId, ExpectedSpecDigest, SpecCommitSha,
            "PHASE43-EXECUTION-DOMAIN-OWNER-REVIEW-2026-08-11-46FC02C");
        VirtualizationDecisionReviewEvidenceV2 architectureReview = ownerReview with
        {
            Role = VirtualizationDecisionReviewRoleV2.ArchitectureReviewRole,
            AuthorityPlane = VirtualizationDecisionReviewAuthorityPlaneV2.Architecture,
            EvidenceId = "PHASE43-ARCHITECTURE-REVIEW-2026-08-11-46FC02C",
        };
        VirtualizationDecisionAcceptanceRecordV2 record = new(
            GuestPcSpFlagsVmReadScalarDeliveryDecisionValidatorV2.SchemaVersion,
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
            throw new InvalidOperationException($"Phase 43 AcceptanceRecordV2 digest is {record.AcceptanceDigest}.");
        return record;
    }

    private static VirtualizationCodeOwnersRuleV2 Rule(string scope) => new(scope, RepositoryPrincipal);
}
