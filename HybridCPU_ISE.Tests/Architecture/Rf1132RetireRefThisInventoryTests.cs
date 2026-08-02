using System.Text.RegularExpressions;

namespace HybridCPU_ISE.Tests.Architecture;

public sealed class Rf1132RetireRefThisInventoryTests
{
    [Fact]
    public void PostInventoryRetireSelfPassingCallsRemainFrozenByCalleeFamily()
    {
        string retire = Read(FindRoot(), "HybridCPU_ISE", "CloseToHSL", "Core", "Pipeline",
            "Retire", "Evidence", "CPU_Core.PipelineExecution.Retire.cs");

        Assert.Empty(Regex.Matches(retire, @"\bref\s+this\s*[,\)]"));
        Assert.Empty(Regex.Matches(retire, @"retireBatch\.EmitMicroOpRetireRecords\(\s*ref this,"));
        Assert.Equal(2, Regex.Matches(retire,
            @"EmitBatchMicroOpRetireRecordsWithStableCoreIdentity\(\s*ref retireBatch,").Count);
        Assert.Empty(Regex.Matches(retire, @"liveState\.ApplyTo\(ref this\)"));
        Assert.Empty(Regex.Matches(retire, @"systemEventMicroOp\.CreatePipelineEvent\(ref this\)"));
        Assert.Empty(Regex.Matches(retire, @"csrMicroOp\.CreateRetireEffect\(ref this\)"));
        Assert.Empty(Regex.Matches(retire, @"Core\.CSRMicroOp\.WriteCsr\(\s*ref this,"));
        Assert.Empty(Regex.Matches(retire, @"lane\.MicroOp\.EmitWriteBackRetireRecords\(\s*ref this,"));
    }

    [Fact]
    public void RetireCalleesDoNotReplaceTheSuppliedCoreFacade()
    {
        string root = FindRoot();
        string microOps = Path.Combine(root, "HybridCPU_ISE", "CloseToHSL", "Core", "Pipeline", "MicroOps");
        string[] emitFiles = SourceFiles(microOps)
            .Where(file => File.ReadAllText(file).Contains("EmitWriteBackRetireRecords(", StringComparison.Ordinal))
            .ToArray();
        string emits = string.Join('\n', emitFiles.Select(File.ReadAllText));
        Assert.Equal(18, emitFiles.Length);
        Assert.Equal(24, Regex.Matches(emits,
            @"(?:virtual|override)\s+void\s+EmitWriteBackRetireRecords\s*\(").Count);
        Assert.Equal(0, Regex.Matches(emits, @"\bcore\s*=(?!=)").Count);

        string directCallees = string.Join('\n', new[]
        {
            Read(root, "HybridCPU_ISE", "CloseToHSL", "Core", "Pipeline", "ExecutionFlow", "StageFlow", "CPU_Core.PipelineExecution.Types.cs"),
            Read(root, "HybridCPU_ISE", "CloseToHSL", "Core", "State", "LiveCpuStateAdapter.cs"),
            Read(root, "HybridCPU_ISE", "CloseToHSL", "Core", "Pipeline", "MicroOps", "System", "MicroOp.System.cs"),
            Read(root, "HybridCPU_ISE", "CloseToHSL", "Core", "Pipeline", "MicroOps", "Control", "MicroOp.Control.cs"),
        });
        Assert.Equal(0, Regex.Matches(directCallees, @"\bcore\s*=(?!=)").Count);
    }

    [Fact]
    public void RetirePublicationAndCycleOrderRemainFrozen()
    {
        string root = FindRoot();
        string retire = Read(root, "HybridCPU_ISE", "CloseToHSL", "Core", "Pipeline",
            "Retire", "Evidence", "CPU_Core.PipelineExecution.Retire.cs");
        Assert.Contains("RetireCoordinator.Prevalidate(retireBatch.RetireRecords);", retire, StringComparison.Ordinal);
        Assert.Contains("RetireCoordinator.Retire(retireBatch.RetireRecords);", retire, StringComparison.Ordinal);

        string stageFlow = Read(root, "HybridCPU_ISE", "CloseToHSL", "Core", "Pipeline",
            "ExecutionFlow", "StageFlow", "CPU_Core.PipelineExecution.StageFlow.cs");
        AssertOrder(stageFlow, "MemoryCyclePlatformOrchestrator.AdvanceCoreObservedPlatformEdge(",
            "RefreshInFlightExplicitMemoryProgress();", "PipelineStage_WriteBack();",
            "PipelineStage_Memory();", "PipelineStage_Execute();", "PipelineStage_Decode();",
            "PipelineStage_Fetch();");
    }

    [Fact]
    public void InventoryDoesNotChangeFacadeOrExecutionAdapterTopology()
    {
        string root = FindRoot();
        string production = ReadSources(Path.Combine(root, "HybridCPU_ISE"));
        Assert.Equal(66, Regex.Matches(production, @"partial\s+class\s+CPU_Core").Count);
        Assert.Empty(Regex.Matches(production, @"\bref\s+this\s*[,\)]"));
        Assert.Equal(3, Regex.Matches(production,
            @"\bExecuteMicroOpWithStableCoreIdentity\((?:pipeEX\.MicroOp|lane\.MicroOp!?)\)").Count);
    }

    [Fact]
    public void LedgerClosesInventoryWithoutProductionMutation()
    {
        string root = FindRoot();
        string ledger = Read(root, "Documentation", "ArchitectureAuthorityRefactor", "10_RF11", "00_CURRENT_STATUS_AND_LEDGER.md");
        string evidence = Read(root, "Documentation", "ArchitectureAuthorityRefactor", "Evidence", "RF11",
            "rf11.32-retire-ref-this-inventory-freeze.md");
        Assert.Contains("RF-11.32 retire ref-this inventory/freeze", ledger, StringComparison.Ordinal);
        Assert.Contains("no production state declaration", evidence, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("TestAssemblerConsoleApps was not run", evidence, StringComparison.Ordinal);
        Assert.Contains("RF-11.33", ledger, StringComparison.Ordinal);
    }

    private static void AssertOrder(string text, params string[] markers)
    {
        int prior = -1;
        foreach (string marker in markers)
        {
            int current = text.IndexOf(marker, prior + 1, StringComparison.Ordinal);
            Assert.True(current > prior, $"Expected '{marker}' after prior marker.");
            prior = current;
        }
    }
    private static string ReadSources(string path) => string.Join('\n', SourceFiles(path).Select(File.ReadAllText));
    private static IEnumerable<string> SourceFiles(string path) => Directory.GetFiles(path, "*.cs", SearchOption.AllDirectories)
        .Where(file => !file.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase))
        .Where(file => !file.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase));
    private static string Read(string root, params string[] parts) => File.ReadAllText(parts.Aggregate(root, Path.Combine));
    private static string FindRoot()
    {
        string? current = AppContext.BaseDirectory;
        while (current != null)
        {
            if (File.Exists(Path.Combine(current, "HybridCPU v2.slnx"))) return current;
            current = Directory.GetParent(current)?.FullName;
        }
        throw new DirectoryNotFoundException();
    }
}
