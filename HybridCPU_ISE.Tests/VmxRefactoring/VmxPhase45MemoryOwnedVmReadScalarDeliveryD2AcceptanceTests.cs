using System.Collections.Immutable;
using System.Text.Json;
using YAKSys_Hybrid_CPU.Core;

namespace HybridCPU_ISE.Tests.VmxRefactoring;

public sealed class VmxPhase45MemoryOwnedVmReadScalarDeliveryD2AcceptanceTests
{
    private const string LaterContainingCommit = "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";

    [Fact]
    public void Acceptance_IsLaterNonSelfReferentialAndGovernanceOnly()
    {
        VirtualizationDecisionAcceptanceRecordV2 record =
            Phase45MemoryOwnedVmReadScalarDeliveryDecisionAcceptanceV2.Record;
        Assert.Equal(Phase45MemoryOwnedVmReadScalarDeliveryDecisionAcceptanceV2.SpecCommitSha,
            record.SpecCommitSha);
        Assert.NotEqual(LaterContainingCommit, record.SpecCommitSha);
        Assert.Equal(Phase45MemoryOwnedVmReadScalarDeliveryDecisionAcceptanceV2.ExpectedAcceptanceDigest,
            record.AcceptanceDigest);
        Assert.Equal(VmReadScalarDeliveryDecisionValidationDecisionV2.AcceptedPolicyObject,
            Phase45MemoryOwnedVmReadScalarDeliveryDecisionAcceptanceV2.ValidateRepositoryArtifact(
                LaterContainingCommit).Decision);
        Assert.Null(record.SupersedesDecisionId);
        Assert.Null(record.SupersedesAcceptanceDigest);

        Assert.False(Phase45MemoryOwnedVmReadScalarDeliveryDecisionAcceptanceV2.RuntimeAuthorityGranted);
        Assert.False(Phase45MemoryOwnedVmReadScalarDeliveryDecisionAcceptanceV2.SourceValueAvailable);
        Assert.False(Phase45MemoryOwnedVmReadScalarDeliveryDecisionAcceptanceV2.ResultReceiptIssued);
        Assert.False(Phase45MemoryOwnedVmReadScalarDeliveryDecisionAcceptanceV2.RegisterWritebackAuthorized);
        Assert.False(Phase45MemoryOwnedVmReadScalarDeliveryDecisionAcceptanceV2.RetireCommitAuthorized);
        Assert.False(Phase45MemoryOwnedVmReadScalarDeliveryDecisionAcceptanceV2.BackendExecutionAuthorized);
        Assert.False(Phase45MemoryOwnedVmReadScalarDeliveryDecisionAcceptanceV2.UnderlyingMemoryMutationAuthorized);
        Assert.False(Phase45MemoryOwnedVmReadScalarDeliveryDecisionAcceptanceV2.CompletionPublicationAuthorized);
        Assert.False(Phase45MemoryOwnedVmReadScalarDeliveryDecisionAcceptanceV2.ProductionCompositionAuthorized);
    }

    [Fact]
    public void Validator_FailsClosedForMissingWrongOrRevokedAcceptanceEvidence()
    {
        VirtualizationDecisionSpecV2 spec = Phase45MemoryOwnedVmReadScalarDeliveryDecisionSpecV2.Instance;
        VirtualizationDecisionAcceptanceRecordV2 record =
            Phase45MemoryOwnedVmReadScalarDeliveryDecisionAcceptanceV2.Record;
        Assert.Equal(VmReadScalarDeliveryDecisionValidationDecisionV2.DeniedMissingArtifact,
            MemoryOwnedVmReadScalarDeliveryDecisionValidatorV2.Validate(spec, null, null).Decision);
        Assert.Equal(VmReadScalarDeliveryDecisionValidationDecisionV2.DeniedAcceptance,
            Validate(record with { AcceptedBy = "@compatibility-frontend" }).Decision);
        Assert.Equal(VmReadScalarDeliveryDecisionValidationDecisionV2.DeniedLineage,
            Validate(record, revocations: [null!]).Decision);
        Assert.Equal(VmReadScalarDeliveryDecisionValidationDecisionV2.DeniedProvenance,
            Validate(record, containingSha: record.SpecCommitSha).Decision);
    }

    [Fact]
    public void AcceptanceEvidence_PinsEarlierSpecAndNoProductionAuthority()
    {
        string root = VmxDocumentationMigrationClaimHygieneTests.FindRepositoryRoot();
        string path = Path.Combine(root, "HybridCPU_ISE", "docs", "ref2",
            "VirtualizationActivationPlan", "evidence",
            "2026-08-12-phase45-memory-owned-vmread-scalar-delivery-d2-acceptance.json");
        using JsonDocument document = JsonDocument.Parse(File.ReadAllText(path));
        JsonElement record = document.RootElement;
        Assert.Equal(Phase45MemoryOwnedVmReadScalarDeliveryDecisionAcceptanceV2.SpecCommitSha,
            record.GetProperty("spec_commit_sha").GetString());
        Assert.Equal(Phase45MemoryOwnedVmReadScalarDeliveryDecisionAcceptanceV2.SpecTreeSha,
            record.GetProperty("spec_tree_sha").GetString());
        Assert.Equal(Phase45MemoryOwnedVmReadScalarDeliveryDecisionAcceptanceV2.ExpectedAcceptanceDigest,
            record.GetProperty("acceptance_digest_sha256").GetString());
        Assert.True(record.GetProperty("non_self_referential").GetBoolean());
        Assert.False(record.GetProperty("runtime_authority_granted").GetBoolean());
        Assert.False(record.GetProperty("production_scalar_delivery_activated").GetBoolean());
        Assert.False(record.GetProperty("next_pool_automatically_opened").GetBoolean());
    }

    private static VmReadScalarDeliveryDecisionValidationResultV2 Validate(
        VirtualizationDecisionAcceptanceRecordV2 record,
        ImmutableArray<VirtualizationDecisionRevocationEvidenceV2> revocations = default,
        string containingSha = LaterContainingCommit)
    {
        VirtualizationDecisionSpecV2 spec = Phase45MemoryOwnedVmReadScalarDeliveryDecisionSpecV2.Instance;
        return MemoryOwnedVmReadScalarDeliveryDecisionValidatorV2.Validate(spec, record,
            new(spec is null ? [] : Phase45MemoryOwnedVmReadScalarDeliveryDecisionSpecV2.CanonicalBytes,
                VirtualizationDecisionCanonicalEncoderV2.EncodeAcceptance(record),
                Phase45MemoryOwnedVmReadScalarDeliveryDecisionSpecV2.CanonicalBytes,
                Phase45MemoryOwnedVmReadScalarDeliveryDecisionAcceptanceV2.SpecCommitSha,
                containingSha,
                Phase45MemoryOwnedVmReadScalarDeliveryDecisionAcceptanceV2.CodeOwnersEvidence,
                [], revocations.IsDefault ? [] : revocations, []));
    }
}
