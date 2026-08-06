namespace HybridCPU_ISE.Tests.Architecture;

public sealed class Rf100MemoryEntryInventoryFreezeTests
{
    [Fact]
    public void CpuAndMemoryCycleEntryPoints_RemainAtTheInventoriedTopology()
    {
        string root = FindRepositoryRoot();
        string production = ReadProductionSources(root);
        string stageFlow = Read(
            root,
            "HybridCPU_ISE",
            "CloseToHSL",
            "Core",
            "Pipeline",
            "ExecutionFlow",
            "StageFlow",
            "CPU_Core.PipelineExecution.StageFlow.cs");
        string memoryHelpers = Read(
            root,
            "HybridCPU_ISE",
            "CloseToHSL",
            "Memory",
            "Subsystem",
            "MemorySubsystem.Helpers.cs");

        Assert.Equal(0, Count(production, ".AdvanceCycles(1);"));
        Assert.DoesNotContain("GetBoundMemorySubsystem()?.AdvanceCycles(1);", stageFlow, StringComparison.Ordinal);
        Assert.Contains("MemoryCyclePlatformOrchestrator.AdvanceCoreObservedPlatformEdge(", stageFlow, StringComparison.Ordinal);
        Assert.Contains("pipeCtrl.CycleCount++;", stageFlow, StringComparison.Ordinal);

        Assert.Contains("CycleController.AdvanceCompatibilityCycles(cycles);", memoryHelpers, StringComparison.Ordinal);
        Assert.Contains("currentCycle = checked(currentCycle + 1);", memoryHelpers, StringComparison.Ordinal);
        Assert.Contains("L3Cache.AdvanceCycle();", memoryHelpers, StringComparison.Ordinal);
        Assert.Contains("while (TRB.TryRetire", memoryHelpers, StringComparison.Ordinal);
        Assert.Contains("ProcessBankQueues();", memoryHelpers, StringComparison.Ordinal);
        Assert.Contains("dma?.ExecuteCycle();", memoryHelpers, StringComparison.Ordinal);
        Assert.DoesNotContain("AdvancePTW(", memoryHelpers, StringComparison.Ordinal);
    }

    [Fact]
    public void HistoricalInventoryFreezesExactlyFourNestedDmaContours()
    {
        string root = FindRepositoryRoot();
        string evidence = Read(
            root,
            "Documentation",
            "Documentation", "ArchitectureAuthorityRefactor",
            "Evidence",
            "RF10",
            "rf10.0-entry-inventory-freeze.md");

        Assert.Contains("exactly four nested DMA `ExecuteCycle` contours", evidence, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("four 10,000-iteration watchdogs", evidence, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void RequestAdmissionAndStorePublication_RemainAtTheInventoriedBoundaries()
    {
        string root = FindRepositoryRoot();
        string production = ReadProductionSources(root);
        string explicitMemoryStage = Read(
            root,
            "HybridCPU_ISE",
            "CloseToHSL",
            "Core",
            "Pipeline",
            "Stages",
            "Memory",
            "CPU_Core.PipelineExecution.Memory.cs");
        string scalarMicroOps = Read(
            root,
            "HybridCPU_ISE",
            "CloseToHSL",
            "Core",
            "Pipeline",
            "MicroOps",
            "Memory",
            "MicroOp.LoadStore.cs");
        string vectorMicroOps = Read(
            root,
            "HybridCPU_ISE",
            "CloseToHSL",
            "Core",
            "Pipeline",
            "MicroOps",
            "Vector",
            "VectorMicroOps.Memory.cs");
        string retire = Read(
            root,
            "HybridCPU_ISE",
            "CloseToHSL",
            "Core",
            "Pipeline",
            "Retire",
            "Evidence",
            "CPU_Core.PipelineExecution.Retire.cs");

        Assert.Equal(0, Count(production, ".EnqueueRead("));
        Assert.Equal(1, Count(production, ".EnqueueWrite("));
        Assert.Contains("TryAcceptExplicitPacketScalarLoad", explicitMemoryStage, StringComparison.Ordinal);
        Assert.Contains("TryAcceptExplicitPacketScalarStore", explicitMemoryStage, StringComparison.Ordinal);
        Assert.Contains("TryAcceptSingleLaneScalarLoad", scalarMicroOps, StringComparison.Ordinal);
        Assert.Contains("TryAcceptVectorSegmentLoad", vectorMicroOps, StringComparison.Ordinal);
        Assert.DoesNotContain("memSub.EnqueueRead(", vectorMicroOps, StringComparison.Ordinal);
        Assert.DoesNotContain("deferPhysicalWriteUntilRetire: true", explicitMemoryStage, StringComparison.Ordinal);
        Assert.Contains("TryAcceptSingleLaneScalarStore", scalarMicroOps, StringComparison.Ordinal);
        Assert.DoesNotContain("memSub.EnqueueWrite(0 /* CPU Device ID */, Address, Size, buffer)", scalarMicroOps, StringComparison.Ordinal);
        Assert.DoesNotContain("deferPhysicalWriteUntilRetire: true", scalarMicroOps, StringComparison.Ordinal);
        Assert.Contains("ApplyRetiredScalarStoreCommit(", retire, StringComparison.Ordinal);
        Assert.Contains("AtomicMemoryUnit.ApplyRetireEffect(", retire, StringComparison.Ordinal);
    }

    [Fact]
    public void DisconnectedAndIndependentClockContours_RemainVisible()
    {
        string root = FindRepositoryRoot();
        string production = ReadProductionSources(root);
        string podController = Read(
            root,
            "HybridCPU_ISE",
            "CloseToHSL",
            "Processor",
            "Core",
            "PodController.cs");
        string acceleratorRuntime = Read(
            root,
            "HybridCPU_ISE",
            "CloseToHSL",
            "Core",
            "Execution",
            "ExternalAccelerators",
            "ExternalAcceleratorRuntime.cs");

        Assert.Equal(0, Count(production, "IOMMU.AdvancePTW();"));
        Assert.Equal(0, Count(production, ".BeginCycle();"));
        Assert.Contains("L2Cache.AdvanceCycle();", podController, StringComparison.Ordinal);
        Assert.Equal(1, Count(production, "_backend.Tick("));
        Assert.Contains("_backend.Tick(", acceleratorRuntime, StringComparison.Ordinal);
    }

    [Fact]
    public void Rf100Documents_CloseOnlyInventoryAndRequireATickOwnerDecision()
    {
        string root = FindRepositoryRoot();
        string status = Read(
            root,
            "Documentation",
            "Documentation", "ArchitectureAuthorityRefactor",
            "09_RF10",
            "00_CURRENT_STATUS_AND_LEDGER.md");
        string evidence = Read(
            root,
            "Documentation",
            "Documentation", "ArchitectureAuthorityRefactor",
            "Evidence",
            "RF10",
            "rf10.0-entry-inventory-freeze.md");

        Assert.Contains("RF-10.0 | closed", status, StringComparison.Ordinal);
        Assert.Contains("RF-10.3 | closed", status, StringComparison.Ordinal);
        Assert.Contains("RF-10.1 | closed", status, StringComparison.Ordinal);
        Assert.Contains("separate architecture decision", evidence, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("no runtime code was changed", evidence, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("TestAssemblerConsoleApps was not run", evidence, StringComparison.OrdinalIgnoreCase);
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

    private static string Read(string root, params string[] components)
    {
        string path = components.Aggregate(root, Path.Combine);
        return File.ReadAllText(path);
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
