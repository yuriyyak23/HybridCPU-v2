using System.Reflection;
using YAKSys_Hybrid_CPU;

namespace HybridCPU_ISE.Tests.Architecture;

public sealed class Rf110StateOwnerEntryInventoryFreezeTests
{
    private const BindingFlags InstanceFields =
        BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly;

    [Fact]
    public void CpuCore_RemainsAValueFacadeOverTheFrozenMixedOwnerGraph()
    {
        Type core = typeof(Processor.CPU_Core);

        Assert.False(core.IsValueType);
        PropertyInfo architecturalContexts = core.GetProperty("ArchContexts", InstanceFields) ??
            throw new InvalidOperationException("Extracted architectural context facade is missing.");
        Assert.True(architecturalContexts.PropertyType.IsByRef);
        Assert.Equal("PhysicalRegisterFile", ByRefElement(core, "PhysicalRegisters").Name);
        Assert.Equal("RenameMap", ByRefElement(core, "ArchRenameMap").Name);
        Assert.Equal("CommitMap", ByRefElement(core, "ArchCommitMap").Name);
        Assert.Equal("FreeList", ByRefElement(core, "PhysRegFreeList").Name);
        PropertyInfo retireFacade = core.GetProperty("RetireCoordinator", InstanceFields) ??
            throw new InvalidOperationException("Extracted retire facade is missing.");
        Assert.True(retireFacade.PropertyType.IsByRef);
        Assert.Equal("RetireCoordinator", retireFacade.PropertyType.GetElementType()!.Name);

        PropertyInfo fetchFacade = core.GetProperty("pipeIF", InstanceFields) ??
            throw new InvalidOperationException("Extracted frontend facade 'pipeIF' is missing.");
        Type fetchFacadeType = fetchFacade.PropertyType;
        Assert.True(fetchFacadeType.IsByRef);
        Assert.Equal("FetchStage", fetchFacadeType.GetElementType()!.Name);
        Assert.True(fetchFacade.GetMethod!.ReturnParameter.ParameterType.IsByRef);
        PropertyInfo decodeFacade = core.GetProperty("pipeID", InstanceFields) ??
            throw new InvalidOperationException("Extracted decode facade 'pipeID' is missing.");
        Assert.True(decodeFacade.PropertyType.IsByRef);
        Assert.Equal("DecodeStage", decodeFacade.PropertyType.GetElementType()!.Name);
        PropertyInfo executeFacade = core.GetProperty("pipeEX", InstanceFields) ??
            throw new InvalidOperationException("Extracted execute facade is missing.");
        Assert.True(executeFacade.PropertyType.IsByRef);
        Assert.Equal("ExecuteStage", executeFacade.PropertyType.GetElementType()!.Name);
        Assert.Equal("MemoryStage", ByRefElement(core, "pipeMEM").Name);
        Assert.Equal("WriteBackStage", ByRefElement(core, "pipeWB").Name);
        PropertyInfo controlFacade = core.GetProperty("pipeCtrl", InstanceFields) ??
            throw new InvalidOperationException("Extracted pipeline control facade is missing.");
        Assert.True(controlFacade.PropertyType.IsByRef);
        Assert.Equal("PipelineControl", controlFacade.PropertyType.GetElementType()!.Name);

        PropertyInfo schedulerFacade = core.GetProperty("_fspScheduler", InstanceFields) ??
            throw new InvalidOperationException("Extracted scheduler binding facade is missing.");
        Assert.True(schedulerFacade.PropertyType.IsByRef);
        Assert.Equal("MicroOpScheduler", schedulerFacade.PropertyType.GetElementType()!.Name);
        PropertyInfo replayFacade = core.GetProperty("_loopBuffer", InstanceFields) ??
            throw new InvalidOperationException("Extracted replay facade '_loopBuffer' is missing.");
        Assert.True(replayFacade.PropertyType.IsByRef);
        Assert.Equal("LoopBuffer", replayFacade.PropertyType.GetElementType()!.Name);
        Assert.Equal("MemorySubsystem", ByRefElement(core, "_memorySubsystem").Name);
        Assert.Equal("MainMemoryArea", ByRefElement(core, "_mainMemory").Name);

        Type runtimeRoot = core.Assembly.GetType("YAKSys_Hybrid_CPU.Core.CoreRuntimeState") ??
            throw new InvalidOperationException("CoreRuntimeState was not found.");
        string[] runtimeDomains = runtimeRoot.GetFields(InstanceFields)
            .Select(field => field.FieldType.Name)
            .OrderBy(name => name, StringComparer.Ordinal)
            .ToArray();
        Assert.Equal(new[] { "AdmissionState", "ArchitecturalState", "AssistState", "BackendState", "CacheState", "CoreBindingState", "DecodeState", "ExecutionState", "ExtensionState", "FrontendState", "LegacyCompatibilityState", "MemoryPipelineState", "ReplayState", "ResourceState", "RetireState", "SchedulingState", "ScratchState", "TelemetryState", "VirtualThreadControlState" }, runtimeDomains);
        Assert.NotNull(core.Assembly.GetType("YAKSys_Hybrid_CPU.Core.BackendState"));
        Assert.NotNull(core.Assembly.GetType("YAKSys_Hybrid_CPU.Core.ArchitecturalState"));
    }

