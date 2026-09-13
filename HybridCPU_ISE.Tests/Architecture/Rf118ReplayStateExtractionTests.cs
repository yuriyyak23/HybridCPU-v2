using System.Reflection;
using YAKSys_Hybrid_CPU;

namespace HybridCPU_ISE.Tests.Architecture;

public sealed class Rf118ReplayStateExtractionTests
{
    private const BindingFlags InstanceDeclared =
        BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic |
        BindingFlags.DeclaredOnly;

    private static readonly string[] Forwarders =
    [
        "_loopBuffer",
        "_observedReplayRelevantMemoryEpoch",
        "_replayCodeGenerationEpoch",
        "_replaySemanticShadowLookup"
    ];

    [Fact]
    public void RuntimeContainsOneExactFourFieldReplayDomain()
    {
        Type runtime = RequiredType("YAKSys_Hybrid_CPU.Core.CoreRuntimeState");
        Type replay = RequiredType("YAKSys_Hybrid_CPU.Core.ReplayState");
        Assert.Single(runtime.GetFields(InstanceDeclared), field => field.FieldType == replay);
        Assert.Equal(
            new[] { "CodeGenerationEpoch", "LoopBuffer", "ObservedRelevantMemoryEpoch", "SemanticShadowLookup" },
            replay.GetFields(InstanceDeclared).Select(field => field.Name)
                .OrderBy(name => name, StringComparer.Ordinal));
    }

    [Fact]
    public void LegacyReplayFieldsAreRemovedAndForwardersAreByRef()
    {
        Type core = typeof(Processor.CPU_Core);
        foreach (string name in Forwarders)
        {
            Assert.Null(core.GetField(name, InstanceDeclared));
            PropertyInfo property = core.GetProperty(name, InstanceDeclared) ??
                throw new InvalidOperationException($"Replay ref-forwarder '{name}' is missing.");
            Assert.True(property.PropertyType.IsByRef);
        }
    }

    [Fact]
    public void TransitionalCopiesAliasReplayIdentity()
    {
#pragma warning disable CS0618
        var core = new Processor.CPU_Core(0);
#pragma warning restore CS0618
        Processor.CPU_Core copy = core;
        core.Runtime.Replay.CodeGenerationEpoch = 23;
        core.Runtime.Replay.ObservedRelevantMemoryEpoch = 29;
        Assert.Equal(23UL, copy.Runtime.Replay.CodeGenerationEpoch);
        Assert.Equal(29UL, copy.Runtime.Replay.ObservedRelevantMemoryEpoch);
    }

    [Fact]
    public void ReplayLifecycleAndCrossOwnerInvalidationsRemainInPlace()
    {
        string root = FindRepositoryRoot();
        string pipeline = Read(root, "HybridCPU_ISE", "CloseToHSL", "Core", "Pipeline",
            "Stages", "Issue", "CPU_Core.Pipeline.cs");
        string stageFlow = Read(root, "HybridCPU_ISE", "CloseToHSL", "Core", "Pipeline",
            "ExecutionFlow", "StageFlow", "CPU_Core.PipelineExecution.StageFlow.cs");
        string cache = Read(root, "HybridCPU_ISE", "CloseToHSL", "Core", "Execution",
            "Memory", "Cache", "CPU_Core.Cache.cs");
        string retire = Read(root, "HybridCPU_ISE", "CloseToHSL", "Core", "Pipeline",
            "Retire", "Evidence", "CPU_Core.PipelineExecution.Retire.cs");

        Assert.Contains("_loopBuffer.Initialize();", pipeline, StringComparison.Ordinal);
        Assert.Contains("_loopBuffer.EndCycle();", stageFlow, StringComparison.Ordinal);
        Assert.Contains("_loopBuffer.TryGetReplayEntry(", stageFlow, StringComparison.Ordinal);
        Assert.Contains("_loopBuffer.BeginSemanticLoad(", stageFlow, StringComparison.Ordinal);
        Assert.Contains("_loopBuffer.Invalidate", cache, StringComparison.Ordinal);
        Assert.Contains("_loopBuffer.Invalidate", retire, StringComparison.Ordinal);
    }

