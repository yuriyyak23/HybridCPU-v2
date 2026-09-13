using System.Diagnostics;
using System.Text.Json;
using Xunit;
using YAKSys_Hybrid_CPU.Core;

namespace HybridCPU_ISE.Tests.VmxRefactoring;

public sealed class VmxD2V2MaterializedAcceptanceTests
{
    [Fact]
    public void AcceptanceRecord_BindsExactEarlierSpecAndAttributableRoleEvidence()
    {
        VirtualizationDecisionSpecV2 spec = Phase38VirtualizationDecisionSpecV2.Instance;
        VirtualizationDecisionAcceptanceRecordV2 acceptance =
            Phase38VirtualizationDecisionAcceptanceV2.Record;

        Assert.Equal(Phase38VirtualizationDecisionAcceptanceV2.SpecCommitSha, acceptance.SpecCommitSha);
        Assert.Equal(spec.SpecDigest, acceptance.SpecDigest);
        Assert.Equal(VirtualizationDecisionAcceptanceStateV2.Accepted, acceptance.AcceptanceState);
        Assert.Equal(Phase38VirtualizationDecisionAcceptanceV2.RepositoryPrincipal, acceptance.AcceptedBy);
        Assert.Equal(acceptance.AcceptedBy, acceptance.OwnerReviewEvidence.Principal);
        Assert.Equal(acceptance.AcceptedBy, acceptance.ArchitectureReviewEvidence.Principal);
        Assert.Equal(VirtualizationDecisionReviewStateV2.Completed, acceptance.OwnerReviewEvidence.State);
        Assert.Equal(VirtualizationDecisionReviewStateV2.Completed, acceptance.ArchitectureReviewEvidence.State);
        Assert.Equal(
            Phase38VirtualizationDecisionAcceptanceV2.ExpectedAcceptanceDigest,
            acceptance.AcceptanceDigest);
        Assert.NotEqual(acceptance.SpecCommitSha, CurrentHead());
        Assert.True(VirtualizationDecisionCanonicalEncoderV2.IsCanonicalSha256(
            acceptance.AcceptanceDigest));
        Assert.Equal(
            VirtualizationDecisionCanonicalEncoderV2.ComputeAcceptanceDigest(acceptance),
            acceptance.AcceptanceDigest);
    }

    [Fact]
    public void RepositoryEvidence_ResolvesRealCodeOwnersBlobAndValidatesAcceptedPolicyOnly()
    {
        string repositoryRoot = VmxDocumentationMigrationClaimHygieneTests.FindRepositoryRoot();
        string currentHead = Git(repositoryRoot, "rev-parse", "HEAD");
        string codeOwnersBlob = Git(
            repositoryRoot,
            "rev-parse",
            $"{Phase38VirtualizationDecisionAcceptanceV2.SpecCommitSha}:.github/CODEOWNERS");

        Assert.Equal(Phase38VirtualizationDecisionAcceptanceV2.CodeOwnersBlobSha, codeOwnersBlob);
        Assert.Equal(CurrentHead(), currentHead);

        VirtualizationDecisionValidationResultV2 result =
            Phase38VirtualizationDecisionAcceptanceV2.ValidateRepositoryArtifact(currentHead);
        Assert.Equal(VirtualizationDecisionValidationDecisionV2.AcceptedPolicyObject, result.Decision);
        Assert.True(result.IsAcceptedPolicyObject);
        Assert.NotNull(result.AcceptedDecision);
        Assert.False(result.RuntimeCapabilityGranted);
        Assert.False(result.BackendExecutionAuthorized);
        Assert.False(result.CompletionPublicationAuthorized);
        Assert.False(result.RetirePublicationAuthorized);
    }

    [Fact]
    public void GeneratedLookup_IsExactAndAllocationsRemainNonAuthorizing()
    {
        Assert.True(Phase38AcceptedVirtualizationDecisionRegistry.TryResolvePolicy(
            VirtualizationDecisionValidatorV2.ExpectedOperationNamespace,
            1,
            out AcceptedVirtualizationDecisionRegistryEntry entry));
        Assert.Equal(VirtualizationDecisionValidatorV2.ExpectedDecisionId, entry.Policy.DecisionId);
        Assert.False(Phase38AcceptedVirtualizationDecisionRegistry.TryResolvePolicy(
            VirtualizationDecisionValidatorV2.ExpectedOperationNamespace,
            0,
            out _));
        Assert.False(Phase38AcceptedVirtualizationDecisionRegistry.TryResolvePolicy(
            VirtualizationDecisionValidatorV2.ExpectedOperationNamespace,
            2,
            out _));
        Assert.False(Phase38AcceptedVirtualizationDecisionRegistry.TryResolvePolicy(
            "HybridCPU.VMFUNC.FrozenAbi.v1",
            1,
            out _));

        Assert.True(HypercallRuntimeOwnerRegistry.TryGetAllocation(
            VirtualizationDecisionValidatorV2.ExpectedOwnerId,
            out HypercallRuntimeOwnerAllocation owner));
        Assert.False(owner.RuntimeOwnerLoaded);
        Assert.False(owner.BackendExecutionAuthorized);
        Assert.False(HypercallRuntimeOwnerRegistry.TryGetAllocation(0, out _));

        VirtualizationCapabilityAllocation capability =
            VirtualizationCapabilityAllocationRegistry.Phase38Probe;
        Assert.Equal(1UL << 41, capability.CapabilityMask);
        Assert.Equal((ulong)1, capability.AllocationGeneration);
        Assert.Equal(VirtualizationProjectionPolicyV2.NeverProject, capability.ProjectionPolicy);
        Assert.False(capability.IsGrant);
        Assert.False(capability.RuntimeCapabilityGranted);
        Assert.False(entry.RuntimeCapabilityGranted);
        Assert.False(entry.BackendExecutionAuthorized);
        Assert.False(entry.CompletionPublicationAuthorized);
        Assert.False(entry.RetirePublicationAuthorized);
    }