    [Fact]
    public void PartialGraphAndCrossStageWriters_RemainAtTheInventoriedTopology()
    {
        string root = FindRepositoryRoot();
        string coreRoot = Path.Combine(root, "HybridCPU_ISE", "CloseToHSL", "Core");
        string productionRoot = Path.Combine(root, "HybridCPU_ISE");

        string[] partialFiles = Directory.EnumerateFiles(productionRoot, "*.cs", SearchOption.AllDirectories)
            .Where(path => !IsBuildOutput(path))
            .Where(path => File.ReadAllText(path).Contains("sealed partial class CPU_Core", StringComparison.Ordinal))
            .OrderBy(path => path, StringComparer.Ordinal)
            .ToArray();

        Assert.Equal(67, partialFiles.Length);

        string materialization = Read(root,
            "HybridCPU_ISE", "CloseToHSL", "Core", "Pipeline", "ExecutionFlow", "Materialization",
            "CPU_Core.PipelineExecution.Materialization.cs");
        string stageFlow = Read(root,
            "HybridCPU_ISE", "CloseToHSL", "Core", "Pipeline", "ExecutionFlow", "StageFlow",
            "CPU_Core.PipelineExecution.StageFlow.cs");
        string retire = Read(root,
            "HybridCPU_ISE", "CloseToHSL", "Core", "Pipeline", "Retire", "Evidence",
            "CPU_Core.PipelineExecution.Retire.cs");
        string testSupport = Read(root,
            "HybridCPU_ISE", "CloseToHSL", "Core", "Pipeline", "Core", "CPU_Core.TestSupport.cs");

        Assert.Contains("pipeEX.Valid = pipeID.Valid;", materialization, StringComparison.Ordinal);
        Assert.Contains("pipeMEM.Valid = pipeEX.Valid;", materialization, StringComparison.Ordinal);
        Assert.Contains("pipeWB.Valid = pipeMEM.Valid;", materialization, StringComparison.Ordinal);
        Assert.Contains("ScalarWriteBackLaneState lane = pipeWB.GetLane", retire, StringComparison.Ordinal);
        Assert.Contains("pipeWB = writeBackStage;", testSupport, StringComparison.Ordinal);

        AssertOrder(stageFlow,
            "MemoryCyclePlatformOrchestrator.AdvanceCoreObservedPlatformEdge(",
            "RefreshInFlightExplicitMemoryProgress();",
            "PipelineStage_WriteBack();",
            "PipelineStage_Memory();",
            "PipelineStage_Execute();",
            "PipelineStage_Decode();",
            "PipelineStage_Fetch();");

        Assert.True(Directory.EnumerateFiles(coreRoot, "*.cs", SearchOption.AllDirectories).Any());
    }

