namespace HybridCPU_ISE.Tests.Architecture;

public sealed class Rf107VectorSegmentStorePublicationInventoryTests
{
    [Fact]
    public void StoreSegment_IsRetainedEagerCompatibilityWithoutRetireByteCarrier()
    {
        string root = FindRepositoryRoot();
        string vectorMemory = Read(root,
            "HybridCPU_ISE/CloseToHSL/Core/Pipeline/MicroOps/Vector/VectorMicroOps.Memory.cs");
        string storeSegment = Between(
            vectorMemory,
            "public class StoreSegmentMicroOp",
            "public class Store2DMicroOp");

        Assert.Contains("MemoryRequestToken", storeSegment, StringComparison.Ordinal);
        Assert.Contains("memSub.EnqueueWrite(", storeSegment, StringComparison.Ordinal);
        Assert.DoesNotContain("deferPhysicalWriteUntilRetire", storeSegment, StringComparison.Ordinal);
        Assert.DoesNotContain("TryAccept", storeSegment, StringComparison.Ordinal);
        Assert.DoesNotContain("EmitWriteBackRetireRecords", storeSegment, StringComparison.Ordinal);

        string productionRoot = Path.Combine(root, "HybridCPU_ISE");
        string[] constructors = Directory
            .EnumerateFiles(productionRoot, "*.cs", SearchOption.AllDirectories)
            .Where(path => !path.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase))
            .Where(path => !path.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase))
            .Where(path => File.ReadAllText(path).Contains("new StoreSegmentMicroOp", StringComparison.Ordinal))
            .ToArray();
        Assert.Empty(constructors);
    }

    [Fact]
    public void MainlineVstore_IsSeparateVectorTransferAndStillPublishesInExecute()
    {
        string root = FindRepositoryRoot();
        string factory = Read(root,
            "HybridCPU_ISE/CloseToHSL/Core/Diagnostics/InstructionRegistry.Helpers.Vector.cs");
        string vectorData = Read(root,
            "HybridCPU_ISE/CloseToHSL/Core/Pipeline/MicroOps/Vector/VectorMicroOps.Data.cs");
        string transfer = Between(
            vectorData,
            "public sealed class VectorTransferMicroOp",
            "public class VConfigMicroOp");

        Assert.Contains("VectorTransferMicroOp vectorTransferMicroOp = new VectorTransferMicroOp", factory, StringComparison.Ordinal);
        Assert.Contains("IsaOpcodeValues.VSTORE", transfer, StringComparison.Ordinal);
        Assert.Contains("BurstIO.BurstRead(", transfer, StringComparison.Ordinal);
        Assert.Contains("BurstIO.BurstWrite(", transfer, StringComparison.Ordinal);
        Assert.DoesNotContain("EmitWriteBackRetireRecords", transfer, StringComparison.Ordinal);
    }

    [Fact]
    public void ExistingRetireStoreCarrier_IsScalarOnlyAndCannotPublishVectorBytes()
    {
        string root = FindRepositoryRoot();
        string stageTypes = Read(root,
            "HybridCPU_ISE/CloseToHSL/Core/Pipeline/ExecutionFlow/StageFlow/CPU_Core.PipelineExecution.Types.cs");
        string retire = Read(root,
            "HybridCPU_ISE/CloseToHSL/Core/Pipeline/Retire/Evidence/CPU_Core.PipelineExecution.Retire.cs");

        Assert.Contains("DeferredStoreCommit", stageTypes, StringComparison.Ordinal);
        Assert.Contains("AppendDeferredStoreLane", stageTypes, StringComparison.Ordinal);
        Assert.DoesNotContain("VectorStoreCommit", stageTypes, StringComparison.Ordinal);
        Assert.DoesNotContain("VectorStoreCommit", retire, StringComparison.Ordinal);
        Assert.Contains("ApplyRetiredScalarStoreCommit", retire, StringComparison.Ordinal);
    }

    [Fact]
    public void PaperAndLedger_CloseInventoryOnlyAndAuthorizeNoRuntimeCutover()
    {
        string root = FindRepositoryRoot();
        string paper = Read(root,
            "ResearchPaper/section/md base/7_Replay_Stable_Placement_Replay_Tokens_and_Execution_Boundaries.md");
        string status = Read(root,
            "Documentation/ArchitectureAuthorityRefactor/09_RF10/00_CURRENT_STATUS_AND_LEDGER.md");
        string evidence = Read(root,
            "Documentation/ArchitectureAuthorityRefactor/Evidence/RF10/rf10.7-vector-segment-store-publication-inventory.md");

        Assert.Contains("RF-10.7 freezes exactly the retained `StoreSegmentMicroOp`", paper, StringComparison.Ordinal);
        Assert.Contains("does not authorize a runtime cutover", paper, StringComparison.Ordinal);
        Assert.Contains("RF-10.7 | closed inventory/blocker", status, StringComparison.Ordinal);
        Assert.Contains("RF-10 overall | closed", status, StringComparison.Ordinal);
        Assert.Contains("No production or timing source changed", evidence, StringComparison.Ordinal);
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
