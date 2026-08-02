namespace HybridCPU_ISE.Tests.Architecture;

public sealed class Rf101MemoryCycleAuthorityDecisionTests
{
    [Fact]
    public void PaperAuthority_NamesOneMemoryCycleOwnerAndSharedDomainCallerBoundary()
    {
        string paper = NormalizeWhitespace(ReadPaper());

        Assert.Contains(
            "`MemoryCycleController` is the sole owner of the future `MemoryCycle`",
            paper,
            StringComparison.Ordinal);
        Assert.Contains(
            "supplies exactly one edge to each controller clock domain",
            paper,
            StringComparison.Ordinal);
        Assert.Contains(
            "multiple cores sharing a controller must not each tick that controller",
            paper,
            StringComparison.Ordinal);
        Assert.Contains(
            "none may own or recursively advance `MemoryCycle`",
            paper,
            StringComparison.Ordinal);
    }

    [Fact]
    public void PaperAuthority_FreezesIncludedAndExcludedClockDomains()
    {
        string paper = NormalizeWhitespace(ReadPaper());

        Assert.Contains("controller-side data-cache timing", paper, StringComparison.Ordinal);
        Assert.Contains("timed IOMMU page-table-walk progress", paper, StringComparison.Ordinal);
        Assert.Contains("persistent `DMAController` transfer progress", paper, StringComparison.Ordinal);
        Assert.Contains(
            "memory-service portion of scalar, vector, atomic, DSC, and external accelerator requests",
            paper,
            StringComparison.Ordinal);

        Assert.Contains("CPU-local L1 hit lookup", paper, StringComparison.Ordinal);
        Assert.Contains("NoC/coherence timing", paper, StringComparison.Ordinal);
        Assert.Contains("external accelerator compute/backend clocks", paper, StringComparison.Ordinal);
        Assert.Contains(
            "main-memory mutation epoch is also excluded from elapsed-time ownership",
            paper,
            StringComparison.Ordinal);
    }

    [Fact]
    public void PaperAuthority_FreezesBoundedAdmissionIdentityAndCompletionCausality()
    {
        string paper = NormalizeWhitespace(ReadPaper());

        Assert.Contains("Each ingress port and request class has a declared finite capacity", paper, StringComparison.Ordinal);
        Assert.Contains("`Accepted` reserves capacity and returns one fresh `MemoryRequestId`", paper, StringComparison.Ordinal);
        Assert.Contains("`Backpressured` returns no request identity", paper, StringComparison.Ordinal);
        Assert.Contains("`Rejected` returns no request identity", paper, StringComparison.Ordinal);
        Assert.Contains("publish the previously latched completion set", paper, StringComparison.Ordinal);
        Assert.Contains("advance each included timed agent at most once", paper, StringComparison.Ordinal);
        Assert.Contains("cannot become consumer-visible before `Tick(n+1)`", paper, StringComparison.Ordinal);
        Assert.Contains("published exactly once", paper, StringComparison.Ordinal);
    }

    [Fact]
    public void PaperAuthority_PreservesFaultAndRetirePublicationOwners()
    {
        string paper = NormalizeWhitespace(ReadPaper());

        Assert.Contains(
            "does not select the architectural fault winner",
            paper,
            StringComparison.Ordinal);
        Assert.Contains(
            "Physical publication remains at the existing selected-retire boundary",
            paper,
            StringComparison.Ordinal);
        Assert.Contains(
            "`MemoryRequestId` names one accepted memory transaction",
            paper,
            StringComparison.Ordinal);
        Assert.Contains(
            "It is not a `VliwOperationId`, replay token, FSP MSHR slot, PTW MSHR, DMA channel",
            paper,
            StringComparison.Ordinal);
    }

