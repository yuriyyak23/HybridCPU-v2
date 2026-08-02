using System.Reflection;
using System.Text.RegularExpressions;
using YAKSys_Hybrid_CPU;

namespace HybridCPU_ISE.Tests.Architecture;

public sealed class Rf1116ResidualOwnerAndFacadeReadinessTests
{
    private const BindingFlags Fields = BindingFlags.Instance | BindingFlags.Public |
        BindingFlags.NonPublic | BindingFlags.DeclaredOnly;

    [Fact]
    public void ResidualDirectFieldSetIsOnlyStableRuntimeReference()
    {
        Type core = typeof(Processor.CPU_Core);
        Assert.False(core.IsValueType);

        string[] expected = { "_runtime" };

        FieldInfo[] actualFields = core.GetFields(Fields);
        Assert.Equal(expected.OrderBy(name => name, StringComparer.Ordinal), actualFields.Select(field => field.Name)
            .OrderBy(name => name, StringComparer.Ordinal));
        Assert.Single(actualFields);
        Assert.Single(actualFields, field => field.IsInitOnly && field.Name == "_runtime");
        Assert.Empty(actualFields.Where(field => !field.IsInitOnly));
        Assert.True((core.GetProperty("ulong_InstructionPointer", Fields) ?? throw new InvalidOperationException()).PropertyType.IsByRef);
        Assert.True((core.GetProperty("_hasMaterializedVliwFetchState", Fields) ?? throw new InvalidOperationException()).PropertyType.IsByRef);
        foreach (string name in new[] { "CsrPodId", "CsrPodAffinityMask", "CsrMemDomainCert", "CsrNocRouteCfg" })
            Assert.True((core.GetProperty(name, Fields) ?? throw new InvalidOperationException(name)).PropertyType.IsByRef);
        Assert.True((core.GetProperty("differentialTraceCapture", Fields) ?? throw new InvalidOperationException()).PropertyType.IsByRef);
        Assert.True((core.GetProperty("_assistRuntimeEpoch", Fields) ?? throw new InvalidOperationException()).PropertyType.IsByRef);
        Assert.True((core.GetProperty("_lastAssistInvalidationReason", Fields) ?? throw new InvalidOperationException()).PropertyType.IsByRef);
        foreach (string name in new[]
        {
            "ScratchA", "ScratchB", "ScratchDst", "ScratchIndex", "ScratchA_DB0", "ScratchB_DB0",
            "ScratchA_DB1", "ScratchB_DB1", "ScratchDst_DB0", "ScratchDst_DB1",
            "BankedScratchA", "BankedScratchB", "BankedScratchDst", "ActiveBufferSet"
        })
            Assert.True((core.GetProperty(name, Fields) ?? throw new InvalidOperationException(name)).PropertyType.IsByRef);
        foreach (string name in new[]
        {
            "globalResourceLocks", "tokenGeneration", "resourceTokens", "resourceUsageCounts",
            "resourceContentionCounts", "_readCounters", "syncCounter", "_grlbBanks",
            "_bankContentionCounts"
        })
            Assert.True((core.GetProperty(name, Fields) ?? throw new InvalidOperationException(name)).PropertyType.IsByRef);
        PropertyInfo structuralStalls = core.GetProperty("StructuralStalls", Fields) ??
            throw new InvalidOperationException("StructuralStalls");
        Assert.False(structuralStalls.PropertyType.IsByRef);
        Assert.True(structuralStalls.SetMethod?.IsPrivate);
        foreach (string name in new[] { "IsVMXRoot", "VirtualThreadPipelineStates", "_vmxExecutionPlaneWired" })
            Assert.True((core.GetProperty(name, Fields) ?? throw new InvalidOperationException(name)).PropertyType.IsByRef);
        foreach (string name in new[] { "CycleCounter", "StageCycleCounter", "Stalled" })
            Assert.True((core.GetProperty(name, Fields) ?? throw new InvalidOperationException(name)).PropertyType.IsByRef);
        foreach (string name in new[] { "CoreID", "_platformContext", "_executionMode", "_interruptDispatcher" })
            Assert.True((core.GetProperty(name, Fields) ?? throw new InvalidOperationException(name)).PropertyType.IsByRef);
        foreach (string name in new[]
        {
            "_matrixTileStreamInvalidationCount", "_nextMatrixTileCaptureOrdinal",
            "_nextMatrixTileReplayCheckpointOrdinal", "_matrixTileReplayInvalidationEpoch"
        })
            Assert.True((core.GetProperty(name, Fields) ?? throw new InvalidOperationException(name)).PropertyType.IsByRef);
        foreach (string name in new[]
        {
            "L1_VLIWBundles", "L1_Data", "L2_VLIWBundles", "L2_Data",
            "ulong_MinL1Query", "ulong_MinL2Query", "Current_VLIWBundle_Position",
            "Current_DataObject_Position"
        })
            Assert.True((core.GetProperty(name, Fields) ?? throw new InvalidOperationException(name)).PropertyType.IsByRef);
    }

