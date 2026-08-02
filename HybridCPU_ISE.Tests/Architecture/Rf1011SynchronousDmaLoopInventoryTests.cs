namespace HybridCPU_ISE.Tests.Architecture;

public sealed class Rf1011SynchronousDmaLoopInventoryTests
{
    [Fact]
    public void HistoricalEvidenceFreezesExactlyFourCallerLocalLoopsAndWatchdogs()
    {
        string root = FindRepositoryRoot();
        string evidence = NormalizeWhitespace(Read(root,
            "Documentation/ArchitectureAuthorityRefactor/Evidence/RF10/rf10.11-synchronous-dma-loop-inventory-freeze.md"));

        Assert.Contains("exactly four caller-local synchronous DMA completion loops", evidence, StringComparison.Ordinal);
        Assert.Contains("All four use fixed channels 0/1 and a 10,000-iteration safety cap", evidence, StringComparison.Ordinal);
    }

    [Fact]
    public void HistoricalEvidenceFreezesMemorySubsystemCallersAndChannels()
    {
        string root = FindRepositoryRoot();
        string evidence = NormalizeWhitespace(Read(root,
            "Documentation/ArchitectureAuthorityRefactor/Evidence/RF10/rf10.11-synchronous-dma-loop-inventory-freeze.md"));

        Assert.Contains("called by public `Read` for large direct bursts and by `ProcessBankRequest`", evidence, StringComparison.Ordinal);
        Assert.Contains("called by public `Write` for large direct bursts and by `ProcessBankRequest`", evidence, StringComparison.Ordinal);
    }

    [Fact]
    public void HistoricalEvidenceFreezesStreamEnginePublicationOrdering()
    {
        string root = FindRepositoryRoot();
        string evidence = NormalizeWhitespace(Read(root,
            "Documentation/ArchitectureAuthorityRefactor/Evidence/RF10/rf10.11-synchronous-dma-loop-inventory-freeze.md"));

        Assert.Contains("publishes the full input before DMA configuration", evidence, StringComparison.Ordinal);
        Assert.Contains("Busy/start failure and timeout still return the full element count", evidence, StringComparison.Ordinal);
    }


    [Fact]
    public void AuthorityAndLedgerCloseInventoryOnlyAndRequireSeparateDecision()
    {
        string root = FindRepositoryRoot();
        string paper = NormalizeWhitespace(Read(root,
            "ResearchPaper/section/md base/7_Replay_Stable_Placement_Replay_Tokens_and_Execution_Boundaries.md"));
        string evidence = NormalizeWhitespace(Read(root,
            "Documentation/ArchitectureAuthorityRefactor/Evidence/RF10/rf10.11-synchronous-dma-loop-inventory-freeze.md"));
        string status = NormalizeWhitespace(Read(root,
            "Documentation/ArchitectureAuthorityRefactor/09_RF10/00_CURRENT_STATUS_AND_LEDGER.md"));

        Assert.Contains("RF-10.11 freezes exactly four", paper, StringComparison.Ordinal);
        Assert.Contains("RF-10.12", paper, StringComparison.Ordinal);
        Assert.Contains("No production or timing source changed", evidence, StringComparison.Ordinal);
        Assert.Contains("RF-10.11 | closed inventory/freeze", status, StringComparison.Ordinal);
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
