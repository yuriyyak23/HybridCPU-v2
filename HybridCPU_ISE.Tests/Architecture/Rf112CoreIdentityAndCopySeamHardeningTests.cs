using System.Reflection;
using HybridCPU_ISE.Machine;
using YAKSys_Hybrid_CPU;

namespace HybridCPU_ISE.Tests.Architecture;

public sealed class Rf112CoreIdentityAndCopySeamHardeningTests
{
    [Fact]
    public void ProcessorPublishesDistinctLiveSnapshotAndReplacementApis()
    {
        MethodInfo getRef = RequiredMethod(nameof(Processor.GetCoreRef));
        MethodInfo getSnapshot = RequiredMethod(nameof(Processor.GetCoreSnapshot));
        MethodInfo replace = RequiredMethod(nameof(Processor.ReplaceCore));

        Assert.False(getRef.ReturnParameter.ParameterType.IsByRef);
        Assert.Equal(typeof(Processor.CPU_Core), getRef.ReturnType);
        Assert.Equal(typeof(CpuCoreDiagnosticSnapshot), getSnapshot.ReturnType);
        Assert.Equal(typeof(void), replace.ReturnType);

        string identity = Read(FindRepositoryRoot(), "HybridCPU_ISE", "NonRTL", "Processor", "Core",
            "Processor.CoreIdentity.cs");
        Assert.Contains("Returns the existing live facade identity", identity, StringComparison.Ordinal);
        Assert.Contains("detached, read-only diagnostic projection", identity, StringComparison.Ordinal);
        Assert.Contains("Explicit whole-core lifecycle replacement", identity, StringComparison.Ordinal);
    }

    [Fact]
    public void ProductionAndDiagnosticConsoleDoNotIndexCoreTableDirectly()
    {
        string root = FindRepositoryRoot();
        string production = ReadTree(Path.Combine(root, "HybridCPU_ISE"));
        string diagnostics = ReadTree(Path.Combine(root, "TestAssemblerConsoleApps"));

        Assert.DoesNotContain("CPU_Cores[", production, StringComparison.Ordinal);
        Assert.DoesNotContain("CPU_Cores [", production, StringComparison.Ordinal);
        Assert.DoesNotContain("CPU_Cores[", diagnostics, StringComparison.Ordinal);
        Assert.DoesNotContain("CPU_Cores [", diagnostics, StringComparison.Ordinal);
    }

    [Fact]
    public void ProductionDoesNotCacheWholeCoreInDeferredClosure()
    {
        string root = FindRepositoryRoot();
        string source = Read(root, "HybridCPU_ISE", "CloseToHSL", "Core", "ISA",
            "Instructions", "NonVmx", "Lanes00_03Vector", "MatrixTile",
            "MatrixTileStreamTransferAbi.cs");

        Assert.DoesNotContain("CPU_Core coreCopy", source, StringComparison.Ordinal);
        Assert.Contains("core.CaptureMatrixTileMemoryReader()", source, StringComparison.Ordinal);
    }

    [Fact]
    public void ObservationIsSnapshotOnlyAndDiagnosticMutationIsLiveRefOnly()
    {
        string root = FindRepositoryRoot();
        string source = Read(root, "HybridCPU_ISE", "Machine", "IIseMachineStateSource.cs");
        string observation = Read(root, "HybridCPU_ISE", "Machine", "IseObservationService.MachineState.cs");
        string session = Read(root, "TestAssemblerConsoleApps", "DiagnosticRuntimeSession.cs");
        string showcase = Read(root, "TestAssemblerConsoleApps", "SimpleAsmApp.Showcase.cs");
        string matrix = Read(root, "TestAssemblerConsoleApps", "MatrixTileSpecSuite.cs");

        Assert.Contains("CpuCoreDiagnosticSnapshot GetCoreSnapshot(int coreId)", source, StringComparison.Ordinal);
        Assert.DoesNotContain("CPU_Core GetCore(int coreId)", source, StringComparison.Ordinal);
        Assert.Contains("machineStateSource.GetCoreSnapshot(coreId)", observation, StringComparison.Ordinal);

        Assert.Contains("public Processor.CPU_Core GetCoreRef()", session, StringComparison.Ordinal);
        Assert.Contains("public CpuCoreDiagnosticSnapshot GetCoreSnapshot()", session, StringComparison.Ordinal);
        Assert.DoesNotContain("void SetCore(", session, StringComparison.Ordinal);
        Assert.Contains("Processor.CPU_Core core = _runtime.GetCoreRef();", showcase, StringComparison.Ordinal);
        Assert.DoesNotContain("_runtime.SetCore", showcase, StringComparison.Ordinal);
        Assert.Contains("Processor.ReplaceCore(0, originalCoreLifecycleHandle);", matrix, StringComparison.Ordinal);
        Assert.Contains("Processor.CPU_Core core = Processor.GetCoreRef(0);", matrix, StringComparison.Ordinal);
    }