    [Fact]
    public void RuntimeRootRemainsAuthorityPreservingDomains()
    {
        Type runtime = Required("YAKSys_Hybrid_CPU.Core.CoreRuntimeState");
        Assert.Equal(new[]
        {
            "AdmissionState", "ArchitecturalState", "AssistState", "BackendState", "CacheState", "CoreBindingState", "DecodeState", "ExecutionState",
            "ExtensionState", "FrontendState", "LegacyCompatibilityState", "MemoryPipelineState", "ReplayState", "ResourceState", "RetireState",
            "SchedulingState", "ScratchState", "TelemetryState", "VirtualThreadControlState"
        }, runtime.GetFields(Fields).Select(field => field.FieldType.Name)
            .OrderBy(name => name, StringComparer.Ordinal));
        Assert.Empty(runtime.GetMethods(Fields).Where(method =>
            method.Name is "Execute" or "Commit" or "Rollback" or "Publish" or "Migrate"));
    }

    [Fact]
    public void StructCopyReflectionAndTestMutationSeamsRemainExplicit()
    {
        string root = FindRoot();
        string production = ReadSources(Path.Combine(root, "HybridCPU_ISE"));
        string tests = string.Join("\n", SourceFiles(Path.Combine(root, "HybridCPU_ISE.Tests"))
            .Where(file => !file.EndsWith(nameof(Rf1116ResidualOwnerAndFacadeReadinessTests) + ".cs", StringComparison.Ordinal))
            .Select(File.ReadAllText));
        string identity = Read(root, "HybridCPU_ISE", "NonRTL", "Processor", "Core", "Processor.CoreIdentity.cs");
        string testSupport = Read(root, "HybridCPU_ISE", "CloseToHSL", "Core", "Pipeline", "Core", "CPU_Core.TestSupport.cs");

        Assert.Contains("public static CPU_Core GetCoreRef", identity, StringComparison.Ordinal);
        Assert.Contains("CpuCoreDiagnosticSnapshot GetCoreSnapshot", identity, StringComparison.Ordinal);
        Assert.Contains("public static void ReplaceCore", identity, StringComparison.Ordinal);
        Assert.DoesNotContain("CPU_Cores[", production, StringComparison.Ordinal);
        Assert.Equal(289, Regex.Matches(production,
            @"\b(?:ref|in|out)\s+(?:Processor\.)?CPU_Core\b").Count);
        Assert.Equal(99, Regex.Matches(tests, @"CPU_Cores\s*\[").Count);
        Assert.Contains("SetValueDirect(__makeref(core), value);", tests, StringComparison.Ordinal);
        Assert.DoesNotContain("SetValueDirect", production, StringComparison.Ordinal);
        Assert.True(Regex.Matches(testSupport,
            @"internal\s+(?:[A-Za-z0-9_<>,?\[\].]+\s+)+Test[A-Za-z0-9_]*\s*\(").Count >= 80);
        Assert.DoesNotContain("JsonSerializer.Serialize(core", production, StringComparison.Ordinal);
        Assert.DoesNotContain("JsonSerializer.Deserialize<Processor.CPU_Core", production, StringComparison.Ordinal);
    }

    [Fact]
    public void PartialAndInPlaceCrossStageTopologyIsFrozenWithoutCurrentNextRewrite()
    {
        string root = FindRoot();
        string productionRoot = Path.Combine(root, "HybridCPU_ISE");
        string[] sources = SourceFiles(productionRoot).Select(File.ReadAllText).ToArray();
        Assert.Equal(66, sources.Count(source => source.Contains("sealed partial class CPU_Core", StringComparison.Ordinal)));

        string production = string.Join("\n", sources);
        Assert.Equal(34, AssignmentCount(production, "IF"));
        Assert.Equal(49, AssignmentCount(production, "ID"));
        Assert.Equal(119, AssignmentCount(production, "EX"));
        Assert.Equal(62, AssignmentCount(production, "MEM"));
        Assert.Equal(60, AssignmentCount(production, "WB"));

        string stageFlow = Read(root, "HybridCPU_ISE", "CloseToHSL", "Core", "Pipeline",
            "ExecutionFlow", "StageFlow", "CPU_Core.PipelineExecution.StageFlow.cs");
        AssertOrder(stageFlow,
            "MemoryCyclePlatformOrchestrator.AdvanceCoreObservedPlatformEdge(",
            "RefreshInFlightExplicitMemoryProgress();",
            "PipelineStage_WriteBack();", "PipelineStage_Memory();", "PipelineStage_Execute();",
            "PipelineStage_Decode();", "PipelineStage_Fetch();");
    }

