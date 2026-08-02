namespace HybridCPU_ISE.Tests.Architecture;

public sealed class Rf129brDonorAssistEpochRawCompatibilityRetentionDecisionTests
{
    [Fact]
    public void RawCompatibilityApisRemainRetainedWithoutNewAuthority()
    {
        string paper = Read("ResearchPaper", "section", "md base", "6_Assist_Coupled_Data_Movement_and_Donor_Semantics.md");
        string runtime = Read("HybridCPU_ISE", "CloseToHSL", "Core", "Pipeline", "Assist", "Runtime", "AssistRuntime.cs");
        string factory = Read("HybridCPU_ISE", "CloseToHSL", "Core", "Pipeline", "MicroOps", "Assist", "AssistMicroOp.cs");
        string ledger = Read("Documentation", "ArchitectureAuthorityRefactor", "11_RF12", "00_ENTRY_STATUS_AND_ROADMAP.md");
        Assert.Contains("raw wrap/default behavior is compatibility state", paper, StringComparison.Ordinal);
        Assert.Contains("no common checked `AssistEpoch` type", paper, StringComparison.Ordinal);
        Assert.Contains("no JSON, binary replay trace or compiler/ISA encoding", paper, StringComparison.Ordinal);
        Assert.Contains("public AssistInterCoreTransport(", runtime, StringComparison.Ordinal);
        Assert.Contains("public AssistDonorSourceDescriptor(", runtime, StringComparison.Ordinal);
        Assert.Contains("donorAssistEpochId: 0", factory, StringComparison.Ordinal);
        Assert.Contains("RF-12.9br | closed architecture decision", ledger, StringComparison.Ordinal);
    }

    private static string Read(params string[] path) => File.ReadAllText(Path.Combine(new[] { FindRepositoryRoot() }.Concat(path).ToArray()));
    private static string FindRepositoryRoot()
    {
        DirectoryInfo? current = new(AppContext.BaseDirectory);
        while (current is not null)
        {
            if (Directory.Exists(Path.Combine(current.FullName, "HybridCPU_ISE"))) return current.FullName;
            current = current.Parent;
        }
        throw new DirectoryNotFoundException();
    }
}
