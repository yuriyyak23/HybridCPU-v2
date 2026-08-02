using System.Reflection;
using YAKSys_Hybrid_CPU;

namespace HybridCPU_ISE.Tests.Architecture;

public sealed class Rf1113MemoryPipelineStateExtractionTests
{
    private const BindingFlags Flags = BindingFlags.Instance | BindingFlags.Public |
        BindingFlags.NonPublic | BindingFlags.DeclaredOnly;

    [Fact]
    public void RuntimeContainsExactMemWbAndBindingContour()
    {
        Type runtime = Required("YAKSys_Hybrid_CPU.Core.CoreRuntimeState");
        Type memory = Required("YAKSys_Hybrid_CPU.Core.MemoryPipelineState");
        Assert.Single(runtime.GetFields(Flags), field => field.FieldType == memory);
        Assert.Equal(new[] { "AtomicMemoryUnit", "ExplicitPacketImmediateReadBuffer", "MainMemory", "Memory", "MemorySubsystem", "MemorySubsystemCaptured", "WriteBack" },
            memory.GetFields(Flags).Select(field => field.Name).OrderBy(name => name, StringComparer.Ordinal));
        Assert.Empty(memory.GetMethods(Flags).Where(method => method.Name is "Advance" or "Commit" or "Publish" or "Retire"));
    }

    [Fact]
    public void LegacyStorageIsRemovedAndAllSevenNamesForwardByRef()
    {
        Type core = typeof(Processor.CPU_Core);
        foreach (string name in new[] { "pipeMEM", "pipeWB", "_explicitPacketImmediateReadBuffer", "_mainMemory", "_memorySubsystem", "_memorySubsystemCaptured", "_atomicMemoryUnit" })
        {
            Assert.Null(core.GetField(name, Flags));
            Assert.True((core.GetProperty(name, Flags) ?? throw new InvalidOperationException(name)).PropertyType.IsByRef);
        }
    }

    [Fact]
    public void FacadeCopiesAliasStagesBuffersAndExactBindings()
    {
#pragma warning disable CS0618
        var core = new Processor.CPU_Core(0);
#pragma warning restore CS0618
        Processor.CPU_Core copy = core;
        copy.Runtime.MemoryPipeline.Memory.Valid = true;
        copy.Runtime.MemoryPipeline.WriteBack.Valid = true;
        copy.Runtime.MemoryPipeline.ExplicitPacketImmediateReadBuffer = new byte[8];
        Assert.Same(core.Runtime.MemoryPipeline, copy.Runtime.MemoryPipeline);
        Assert.Same(core.Runtime.MemoryPipeline.MainMemory, copy.Runtime.MemoryPipeline.MainMemory);
        Assert.Same(core.Runtime.MemoryPipeline.AtomicMemoryUnit, copy.Runtime.MemoryPipeline.AtomicMemoryUnit);
        Assert.True(core.Runtime.MemoryPipeline.Memory.Valid);
        Assert.True(core.Runtime.MemoryPipeline.WriteBack.Valid);
        Assert.Equal(8, core.Runtime.MemoryPipeline.ExplicitPacketImmediateReadBuffer!.Length);
    }

    [Fact]
    public void CrossStageOrderAndRf10ControllerAuthorityRemainExact()
    {
        string root = FindRoot();
        string materialization = Read(root, "HybridCPU_ISE", "CloseToHSL", "Core", "Pipeline", "ExecutionFlow", "Materialization", "CPU_Core.PipelineExecution.Materialization.cs");
        string stageFlow = Read(root, "HybridCPU_ISE", "CloseToHSL", "Core", "Pipeline", "ExecutionFlow", "StageFlow", "CPU_Core.PipelineExecution.StageFlow.cs");
        Assert.Contains("pipeMEM.Valid = pipeEX.Valid;", materialization, StringComparison.Ordinal);
        Assert.Contains("pipeWB.Valid = pipeMEM.Valid;", materialization, StringComparison.Ordinal);
        AssertOrder(stageFlow, "MemoryCyclePlatformOrchestrator.AdvanceCoreObservedPlatformEdge(", "RefreshInFlightExplicitMemoryProgress();", "PipelineStage_WriteBack();", "PipelineStage_Memory();", "PipelineStage_Execute();");
    }

    [Fact]
    public void ControllerQueuesBackendMapsAndExtensionRuntimesRemainOutside()
    {
        Type state = Required("YAKSys_Hybrid_CPU.Core.MemoryPipelineState");
        Assert.DoesNotContain(state.GetFields(Flags), field =>
            field.Name.Contains("Controller", StringComparison.Ordinal) ||
            field.Name.Contains("RequestId", StringComparison.Ordinal) ||
            field.Name.Contains("Queue", StringComparison.Ordinal) ||
            field.Name.Contains("Retire", StringComparison.Ordinal));
        Type core = typeof(Processor.CPU_Core);
        foreach (string name in new[] { "PhysicalRegisters", "ArchRenameMap", "ArchCommitMap", "PhysRegFreeList" })
            Assert.True((core.GetProperty(name, Flags) ?? throw new InvalidOperationException(name)).PropertyType.IsByRef);
        foreach (string name in new[] { "_dmaStreamComputeTokenStore", "_externalAcceleratorRuntime", "_matrixTileRegisterFile" })
            Assert.True((core.GetProperty(name, Flags) ?? throw new InvalidOperationException(name)).PropertyType.IsByRef);
    }

    [Fact]
    public void LedgerAndEvidenceCloseOnlyMemoryPipelineState()
    {
        string root = FindRoot();
        string ledger = Read(root, "Documentation", "ArchitectureAuthorityRefactor", "10_RF11", "00_CURRENT_STATUS_AND_LEDGER.md");
        string evidence = Read(root, "Documentation", "ArchitectureAuthorityRefactor", "Evidence", "RF11", "rf11.13-memory-pipeline-state-extraction.md");
        Assert.Contains("RF-11.13 | closed MemoryPipelineState", ledger, StringComparison.Ordinal);
        Assert.Contains("RF-11.14 BackendState", ledger, StringComparison.Ordinal);
        Assert.Contains("MemoryCycleController", evidence, StringComparison.Ordinal);
        Assert.Contains("store visibility", evidence, StringComparison.OrdinalIgnoreCase);
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
