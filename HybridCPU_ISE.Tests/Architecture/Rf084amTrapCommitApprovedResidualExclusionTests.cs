namespace HybridCPU_ISE.Tests.Architecture;

public sealed class Rf084amTrapCommitApprovedResidualExclusionTests
{
    [Fact]
    public void PaperDefinesTrapCommitAsStageAwarePageFaultOnly()
    {
        string paper = ReadPaper();

        Assert.Contains("RF-08.4am approved `TrapCommit` C-C residual exclusion", paper, StringComparison.Ordinal);
        Assert.Contains("defines `TrapCommit` for RF-08 as the stage-aware", paper, StringComparison.Ordinal);
        Assert.Contains("page-fault delivery contour only", paper, StringComparison.Ordinal);
        Assert.Contains("deterministic WB, MEM, EX and lane-order", paper, StringComparison.Ordinal);
        Assert.Contains("admissible at RF-08 exit", paper, StringComparison.Ordinal);
    }

    [Fact]
    public void DecisionKeepsTrapEntryEventInPipelineEventFamily()
    {
        string paper = ReadPaper();

        Assert.Contains("intentionally has no `RetireWindowEffectKind.Trap`", paper, StringComparison.Ordinal);
        Assert.Contains("Explicit `TrapMicroOp` `TrapEntryEvent`", paper, StringComparison.Ordinal);
        Assert.Contains("separate `PipelineEventPublication`", paper, StringComparison.Ordinal);
        Assert.Contains("Identity must not be", paper, StringComparison.Ordinal);
        Assert.Contains("reconstructed from stage, lane, order, owner thread, VT, PC, address,", paper, StringComparison.Ordinal);
        Assert.Contains("provide differential stage-aware", paper, StringComparison.Ordinal);
    }

    private static string ReadPaper()
    {
        string root = FindRepositoryRoot();
        return File.ReadAllText(Path.Combine(root, "ResearchPaper", "section", "md base",
            "5_Two_Stage_Admission_and_Bundle_Compositional_SMT_Packing.md"));
    }

    private static string FindRepositoryRoot()
    {
        for (DirectoryInfo? current = new(AppContext.BaseDirectory); current is not null; current = current.Parent)
        {
            if (Directory.Exists(Path.Combine(current.FullName, "HybridCPU_ISE")) &&
                Directory.Exists(Path.Combine(current.FullName, "ResearchPaper")))
            {
                return current.FullName;
            }
        }

        throw new DirectoryNotFoundException("Repository root was not found.");
    }
}
