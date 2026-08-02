namespace HybridCPU_ISE.Tests.Architecture;

public sealed class Rf108CanonicalVectorTransferInventoryTests
{
    [Fact]
    public void CanonicalVloadVstore_HaveOneProductionVectorTransferCarrier()
    {
        string root = FindRepositoryRoot();
        string initialize = Read(root,
            "HybridCPU_ISE/CloseToHSL/Core/Diagnostics/InstructionRegistry.Initialize.Vector.cs");
        string factory = Read(root,
            "HybridCPU_ISE/CloseToHSL/Core/Diagnostics/InstructionRegistry.Helpers.Vector.cs");

        Assert.Equal(2, Count(initialize, "RegisterVectorTransferOp("));
        Assert.Contains("InstructionsEnum.VLOAD, 4", initialize, StringComparison.Ordinal);
        Assert.Contains("InstructionsEnum.VSTORE, 4", initialize, StringComparison.Ordinal);
        Assert.Equal(1, Count(factory, "new VectorTransferMicroOp"));
        Assert.Contains("GetRequiredProjectedVectorInstruction", factory, StringComparison.Ordinal);
    }

    [Fact]
    public void SliceEvidenceFreezesTheHistoricalSynchronousEagerContour()
    {
        string root = FindRepositoryRoot();
        string evidence = Read(root,
            "Documentation/ArchitectureAuthorityRefactor/Evidence/RF10/rf10.8-canonical-vector-transfer-inventory.md");

        Assert.Contains("No controller admission or finite capacity", evidence, StringComparison.Ordinal);
        Assert.Contains("synchronous `BurstRead` then `BurstWrite`", evidence, StringComparison.Ordinal);
        Assert.Contains("`BurstWrite` mutates before `Execute` returns", evidence, StringComparison.Ordinal);
        Assert.Contains("No request identity, completion port", evidence, StringComparison.Ordinal);
        Assert.Contains("No `EmitWriteBackRetireRecords` override", evidence, StringComparison.Ordinal);
    }

    [Fact]
    public void HistoricalEvidenceFreezesNestedDmaTimingAndCurrentSourceRemovesIt()
    {
        string root = FindRepositoryRoot();
        string burst = Read(root,
            "HybridCPU_ISE/CloseToHSL/Core/Execution/StreamEngine/BurstIO/StreamEngine.BurstIO.cs");
        string evidence = Read(root,
            "Documentation/ArchitectureAuthorityRefactor/Evidence/RF10/rf10.8-canonical-vector-transfer-inventory.md");

        Assert.Contains("private static IBurstBackend _backend", burst, StringComparison.Ordinal);
        Assert.Contains("ResolveActiveMemorySubsystem", burst, StringComparison.Ordinal);
        Assert.Contains("large packed transfers use one of two nested DMA loops", evidence, StringComparison.Ordinal);
        Assert.Contains("Large-write helper mutates first", evidence, StringComparison.Ordinal);
        Assert.DoesNotContain("BurstReadViaDMA", burst, StringComparison.Ordinal);
        Assert.DoesNotContain("BurstWriteViaDMA", burst, StringComparison.Ordinal);
    }

    [Fact]
    public void PaperAndLedger_CloseInventoryOnlyAndRequirePublicationDecision()
    {
        string root = FindRepositoryRoot();
        string paper = Read(root,
            "ResearchPaper/section/md base/7_Replay_Stable_Placement_Replay_Tokens_and_Execution_Boundaries.md");
        string status = Read(root,
            "Documentation/ArchitectureAuthorityRefactor/09_RF10/00_CURRENT_STATUS_AND_LEDGER.md");
        string evidence = Read(root,
            "Documentation/ArchitectureAuthorityRefactor/Evidence/RF10/rf10.8-canonical-vector-transfer-inventory.md");

        Assert.Contains("RF-10.8 freezes the canonical production `VectorTransferMicroOp`", paper, StringComparison.Ordinal);
        Assert.Contains("does not authorize request/completion cutover", paper, StringComparison.Ordinal);
        Assert.Contains("RF-10.8 | closed inventory/blocker", status, StringComparison.Ordinal);
        Assert.Contains("RF-10 overall | closed", status, StringComparison.Ordinal);
        Assert.Contains("No production or timing source changed", evidence, StringComparison.Ordinal);
    }

    private static int Count(string source, string marker)
    {
        int count = 0;
        int offset = 0;
        while ((offset = source.IndexOf(marker, offset, StringComparison.Ordinal)) >= 0)
        {
            count++;
            offset += marker.Length;
        }

        return count;
    }

    private static string Between(string source, string startMarker, string endMarker)
    {
        int start = source.IndexOf(startMarker, StringComparison.Ordinal);
        int end = source.IndexOf(endMarker, start + startMarker.Length, StringComparison.Ordinal);
        Assert.True(start >= 0 && end > start, $"Could not isolate {startMarker}.");
        return source[start..end];
    }

    private static string Read(string root, string relativePath) =>
        File.ReadAllText(Path.Combine(root, relativePath.Replace('/', Path.DirectorySeparatorChar)));

    private static string FindRepositoryRoot()
    {
        string? current = Directory.GetCurrentDirectory();
        while (current != null)
        {
            if (Directory.Exists(Path.Combine(current, "Documentation")) &&
                Directory.Exists(Path.Combine(current, "HybridCPU_ISE")) &&
                Directory.Exists(Path.Combine(current, "HybridCPU_ISE.Tests")))
            {
                return current;
            }

            current = Directory.GetParent(current)?.FullName;
        }

        throw new DirectoryNotFoundException("Could not locate repository root.");
    }
}
