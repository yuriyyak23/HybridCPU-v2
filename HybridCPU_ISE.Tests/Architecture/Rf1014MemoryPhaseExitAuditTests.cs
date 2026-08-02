namespace HybridCPU_ISE.Tests.Architecture;

public sealed class Rf1014MemoryPhaseExitAuditTests
{
    [Fact]
    public void ClosedWorldHasOneBoundDmaProgressCallAndNoNestedLoopHelpers()
    {
        string root = FindRepositoryRoot();
        string production = ReadProduction(root);

        Assert.Equal(1, Count(production, "dma?.ExecuteCycle();"));
        Assert.Equal(0, Count(production, "dma.ExecuteCycle();"));
        Assert.Equal(0, Count(production, "dmaController.ExecuteCycle();"));
        Assert.DoesNotContain("ReadViaDMA", production, StringComparison.Ordinal);
        Assert.DoesNotContain("WriteViaDMA", production, StringComparison.Ordinal);
        Assert.DoesNotContain("BurstReadViaDMA", production, StringComparison.Ordinal);
        Assert.DoesNotContain("BurstWriteViaDMA", production, StringComparison.Ordinal);
        Assert.Equal(0, Count(production, "maxCycles = 10000"));
    }

    [Fact]
    public void ControllerNativeRequestAndPublicationOwnersRemainClosed()
    {
        string root = FindRepositoryRoot();
        string controller = Read(root,
            "HybridCPU_ISE/CloseToHSL/Memory/Timing/MemoryCycleController.cs");
        string retire = Read(root,
            "HybridCPU_ISE/CloseToHSL/Core/Pipeline/Retire/Evidence/CPU_Core.PipelineExecution.Retire.cs");

        Assert.Contains("TryAcceptExplicitPacketScalarLoad", controller, StringComparison.Ordinal);
        Assert.Contains("TryAcceptSingleLaneScalarLoad", controller, StringComparison.Ordinal);
        Assert.Contains("TryAcceptVectorSegmentLoad", controller, StringComparison.Ordinal);
        Assert.Contains("TryAcceptCanonicalVectorTransfer", controller, StringComparison.Ordinal);
        Assert.Contains("TryAcceptExplicitPacketScalarStore", controller, StringComparison.Ordinal);
        Assert.Contains("TryAcceptSingleLaneScalarStore", controller, StringComparison.Ordinal);
        Assert.Contains("ApplyRetiredScalarStoreCommit", retire, StringComparison.Ordinal);
        Assert.Contains("PrevalidateVectorTransferEffect", retire, StringComparison.Ordinal);
        Assert.Contains("ApplyRetiredVectorTransferEffect", retire, StringComparison.Ordinal);
    }

    [Fact]
    public void EveryRf10SliceHasImmutableEvidenceAndExitValidationProvenance()
    {
        string root = FindRepositoryRoot();
        string evidenceDirectory = Path.Combine(root, "Documentation", "ArchitectureAuthorityRefactor", "Evidence", "RF10");

        for (int slice = 0; slice <= 14; slice++)
        {
            Assert.Contains(
                Directory.EnumerateFiles(evidenceDirectory, $"rf10.{slice}-*.md"),
                static path => File.Exists(path));
        }

        string operational = Read(root,
            "Documentation/ArchitectureAuthorityRefactor/Evidence/RF10/rf10.13-single-edge-dma-and-loop-removal.md");
        Assert.Contains("20260727_015231_607_matrix", operational, StringComparison.Ordinal);
        Assert.Contains("20260727_015423_504_matrix", operational, StringComparison.Ordinal);
        Assert.Contains("20260727-051520-IsaParity", operational, StringComparison.Ordinal);
        Assert.Contains("20260727-051919-MemoryCycle", operational, StringComparison.Ordinal);
    }

    [Fact]
    public void PaperApprovesResidualFunctionalAndDisconnectedLimitationsWithoutOverclaim()
    {
        string root = FindRepositoryRoot();
        string paper = NormalizeWhitespace(Read(root,
            "ResearchPaper/section/md base/7_Replay_Stable_Placement_Replay_Tokens_and_Execution_Boundaries.md"));

        Assert.Contains("RF-10.14 closes RF-10", paper, StringComparison.Ordinal);
        Assert.Contains("synchronous direct and StreamEngine adapters remain functional compatibility", paper, StringComparison.Ordinal);
        Assert.Contains("disconnected PTW and L2 hooks", paper, StringComparison.Ordinal);
        Assert.Contains("does not establish a complete memory model, coherence, overlap, timing superiority or PPA improvement", paper, StringComparison.Ordinal);
    }

    [Fact]
    public void CurrentStatusClosesRf10AndLinksSubsequentRf11Closure()
    {
        string root = FindRepositoryRoot();
        string status = NormalizeWhitespace(Read(root,
            "Documentation/ArchitectureAuthorityRefactor/09_RF10/00_CURRENT_STATUS_AND_LEDGER.md"));

        Assert.Contains("RF-10.14 | closed exit audit", status, StringComparison.Ordinal);
        Assert.Contains("RF-10 overall | closed", status, StringComparison.Ordinal);
        Assert.Contains("RF-11 | closed", status, StringComparison.Ordinal);
        Assert.Contains("../10_RF11/00_CURRENT_STATUS_AND_LEDGER.md", status, StringComparison.Ordinal);
        Assert.Contains("RF-08 | closed", status, StringComparison.Ordinal);
        Assert.Contains("RF-09 | closed", status, StringComparison.Ordinal);
    }

    private static string ReadProduction(string root) =>
        string.Join('\n', Directory.EnumerateFiles(
                Path.Combine(root, "HybridCPU_ISE", "CloseToHSL"), "*.cs", SearchOption.AllDirectories)
            .OrderBy(static path => path, StringComparer.Ordinal)
            .Select(File.ReadAllText));

    private static int Count(string source, string marker)
    {
        int count = 0;
        for (int offset = 0; (offset = source.IndexOf(marker, offset, StringComparison.Ordinal)) >= 0; offset += marker.Length)
        {
            count++;
        }
        return count;
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
