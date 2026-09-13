using System.Diagnostics;
using System.Text.Json;
using Xunit;
using YAKSys_Hybrid_CPU.Core;

namespace HybridCPU_ISE.Tests.VmxRefactoring;

public sealed class VmxPhase40VmReadD2MaterializedAcceptanceTests
{
    [Fact]
    public void AcceptanceRecord_BindsExactEarlierSpecAndTwoAttributableReviews()
    {
        VirtualizationDecisionSpecV2 spec = Phase40VmReadProjectionDecisionSpecV2.Instance;
        VirtualizationDecisionAcceptanceRecordV2 acceptance =
            Phase40VmReadProjectionDecisionAcceptanceV2.Record;

        Assert.Equal(Phase40VmReadProjectionDecisionAcceptanceV2.SpecCommitSha,
            acceptance.SpecCommitSha);
        Assert.Equal(spec.SpecDigest, acceptance.SpecDigest);
        Assert.Equal(VirtualizationDecisionAcceptanceStateV2.Accepted,
            acceptance.AcceptanceState);
        Assert.Equal(Phase40VmReadProjectionDecisionAcceptanceV2.RepositoryPrincipal,
            acceptance.AcceptedBy);
        Assert.Equal(acceptance.AcceptedBy, acceptance.OwnerReviewEvidence.Principal);
        Assert.Equal(acceptance.AcceptedBy, acceptance.ArchitectureReviewEvidence.Principal);
        Assert.Equal(VirtualizationDecisionReviewAuthorityPlaneV2.NeutralRuntimeOwner,
            acceptance.OwnerReviewEvidence.AuthorityPlane);
        Assert.Equal(VirtualizationDecisionReviewAuthorityPlaneV2.Architecture,
            acceptance.ArchitectureReviewEvidence.AuthorityPlane);
        Assert.Equal(Phase40VmReadProjectionDecisionAcceptanceV2.ExpectedAcceptanceDigest,
            acceptance.AcceptanceDigest);
        Assert.Equal(VirtualizationDecisionCanonicalEncoderV2.ComputeAcceptanceDigest(acceptance),
            acceptance.AcceptanceDigest);
    }

    [Fact]
    public void RepositoryEvidence_ResolvesEarlierSpecCommitAndMachineAcceptsPolicyOnly()
    {
        string root = VmxDocumentationMigrationClaimHygieneTests.FindRepositoryRoot();
        string currentHead = Git(root, "rev-parse", "HEAD");
        string currentTree = Git(root, "rev-parse", "HEAD^{tree}");
        string codeOwnersBlob = Git(root, "rev-parse",
            $"{Phase40VmReadProjectionDecisionAcceptanceV2.SpecCommitSha}:.github/CODEOWNERS");
        string specSourceAtCommit = Git(root, "show",
            $"{Phase40VmReadProjectionDecisionAcceptanceV2.SpecCommitSha}:HybridCPU_ISE/CloseToHSL/Core/Runtime/Governance/Virtualization/Phase40VmReadProjectionDecisionSpecV2.cs");
        string currentSpecSource = File.ReadAllText(Path.Combine(root,
            "HybridCPU_ISE", "CloseToHSL", "Core", "Runtime", "Governance",
            "Virtualization", "Phase40VmReadProjectionDecisionSpecV2.cs"));

        Assert.NotEqual(Phase40VmReadProjectionDecisionAcceptanceV2.SpecCommitSha, currentHead);
        Assert.Equal(40, currentHead.Length);
        Assert.Equal(40, currentTree.Length);
        Assert.Equal(Phase40VmReadProjectionDecisionAcceptanceV2.CodeOwnersBlobSha,
            codeOwnersBlob);
        Assert.Equal(Normalize(currentSpecSource), Normalize(specSourceAtCommit));

        VmReadProjectionDecisionValidationResultV2 result =
            Phase40VmReadProjectionDecisionAcceptanceV2.ValidateRepositoryArtifact(currentHead);
        Assert.Equal(VmReadProjectionDecisionValidationDecisionV2.AcceptedPolicyObject,
            result.Decision);
        Assert.True(result.IsAcceptedPolicyObject);
        Assert.NotNull(result.AcceptedDecision);
        Assert.Equal(Phase40VmReadProjectionE0Contract.ExactFieldIds,
            result.AcceptedDecision.ExactFieldIds);
        Assert.Equal(VirtualizationDecisionMutationClassV2.ReadOnly,
            result.AcceptedDecision.MutationClass);
        Assert.False(result.RuntimeCapabilityGranted);
        Assert.False(result.ProjectionValueAvailable);
        Assert.False(result.BackendExecutionAuthorized);
        Assert.False(result.MutationAuthorized);
        Assert.False(result.CompletionPublicationAuthorized);
        Assert.False(result.RetirePublicationAuthorized);
    }

