using System.Text.RegularExpressions;

namespace HybridCPU_ISE.Tests.Architecture;

/// <summary>
/// RF-08.4t proves that the exit queue is closed over the current 15-family
/// vocabulary and the direct RegisterWrite constructor inventory.
/// </summary>
public sealed class Rf084tExitReadinessClosedWorldReconciliationTests
{
    private static readonly string[] ExpectedKinds =
    [
        "RegisterWrite",
        "PcWrite",
        "CsrWrite",
        "VectorConfigWrite",
        "DeferredStoreCommit",
        "ScalarMemoryStoreCommit",
        "AtomicCommit",
        "SystemCommit",
        "VmxCommit",
        "TrapCommit",
        "PipelineEventPublication",
        "PredicateStateWrite",
        "VectorStreamDirty",
        "MatrixTileCommit",
        "AcceleratorCommit"
    ];

    private static readonly string[] ExpectedRegisterWriteConstructorFiles =
    [
        "Architecture/Registers/Architectural/CPU_Core.Registers.cs",
        "Architecture/State/Architectural/CPU_Core.StateData.cs",
        "Execution/Dispatch/ExecutionDispatcherV4.CsrAndSmtVt.cs",
        "Execution/Dispatch/ExecutionDispatcherV4.MemoryAndControl.cs",
        "Execution/Dispatch/ExecutionDispatcherV4.Scalar.cs",
        "Execution/StreamEngine/Modes/StreamEngine.Execute1D.cs",
        "Pipeline/MicroOps/Control/MicroOp.Control.cs",
        "Pipeline/MicroOps/Lane6DmaStream/DmaStreamComputeQueryCapsMicroOp.cs",
        "Pipeline/MicroOps/Lane6DmaStream/DmaStreamComputeStatusMicroOp.cs",
        "Pipeline/MicroOps/Lane7Accelerator/SystemDeviceCommandMicroOp.cs",
        "Pipeline/MicroOps/Memory/MicroOp.LoadStore.cs",
        "Pipeline/MicroOps/Replay/ReplayToken.cs",
        "Pipeline/MicroOps/Types/MicroOp.Misc.cs",
        "Pipeline/MicroOps/Vector/MicroOp.Compute.cs",
        "Pipeline/MicroOps/Vector/VectorMicroOps.Compute.cs",
        "Pipeline/Retire/Evidence/CPU_Core.PipelineExecution.Retire.cs",
        "Pipeline/Retire/Evidence/CPU_Core.PipelineExecution.VmxRetire.cs",
        "State/LiveCpuStateAdapter.cs"
    ];

    [Fact]
    public void RetireVisibleEffectVocabularyRemainsExactlyFifteenFamilies()
    {
        string root = FindRepositoryRoot();
        string contracts = Read(root, "HybridCPU_ISE", "CloseToHSL", "Core",
            "Architecture", "Registers", "Retire", "Rf08RetireEffectIdentityContracts.cs");
        string enumBody = ExtractBetween(
            contracts,
            "public enum RetireVisibleEffectKind : byte",
            "/// <summary>",
            searchEndAfterStart: true);
        string[] actual = Regex.Matches(enumBody, @"^\s*([A-Za-z0-9_]+)\s*=\s*\d+,", RegexOptions.Multiline)
            .Select(match => match.Groups[1].Value)
            .ToArray();

        Assert.Equal(ExpectedKinds, actual);
    }

    [Fact]
    public void DirectRegisterWriteConstructorInventoryIsClosed()
    {
        string root = FindRepositoryRoot();
        string coreRoot = Path.Combine(root, "HybridCPU_ISE", "CloseToHSL", "Core");
        string[] actual = Directory.GetFiles(coreRoot, "*.cs", SearchOption.AllDirectories)
            .Where(path => File.ReadAllText(path).Contains(
                "RetireRecord.RegisterWrite(",
                StringComparison.Ordinal))
            .Select(path => Path.GetRelativePath(coreRoot, path).Replace('\\', '/'))
            .OrderBy(path => path, StringComparer.Ordinal)
            .ToArray();

        Assert.Equal(ExpectedRegisterWriteConstructorFiles, actual);
    }