    [Fact]
    public void DangerousCopyReflectionAndTestMutationSeams_RemainExplicit()
    {
        string root = FindRepositoryRoot();
        string machineSource = Read(root, "HybridCPU_ISE", "Machine", "IIseMachineStateSource.cs");
        string legacySource = Read(root, "HybridCPU_ISE", "Legacy", "NonRTL", "Legacy", "LegacyProcessorMachineStateSource.cs");
        string diagnosticSession = Read(root, "TestAssemblerConsoleApps", "DiagnosticRuntimeSession.cs");
        string simpleAsm = Read(root, "TestAssemblerConsoleApps", "SimpleAsmApp.cs");
        string reflectionMutation = Read(root,
            "HybridCPU_ISE.Tests", "tests", "Phase09StreamEngineDeferredParityTests.cs");
        string testSupport = Read(root,
            "HybridCPU_ISE", "CloseToHSL", "Core", "Pipeline", "Core", "CPU_Core.TestSupport.cs");

        Assert.Contains("CpuCoreDiagnosticSnapshot GetCoreSnapshot(int coreId);", machineSource, StringComparison.Ordinal);
        Assert.Contains("cores[coreIndex].ArchContexts", legacySource, StringComparison.Ordinal);
        Assert.Contains("public Processor.CPU_Core GetCoreRef()", diagnosticSession, StringComparison.Ordinal);
        Assert.Contains("public CpuCoreDiagnosticSnapshot GetCoreSnapshot()", diagnosticSession, StringComparison.Ordinal);
        Assert.DoesNotContain("public void SetCore(", diagnosticSession, StringComparison.Ordinal);
        Assert.DoesNotContain("Processor.CPU_Cores[CoreId] =", diagnosticSession, StringComparison.Ordinal);

        Assert.Contains("typeof(Processor.CPU_Core.PipelineControl).GetFields", simpleAsm, StringComparison.Ordinal);
        Assert.Contains("typeof(Processor.CPU_Core.PipelineControl).GetProperties", simpleAsm, StringComparison.Ordinal);
        Assert.Contains("field.SetValue(boxedAccumulator, mergedValue);", simpleAsm, StringComparison.Ordinal);
        Assert.Contains("typeof(ScratchState).GetField", reflectionMutation, StringComparison.Ordinal);
        Assert.Contains("field.SetValue(core.Runtime.Scratch, value);", reflectionMutation, StringComparison.Ordinal);
        Assert.DoesNotContain("field.SetValueDirect(__makeref(core), value);", reflectionMutation, StringComparison.Ordinal);
        Assert.Contains("SetPrivateField(ref core, \"ScratchA\"", reflectionMutation, StringComparison.Ordinal);
        Assert.Contains("TEST-ONLY partial extension for CPU_Core", testSupport, StringComparison.Ordinal);
        Assert.True(Count(testSupport, "internal void Test") >= 50);

        string production = ReadProductionSources(root);
        Assert.DoesNotContain("JsonSerializer.Serialize(core", production, StringComparison.Ordinal);
        Assert.DoesNotContain("JsonSerializer.Deserialize<Processor.CPU_Core", production, StringComparison.Ordinal);
    }

    [Fact]
    public void StaticAndExternalOwners_RemainVisibleAndDistinct()
    {
        string root = FindRepositoryRoot();
        string processor = Read(root, "HybridCPU_ISE", "NonRTL", "Processor", "Core", "Processor.cs");
        string processorMemory = Read(root, "HybridCPU_ISE", "NonRTL", "Processor", "Memory", "Processor.Memory.cs");
        string verification = Read(root, "HybridCPU_ISE", "NonRTL", "Processor", "Verification", "Processor.Verification.cs");
        string iommu = Read(root, "HybridCPU_ISE", "CloseToHSL", "Memory", "MMU", "IOMMU.cs");
        string domainBinding = Read(root, "HybridCPU_ISE", "CloseToHSL", "Memory", "MMU", "IOMMU.DomainBinding.cs");
        string bankedMemory = Read(root, "HybridCPU_ISE", "CloseToHSL", "Memory", "Banks", "MultiBankMemoryArea.cs");
        string registry = Read(root, "HybridCPU_ISE", "CloseToHSL", "Core", "Diagnostics", "InstructionRegistry.cs");
        string stream = Read(root, "HybridCPU_ISE", "CloseToHSL", "Core", "Execution", "StreamEngine", "BurstIO", "StreamEngine.BurstIO.cs");

        Assert.Contains("public static CPU_Core[] CPU_Cores", processor, StringComparison.Ordinal);
        Assert.Contains("public static DMAController? DMAController;", processor, StringComparison.Ordinal);
        Assert.Contains("public static YAKSys_Hybrid_CPU.Memory.MemorySubsystem? Memory;", processor, StringComparison.Ordinal);
        Assert.Contains("public static MainMemoryArea MainMemory", processorMemory, StringComparison.Ordinal);
        Assert.Contains("private static ReplayToken? currentReplayToken;", verification, StringComparison.Ordinal);
        Assert.Contains("private static PageTableWalker _ptw;", iommu, StringComparison.Ordinal);
        Assert.Contains("private static Dictionary<IoDomainKey, IommuDomainBinding>? _ioDomainBindings;", domainBinding, StringComparison.Ordinal);
        Assert.Contains("private static bool _lastAccessSilentlySquashed;", bankedMemory, StringComparison.Ordinal);
        Assert.Contains("private static Dictionary<uint, MicroOpFactory> _factories", registry, StringComparison.Ordinal);
        Assert.Contains("private static IBurstBackend _backend", stream, StringComparison.Ordinal);

        Type core = typeof(Processor.CPU_Core);
        Assert.Equal("DmaStreamComputeTokenStore", ByRefElement(core, "_dmaStreamComputeTokenStore").Name);
        Assert.Equal("ExternalAcceleratorRuntime", ByRefElement(core, "_externalAcceleratorRuntime").Name);
        Assert.Equal("MatrixTileArchitecturalTileRegisterFile", ByRefElement(core, "_matrixTileRegisterFile").Name);
        Assert.Equal("StreamRegisterFile", ByRefElement(core, "_matrixTileStreamRegisterFile").Name);
        Assert.Null(core.GetField("VmcsManager", InstanceFields));
    }

