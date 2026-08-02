using System.Text.RegularExpressions;

namespace HybridCPU_ISE.Tests.Architecture;

public sealed class Rf1134SystemEventRefThisHardeningTests
{
    [Fact]
    public void SystemEventMaterializationUsesOneStableIdentityAdapter()
    {
        string retire = RetireSource();
        Assert.Single(Regex.Matches(retire,
            @"MaterializeSystemEventWithStableCoreIdentity\(systemEventMicroOp\)"));
        Assert.Single(Regex.Matches(retire,
            @"systemEventMicroOp\.CreatePipelineEvent\(ref stableCoreIdentity\)"));
        Assert.Contains("CPU_Core stableCoreIdentity = this;", retire, StringComparison.Ordinal);
        Assert.DoesNotContain("systemEventMicroOp.CreatePipelineEvent(ref this)", retire, StringComparison.Ordinal);
    }

    [Fact]
    public void EcallReadAndTypedEventContractRemainUnchanged()
    {
        string root = FindRoot();
        string system = Read(root, "HybridCPU_ISE", "CloseToHSL", "Core", "Pipeline",
            "MicroOps", "System", "MicroOp.System.cs");
        Assert.Contains("SystemEventKind.Ecall => new Pipeline.EcallEvent", system, StringComparison.Ordinal);
        Assert.Contains("EcallCode = ReadEcallCode(ref core, vtId)", system, StringComparison.Ordinal);
        Assert.Contains("TryReadUnifiedArchValue(ref core, vtId, EcallCodeRegister, out ulong value)", system, StringComparison.Ordinal);
        Assert.Contains("refusing hidden zero-code fallback", system, StringComparison.Ordinal);
        Assert.Equal(0, Regex.Matches(system, @"\bcore\s*=(?!=)").Count);
    }

    [Fact]
    public void EventLatchAndBoundedRetirePublicationTopologyIsFrozen()
    {
        string root = FindRoot();
        string execute = Read(root, "HybridCPU_ISE", "CloseToHSL", "Core", "Pipeline",
            "ExecutionFlow", "Materialization", "CPU_Core.PipelineExecution.ExecuteHelpers.cs");
        string materialization = Read(root, "HybridCPU_ISE", "CloseToHSL", "Core", "Pipeline",
            "ExecutionFlow", "Materialization", "CPU_Core.PipelineExecution.Materialization.cs");
        string retire = RetireSource();
        Assert.Contains("pipeEX.GeneratedEvent = MaterializeLaneGeneratedEvent(pipeEX.MicroOp);", execute, StringComparison.Ordinal);
        Assert.Contains("lane.GeneratedEvent = executeLane.GeneratedEvent;", materialization, StringComparison.Ordinal);
        Assert.Contains("lane.GeneratedEvent = memoryLane.GeneratedEvent;", materialization, StringComparison.Ordinal);
        AssertOrder(retire, "retireBatch.CaptureGeneratedSystemEvent(",
            "PrevalidateRetireWindowBatchForPublication(", "ApplyRetireBatchImmediateEffects(");
    }

    [Fact]
    public void ResidualSelfPassingIsOneAtomicTestSupportCall()
    {
        string root = FindRoot();
        string production = ReadSources(Path.Combine(root, "HybridCPU_ISE"));
        Assert.Empty(Regex.Matches(RetireSource(), @"\bref\s+this\s*[,\)]"));
        string testSupport = Read(root, "HybridCPU_ISE", "CloseToHSL", "Core", "Pipeline", "Core", "CPU_Core.TestSupport.cs");
        Assert.Empty(Regex.Matches(testSupport, @"\bref\s+this\s*[,\)]"));
        Assert.Empty(Regex.Matches(production, @"\bref\s+this\s*[,\)]"));
        Assert.Equal(66, Regex.Matches(production, @"partial\s+class\s+CPU_Core").Count);
    }

    [Fact]
    public void EvidenceClosesOnlySystemEventFamily()
    {
        string root = FindRoot();
        string ledger = Read(root, "Documentation", "ArchitectureAuthorityRefactor", "10_RF11", "00_CURRENT_STATUS_AND_LEDGER.md");
        string evidence = Read(root, "Documentation", "ArchitectureAuthorityRefactor", "Evidence", "RF11",
            "rf11.34-system-event-ref-this-hardening.md");
        Assert.Contains("RF-11.34 system-event materialization ref-this seam hardening", ledger, StringComparison.Ordinal);
        Assert.Contains("no production state declaration", evidence, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("--minimal-logs", evidence, StringComparison.Ordinal);
        Assert.Contains("RF-11.35", ledger, StringComparison.Ordinal);
    }

    private static string RetireSource() => Read(FindRoot(), "HybridCPU_ISE", "CloseToHSL", "Core", "Pipeline",
        "Retire", "Evidence", "CPU_Core.PipelineExecution.Retire.cs");
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
    private static string ReadSources(string path) => string.Join('\n', Directory.GetFiles(path, "*.cs", SearchOption.AllDirectories)
        .Where(file => !file.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase))
        .Where(file => !file.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase))
        .Select(File.ReadAllText));
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
