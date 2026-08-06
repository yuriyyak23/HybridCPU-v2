using System.Reflection;
using System.Text.RegularExpressions;
using YAKSys_Hybrid_CPU;

namespace HybridCPU_ISE.Tests.Architecture;

public sealed class Rf1152FinalClosedWorldExitAuditTests
{
    [Fact]
    public void FacadeHasOneStableRuntimeIdentityAndNoCompetingStorage()
    {
        Type facade = typeof(Processor.CPU_Core);
        Assert.True(facade.IsClass);
        Assert.True(facade.IsSealed);
        FieldInfo runtime = Assert.Single(facade.GetFields(Flags | BindingFlags.DeclaredOnly));
        Assert.Equal("_runtime", runtime.Name);
        Assert.True(runtime.IsInitOnly);

        string production = Sources(Path.Combine(Root(), "HybridCPU_ISE"));
        Assert.Equal(67, Regex.Matches(production, @"sealed\s+partial\s+class\s+CPU_Core").Count);
        Assert.Empty(Regex.Matches(production, @"partial\s+struct\s+CPU_Core"));
    }

    [Fact]
    public void RuntimeRootContainsEveryReviewedDomainExactlyOnce()
    {
        Type root = Required("YAKSys_Hybrid_CPU.Core.CoreRuntimeState");
        string[] expected =
        [
            "Telemetry", "Assist", "Scratch", "Cache", "Resources", "VirtualThreadControl",
            "LegacyCompatibility", "Binding", "Frontend", "Decode", "Admission", "Replay",
            "Retire", "Architectural", "Scheduling", "Execution", "MemoryPipeline", "Backend", "Extensions"
        ];
        PropertyInfo[] properties = root.GetProperties(Flags | BindingFlags.DeclaredOnly);
        Assert.Equal(expected.Order(), properties.Select(property => property.Name).Order());
        Assert.All(properties, property => Assert.Null(property.SetMethod));

        string source = Read("HybridCPU_ISE", "CloseToHSL", "Core", "State", "CoreRuntimeState.cs");
        Assert.DoesNotContain(" Commit(", source, StringComparison.Ordinal);
        Assert.DoesNotContain(" Rollback(", source, StringComparison.Ordinal);
        Assert.DoesNotContain(" Execute(", source, StringComparison.Ordinal);
        Assert.DoesNotContain(" Publish(", source, StringComparison.Ordinal);
        Assert.DoesNotContain(" Migrate(", source, StringComparison.Ordinal);
    }

    [Fact]
    public void AuthoritySensitiveDomainsRetainTheirOwners()
    {
        string backend = Read("HybridCPU_ISE", "CloseToHSL", "Core", "State", "BackendState.cs");
        Assert.Contains("PhysicalRegisterFile PhysicalRegisters", backend, StringComparison.Ordinal);
        Assert.Contains("RenameMap RenameMap", backend, StringComparison.Ordinal);
        Assert.Contains("CommitMap CommitMap", backend, StringComparison.Ordinal);
        Assert.Contains("FreeList FreeList", backend, StringComparison.Ordinal);

        string architectural = Read("HybridCPU_ISE", "CloseToHSL", "Core", "State", "ArchitecturalState.cs");
        Assert.DoesNotContain("PhysicalRegisterFile", architectural, StringComparison.Ordinal);
        Assert.DoesNotContain("RenameMap", architectural, StringComparison.Ordinal);
        Assert.DoesNotContain("CommitMap", architectural, StringComparison.Ordinal);
        Assert.DoesNotContain("FreeList", architectural, StringComparison.Ordinal);

        string retire = Read("HybridCPU_ISE", "CloseToHSL", "Core", "Pipeline", "Retire", "Evidence",
            "CPU_Core.PipelineExecution.Retire.cs");
        AssertOrder(retire, "RetireCoordinator.Prevalidate(retireBatch.RetireRecords);",
            "RetireCoordinator.Retire(retireBatch.RetireRecords);");

        string extensions = Read("HybridCPU_ISE", "CloseToHSL", "Core", "State", "ExtensionState.cs");
        Assert.DoesNotContain(" Execute(", extensions, StringComparison.Ordinal);
        Assert.DoesNotContain(" Commit(", extensions, StringComparison.Ordinal);
        Assert.DoesNotContain(" Fallback(", extensions, StringComparison.Ordinal);
    }

