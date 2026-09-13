using System.Diagnostics;
using System.Text.Json;
using Xunit;
using YAKSys_Hybrid_CPU.Core;

namespace HybridCPU_ISE.Tests.VmxRefactoring;

public sealed class VmxPhase41VmReadScalarDeliveryD2MaterializedAcceptanceTests
{
    [Fact]
    public void AcceptanceRecord_BindsExactEarlierSpecAndReviews()
    {
        VirtualizationDecisionAcceptanceRecordV2 acceptance =
            Phase41VmReadScalarDeliveryDecisionAcceptanceV2.Record;

        Assert.Equal(Phase41VmReadScalarDeliveryDecisionAcceptanceV2.SpecCommitSha,
            acceptance.SpecCommitSha);
        Assert.Equal(Phase41VmReadScalarDeliveryDecisionAcceptanceV2.ExpectedSpecDigest,
            acceptance.SpecDigest);
        Assert.Equal(Phase41VmReadScalarDeliveryDecisionAcceptanceV2.ExpectedAcceptanceDigest,
            acceptance.AcceptanceDigest);
        Assert.Equal(VirtualizationDecisionCanonicalEncoderV2.ComputeAcceptanceDigest(acceptance),
            acceptance.AcceptanceDigest);
        Assert.Equal(VirtualizationDecisionAcceptanceStateV2.Accepted,
            acceptance.AcceptanceState);
        Assert.Equal(VirtualizationDecisionReviewAuthorityPlaneV2.NeutralRuntimeOwner,
            acceptance.OwnerReviewEvidence.AuthorityPlane);
        Assert.Equal(VirtualizationDecisionReviewAuthorityPlaneV2.Architecture,
            acceptance.ArchitectureReviewEvidence.AuthorityPlane);
    }

    [Fact]
    public void RepositoryEvidence_ResolvesEarlierSpecAndAcceptsPolicyOnly()
    {
        string root = VmxDocumentationMigrationClaimHygieneTests.FindRepositoryRoot();
        string currentHead = Git(root, "rev-parse", "HEAD");
        string codeOwnersBlob = Git(root, "rev-parse",
            $"{Phase41VmReadScalarDeliveryDecisionAcceptanceV2.SpecCommitSha}:.github/CODEOWNERS");
        string specAtCommit = Git(root, "show",
            $"{Phase41VmReadScalarDeliveryDecisionAcceptanceV2.SpecCommitSha}:HybridCPU_ISE/CloseToHSL/Core/Runtime/Governance/Virtualization/Phase41VmReadScalarDeliveryDecisionSpecV2.cs");
        string currentSpec = File.ReadAllText(Path.Combine(root, "HybridCPU_ISE", "CloseToHSL",
            "Core", "Runtime", "Governance", "Virtualization",
            "Phase41VmReadScalarDeliveryDecisionSpecV2.cs"));

        Assert.NotEqual(Phase41VmReadScalarDeliveryDecisionAcceptanceV2.SpecCommitSha,
            currentHead);
        Assert.Equal(Phase41VmReadScalarDeliveryDecisionAcceptanceV2.CodeOwnersBlobSha,
            codeOwnersBlob);
        Assert.Equal(Normalize(currentSpec), Normalize(specAtCommit));

        VmReadScalarDeliveryDecisionValidationResultV2 result =
            Phase41VmReadScalarDeliveryDecisionAcceptanceV2.ValidateRepositoryArtifact(currentHead);
        Assert.True(result.IsAcceptedPolicyObject);
        Assert.NotNull(result.AcceptedDecision);
        Assert.Equal(VirtualizationDecisionResultAbiV2.ScalarU64ToDestinationRegister,
            result.AcceptedDecision.ResultAbi);
        Assert.Equal(VirtualizationDecisionEffectClassV2.ArchitecturalRegisterResultOnly,
            result.AcceptedDecision.EffectClass);
        Assert.Equal(VirtualizationOperationMigrationPolicyV2.DrainOnly,
            result.AcceptedDecision.MigrationPolicy);
        Assert.False(result.RuntimeAuthorityGranted);
        Assert.False(result.ResultReceiptIssued);
        Assert.False(result.RegisterWritebackAuthorized);
        Assert.False(result.RetireCommitAuthorized);
        Assert.False(result.BackendExecutionAuthorized);
        Assert.False(result.UnderlyingVirtualizationMutationAuthorized);
        Assert.False(result.CompletionPublicationAuthorized);
    }

    [Fact]
    public void AcceptanceArtifact_HasNoRuntimeShortcut()
    {
        Assert.False(Phase41VmReadScalarDeliveryDecisionAcceptanceV2.RuntimeAuthorityGranted);
        Assert.False(Phase41VmReadScalarDeliveryDecisionAcceptanceV2.SourceValueAvailable);
        Assert.False(Phase41VmReadScalarDeliveryDecisionAcceptanceV2.ResultReceiptIssued);
        Assert.False(Phase41VmReadScalarDeliveryDecisionAcceptanceV2.RegisterWritebackAuthorized);
        Assert.False(Phase41VmReadScalarDeliveryDecisionAcceptanceV2.RetireCommitAuthorized);
        Assert.False(Phase41VmReadScalarDeliveryDecisionAcceptanceV2.BackendExecutionAuthorized);
        Assert.False(Phase41VmReadScalarDeliveryDecisionAcceptanceV2.UnderlyingVirtualizationMutationAuthorized);
        Assert.False(Phase41VmReadScalarDeliveryDecisionAcceptanceV2.CompletionPublicationAuthorized);
    }

    [Fact]
    public void AcceptanceEvidence_MatchesCanonicalRecord()
    {
        string root = VmxDocumentationMigrationClaimHygieneTests.FindRepositoryRoot();
        string path = Path.Combine(root, "HybridCPU_ISE", "docs", "ref2",
            "VirtualizationActivationPlan", "evidence",
            "2026-08-11-phase41-guest-cr0-cr4-scalar-delivery-d2-acceptance.json");
        using JsonDocument document = JsonDocument.Parse(File.ReadAllText(path));
        JsonElement record = document.RootElement;

        Assert.Equal(Phase41VmReadScalarDeliveryDecisionAcceptanceV2.SpecCommitSha,
            record.GetProperty("spec_commit_sha").GetString());
        Assert.Equal(Phase41VmReadScalarDeliveryDecisionAcceptanceV2.ExpectedSpecDigest,
            record.GetProperty("spec_digest_sha256").GetString());
        Assert.Equal(Phase41VmReadScalarDeliveryDecisionAcceptanceV2.ExpectedAcceptanceDigest,
            record.GetProperty("acceptance_digest_sha256").GetString());
        Assert.True(record.GetProperty("non_self_referential").GetBoolean());
        Assert.False(record.GetProperty("runtime_authority_granted").GetBoolean());
        Assert.False(record.GetProperty("result_receipt_issued").GetBoolean());
        Assert.False(record.GetProperty("production_scalar_delivery_activated").GetBoolean());
    }

    private static string Normalize(string value) =>
        value.Replace("\r\n", "\n", StringComparison.Ordinal).TrimEnd();

    private static string Git(string workingDirectory, params string[] arguments)
    {
        var startInfo = new ProcessStartInfo("git")
        {
            WorkingDirectory = workingDirectory,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };
        foreach (string argument in arguments)
            startInfo.ArgumentList.Add(argument);

        using Process process = Process.Start(startInfo)!;
        string output = process.StandardOutput.ReadToEnd();
        string error = process.StandardError.ReadToEnd();
        process.WaitForExit();
        Assert.True(process.ExitCode == 0, error);
        return output.TrimEnd();
    }
}
