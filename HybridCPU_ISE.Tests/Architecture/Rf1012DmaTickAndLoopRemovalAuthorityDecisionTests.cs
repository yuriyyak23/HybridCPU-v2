namespace HybridCPU_ISE.Tests.Architecture;

public sealed class Rf1012DmaTickAndLoopRemovalAuthorityDecisionTests
{
    [Fact]
    public void PaperSelectsBoundMemoryControllerAsSoleDmaTickOwner()
    {
        string root = FindRepositoryRoot();
        string paper = NormalizeWhitespace(Read(root,
            "ResearchPaper/section/md base/7_Replay_Stable_Placement_Replay_Tokens_and_Execution_Boundaries.md"));

        Assert.Contains("RF-10.12 selects the `MemoryCycleController`", paper, StringComparison.Ordinal);
        Assert.Contains("at most one call to that bound DMA agent", paper, StringComparison.Ordinal);
        Assert.Contains("after the legacy bank/cache service edge and before controller-native request service", paper, StringComparison.Ordinal);
        Assert.Contains("must not rediscover DMA through the mutable global", paper, StringComparison.Ordinal);
    }

    [Fact]
    public void DecisionClassifiesAllFourSynchronousCallerFamilies()
    {
        string root = FindRepositoryRoot();
        string paper = NormalizeWhitespace(Read(root,
            "ResearchPaper/section/md base/7_Replay_Stable_Placement_Replay_Tokens_and_Execution_Boundaries.md"));

        Assert.Contains("Direct `MemorySubsystem.Read` and `MemorySubsystem.Write`", paper, StringComparison.Ordinal);
        Assert.Contains("Legacy queued bank requests", paper, StringComparison.Ordinal);
        Assert.Contains("StreamEngine `BurstRead` and `BurstWrite`", paper, StringComparison.Ordinal);
        Assert.Contains("public eight-channel DMA API", paper, StringComparison.Ordinal);
    }

    [Fact]
    public void DecisionSliceLeavesFourLoopsAndRuntimeUnchanged()
    {
        string root = FindRepositoryRoot();
        string evidence = NormalizeWhitespace(Read(root,
            "Documentation/ArchitectureAuthorityRefactor/Evidence/RF10/rf10.12-dma-tick-and-loop-removal-authority-decision.md"));

        Assert.Contains("No production or timing source changes in RF-10.12", evidence, StringComparison.Ordinal);
        Assert.Contains("RF-10.13 is authorized", evidence, StringComparison.Ordinal);
    }

    [Fact]
    public void LedgerClosesDecisionOnlyAndNamesOperationalSlice()
    {
        string root = FindRepositoryRoot();
        string status = NormalizeWhitespace(Read(root,
            "Documentation/ArchitectureAuthorityRefactor/09_RF10/00_CURRENT_STATUS_AND_LEDGER.md"));

        Assert.Contains("RF-10.12 | closed architecture decision", status, StringComparison.Ordinal);
        Assert.Contains("RF-10.13", status, StringComparison.Ordinal);
        Assert.Contains("RF-10 overall | closed", status, StringComparison.Ordinal);
    }

    private static string Read(string root, string relativePath) =>
        File.ReadAllText(Path.Combine(root, relativePath.Replace('/', Path.DirectorySeparatorChar)));

    private static string NormalizeWhitespace(string value) =>
        string.Join(' ', value.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));

    private static string FindRepositoryRoot()
    {
        for (DirectoryInfo? current = new(AppContext.BaseDirectory); current != null; current = current.Parent)
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
