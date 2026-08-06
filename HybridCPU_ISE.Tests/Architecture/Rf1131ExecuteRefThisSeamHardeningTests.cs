using System.Text.RegularExpressions;

namespace HybridCPU_ISE.Tests.Architecture;

public sealed class Rf1131ExecuteRefThisSeamHardeningTests
{
    [Fact]
    public void ExecuteStageUsesOneStableIdentityAdapterAndNoLongerPassesThisByReference()
    {
        string root = FindRoot();
        string execute = Read(root, "HybridCPU_ISE", "CloseToHSL", "Core", "Pipeline",
            "ExecutionFlow", "Materialization", "CPU_Core.PipelineExecution.ExecuteHelpers.cs");

        Assert.DoesNotContain(".Execute(ref this)", execute, StringComparison.Ordinal);
        Assert.Equal(3, Regex.Matches(execute,
            @"\bExecuteMicroOpWithStableCoreIdentity\((?:pipeEX\.MicroOp|lane\.MicroOp!?)\)").Count);
        Assert.Equal(1, Regex.Matches(execute,
            @"\bmicroOp\.Execute\(ref stableCoreIdentity\)").Count);
        Assert.Contains("CPU_Core stableCoreIdentity = this;", execute, StringComparison.Ordinal);
    }

    [Fact]
    public void ClosedWorldMicroOpExecuteAbiNeverReassignsTheCoreParameter()
    {
        string root = FindRoot();
        string microOps = Path.Combine(root, "HybridCPU_ISE", "CloseToHSL", "Core", "Pipeline", "MicroOps");
        string[] implementationFiles = SourceFiles(microOps)
            .Where(file => Regex.IsMatch(File.ReadAllText(file),
                @"(?:abstract|override)\s+bool\s+Execute\s*\(\s*ref\s+Processor\.CPU_Core\s+core"))
            .ToArray();
        string implementations = string.Join('\n', implementationFiles.Select(File.ReadAllText));

        Assert.Equal(22, implementationFiles.Length);
        Assert.Equal(51, Regex.Matches(implementations,
            @"(?:abstract|override)\s+bool\s+Execute\s*\(\s*ref\s+Processor\.CPU_Core\s+core").Count);
        Assert.Equal(0, Regex.Matches(implementations, @"\bcore\s*=(?!=)").Count);
    }

    [Fact]
    public void RemainingSelfPassingIsFrozenToRetireAndTestSupportOnly()
    {
        string root = FindRoot();
        string productionRoot = Path.Combine(root, "HybridCPU_ISE");
        var residuals = SourceFiles(productionRoot)
            .Select(file => new
            {
                File = Path.GetRelativePath(root, file).Replace('\\', '/'),
                Count = Regex.Matches(File.ReadAllText(file), @"\bref\s+this\s*[,\)]").Count,
            })
            .Where(item => item.Count != 0)
            .ToArray();

        Assert.Empty(residuals);
    }

    [Fact]
    public void FacadeStorageAndFrozenCycleOrderRemainUnchanged()
    {
        string root = FindRoot();
        string production = ReadSources(Path.Combine(root, "HybridCPU_ISE"));
        Assert.Equal(0, Regex.Matches(production, @"partial\s+struct\s+CPU_Core").Count);
        Assert.Equal(67, Regex.Matches(production, @"partial\s+class\s+CPU_Core").Count);

        string runtime = Read(root, "HybridCPU_ISE", "CloseToHSL", "Core", "State",
            "CPU_Core.RuntimeState.cs");
        Assert.Contains("private readonly CoreRuntimeState _runtime;", runtime, StringComparison.Ordinal);

        string stageFlow = Read(root, "HybridCPU_ISE", "CloseToHSL", "Core", "Pipeline",
            "ExecutionFlow", "StageFlow", "CPU_Core.PipelineExecution.StageFlow.cs");
        AssertOrder(stageFlow, "MemoryCyclePlatformOrchestrator.AdvanceCoreObservedPlatformEdge(",
            "RefreshInFlightExplicitMemoryProgress();", "PipelineStage_WriteBack();",
            "PipelineStage_Memory();", "PipelineStage_Execute();", "PipelineStage_Decode();",
            "PipelineStage_Fetch();");
    }

    [Fact]
    public void EvidenceClosesOnlyTheExecuteSelfPassingFamily()
    {
        string root = FindRoot();
        string ledger = Read(root, "Documentation", "Documentation", "ArchitectureAuthorityRefactor", "10_RF11", "00_CURRENT_STATUS_AND_LEDGER.md");
        string evidence = Read(root, "Documentation", "Documentation", "ArchitectureAuthorityRefactor", "Evidence", "RF11",
            "rf11.31-execute-ref-this-seam-hardening.md");

        Assert.Contains("RF-11.31 execute ref-this seam hardening", ledger, StringComparison.Ordinal);
        Assert.Contains("51", evidence, StringComparison.Ordinal);
        Assert.Contains("22", evidence, StringComparison.Ordinal);
        Assert.Contains("no production state declaration", evidence, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("RF-11.32", ledger, StringComparison.Ordinal);
        Assert.Contains("--minimal-logs", evidence, StringComparison.Ordinal);
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