    [Fact]
    public void ReplayContextAndSemanticShadowMutationSitesRemainInPlace()
    {
        string source = Read(FindRepositoryRoot(), "HybridCPU_ISE", "CloseToHSL", "Core",
            "Frontend", "Decode", "CPU_Core.ReplayDecodeContext.cs");
        Assert.Contains("_replayCodeGenerationEpoch = checked", source, StringComparison.Ordinal);
        Assert.Contains("_replaySemanticShadowLookup?.Invalidate();", source, StringComparison.Ordinal);
        Assert.Contains("_replaySemanticShadowLookup ??= new ReplaySemanticShadowLookup();", source, StringComparison.Ordinal);
        Assert.Contains("_observedReplayRelevantMemoryEpoch = currentEpoch;", source, StringComparison.Ordinal);
    }

    [Fact]
    public void BackendRetireMemoryVerificationAndExtensionOwnersStayOutsideReplayState()
    {
        Type core = typeof(Processor.CPU_Core);
        foreach (string name in new[] { "PhysicalRegisters", "ArchRenameMap", "ArchCommitMap", "PhysRegFreeList" })
            Assert.True((core.GetProperty(name, InstanceDeclared) ?? throw new InvalidOperationException(name)).PropertyType.IsByRef);
        Assert.True((core.GetProperty("_matrixTileRegisterFile", InstanceDeclared) ?? throw new InvalidOperationException()).PropertyType.IsByRef);
        Assert.True((core.GetProperty("_memorySubsystem", InstanceDeclared) ?? throw new InvalidOperationException("_memorySubsystem")).PropertyType.IsByRef);

        Type replay = RequiredType("YAKSys_Hybrid_CPU.Core.ReplayState");
        string[] names = replay.GetFields(InstanceDeclared).Select(field => field.Name).ToArray();
        Assert.DoesNotContain(names, name => name.Contains("Checkpoint", StringComparison.Ordinal));
        Assert.DoesNotContain(names, name => name.Contains("Rename", StringComparison.Ordinal));
        Assert.DoesNotContain(names, name => name.Contains("Retire", StringComparison.Ordinal));
    }

    [Fact]
    public void LedgerAndEvidenceCloseOnlyReplayState()
    {
        string root = FindRepositoryRoot();
        string ledger = Read(root, "Documentation", "Documentation", "ArchitectureAuthorityRefactor", "10_RF11", "00_CURRENT_STATUS_AND_LEDGER.md");
        string evidence = Read(root, "Documentation", "Documentation", "ArchitectureAuthorityRefactor", "Evidence", "RF11", "rf11.8-replay-state-extraction.md");
        Assert.Contains("RF-11.8 | closed ReplayState", ledger, StringComparison.Ordinal);
        Assert.Contains("RF-11.9 RetireState", ledger, StringComparison.Ordinal);
        Assert.Contains("not a pipeline image", evidence, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("four", evidence, StringComparison.OrdinalIgnoreCase);
    }

    private static Type RequiredType(string name) =>
        typeof(Processor.CPU_Core).Assembly.GetType(name) ?? throw new InvalidOperationException(name);

    private static string Read(string root, params string[] components) =>
        File.ReadAllText(components.Aggregate(root, Path.Combine));

    private static string FindRepositoryRoot()
    {
        string? current = AppContext.BaseDirectory;
        while (!string.IsNullOrWhiteSpace(current))
        {
            if (File.Exists(Path.Combine(current, "HybridCPU v2.slnx"))) return current;
            current = Directory.GetParent(current)?.FullName;
        }
        throw new DirectoryNotFoundException();
    }
}