    [Fact]
    public void Rf110DocumentsInventoryOnlyAndQueuesOneArchitectureDecision()
    {
        string root = FindRepositoryRoot();
        string status = Read(root,
            "Documentation", "Documentation", "ArchitectureAuthorityRefactor", "10_RF11", "00_CURRENT_STATUS_AND_LEDGER.md");
        string evidence = Read(root,
            "Documentation", "Documentation", "ArchitectureAuthorityRefactor", "Evidence", "RF11", "rf11.0-entry-inventory-freeze.md");

        Assert.Contains("RF-11.0 | closed inventory/freeze", status, StringComparison.Ordinal);
        Assert.Matches(@"RF-11 overall \| (?:open|closed)", status);
        Assert.Contains("RF-11.1 | closed architecture decision", status, StringComparison.Ordinal);
        Assert.Contains("no runtime state was moved", evidence, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("paper does not define", evidence, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("TestAssemblerConsoleApps was not run", evidence, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("split state", evidence, StringComparison.OrdinalIgnoreCase);
    }

    private static FieldInfo Field(Type type, string name) =>
        type.GetField(name, InstanceFields) ??
        throw new InvalidOperationException($"Inventoried field '{name}' is missing from {type.FullName}.");

    private static Type ByRefElement(Type type, string name)
    {
        PropertyInfo property = type.GetProperty(name, InstanceFields) ?? throw new InvalidOperationException(name);
        Assert.True(property.PropertyType.IsByRef);
        return property.PropertyType.GetElementType()!;
    }

    private static void AssertOrder(string text, params string[] markers)
    {
        int prior = -1;
        foreach (string marker in markers)
        {
            int current = text.IndexOf(marker, StringComparison.Ordinal);
            Assert.True(current > prior, $"Expected '{marker}' after the prior cycle marker.");
            prior = current;
        }
    }

    private static string ReadProductionSources(string root)
    {
        string sourceRoot = Path.Combine(root, "HybridCPU_ISE");
        return string.Join("\n",
            Directory.EnumerateFiles(sourceRoot, "*.cs", SearchOption.AllDirectories)
                .Where(path => !IsBuildOutput(path))
                .OrderBy(path => path, StringComparer.Ordinal)
                .Select(File.ReadAllText));
    }

    private static bool IsBuildOutput(string path) =>
        path.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase) ||
        path.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase);

    private static string Read(string root, params string[] components) =>
        File.ReadAllText(components.Aggregate(root, Path.Combine));

    private static int Count(string text, string value)
    {
        int count = 0;
        int offset = 0;
        while ((offset = text.IndexOf(value, offset, StringComparison.Ordinal)) >= 0)
        {
            count++;
            offset += value.Length;
        }

        return count;
    }

    private static string FindRepositoryRoot()
    {
        string? current = AppContext.BaseDirectory;
        while (!string.IsNullOrWhiteSpace(current))
        {
            if (File.Exists(Path.Combine(current, "HybridCPU v2.slnx")))
                return current;

            current = Directory.GetParent(current)?.FullName;
        }

        throw new DirectoryNotFoundException("HybridCPU repository root was not found.");
    }
}
