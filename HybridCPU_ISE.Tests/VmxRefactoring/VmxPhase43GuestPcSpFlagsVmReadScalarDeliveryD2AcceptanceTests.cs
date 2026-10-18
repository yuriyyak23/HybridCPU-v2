using Xunit;
using YAKSys_Hybrid_CPU.Core;
using System.Text.Json;

namespace HybridCPU_ISE.Tests.VmxRefactoring;

public sealed class VmxPhase43GuestPcSpFlagsVmReadScalarDeliveryD2AcceptanceTests
{
    [Fact]
    public void Acceptance_BindsEarlierSpecAndProducesPolicyMetadataOnly()
    {
        VirtualizationDecisionAcceptanceRecordV2 acceptance =
            Phase43GuestPcSpFlagsVmReadScalarDeliveryDecisionAcceptanceV2.Record;
        Assert.Equal("46fc02c26e54aea5d5a905e953c490340b1e338a", acceptance.SpecCommitSha);
        Assert.Equal(Phase43GuestPcSpFlagsVmReadScalarDeliveryDecisionAcceptanceV2.ExpectedSpecDigest,
            acceptance.SpecDigest);
        Assert.Equal(VirtualizationDecisionAcceptanceStateV2.Accepted, acceptance.AcceptanceState);
        Assert.Null(acceptance.SupersedesDecisionId);
        Assert.Null(acceptance.SupersedesAcceptanceDigest);

        VmReadScalarDeliveryDecisionValidationResultV2 result =
            Phase43GuestPcSpFlagsVmReadScalarDeliveryDecisionAcceptanceV2.ValidateRepositoryArtifact(
                "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa");
        Assert.True(result.IsAcceptedPolicyObject);
        Assert.NotNull(result.AcceptedDecision);
        Assert.True(result.AcceptedDecision.ExactFieldIds.SequenceEqual(
            [(ushort)VmcsField.GuestPc, (ushort)VmcsField.GuestSp, (ushort)VmcsField.GuestFlags]));
        Assert.Equal(VirtualizationDecisionResultAbiV2.ScalarU64ToDestinationRegister,
            result.AcceptedDecision.ResultAbi);
        Assert.Equal(VirtualizationOperationMigrationPolicyV2.DrainOnly,
            result.AcceptedDecision.MigrationPolicy);
        Assert.False(result.RuntimeAuthorityGranted);
        Assert.False(result.ResultReceiptIssued);
        Assert.False(result.RegisterWritebackAuthorized);
        Assert.False(result.RetireCommitAuthorized);
        Assert.False(result.BackendExecutionAuthorized);
        Assert.False(result.CompletionPublicationAuthorized);
    }

    [Fact]
    public void Acceptance_DeniesSelfReferenceWrongReviewAndSourceOwnerDrift()
    {
        VirtualizationDecisionAcceptanceRecordV2 acceptance =
            Phase43GuestPcSpFlagsVmReadScalarDeliveryDecisionAcceptanceV2.Record;
        Assert.Equal(VmReadScalarDeliveryDecisionValidationDecisionV2.DeniedProvenance,
            Phase43GuestPcSpFlagsVmReadScalarDeliveryDecisionAcceptanceV2.ValidateRepositoryArtifact(
                acceptance.SpecCommitSha).Decision);

        VirtualizationDecisionSpecV2 spec = Phase43GuestPcSpFlagsVmReadScalarDeliveryDecisionSpecV2.Instance;
        VirtualizationDecisionAcceptanceRecordV2 wrongReview = acceptance with
        {
            OwnerReviewEvidence = acceptance.OwnerReviewEvidence with
            {
                AuthorityPlane = VirtualizationDecisionReviewAuthorityPlaneV2.CompatibilityFrontend,
            },
        };
        Assert.Equal(VmReadScalarDeliveryDecisionValidationDecisionV2.DeniedAcceptance,
            GuestPcSpFlagsVmReadScalarDeliveryDecisionValidatorV2.Validate(spec, wrongReview,
                Evidence(wrongReview, "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa")).Decision);

        Assert.Equal(VmReadScalarDeliveryDecisionValidationDecisionV2.DeniedOwnerMap,
            GuestPcSpFlagsVmReadScalarDeliveryDecisionValidatorV2.ValidateSpecShape(spec with
            {
                OwnerMap = [spec.OwnerMap[0] with { Owner = "VmcsV2Descriptor" },
                    spec.OwnerMap[1], spec.OwnerMap[2]],
            }).Decision);
    }