    [Fact]
    public void ExistingLaneCopiesRemainCallLocalExplicitGetModifySetProtocols()
    {
        string root = FindRepositoryRoot();
        string core = ReadTree(Path.Combine(root, "HybridCPU_ISE", "CloseToHSL", "Core"));

        Assert.Contains("ScalarExecuteLaneState lane = pipeEX.GetLane", core, StringComparison.Ordinal);
        Assert.Contains("pipeEX.SetLane", core, StringComparison.Ordinal);
        Assert.Contains("ScalarMemoryLaneState lane = pipeMEM.GetLane", core, StringComparison.Ordinal);
        Assert.Contains("pipeMEM.SetLane", core, StringComparison.Ordinal);
        Assert.Contains("ScalarWriteBackLaneState lane = pipeWB.GetLane", core, StringComparison.Ordinal);
        Assert.Contains("pipeWB.SetLane", core, StringComparison.Ordinal);

        Type runtimeRoot = typeof(Processor.CPU_Core).Assembly.GetType(
            "YAKSys_Hybrid_CPU.Core.CoreRuntimeState") ??
            throw new InvalidOperationException("CoreRuntimeState was not found.");
        FieldInfo[] runtimeFields = runtimeRoot.GetFields(
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic |
            BindingFlags.DeclaredOnly);
        Assert.Equal(
            new[] { "AdmissionState", "ArchitecturalState", "AssistState", "BackendState", "CacheState", "CoreBindingState", "DecodeState", "ExecutionState", "ExtensionState", "FrontendState", "LegacyCompatibilityState", "MemoryPipelineState", "ReplayState", "ResourceState", "RetireState", "SchedulingState", "ScratchState", "TelemetryState", "VirtualThreadControlState" },
            runtimeFields.Select(field => field.FieldType.Name)
                .OrderBy(name => name, StringComparer.Ordinal));
    }

    [Fact]
    public void ReflectionAndTestSupportRemainExplicitTestOnlyAdapters()
    {
        string root = FindRepositoryRoot();
        string reflection = Read(root, "HybridCPU_ISE.Tests", "tests",
            "Phase09StreamEngineDeferredParityTests.cs");
        string support = Read(root, "HybridCPU_ISE", "CloseToHSL", "Core", "Pipeline", "Core",
            "CPU_Core.TestSupport.cs");

        Assert.Contains("RF-11 TEST-ONLY REFLECTION MUTATION ADAPTER", reflection, StringComparison.Ordinal);
        Assert.Contains("TEST-ONLY partial extension for CPU_Core", support, StringComparison.Ordinal);
    }

    [Fact]
    public void LedgerClosesOnlyCopyHardeningAndQueuesEmptyRoot()
    {
        string root = FindRepositoryRoot();
        string status = Read(root, "Documentation", "ArchitectureAuthorityRefactor", "10_RF11",
            "00_CURRENT_STATUS_AND_LEDGER.md");
        string evidence = Read(root, "Documentation", "ArchitectureAuthorityRefactor", "Evidence", "RF11",
            "rf11.2-core-identity-and-copy-seam-hardening.md");

        Assert.Contains("RF-11.2 | closed identity/copy hardening", status, StringComparison.Ordinal);
        Assert.Contains("RF-11.3 empty containment root", status, StringComparison.Ordinal);
        Assert.Contains("no state domain was moved", evidence, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("one next task", evidence, StringComparison.OrdinalIgnoreCase);
    }

    private static MethodInfo RequiredMethod(string name) =>
        typeof(Processor).GetMethod(name, BindingFlags.Public | BindingFlags.Static) ??
        throw new InvalidOperationException($"Required processor identity API '{name}' was not found.");

    private static string ReadTree(string path) => string.Join("\n",
        Directory.EnumerateFiles(path, "*.cs", SearchOption.AllDirectories)
            .Where(file => !file.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase))
            .Where(file => !file.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase))
            .OrderBy(file => file, StringComparer.Ordinal)
            .Select(File.ReadAllText));

    private static string Read(string root, params string[] components) =>
        File.ReadAllText(components.Aggregate(root, Path.Combine));

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
