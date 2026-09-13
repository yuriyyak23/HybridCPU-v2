namespace HybridCPU_ISE.Tests.Architecture;

/// <summary>RF-12.8i authority against conflating execution DomainTag invalid outcomes.</summary>
public sealed class Rf128iExecutionDomainTagInvalidBehaviorDecisionTests
{
    [Fact]
    public void PaperRetainsDistinctOwnerLocalInvalidOutcomes()
    {
        string paper = File.ReadAllText(Path.Combine(Root(), "ResearchPaper", "section", "md base",
            "3_Architectural_Overview_and_Frontend_Contract.md"));
        Assert.Contains("#### Execution domain-tag invalid-behavior ownership boundary", paper, StringComparison.Ordinal);
        Assert.Contains("scheduler legality guard emits", paper, StringComparison.Ordinal);
        Assert.Contains("memory/write-back owners apply", paper, StringComparison.Ordinal);
        Assert.Contains("timed-memory bank owner applies", paper, StringComparison.Ordinal);
        Assert.Contains("global invalid-domain result", paper, StringComparison.Ordinal);
        Assert.Contains("no behavior change, shared error API", paper, StringComparison.Ordinal);
    }

    [Fact]
    public void ExistingGuardAndSquashOwnersRemainSeparate()
    {
        string root = Root();
        string guard = File.ReadAllText(Path.Combine(root, "HybridCPU_ISE", "CloseToHSL", "Core", "Pipeline",
            "Safety", "SafetyVerifier.Guards.cs"));
        string memory = File.ReadAllText(Path.Combine(root, "HybridCPU_ISE", "CloseToHSL", "Core", "Pipeline",
            "Stages", "Memory", "CPU_Core.PipelineExecution.Memory.cs"));
        string bank = File.ReadAllText(Path.Combine(root, "HybridCPU_ISE", "CloseToHSL", "Memory", "Banks",
            "MultiBankMemoryArea.cs"));
        Assert.Contains("CreateGuardReject(RejectKind.DomainMismatch)", guard, StringComparison.Ordinal);
        Assert.Contains("pipeCtrl.DomainSquashCount++", memory, StringComparison.Ordinal);
        Assert.Contains("SilentSquashCount++", bank, StringComparison.Ordinal);
    }

    private static string Root()
    {
        DirectoryInfo? current = new(AppContext.BaseDirectory);
        while (current is not null)
        {
            if (Directory.Exists(Path.Combine(current.FullName, "HybridCPU_ISE")) &&
                Directory.Exists(Path.Combine(current.FullName, "ResearchPaper"))) return current.FullName;
            current = current.Parent;
        }
        throw new DirectoryNotFoundException();
    }
}