    [Fact]
    public void AcceptanceArtifact_ContainsNoSideAuthority()
    {
        Assert.False(Phase43GuestPcSpFlagsVmReadScalarDeliveryDecisionAcceptanceV2.RuntimeAuthorityGranted);
        Assert.False(Phase43GuestPcSpFlagsVmReadScalarDeliveryDecisionAcceptanceV2.SourceValueAvailable);
        Assert.False(Phase43GuestPcSpFlagsVmReadScalarDeliveryDecisionAcceptanceV2.ResultReceiptIssued);
        Assert.False(Phase43GuestPcSpFlagsVmReadScalarDeliveryDecisionAcceptanceV2.RegisterWritebackAuthorized);
        Assert.False(Phase43GuestPcSpFlagsVmReadScalarDeliveryDecisionAcceptanceV2.RetireCommitAuthorized);
        Assert.False(Phase43GuestPcSpFlagsVmReadScalarDeliveryDecisionAcceptanceV2.BackendExecutionAuthorized);
        Assert.False(Phase43GuestPcSpFlagsVmReadScalarDeliveryDecisionAcceptanceV2.CompletionPublicationAuthorized);
        Assert.False(Phase43GuestPcSpFlagsVmReadScalarDeliveryDecisionAcceptanceV2.ProductionCompositionAuthorized);
    }

    [Fact]
    public void AcceptanceEvidence_IsExactAndNonSelfReferential()
    {
        string root = VmxDocumentationMigrationClaimHygieneTests.FindRepositoryRoot();
        string path = Path.Combine(root, "HybridCPU_ISE", "docs", "ref2",
            "VirtualizationActivationPlan", "evidence",
            "2026-08-11-phase43-guest-pc-sp-flags-scalar-delivery-d2-acceptance.json");
        using JsonDocument document = JsonDocument.Parse(File.ReadAllText(path));
        JsonElement record = document.RootElement;
        Assert.Equal(Phase43GuestPcSpFlagsVmReadScalarDeliveryDecisionAcceptanceV2.SpecCommitSha,
            record.GetProperty("spec_commit_sha").GetString());
        Assert.Equal(Phase43GuestPcSpFlagsVmReadScalarDeliveryDecisionAcceptanceV2.ExpectedSpecDigest,
            record.GetProperty("spec_digest_sha256").GetString());
        Assert.Equal(Phase43GuestPcSpFlagsVmReadScalarDeliveryDecisionAcceptanceV2.ExpectedAcceptanceDigest,
            record.GetProperty("acceptance_digest_sha256").GetString());
        Assert.True(record.GetProperty("non_self_referential").GetBoolean());
        Assert.False(record.GetProperty("runtime_authority_granted").GetBoolean());
        Assert.False(record.GetProperty("production_scalar_delivery_activated").GetBoolean());
        Assert.False(record.GetProperty("next_pool_automatically_opened").GetBoolean());
    }

    private static VirtualizationDecisionValidationEvidenceV2 Evidence(
        VirtualizationDecisionAcceptanceRecordV2 acceptance, string containingSha) => new(
            Phase43GuestPcSpFlagsVmReadScalarDeliveryDecisionSpecV2.CanonicalBytes,
            VirtualizationDecisionCanonicalEncoderV2.EncodeAcceptance(acceptance),
            Phase43GuestPcSpFlagsVmReadScalarDeliveryDecisionSpecV2.CanonicalBytes,
            Phase43GuestPcSpFlagsVmReadScalarDeliveryDecisionAcceptanceV2.SpecCommitSha,
            containingSha,
            Phase43GuestPcSpFlagsVmReadScalarDeliveryDecisionAcceptanceV2.CodeOwnersEvidence,
            [], [], []);
}