    [Fact]
    public void FirstCutover_IsExactlyExplicitPacketScalarLoadAndNotAStoreOrFamilyExpansion()
    {
        string paper = NormalizeWhitespace(ReadPaper());
        string evidence = NormalizeWhitespace(Read(
            "Documentation",
            "ArchitectureAuthorityRefactor",
            "Evidence",
            "RF10",
            "rf10.1-memory-cycle-authority-decision.md"));

        Assert.Contains(
            "first authorized migration contour is exactly the explicit-packet scalar load path",
            paper,
            StringComparison.Ordinal);
        Assert.Contains("`CPU_Core.PipelineExecution.Memory`", paper, StringComparison.Ordinal);
        Assert.Contains(
            "does not authorize the same slice to migrate `LoadMicroOp`, any scalar store",
            paper,
            StringComparison.Ordinal);
        Assert.Contains(
            "RF-10.2 is authorized to migrate exactly the explicit-packet scalar-load path",
            evidence,
            StringComparison.Ordinal);
        Assert.Contains("fresh 100- and 10,000-iteration baselines", evidence, StringComparison.Ordinal);
    }

    [Fact]
    public void DecisionSlice_RemainsTheOwnerAcrossClosedRf102ThroughRf106Contours()
    {
        string root = FindRepositoryRoot();
        string production = ReadProductionSources(root);
        string status = Read(
            "Documentation",
            "ArchitectureAuthorityRefactor",
            "09_RF10",
            "00_CURRENT_STATUS_AND_LEDGER.md");

        Assert.Contains("class MemoryCycleController", production, StringComparison.Ordinal);
        Assert.Contains("TryAcceptExplicitPacketScalarLoad", production, StringComparison.Ordinal);
        Assert.Equal(0, Count(production, ".AdvanceCycles(1);"));
        Assert.Equal(1, Count(production, "dma?.ExecuteCycle();"));
        Assert.Equal(0, Count(production, "dmaController.ExecuteCycle();"));

        Assert.Contains("RF-10.1 | closed", status, StringComparison.Ordinal);
        Assert.Contains("RF-10.3 | closed", status, StringComparison.Ordinal);
        Assert.Contains("RF-10.4 | closed", status, StringComparison.Ordinal);
        Assert.Contains("RF-10.5 | closed", status, StringComparison.Ordinal);
        Assert.Contains("RF-10.6 | closed", status, StringComparison.Ordinal);
        Assert.Contains(
            "RF-10 overall | closed",
            status,
            StringComparison.Ordinal);
    }

    private static string ReadPaper()
    {
        return Read(
            "ResearchPaper",
            "section",
            "md base",
            "7_Replay_Stable_Placement_Replay_Tokens_and_Execution_Boundaries.md");
    }

    private static string Read(params string[] components)
    {
        string path = components.Aggregate(FindRepositoryRoot(), Path.Combine);
        return File.ReadAllText(path);
    }

    private static string ReadProductionSources(string root)
    {
        string sourceRoot = Path.Combine(root, "HybridCPU_ISE");
        return string.Join(
            "\n",
            Directory.EnumerateFiles(sourceRoot, "*.cs", SearchOption.AllDirectories)
                .Where(path =>
                    !path.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase) &&
                    !path.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase))
                .OrderBy(path => path, StringComparer.Ordinal)
                .Select(File.ReadAllText));
    }

    private static int Count(string text, string value)
    {
        int count = 0;
        int offset = 0;
        while ((offset = text.IndexOf(value, offset, StringComparison.Ordinal)) >= 0)
        {
            count++;
            offset += value.Length;
        }

        return count;
    }

    private static string NormalizeWhitespace(string text)
    {
        return string.Join(
            " ",
            text.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
    }

    private static string FindRepositoryRoot()
    {
        string? current = AppContext.BaseDirectory;
        while (!string.IsNullOrWhiteSpace(current))
        {
            if (File.Exists(Path.Combine(current, "HybridCPU v2.slnx")))
            {
                return current;
            }

            current = Directory.GetParent(current)?.FullName;
        }

        throw new DirectoryNotFoundException("HybridCPU repository root was not found.");
    }
}