    [Fact]
    public void ReviewReceipt_IsMachineReadableAndMatchesAcceptedRecord()
    {
        string repositoryRoot = VmxDocumentationMigrationClaimHygieneTests.FindRepositoryRoot();
        string path = Path.Combine(
            repositoryRoot,
            "HybridCPU_ISE",
            "docs",
            "ref2",
            "VirtualizationActivationPlan",
            "evidence",
            "2026-08-09-pr-b-attributable-machine-d2-review.json");
        using JsonDocument receipt = JsonDocument.Parse(File.ReadAllText(path));
        JsonElement root = receipt.RootElement;

        Assert.Equal(
            Phase38VirtualizationDecisionAcceptanceV2.SpecCommitSha,
            root.GetProperty("spec_commit_sha").GetString());
        Assert.Equal(
            Phase38VirtualizationDecisionAcceptanceV2.ExpectedSpecDigest,
            root.GetProperty("spec_digest").GetString());
        Assert.Equal(
            Phase38VirtualizationDecisionAcceptanceV2.ExpectedAcceptanceDigest,
            root.GetProperty("acceptance_digest").GetString());
        Assert.Equal(
            Phase38VirtualizationDecisionAcceptanceV2.CodeOwnersBlobSha,
            root.GetProperty("codeowners_blob_sha").GetString());
        Assert.Equal(
            Phase38VirtualizationDecisionAcceptanceV2.RepositoryPrincipal,
            root.GetProperty("accepted_by").GetString());
        Assert.Equal(2, root.GetProperty("reviews").GetArrayLength());
        Assert.False(root.GetProperty("runtime_capability_granted").GetBoolean());
        Assert.False(root.GetProperty("backend_execution_authorized").GetBoolean());
        Assert.False(root.GetProperty("completion_publication_authorized").GetBoolean());
        Assert.False(root.GetProperty("retire_publication_authorized").GetBoolean());
    }

    [Fact]
    public void MaterializationSources_ContainNoRuntimeShortcut()
    {
        string repositoryRoot = VmxDocumentationMigrationClaimHygieneTests.FindRepositoryRoot();
        string coreRoot = Path.Combine(repositoryRoot, "HybridCPU_ISE", "CloseToHSL", "Core");
        string[] sources =
        [
            Path.Combine(coreRoot, "Runtime", "Events", "Hypercalls", "Governance", "Phase38VirtualizationDecisionAcceptanceV2.cs"),
            Path.Combine(coreRoot, "Runtime", "Events", "Hypercalls", "Governance", "Phase38AcceptedVirtualizationDecisionRegistry.g.cs"),
            Path.Combine(coreRoot, "Runtime", "Events", "Hypercalls", "Governance", "HypercallRuntimeOwnerRegistry.cs"),
            Path.Combine(coreRoot, "Runtime", "Capabilities", "Governance", "RuntimeCapabilityIds.Virtualization.cs"),
        ];
        string text = string.Concat(sources.Select(File.ReadAllText));

        Assert.DoesNotContain("InvokeHypercall", text);
        Assert.DoesNotContain("HypercallBackendAdmissionDecision.Allowed", text);
        Assert.DoesNotContain("BackendExecutionAuthorized => true", text);
        Assert.DoesNotContain("BackendExecutionAuthorized: true", text);
        Assert.DoesNotContain("new CapabilityGrant", text);
        Assert.DoesNotContain("new CompletionRecord", text);
        Assert.DoesNotContain("VmxRetireEffect.", text);
        Assert.DoesNotContain("VirtualizationOperationOwnerSnapshot", text);
    }

    private static string CurrentHead()
    {
        string root = VmxDocumentationMigrationClaimHygieneTests.FindRepositoryRoot();
        return Git(root, "rev-parse", "HEAD");
    }

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
        return output.Trim();
    }
}
