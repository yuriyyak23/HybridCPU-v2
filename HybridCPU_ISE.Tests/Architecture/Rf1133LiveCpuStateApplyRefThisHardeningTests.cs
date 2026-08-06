using System.Text.RegularExpressions;

namespace HybridCPU_ISE.Tests.Architecture;

public sealed class Rf1133LiveCpuStateApplyRefThisHardeningTests
{
    [Fact]
    public void TwoApplyToCallersUseOneStableIdentityAdapter()
    {
        string retire = Read(FindRoot(), "HybridCPU_ISE", "CloseToHSL", "Core", "Pipeline",
            "Retire", "Evidence", "CPU_Core.PipelineExecution.Retire.cs");
        Assert.Equal(2, Regex.Matches(retire,
            @"ApplyLiveCpuStateWithStableCoreIdentity\(liveState\)").Count);
        Assert.Single(Regex.Matches(retire, @"liveState\.ApplyTo\(ref stableCoreIdentity\)"));
        Assert.Contains("CPU_Core stableCoreIdentity = this;", retire, StringComparison.Ordinal);
        Assert.DoesNotContain("liveState.ApplyTo(ref this)", retire, StringComparison.Ordinal);
    }

    [Fact]
    public void ApplyToWriteAndPublicationOrderIsUnchangedAndCannotReplaceCore()
    {
        string adapter = Read(FindRoot(), "HybridCPU_ISE", "CloseToHSL", "Core", "State",
            "LiveCpuStateAdapter.cs");
        AssertOrder(adapter, "core.PublishGuardedVirtualThreadPipelineState(_vtId, PipelineState);",
            "core.VectorConfig = _vectorConfig;", "core.ExceptionStatus = _exceptionStatus;",
            "core.SetPredicateRegister(i, _predicateRegisters[i]);", "PublishExplicitPcWrite(ref core);");
        Assert.Equal(0, Regex.Matches(adapter, @"\bcore\s*=(?!=)").Count);
        Assert.Contains("_retireCoordinator.Retire(", adapter, StringComparison.Ordinal);
    }

    [Fact]
    public void ResidualSelfPassingIsOneAtomicTestSupportCall()
    {
        string root = FindRoot();
        string retire = Read(root, "HybridCPU_ISE", "CloseToHSL", "Core", "Pipeline",
            "Retire", "Evidence", "CPU_Core.PipelineExecution.Retire.cs");
        string testSupport = Read(root, "HybridCPU_ISE", "CloseToHSL", "Core", "Pipeline",
            "Core", "CPU_Core.TestSupport.cs");
        string production = ReadSources(Path.Combine(root, "HybridCPU_ISE"));
        Assert.Empty(Regex.Matches(retire, @"\bref\s+this\s*[,\)]"));
        Assert.Empty(Regex.Matches(testSupport, @"\bref\s+this\s*[,\)]"));
        Assert.Empty(Regex.Matches(production, @"\bref\s+this\s*[,\)]"));
    }

    [Fact]
    public void FacadeAndFrozenCycleOrderRemainUnchanged()
    {
        string root = FindRoot();
        string production = ReadSources(Path.Combine(root, "HybridCPU_ISE"));
        Assert.Equal(67, Regex.Matches(production, @"partial\s+class\s+CPU_Core").Count);
        string stageFlow = Read(root, "HybridCPU_ISE", "CloseToHSL", "Core", "Pipeline",
            "ExecutionFlow", "StageFlow", "CPU_Core.PipelineExecution.StageFlow.cs");
        AssertOrder(stageFlow, "MemoryCyclePlatformOrchestrator.AdvanceCoreObservedPlatformEdge(",
            "RefreshInFlightExplicitMemoryProgress();", "PipelineStage_WriteBack();",
            "PipelineStage_Memory();", "PipelineStage_Execute();", "PipelineStage_Decode();",
            "PipelineStage_Fetch();");
    }

    [Fact]
    public void EvidenceClosesOnlyApplyToFamily()
    {
        string root = FindRoot();
        string ledger = Read(root, "Documentation", "Documentation", "ArchitectureAuthorityRefactor", "10_RF11", "00_CURRENT_STATUS_AND_LEDGER.md");
        string evidence = Read(root, "Documentation", "Documentation", "ArchitectureAuthorityRefactor", "Evidence", "RF11",
            "rf11.33-live-cpu-state-apply-ref-this-hardening.md");
        Assert.Contains("RF-11.33 LiveCpuStateAdapter.ApplyTo ref-this seam hardening", ledger, StringComparison.Ordinal);
        Assert.Contains("no production state declaration", evidence, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("--minimal-logs", evidence, StringComparison.Ordinal);
        Assert.Contains("RF-11.34", ledger, StringComparison.Ordinal);
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
