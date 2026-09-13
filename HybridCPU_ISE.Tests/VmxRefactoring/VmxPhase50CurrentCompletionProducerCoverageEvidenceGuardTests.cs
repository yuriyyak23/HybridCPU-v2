using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace HybridCPU_ISE.Tests.VmxRefactoring;

public sealed class VmxPhase50CurrentCompletionProducerCoverageEvidenceGuardTests
{
    private const string Subject = "d76fe83ab45cbf6fa42c0fe0a6b116f9d413730d";
    private const string SubjectTree = "12670ab7a91a64119d1ac36ade4c64942bf4d681";
    private const string EvidenceFile =
        "2026-08-12-phase50-current-completion-producer-coverage-prerequisite-clean-evidence.json";

    [Fact]
    public void Evidence_IsLaterNonSelfReferentialAndHashesSubjectGitBytes()
    {
        string repositoryRoot = VmxDocumentationMigrationClaimHygieneTests.FindRepositoryRoot();
        using JsonDocument evidence = JsonDocument.Parse(File.ReadAllText(Path.Combine(
            repositoryRoot, "HybridCPU_ISE", "docs", "ref2", "VirtualizationActivationPlan",
            "evidence", EvidenceFile)));
        JsonElement root = evidence.RootElement;

        Assert.True(root.GetProperty("non_self_referential").GetBoolean());
        Assert.False(root.GetProperty("runtime_authority").GetBoolean());
        Assert.Equal(Subject, root.GetProperty("subject").GetProperty("commit_sha").GetString());
        Assert.Equal(SubjectTree, root.GetProperty("subject").GetProperty("tree_sha").GetString());
        Assert.Equal(SubjectTree, GitText(repositoryRoot, "rev-parse", $"{Subject}^{{tree}}"));

        foreach (JsonProperty source in root
            .GetProperty("source_hashes_sha256_clean_subject_git_bytes")
            .EnumerateObject())
        {
            byte[] bytes = GitBytes(repositoryRoot, "show", $"{Subject}:{source.Name}");
            Assert.Equal(source.Value.GetString(),
                Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant());
        }
    }

    [Fact]
    public void MachineStatus_LinksEvidenceAndGrantsNoAuthority()
    {
        string repositoryRoot = VmxDocumentationMigrationClaimHygieneTests.FindRepositoryRoot();
        using JsonDocument status = JsonDocument.Parse(File.ReadAllText(Path.Combine(
            repositoryRoot, "HybridCPU_ISE", "docs", "ref2",
            "VirtualizationActivationPlan", "VirtualizationActivationStatusV1.json")));
        JsonElement root = status.RootElement;
        JsonElement phase50 = root.GetProperty("Phase50CurrentCompletionProducerCoveragePrerequisite");

        Assert.Equal(Subject, phase50.GetProperty("SubjectSha").GetString());
        Assert.Equal(SubjectTree, phase50.GetProperty("SubjectTree").GetString());
        Assert.Equal(EvidenceFile, phase50.GetProperty("EvidenceRecord").GetString());
        Assert.Equal("NotAuthorized", phase50.GetProperty("ProducerImplementation").GetString());
        Assert.Equal("NotOpened", phase50.GetProperty("D2").GetString());
        Assert.Equal("None", phase50.GetProperty("RuntimeAuthority").GetString());
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
