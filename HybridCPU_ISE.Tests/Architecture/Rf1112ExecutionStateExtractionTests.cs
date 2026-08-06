using System.Reflection;
using YAKSys_Hybrid_CPU;

namespace HybridCPU_ISE.Tests.Architecture;

public sealed class Rf1112ExecutionStateExtractionTests
{
    private const BindingFlags Flags = BindingFlags.Instance | BindingFlags.Public |
        BindingFlags.NonPublic | BindingFlags.DeclaredOnly;

    [Fact]
    public void RuntimeContainsExactExecutionLocalContour()
    {
        Type runtime = Required("YAKSys_Hybrid_CPU.Core.CoreRuntimeState");
        Type execution = Required("YAKSys_Hybrid_CPU.Core.ExecutionState");
        Assert.Single(runtime.GetFields(Flags), field => field.FieldType == execution);
        Assert.Equal(new[] { "Control", "Execute", "ExecuteForwarding", "MemoryForwarding", "OperationAttemptIssuer", "WriteBackForwarding" },
            execution.GetFields(Flags).Select(field => field.Name).OrderBy(name => name, StringComparer.Ordinal));
        Assert.Empty(execution.GetMethods(Flags).Where(method =>
            method.Name is "Execute" or "Commit" or "Retire" or "Publish" or "SelectFault"));
    }

    [Fact]
    public void LegacyStorageIsRemovedAndForwardersPreserveValueLatchMutation()
    {
        Type core = typeof(Processor.CPU_Core);
        foreach (string name in new[] { "pipeEX", "pipeCtrl", "forwardEX", "forwardMEM", "forwardWB" })
        {
            Assert.Null(core.GetField(name, Flags));
            Assert.True((core.GetProperty(name, Flags) ?? throw new InvalidOperationException(name)).PropertyType.IsByRef);
        }
        Assert.Null(core.GetField("rf08OperationAttemptIssuer", Flags));
        Assert.Equal("OperationAttemptIssuer", (core.GetProperty("rf08OperationAttemptIssuer", Flags) ?? throw new InvalidOperationException()).PropertyType.Name);
    }

    [Fact]
    public void FacadeCopiesAliasLatchesControlAndAttemptIssuer()
    {
#pragma warning disable CS0618
        var core = new Processor.CPU_Core(0);
#pragma warning restore CS0618
        Processor.CPU_Core copy = core;
        copy.Runtime.Execution.Execute.Valid = true;
        copy.Runtime.Execution.Control.Stalled = true;
        Assert.Same(core.Runtime.Execution, copy.Runtime.Execution);
        Assert.Same(core.Runtime.Execution.OperationAttemptIssuer, copy.Runtime.Execution.OperationAttemptIssuer);
        Assert.True(core.Runtime.Execution.Execute.Valid);
        Assert.True(core.Runtime.Execution.Control.Stalled);
    }

    [Fact]
    public void InPlaceMaterializationAndPhaseOrderRemainExact()
    {
        string root = FindRoot();
        string materialization = Read(root, "HybridCPU_ISE", "CloseToHSL", "Core", "Pipeline", "ExecutionFlow", "Materialization", "CPU_Core.PipelineExecution.Materialization.cs");
        string fsp = Read(root, "HybridCPU_ISE", "CloseToHSL", "Core", "Pipeline", "Scheduling", "Fsp", "CPU_Core.PipelineExecution.Fsp.cs");
        string stageFlow = Read(root, "HybridCPU_ISE", "CloseToHSL", "Core", "Pipeline", "ExecutionFlow", "StageFlow", "CPU_Core.PipelineExecution.StageFlow.cs");
        Assert.Contains("pipeEX.Valid = pipeID.Valid;", materialization, StringComparison.Ordinal);
        Assert.Contains("rf08OperationAttemptIssuer);", fsp, StringComparison.Ordinal);
        AssertOrder(stageFlow, "PipelineStage_WriteBack();", "PipelineStage_Memory();", "PipelineStage_Execute();", "PipelineStage_Decode();", "PipelineStage_Fetch();");
    }

    [Fact]
    public void MemoryBackendRetireAndFaultAuthoritiesRemainOutsideExecutionState()
    {
        Type core = typeof(Processor.CPU_Core);
        foreach (string name in new[] { "pipeMEM", "pipeWB", "_explicitPacketImmediateReadBuffer", "_memorySubsystem" })
            Assert.True((core.GetProperty(name, Flags) ?? throw new InvalidOperationException(name)).PropertyType.IsByRef);
        foreach (string name in new[] { "PhysicalRegisters", "ArchRenameMap", "ArchCommitMap", "PhysRegFreeList" })
            Assert.True((core.GetProperty(name, Flags) ?? throw new InvalidOperationException(name)).PropertyType.IsByRef);
        Type execution = Required("YAKSys_Hybrid_CPU.Core.ExecutionState");
        Assert.DoesNotContain(execution.GetFields(Flags), field =>
            field.Name.Contains("FaultWinner", StringComparison.Ordinal) ||
            field.Name.Contains("Retire", StringComparison.Ordinal) ||
            field.Name.Contains("MemoryRequest", StringComparison.Ordinal));
    }

    [Fact]
    public void LedgerAndEvidenceCloseOnlyExecutionState()
    {
        string root = FindRoot();
        string ledger = Read(root, "Documentation", "Documentation", "ArchitectureAuthorityRefactor", "10_RF11", "00_CURRENT_STATUS_AND_LEDGER.md");
        string evidence = Read(root, "Documentation", "Documentation", "ArchitectureAuthorityRefactor", "Evidence", "RF11", "rf11.12-execution-state-extraction.md");
        Assert.Contains("RF-11.12 | closed ExecutionState", ledger, StringComparison.Ordinal);
        Assert.Contains("RF-11.13 MemoryPipelineState", ledger, StringComparison.Ordinal);
        Assert.Contains("in-place", evidence, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("attempt identity", evidence, StringComparison.OrdinalIgnoreCase);
    }

    private static void AssertOrder(string text, params string[] markers)
    {
        int previous = -1;
        foreach (string marker in markers)
        {
            int current = text.IndexOf(marker, StringComparison.Ordinal);
            Assert.True(current > previous, $"Expected '{marker}' after prior marker.");
            previous = current;
        }
    }

    private static Type Required(string name) => typeof(Processor.CPU_Core).Assembly.GetType(name) ?? throw new InvalidOperationException(name);
    private static string Read(string root, params string[] parts) => File.ReadAllText(parts.Aggregate(root, Path.Combine));
    private static string FindRoot()
    {
        string? current = AppContext.BaseDirectory;
        while (current != null) { if (File.Exists(Path.Combine(current, "HybridCPU v2.slnx"))) return current; current = Directory.GetParent(current)?.FullName; }
        throw new DirectoryNotFoundException();
    }
}
