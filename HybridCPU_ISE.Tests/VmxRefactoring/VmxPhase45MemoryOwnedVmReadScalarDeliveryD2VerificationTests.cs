using System.Diagnostics;
using System.Text.Json;
using YAKSys_Hybrid_CPU.Core;

namespace HybridCPU_ISE.Tests.VmxRefactoring;

public sealed class VmxPhase45MemoryOwnedVmReadScalarDeliveryD2VerificationTests
{
    private const string AcceptanceCommitSha = "8b3675b5eb4a1a83a7feff95e02c6d7b8e8f1920";
    private const string AcceptanceTreeSha = "aae2962fb7417c2581b9e8739b9166c9367542eb";

    [Fact]
    public void CleanRepositoryChain_ResolvesPrerequisiteImmutableSpecAndLaterAcceptance()
    {
        string root = VmxDocumentationMigrationClaimHygieneTests.FindRepositoryRoot();
        Assert.Equal("840d48603162146de36e51d65bca7d6ebe151d8c",
            Git(root, "rev-parse", "3cb896e37fc7b5775099bf34ca9082e488a73dd3^{tree}"));
        Assert.Equal(Phase45MemoryOwnedVmReadScalarDeliveryDecisionAcceptanceV2.SpecTreeSha,
            Git(root, "rev-parse", $"{Phase45MemoryOwnedVmReadScalarDeliveryDecisionAcceptanceV2.SpecCommitSha}^{{tree}}"));
        Assert.Equal(AcceptanceTreeSha, Git(root, "rev-parse", $"{AcceptanceCommitSha}^{{tree}}"));
        Assert.NotEqual(AcceptanceCommitSha,
            Phase45MemoryOwnedVmReadScalarDeliveryDecisionAcceptanceV2.Record.SpecCommitSha);

        string path = "HybridCPU_ISE/CloseToHSL/Core/Runtime/Governance/Virtualization/Phase45MemoryOwnedVmReadScalarDeliveryDecisionSpecV2.cs";
        Assert.Equal(Normalize(Git(root, "show", $"{Phase45MemoryOwnedVmReadScalarDeliveryDecisionAcceptanceV2.SpecCommitSha}:{path}")),
            Normalize(Git(root, "show", $"{AcceptanceCommitSha}:{path}")));

        VmReadScalarDeliveryDecisionValidationResultV2 result =
            Phase45MemoryOwnedVmReadScalarDeliveryDecisionAcceptanceV2.ValidateRepositoryArtifact(AcceptanceCommitSha);
        Assert.True(result.IsAcceptedPolicyObject);
        Assert.False(result.RuntimeAuthorityGranted);
        Assert.False(result.ResultReceiptIssued);
        Assert.False(result.RegisterWritebackAuthorized);
        Assert.False(result.RetireCommitAuthorized);
        Assert.False(result.BackendExecutionAuthorized);
        Assert.False(result.UnderlyingVirtualizationMutationAuthorized);
        Assert.False(result.CompletionPublicationAuthorized);
    }

    [Fact]
    public void CurrentStatus_RecordsLaterExactProductionWithoutChangingHistoricalAcceptance()
    {
        string root = VmxDocumentationMigrationClaimHygieneTests.FindRepositoryRoot();
        string path = Path.Combine(root, "HybridCPU_ISE", "docs", "ref2",
            "VirtualizationActivationPlan", "VirtualizationActivationStatusV1.json");
        using JsonDocument document = JsonDocument.Parse(File.ReadAllText(path));
        JsonElement phase45 = document.RootElement.GetProperty("VmReadMemoryOwnedScalarDeliveryD2");
        Assert.Equal("ClosedExactProductionCompositionDefaultDisabled", phase45.GetProperty("State").GetString());
        Assert.Equal(AcceptanceCommitSha, phase45.GetProperty("AcceptanceCommitSha").GetString());
        Assert.Equal("ExactGuestCr3EptPointerVpidCr3TargetCountOnly",
            phase45.GetProperty("ProductionImplementation").GetString());
        Assert.Equal("OpaqueSingleUseAttemptD2FieldOwnerGenerationValueDestinationReplayRestoreProfileBound",
            phase45.GetProperty("ReceiptIssuance").GetString());
        Assert.Equal("Disabled", phase45.GetProperty("ActivationDefault").GetString());
        Assert.Equal("ExistingMemoryDomainAuthorityOnly", phase45.GetProperty("RuntimeAuthority").GetString());
        Assert.Equal("NoAutomaticActivationExpansion",
            document.RootElement.GetProperty("NextOpenPool").GetString());
        Assert.Equal("NoneUntilSeparateNeutralCpuTranslationFaultCompletionProducerAuthorization",
            document.RootElement.GetProperty("NextCandidatePool").GetString());

        string evidencePath = Path.Combine(root, "HybridCPU_ISE", "docs", "ref2",
            "VirtualizationActivationPlan", "evidence",
            "2026-08-12-phase45-memory-owned-vmread-scalar-delivery-d2-verification.json");
        using JsonDocument evidence = JsonDocument.Parse(File.ReadAllText(evidencePath));
        Assert.Equal(AcceptanceCommitSha,
            evidence.RootElement.GetProperty("later_acceptance").GetProperty("commit_sha").GetString());
        Assert.False(evidence.RootElement.GetProperty("activation_and_authority")
            .GetProperty("production_composition_authorized").GetBoolean());
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
        foreach (string argument in arguments) startInfo.ArgumentList.Add(argument);
        using Process process = Process.Start(startInfo)!;
        string output = process.StandardOutput.ReadToEnd();
        string error = process.StandardError.ReadToEnd();
        process.WaitForExit();
        Assert.True(process.ExitCode == 0, error);
        return output.TrimEnd();
    }
}
