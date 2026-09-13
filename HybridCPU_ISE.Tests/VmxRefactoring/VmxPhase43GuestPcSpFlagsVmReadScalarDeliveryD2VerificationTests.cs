using System.Diagnostics;
using Xunit;
using YAKSys_Hybrid_CPU.Core;

namespace HybridCPU_ISE.Tests.VmxRefactoring;

public sealed class VmxPhase43GuestPcSpFlagsVmReadScalarDeliveryD2VerificationTests
{
    private const string AcceptanceCommitSha = "cf0a634f94e5d13c67cc4499635b66994abd57d9";
    private const string AcceptanceTreeSha = "c9b1f0b7ad07bc121c238db7be8e95228a8d3fab";

    [Fact]
    public void CleanRepositoryChain_ResolvesImmutableSpecAndLaterAcceptance()
    {
        string root = VmxDocumentationMigrationClaimHygieneTests.FindRepositoryRoot();
        Assert.Equal(Phase43GuestPcSpFlagsVmReadScalarDeliveryDecisionAcceptanceV2.SpecTreeSha,
            Git(root, "rev-parse", $"{Phase43GuestPcSpFlagsVmReadScalarDeliveryDecisionAcceptanceV2.SpecCommitSha}^{{tree}}"));
        Assert.Equal(AcceptanceTreeSha, Git(root, "rev-parse", $"{AcceptanceCommitSha}^{{tree}}"));
        Assert.NotEqual(AcceptanceCommitSha,
            Phase43GuestPcSpFlagsVmReadScalarDeliveryDecisionAcceptanceV2.Record.SpecCommitSha);

        string specAtSubject = Git(root, "show",
            $"{Phase43GuestPcSpFlagsVmReadScalarDeliveryDecisionAcceptanceV2.SpecCommitSha}:HybridCPU_ISE/CloseToHSL/Core/Runtime/Governance/Virtualization/Phase43GuestPcSpFlagsVmReadScalarDeliveryDecisionSpecV2.cs");
        string specAtAcceptance = Git(root, "show",
            $"{AcceptanceCommitSha}:HybridCPU_ISE/CloseToHSL/Core/Runtime/Governance/Virtualization/Phase43GuestPcSpFlagsVmReadScalarDeliveryDecisionSpecV2.cs");
        Assert.Equal(Normalize(specAtSubject), Normalize(specAtAcceptance));

        VmReadScalarDeliveryDecisionValidationResultV2 result =
            Phase43GuestPcSpFlagsVmReadScalarDeliveryDecisionAcceptanceV2.ValidateRepositoryArtifact(
                AcceptanceCommitSha);
        Assert.True(result.IsAcceptedPolicyObject);
        Assert.False(result.RuntimeAuthorityGranted);
        Assert.False(result.ResultReceiptIssued);
        Assert.False(result.RegisterWritebackAuthorized);
        Assert.False(result.RetireCommitAuthorized);
        Assert.False(result.BackendExecutionAuthorized);
        Assert.False(result.UnderlyingVirtualizationMutationAuthorized);
        Assert.False(result.CompletionPublicationAuthorized);
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
