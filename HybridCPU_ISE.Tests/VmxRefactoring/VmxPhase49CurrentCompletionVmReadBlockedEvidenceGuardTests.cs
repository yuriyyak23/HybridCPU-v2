using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace HybridCPU_ISE.Tests.VmxRefactoring;

public sealed class VmxPhase49CurrentCompletionVmReadBlockedEvidenceGuardTests
{
    private const string Subject = "167468683a29b4e49b40adeb89ef4bf40a9d241c";
    private const string SubjectTree = "3a403994ac8c91e94249ec987d43e654095df8b8";
    private const string EvidenceFile =
        "2026-08-12-phase49-current-completion-vmread-e0-reaudit-clean-evidence.json";

    [Fact]
    public void Evidence_IsLaterNonSelfReferentialAndHashesCleanSubjectBytes()
    {
        string repositoryRoot = VmxDocumentationMigrationClaimHygieneTests.FindRepositoryRoot();
        string evidencePath = Path.Combine(repositoryRoot, "HybridCPU_ISE", "docs", "ref2",
            "VirtualizationActivationPlan", "evidence", EvidenceFile);
        using JsonDocument evidence = JsonDocument.Parse(File.ReadAllText(evidencePath));
        JsonElement root = evidence.RootElement;

        Assert.True(root.GetProperty("non_self_referential").GetBoolean());
        Assert.False(root.GetProperty("runtime_authority").GetBoolean());
        Assert.Equal(Subject, root.GetProperty("subject").GetProperty("commit_sha").GetString());
        Assert.Equal(SubjectTree, root.GetProperty("subject").GetProperty("tree_sha").GetString());
        Assert.Equal(SubjectTree, GitText(repositoryRoot, "rev-parse", $"{Subject}^{{tree}}"));

        foreach (JsonProperty source in root
            .GetProperty("source_hashes_sha256_clean_subject_bytes")
            .EnumerateObject())
        {
            byte[] bytes = GitBytes(repositoryRoot, "show", $"{Subject}:{source.Name}");
            Assert.Equal(source.Value.GetString(),
                Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant());
        }
    }

    [Fact]
    public void MachineStatus_RecordsPhase49ProvenanceWithoutOpeningD2()
    {
        string repositoryRoot = VmxDocumentationMigrationClaimHygieneTests.FindRepositoryRoot();
        using JsonDocument status = JsonDocument.Parse(File.ReadAllText(Path.Combine(
            repositoryRoot, "HybridCPU_ISE", "docs", "ref2",
            "VirtualizationActivationPlan", "VirtualizationActivationStatusV1.json")));
        JsonElement root = status.RootElement;
        JsonElement phase49 = root.GetProperty("Phase49CurrentCompletionVmReadScalarDeliveryE0");

        Assert.Equal(Subject, phase49.GetProperty("SubjectSha").GetString());
        Assert.Equal(SubjectTree, phase49.GetProperty("SubjectTree").GetString());
        Assert.Equal(EvidenceFile, phase49.GetProperty("EvidenceRecord").GetString());
        Assert.Equal("NotMaterialized", phase49.GetProperty("SpecV2").GetString());
        Assert.Equal("NotMaterialized", phase49.GetProperty("AcceptanceRecordV2").GetString());
        Assert.Equal("NotAuthorized", phase49.GetProperty("ProductionImplementation").GetString());
        Assert.Equal("None", phase49.GetProperty("RuntimeAuthority").GetString());
    }

    private static string GitText(string workingDirectory, params string[] arguments) =>
        Encoding.UTF8.GetString(GitBytes(workingDirectory, arguments)).TrimEnd();

    private static byte[] GitBytes(string workingDirectory, params string[] arguments)
    {
        using var process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = "git",
                WorkingDirectory = workingDirectory,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
            },
        };
        foreach (string argument in arguments)
            process.StartInfo.ArgumentList.Add(argument);
        Assert.True(process.Start());
        using var output = new MemoryStream();
        process.StandardOutput.BaseStream.CopyTo(output);
        string error = process.StandardError.ReadToEnd();
        process.WaitForExit();
        Assert.True(process.ExitCode == 0, error);
        return output.ToArray();
    }
}