    [Fact]
    public void ProcessStaticOwnersStayOutsidePerCoreContainment()
    {
        string root = FindRoot();
        Assert.Contains("public static CPU_Core[] CPU_Cores", Read(root, "HybridCPU_ISE", "NonRTL", "Processor", "Core", "Processor.cs"), StringComparison.Ordinal);
        Assert.Contains("private static ReplayToken? currentReplayToken", Read(root, "HybridCPU_ISE", "NonRTL", "Processor", "Verification", "Processor.Verification.cs"), StringComparison.Ordinal);
        string registry = Read(root, "HybridCPU_ISE", "CloseToHSL", "Core", "Diagnostics", "InstructionRegistry.cs");
        Assert.Contains("private static Dictionary<uint, MicroOpFactory> _factories", registry, StringComparison.Ordinal);
        Assert.Contains("private static Dictionary<string, ICustomAccelerator> _customAccelerators", registry, StringComparison.Ordinal);
        Assert.Contains("private static IBurstBackend _backend", Read(root, "HybridCPU_ISE", "CloseToHSL", "Core", "Execution", "StreamEngine", "BurstIO", "StreamEngine.BurstIO.cs"), StringComparison.Ordinal);
        Assert.Contains("private static PageTableWalker _ptw", Read(root, "HybridCPU_ISE", "CloseToHSL", "Memory", "MMU", "IOMMU.cs"), StringComparison.Ordinal);
        Assert.Contains("private static readonly Dictionary<(int CoreId, int VirtualThreadId), Reservation> Reservations", Read(root, "HybridCPU_ISE", "CloseToHSL", "Core", "Execution", "Memory", "AtomicMemory", "AtomicMemoryUnit.cs"), StringComparison.Ordinal);
    }

    [Fact]
    public void LedgerAndEvidenceCloseInventoryOnlyAndSelectOneResidualDomain()
    {
        string root = FindRoot();
        string ledger = Read(root, "Documentation", "ArchitectureAuthorityRefactor", "10_RF11", "00_CURRENT_STATUS_AND_LEDGER.md");
        string evidence = Read(root, "Documentation", "ArchitectureAuthorityRefactor", "Evidence", "RF11", "rf11.16-residual-owner-and-facade-readiness-inventory.md");
        Assert.Contains("RF-11.16 | closed residual inventory/freeze", ledger, StringComparison.Ordinal);
        Assert.Contains("reference-facade conversion | blocked", evidence, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("RF-11.17 FrontendState residual completion", ledger, StringComparison.Ordinal);
        Assert.Contains("TestAssemblerConsoleApps was not run", evidence, StringComparison.Ordinal);
        Assert.Contains("no runtime state was moved", evidence, StringComparison.OrdinalIgnoreCase);
    }

    private static int AssignmentCount(string text, string stage) => Regex.Matches(text,
        $@"\bpipe{stage}\.\w+\s*(?:[+\-*/%&|^]=|=(?!=))").Count;

    private static Type Required(string name) => typeof(Processor.CPU_Core).Assembly.GetType(name) ??
        throw new InvalidOperationException(name);

    private static void AssertOrder(string text, params string[] markers)
    {
        int prior = -1;
        foreach (string marker in markers)
        {
            int current = text.IndexOf(marker, StringComparison.Ordinal);
            Assert.True(current > prior, $"Expected '{marker}' after the previous cycle marker.");
            prior = current;
        }
    }

    private static string ReadSources(string path) => string.Join("\n", SourceFiles(path).Select(File.ReadAllText));
    private static IEnumerable<string> SourceFiles(string path) => Directory.EnumerateFiles(path, "*.cs", SearchOption.AllDirectories)
        .Where(file => !file.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase))
        .Where(file => !file.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase))
        .OrderBy(file => file, StringComparer.Ordinal);
    private static string Read(string root, params string[] parts) => File.ReadAllText(parts.Aggregate(root, Path.Combine));
    private static string FindRoot()
    {
        string? current = AppContext.BaseDirectory;
        while (current != null) { if (File.Exists(Path.Combine(current, "HybridCPU v2.slnx"))) return current; current = Directory.GetParent(current)?.FullName; }
        throw new DirectoryNotFoundException();
    }
}