    [Fact]
    public void PaperApprovesOnlyNamedResidualsAndNarrowsOpenRowsAtExit()
    {
        string root = FindRepositoryRoot();
        string paper = Read(root, "ResearchPaper", "section", "md base",
            "5_Two_Stage_Admission_and_Bundle_Compositional_SMT_Packing.md");

        Assert.Contains("RF-08.3o scalar-load contours", paper, StringComparison.Ordinal);
        Assert.Contains("RF-08.4d `PcWrite` contours", paper, StringComparison.Ordinal);
        Assert.Contains("RF-08.4f", paper, StringComparison.Ordinal);
        Assert.Contains("`CsrWrite` contours", paper, StringComparison.Ordinal);
        Assert.Contains("RF-08.4r `MatrixTileCommit` contour", paper, StringComparison.Ordinal);
        Assert.Contains("RF-08.4az and RF-08.4ba resolve the two RF-08.4aw decision rows", paper, StringComparison.Ordinal);
        Assert.Contains(
            "| `AcceleratorCommit` | lane-7 `ACCEL_FENCE` observe",
            paper,
            StringComparison.Ordinal);
        Assert.Contains("RF-08.4aq approves C-C", paper, StringComparison.Ordinal);
    }

    [Fact]
    public void CurrentQueueNamesResidualRegisterWritesAndNoOpenNamedFamily()
    {
        string root = FindRepositoryRoot();
        string status = Read(root, "Documentation", "ArchitectureAuthorityRefactor", "08_RF08_RF09",
            "00_CURRENT_STATUS_AND_READING_ORDER.md");
        string ledger = Read(root, "Documentation", "ArchitectureAuthorityRefactor", "08_RF08_RF09",
            "03_RF08_EXIT_READINESS_LEDGER.md");
        Assert.Contains("| RF-08.4bb | complete; RF-08 exit accepted |", status, StringComparison.Ordinal);
        Assert.Contains("18 source files", ledger, StringComparison.Ordinal);
        Assert.Contains("no named 15-family row", ledger, StringComparison.Ordinal);
        Assert.Contains("RF-08.4ah C-C", ledger, StringComparison.Ordinal);
        Assert.Contains("RF-08.4ai C-C", ledger, StringComparison.Ordinal);
        Assert.Contains("RF-08.4aj C-C", ledger, StringComparison.Ordinal);
        Assert.Contains("RF-08.4ak C-C", ledger, StringComparison.Ordinal);
        Assert.Contains("RF-08.4al C-C", ledger, StringComparison.Ordinal);
        Assert.Contains("RF-08.4am", ledger, StringComparison.Ordinal);
        Assert.Contains("RF-08.4an C-C", ledger, StringComparison.Ordinal);
        Assert.Contains("RF-08.4ao C-C", ledger, StringComparison.Ordinal);
        Assert.Contains("RF-08.4ap C-C", ledger, StringComparison.Ordinal);
        Assert.Contains("RF-08.4aq C-C", ledger, StringComparison.Ordinal);
        Assert.Contains("RF-08.4x coupled C-C", ledger, StringComparison.Ordinal);
        Assert.Contains("retained unreachable constructor sources", ledger, StringComparison.Ordinal);
        Assert.Contains("No open production-reachable `RegisterWrite` row remains", ledger, StringComparison.Ordinal);
        Assert.Contains("RF-08.4az", ledger, StringComparison.Ordinal);
        Assert.Contains("RF-08.4ba", ledger, StringComparison.Ordinal);
        Assert.Contains("RF-09: closed by RF-09.4", ledger, StringComparison.Ordinal);
        Assert.DoesNotContain("slice but has not started", ledger, StringComparison.Ordinal);
    }

    private static string ExtractBetween(
        string source,
        string startMarker,
        string endMarker,
        bool searchEndAfterStart)
    {
        int start = source.IndexOf(startMarker, StringComparison.Ordinal);
        Assert.True(start >= 0);
        int searchFrom = searchEndAfterStart ? start + startMarker.Length : 0;
        int end = source.IndexOf(endMarker, searchFrom, StringComparison.Ordinal);
        Assert.True(end > start);
        return source[start..end];
    }

    private static string Read(string root, params string[] segments) =>
        File.ReadAllText(Path.Combine(new[] { root }.Concat(segments).ToArray()));

    private static string FindRepositoryRoot()
    {
        DirectoryInfo? directory = new(AppContext.BaseDirectory);
        while (directory != null)
        {
            if (Directory.Exists(Path.Combine(directory.FullName, "ResearchPaper")) &&
                Directory.Exists(Path.Combine(directory.FullName, "HybridCPU_ISE")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException("Repository root not found.");
    }
}
