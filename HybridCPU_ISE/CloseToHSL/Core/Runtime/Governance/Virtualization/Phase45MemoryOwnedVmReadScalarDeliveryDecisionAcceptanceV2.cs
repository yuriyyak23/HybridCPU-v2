using System.Collections.Immutable;

namespace YAKSys_Hybrid_CPU.Core;

internal static class Phase45MemoryOwnedVmReadScalarDeliveryDecisionAcceptanceV2
{
    internal const string SpecCommitSha = "4299d743af293f0a0780eb5289a54c3154259ad2";
    internal const string SpecTreeSha = "71ac59e42a8abfec6fb8adee958d483b08de7529";
    internal const string ExpectedSpecDigest =
        "7cc2ad6bca9cc808aa6d42767dba5c7eaefed1a34180cb6caf3b34384662df21";
    internal const string ExpectedAcceptanceDigest =
        "741be410f5e699d9a953b3d8a8fe1171abbbda6d798b9b97dbb89e10bae55dcc";
    internal const string CodeOwnersBlobSha = "8e7843a9e4f2f604df08509eff9f6bcf2365fc5d";
    internal const string RepositoryPrincipal = "@yaksysdev";

    internal static VirtualizationDecisionAcceptanceRecordV2 Record { get; } = CreateRecord();
    internal static ImmutableArray<byte> CanonicalBytes { get; } =
        VirtualizationDecisionCanonicalEncoderV2.EncodeAcceptance(Record);

    internal static VirtualizationCodeOwnersEvidenceV2 CodeOwnersEvidence { get; } = new(
        true, CodeOwnersBlobSha,
        [
            Rule("/HybridCPU_ISE/CloseToHSL/Core/Runtime/Governance/Virtualization/"),
            Rule("/HybridCPU_ISE/CloseToHSL/Core/Runtime/Domains/Descriptors/MemoryDomain/"),
            Rule("/HybridCPU_ISE/CloseToHSL/Core/Runtime/Domains/Admission/Memory/"),
            Rule("/HybridCPU_ISE/CloseToHSL/Core/Runtime/Memory/Translation/"),
            Rule("/HybridCPU_ISE/CloseToHSL/Core/Virtualization/Compatibility/Frontend/Projection/VmcsRead/"),
            Rule("/HybridCPU_ISE/CloseToHSL/Core/Pipeline/Retire/"),
            Rule("/HybridCPU_ISE/docs/ref2/VirtualizationActivationPlan/"),
        ]);

    internal static bool RuntimeAuthorityGranted => false;
    internal static bool SourceValueAvailable => false;
    internal static bool ResultReceiptIssued => false;
    internal static bool RegisterWritebackAuthorized => false;
    internal static bool RetireCommitAuthorized => false;
    internal static bool BackendExecutionAuthorized => false;
    internal static bool UnderlyingMemoryMutationAuthorized => false;
    internal static bool CompletionPublicationAuthorized => false;
    internal static bool ProductionCompositionAuthorized => false;

    internal static VmReadScalarDeliveryDecisionValidationResultV2 ValidateRepositoryArtifact(
        string acceptanceContainingCommitSha) =>
        MemoryOwnedVmReadScalarDeliveryDecisionValidatorV2.Validate(
            Phase45MemoryOwnedVmReadScalarDeliveryDecisionSpecV2.Instance,
            Record,
            new(
                Phase45MemoryOwnedVmReadScalarDeliveryDecisionSpecV2.CanonicalBytes,
                CanonicalBytes,
                Phase45MemoryOwnedVmReadScalarDeliveryDecisionSpecV2.CanonicalBytes,
                SpecCommitSha,
                acceptanceContainingCommitSha,
                CodeOwnersEvidence,
                [], [], []));

    private static VirtualizationDecisionAcceptanceRecordV2 CreateRecord()
    {
        VirtualizationDecisionSpecV2 spec = Phase45MemoryOwnedVmReadScalarDeliveryDecisionSpecV2.Instance;
        if (spec.SpecDigest != ExpectedSpecDigest)
            throw new InvalidOperationException("Phase 45 SpecV2 drifted from its immutable subject commit.");
        VirtualizationDecisionReviewEvidenceV2 ownerReview = new(
            VirtualizationDecisionReviewRoleV2.OwnerReviewRole,
            VirtualizationDecisionReviewAuthorityPlaneV2.NeutralRuntimeOwner,
            VirtualizationDecisionReviewStateV2.Completed,
            RepositoryPrincipal, spec.DecisionId, ExpectedSpecDigest, SpecCommitSha,
            "PHASE45-MEMORY-DOMAIN-OWNER-REVIEW-2026-08-12-4299D74");
        VirtualizationDecisionReviewEvidenceV2 architectureReview = ownerReview with
        {
            Role = VirtualizationDecisionReviewRoleV2.ArchitectureReviewRole,
            AuthorityPlane = VirtualizationDecisionReviewAuthorityPlaneV2.Architecture,
            EvidenceId = "PHASE45-ARCHITECTURE-REVIEW-2026-08-12-4299D74",
        };
        VirtualizationDecisionAcceptanceRecordV2 record = new(
            MemoryOwnedVmReadScalarDeliveryDecisionValidatorV2.SchemaVersion,
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
            throw new InvalidOperationException($"Phase 45 AcceptanceRecordV2 digest is {record.AcceptanceDigest}.");
        return record;
    }

    private static VirtualizationCodeOwnersRuleV2 Rule(string scope) => new(scope, RepositoryPrincipal);
}