    [Fact]
    public void AcceptanceArtifact_HasNoFrontendBackendCompletionOrRetireShortcut()
    {
        Assert.False(Phase40VmReadProjectionDecisionAcceptanceV2.RuntimeAuthorityGranted);
        Assert.False(Phase40VmReadProjectionDecisionAcceptanceV2.ProjectionValueAvailable);
        Assert.False(Phase40VmReadProjectionDecisionAcceptanceV2.CapabilityGranted);
        Assert.False(Phase40VmReadProjectionDecisionAcceptanceV2.BackendExecutionAuthorized);
        Assert.False(Phase40VmReadProjectionDecisionAcceptanceV2.MutationAuthorized);
        Assert.False(Phase40VmReadProjectionDecisionAcceptanceV2.CompletionPublicationAuthorized);
        Assert.False(Phase40VmReadProjectionDecisionAcceptanceV2.RetirePublicationAuthorized);

        string root = VmxDocumentationMigrationClaimHygieneTests.FindRepositoryRoot();
        string source = File.ReadAllText(Path.Combine(root,
            "HybridCPU_ISE", "CloseToHSL", "Core", "Runtime", "Governance",
            "Virtualization", "Phase40VmReadProjectionDecisionAcceptanceV2.cs"));
        Assert.DoesNotContain("CompatibilityFrontend", source);
        Assert.DoesNotContain("Project(", source);
        Assert.DoesNotContain("Admit(", source);
        Assert.DoesNotContain("Execute(", source);
        Assert.DoesNotContain("new CapabilityGrant", source);
        Assert.DoesNotContain("new CompletionRecord", source);
        Assert.DoesNotContain("VmxRetireEffect", source);
        Assert.DoesNotContain("BackendExecutionAuthorized => true", source);
        Assert.DoesNotContain("ProjectionValueAvailable => true", source);
    }

    [Fact]
    public void ReviewReceipt_MatchesTheImmutableAcceptanceRecord()
    {
        string root = VmxDocumentationMigrationClaimHygieneTests.FindRepositoryRoot();
        string path = Path.Combine(root, "HybridCPU_ISE", "docs", "ref2",
            "VirtualizationActivationPlan", "evidence",
            "2026-08-11-phase40-guest-cr0-cr4-d2-acceptance.json");
        using JsonDocument receipt = JsonDocument.Parse(File.ReadAllText(path));
        JsonElement record = receipt.RootElement;

        Assert.Equal(Phase40VmReadProjectionDecisionAcceptanceV2.SpecCommitSha,
            record.GetProperty("spec_commit_sha").GetString());
        Assert.Equal(Phase40VmReadProjectionDecisionAcceptanceV2.ExpectedSpecDigest,
            record.GetProperty("spec_digest_sha256").GetString());
        Assert.Equal(Phase40VmReadProjectionDecisionAcceptanceV2.ExpectedAcceptanceDigest,
            record.GetProperty("acceptance_digest_sha256").GetString());
        Assert.Equal(Phase40VmReadProjectionDecisionAcceptanceV2.CodeOwnersBlobSha,
            record.GetProperty("codeowners_blob_sha").GetString());
        Assert.True(record.GetProperty("non_self_referential").GetBoolean());
        Assert.Equal(2, record.GetProperty("reviews").GetArrayLength());
        Assert.False(record.GetProperty("runtime_authority_granted").GetBoolean());
        Assert.False(record.GetProperty("production_vmread_implementation_authorized").GetBoolean());
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
