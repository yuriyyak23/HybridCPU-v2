using System.Reflection;
using System.Text.RegularExpressions;
using YAKSys_Hybrid_CPU;

namespace HybridCPU_ISE.Tests.Architecture;

public sealed class Rf1150LiveAccessorRefReturnCutoverTests
{
    [Fact]
    public void PublicLiveLookupReturnsExistingReferenceIdentityByValue()
    {
        MethodInfo method = typeof(Processor).GetMethods(BindingFlags.Public | BindingFlags.Static)
            .Single(candidate => candidate.Name == nameof(Processor.GetCoreRef));

        Assert.Equal(typeof(Processor.CPU_Core), method.ReturnType);
        Assert.False(method.ReturnType.IsByRef);
    }

    [Fact]
    public void OnlyPrivateLifecycleHelperCanRebindACoreTableSlot()
    {
        string identity = Read("HybridCPU_ISE", "NonRTL", "Processor", "Core", "Processor.CoreIdentity.cs");
        Assert.Contains("public static CPU_Core GetCoreRef(int coreId)", identity, StringComparison.Ordinal);
        Assert.Contains("private static ref CPU_Core GetCoreSlotRef(int coreId)", identity, StringComparison.Ordinal);
        Assert.DoesNotContain("public static ref CPU_Core GetCoreRef", identity, StringComparison.Ordinal);
        Assert.Contains("return liveCore;", identity, StringComparison.Ordinal);
        Assert.DoesNotContain("return ref liveCore;", identity, StringComparison.Ordinal);
        Assert.Contains("public static void ReplaceCore", identity, StringComparison.Ordinal);
        Assert.Contains("liveCore = replacement;", identity, StringComparison.Ordinal);
    }

    [Fact]
    public void ProductionAndDiagnosticCallersCannotAcquireARebindableTableReference()
    {
        string production = Sources(Path.Combine(Root(), "HybridCPU_ISE"));
        string diagnostics = Sources(Path.Combine(Root(), "TestAssemblerConsoleApps"));
        string combined = production + "\n" + diagnostics;

        Assert.Empty(Regex.Matches(combined, @"=\s*ref\s+(?:Processor\.)?GetCoreRef\s*\("));
        Assert.Empty(Regex.Matches(combined, @"(?:public|internal|protected)\s+(?:static\s+)?ref\s+(?:Processor\.)?CPU_Core\s+GetCoreRef\s*\("));
        Assert.Equal(1, Regex.Matches(production, @"private\s+static\s+ref\s+CPU_Core\s+GetCoreSlotRef\s*\(").Count);
    }

    [Fact]
    public void SnapshotAndReplacementBoundariesRemainDistinct()
    {
        string identity = Read("HybridCPU_ISE", "NonRTL", "Processor", "Core", "Processor.CoreIdentity.cs");
        Assert.Contains("CpuCoreDiagnosticSnapshot GetCoreSnapshot(int coreId)", identity, StringComparison.Ordinal);
        Assert.Contains("CpuCoreDiagnosticSnapshot.Capture(GetCoreRef(coreId))", identity, StringComparison.Ordinal);
        Assert.Contains("ArgumentNullException.ThrowIfNull(replacement);", identity, StringComparison.Ordinal);
        Assert.Contains("_ = replacement.Runtime;", identity, StringComparison.Ordinal);
    }

    [Fact]
    public void FrozenCycleAndRetireAuthorityRemainUnchanged()
    {
        string stage = Read("HybridCPU_ISE", "CloseToHSL", "Core", "Pipeline", "ExecutionFlow", "StageFlow",
            "CPU_Core.PipelineExecution.StageFlow.cs");
        AssertOrder(stage,
            "MemoryCyclePlatformOrchestrator.AdvanceCoreObservedPlatformEdge(",
            "RefreshInFlightExplicitMemoryProgress();", "PipelineStage_WriteBack();",
            "PipelineStage_Memory();", "PipelineStage_Execute();", "PipelineStage_Decode();", "PipelineStage_Fetch();");

        string retire = Read("HybridCPU_ISE", "CloseToHSL", "Core", "Pipeline", "Retire", "Evidence",
            "CPU_Core.PipelineExecution.Retire.cs");
        Assert.Contains("RetireCoordinator.Prevalidate(retireBatch.RetireRecords);", retire, StringComparison.Ordinal);
        Assert.Contains("RetireCoordinator.Retire(retireBatch.RetireRecords);", retire, StringComparison.Ordinal);
    }

    [Fact]
    public void EvidenceNamesOnlyByRefAbiReauditNext()
    {
        string evidence = Read("Documentation", "ArchitectureAuthorityRefactor", "Evidence", "RF11", "rf11.50-live-accessor-ref-return-cutover.md");
        string ledger = Read("Documentation", "ArchitectureAuthorityRefactor", "10_RF11", "00_CURRENT_STATUS_AND_LEDGER.md");
        Assert.Contains("six production callers", evidence, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("--minimal-logs", evidence, StringComparison.Ordinal);
        Assert.Contains("RF-11.51", ledger, StringComparison.Ordinal);
    }

    private static void AssertOrder(string source, params string[] markers)
    {
        int previous = -1;
        foreach (string marker in markers)
        {
            int current = source.IndexOf(marker, previous + 1, StringComparison.Ordinal);
            Assert.True(current > previous, marker);
            previous = current;
        }
    }

    private static string Sources(string path) => string.Join('\n', Directory.GetFiles(path, "*.cs", SearchOption.AllDirectories)
        .Where(file => !file.Contains("\\bin\\") && !file.Contains("\\obj\\")).Select(File.ReadAllText));
    private static string Read(params string[] parts) => File.ReadAllText(parts.Aggregate(Root(), Path.Combine));
    private static string Root()
    {
        string? current = AppContext.BaseDirectory;
        while (current is not null)
        {
            if (File.Exists(Path.Combine(current, "HybridCPU v2.slnx"))) return current;
            current = Directory.GetParent(current)?.FullName;
        }
        throw new DirectoryNotFoundException();
    }
}