    [Fact]
    public void TimedMemoryAndCycleMutationOrderRemainFrozen()
    {
        string memory = Sources(Path.Combine(Root(), "HybridCPU_ISE", "CloseToHSL", "Memory"));
        Assert.Contains("MemoryCycleController", memory, StringComparison.Ordinal);
        string stage = Read("HybridCPU_ISE", "CloseToHSL", "Core", "Pipeline", "ExecutionFlow", "StageFlow",
            "CPU_Core.PipelineExecution.StageFlow.cs");
        AssertOrder(stage,
            "MemoryCyclePlatformOrchestrator.AdvanceCoreObservedPlatformEdge(",
            "RefreshInFlightExplicitMemoryProgress();", "PipelineStage_WriteBack();",
            "PipelineStage_Memory();", "PipelineStage_Execute();", "PipelineStage_Decode();", "PipelineStage_Fetch();");
        AssertOrder(stage,
            "PipelineCycleStallDecision hazardStallDecision = ResolvePipelineHazardStallDecision();",
            "if (hazardStallDecision.ShouldStall)",
            "ApplyPipelineCycleStallDecision(hazardStallDecision);",
            "return;",
            "PipelineStage_WriteBack();");
    }

    [Fact]
    public void CrossStageWritesRemainAtTheReviewedInPlaceTopology()
    {
        string production = Sources(Path.Combine(Root(), "HybridCPU_ISE"));
        Assert.Equal(34, AssignmentCount(production, "IF"));
        Assert.Equal(49, AssignmentCount(production, "ID"));
        Assert.Equal(119, AssignmentCount(production, "EX"));
        Assert.Equal(62, AssignmentCount(production, "MEM"));
        Assert.Equal(60, AssignmentCount(production, "WB"));
    }

    [Fact]
    public void LifecycleCopyReflectionSerializationAndTestSeamsAreClosed()
    {
        string identity = Read("HybridCPU_ISE", "NonRTL", "Processor", "Core", "Processor.CoreIdentity.cs");
        string production = Sources(Path.Combine(Root(), "HybridCPU_ISE"));
        string tests = Sources(Path.Combine(Root(), "HybridCPU_ISE.Tests"));
        Assert.Contains("public static CPU_Core GetCoreRef(int coreId)", identity, StringComparison.Ordinal);
        Assert.Contains("private static ref CPU_Core GetCoreSlotRef(int coreId)", identity, StringComparison.Ordinal);
        Assert.Contains("public static void ReplaceCore(int coreId, CPU_Core replacement)", identity, StringComparison.Ordinal);
        Assert.Empty(Regex.Matches(production, @"(?m)^\s*core\s*=(?!=)"));
        Assert.DoesNotContain("SetValueDirect", production, StringComparison.Ordinal);
        Assert.DoesNotContain("JsonSerializer.Deserialize<Processor.CPU_Core", production, StringComparison.Ordinal);
        Assert.Contains("TEST-ONLY REFLECTION MUTATION ADAPTER", tests, StringComparison.Ordinal);
        Assert.Contains("field.SetValue(core.Runtime.Scratch, value);", tests, StringComparison.Ordinal);
    }

    [Fact]
    public void ExitEvidenceAndLedgerCloseRf11WithoutOpeningAnotherRf11Slice()
    {
        string evidence = Read("Documentation", "Documentation", "ArchitectureAuthorityRefactor", "Evidence", "RF11", "rf11.52-final-closed-world-exit-audit.md");
        string ledger = Read("Documentation", "Documentation", "ArchitectureAuthorityRefactor", "10_RF11", "00_CURRENT_STATUS_AND_LEDGER.md");
        Assert.Contains("RF-11 overall | closed", ledger, StringComparison.Ordinal);
        Assert.Contains("no remaining RF-11 task", ledger, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("closed-world", evidence, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("--minimal-logs", evidence, StringComparison.Ordinal);
    }

    private const BindingFlags Flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
    private static int AssignmentCount(string text, string stage) => Regex.Matches(text,
        $@"\bpipe{stage}\.\w+\s*(?:[+\-*/%&|^]=|=(?!=))").Count;
    private static Type Required(string name) => typeof(Processor.CPU_Core).Assembly.GetType(name) ??
        throw new InvalidOperationException(name);
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
    private static IEnumerable<string> Files(string path) => Directory.GetFiles(path, "*.cs", SearchOption.AllDirectories)
        .Where(file => !file.Contains("\\bin\\") && !file.Contains("\\obj\\"));
    private static string Sources(string path) => string.Join('\n', Files(path).Select(File.ReadAllText));
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
