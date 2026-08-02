using System.Reflection;
using HybridCPU_ISE.Machine;
using YAKSys_Hybrid_CPU;
using YAKSys_Hybrid_CPU.Core;

namespace HybridCPU_ISE.Tests.Architecture;

public sealed class Rf1129DiagnosticSnapshotHardeningTests
{
    private const BindingFlags Declared = BindingFlags.Instance | BindingFlags.Public |
        BindingFlags.NonPublic | BindingFlags.DeclaredOnly;

    [Fact]
    public void SnapshotTypeIsSealedReadOnlyProjectionWithoutRuntimeOwnerReference()
    {
        Type snapshot = typeof(CpuCoreDiagnosticSnapshot);
        Assert.True(snapshot.IsClass);
        Assert.True(snapshot.IsSealed);
        Assert.All(snapshot.GetFields(Declared), field => Assert.True(field.IsPrivate && field.IsInitOnly));
        Assert.All(snapshot.GetProperties(Declared), property => Assert.Null(property.SetMethod));
        Assert.DoesNotContain(snapshot.GetFields(Declared), field =>
            field.FieldType == typeof(Processor.CPU_Core) ||
            field.FieldType.Name is "CoreRuntimeState" or "CoreBindingState");
        Assert.Null(snapshot.GetProperty("Runtime", Declared));
        Assert.DoesNotContain(snapshot.GetMethods(Declared), method => method.Name is
            "Execute" or "Commit" or "Rollback" or "Publish" or "Replace" or "Migrate" or "ApplyTo");
    }

    [Fact]
    public void ProcessorAndMachineSourcesReturnDiagnosticTypeNotFacadeCopies()
    {
        MethodInfo processorSnapshot = typeof(Processor).GetMethod(
            "GetCoreSnapshot", BindingFlags.Static | BindingFlags.Public) ??
            throw new InvalidOperationException("Processor.GetCoreSnapshot");
        Assert.Equal(typeof(CpuCoreDiagnosticSnapshot), processorSnapshot.ReturnType);
        Assert.Equal(
            typeof(CpuCoreDiagnosticSnapshot),
            typeof(IIseMachineStateSource).GetMethod("GetCoreSnapshot")?.ReturnType);
        Assert.Equal(
            typeof(CpuCoreDiagnosticSnapshot),
            typeof(NullMachineStateSource).GetMethod("GetCoreSnapshot")?.ReturnType);
    }

    [Fact]
    public void CapturedValuesAndArrayExportsAreDetachedFromLiveRuntime()
    {
        var core = new Processor.CPU_Core(
            7,
            CpuCorePlatformContext.CreateFixed(new Processor.MainMemoryArea(), ProcessorMode.Compiler));
        core.WriteCommittedArch(0, 3, 0x1234);
        core.VirtualThreadStalled[1] = true;
        core.VirtualThreadPipelineStates[2] = PipelineState.WaitForEvent;
        core.ThreadFPContexts[3].InvalidOp = true;
        core.CoreFlagsRegister.Zero_Flag = true;
        core.Call_Callback_Addresses.Add(0x55);
        core.Interrupt_Callback_Addresses.Add(0x66);
        core.Runtime.Resources.GrlbBanks[0] = 0xA5A5A5A5;
        core.Runtime.Execution.Control.CycleCount = 99;

        CpuCoreDiagnosticSnapshot snapshot = CpuCoreDiagnosticSnapshot.Capture(core);

        core.CoreID = 8;
        core.WriteCommittedArch(0, 3, 0x9999);
        core.VirtualThreadStalled[1] = false;
        core.VirtualThreadPipelineStates[2] = PipelineState.Task;
        core.ThreadFPContexts[3].InvalidOp = false;
        core.CoreFlagsRegister.Zero_Flag = false;
        core.Call_Callback_Addresses.Clear();
        core.Runtime.Resources.GrlbBanks[0] = 0;
        core.Runtime.Execution.Control.CycleCount = 100;

        Assert.Equal(7U, snapshot.CoreId);
        Assert.Equal(0x1234UL, snapshot.ReadArch(0, 3));
        Assert.True(snapshot.ReadVirtualThreadStalled(1));
        Assert.True(snapshot.HasAnyVirtualThreadPipelineState(PipelineState.WaitForEvent));
        Assert.True(snapshot.ReadThreadFpContext(3).InvalidOp);
        Assert.True(snapshot.CoreFlagsRegister.Zero_Flag);
        Assert.Equal(1, snapshot.CallStackDepth);
        Assert.Equal(0x55UL, snapshot.CallStackTop);
        Assert.Equal(1, snapshot.InterruptStackDepth);
        Assert.Equal(0x66UL, snapshot.InterruptStackTop);
        Assert.Equal(99UL, snapshot.GetPipelineControl().CycleCount);

        uint[] firstBanks = snapshot.GetGrlbBanks();
        firstBanks[0] = 0;
        Assert.Equal(0xA5A5A5A5U, snapshot.GetGrlbBanks()[0]);
    }

    [Fact]
    public void ClosedWorldReadersUseSnapshotWhileLifecycleBackupIsExplicit()
    {
        string root = FindRoot();
        string production = ReadSources(Path.Combine(root, "HybridCPU_ISE"));
        string tests = ReadSources(Path.Combine(root, "HybridCPU_ISE.Tests"));
        string console = ReadSources(Path.Combine(root, "TestAssemblerConsoleApps"));
        string mutableFacadeSnapshotReturn = "Processor.CPU_Core GetCore" + "Snapshot";
        Assert.DoesNotContain(mutableFacadeSnapshotReturn, production, StringComparison.Ordinal);
        Assert.DoesNotContain(mutableFacadeSnapshotReturn, tests, StringComparison.Ordinal);
        Assert.DoesNotContain(mutableFacadeSnapshotReturn, console, StringComparison.Ordinal);
        Assert.DoesNotContain("CPU_Core coreSnapshot = GetCoreSnapshot", production, StringComparison.Ordinal);
        Assert.Contains("CpuCoreDiagnosticSnapshot GetCoreSnapshot", production, StringComparison.Ordinal);
        Assert.Contains("CpuCoreDiagnosticSnapshot GetCoreSnapshot", console, StringComparison.Ordinal);

        string matrix = Read(root, "TestAssemblerConsoleApps", "MatrixTileSpecSuite.cs");
        Assert.Contains("originalCoreLifecycleHandle = Processor.GetCoreRef(0);", matrix, StringComparison.Ordinal);
        Assert.Contains("Processor.ReplaceCore(0, originalCoreLifecycleHandle);", matrix, StringComparison.Ordinal);
        Assert.DoesNotContain("Processor.GetCoreSnapshot(0)", matrix, StringComparison.Ordinal);
    }

    [Fact]
    public void CaptureIsObservationOnlyAndFrozenCycleOrderIsUntouched()
    {
        string root = FindRoot();
        string capture = Read(root, "HybridCPU_ISE", "Machine", "IseObservationService.cs");
        Assert.DoesNotContain("GetCoreRef", capture, StringComparison.Ordinal);
        Assert.DoesNotContain("ReplaceCore", capture, StringComparison.Ordinal);
        Assert.DoesNotContain("ExecutePipelineCycle", capture, StringComparison.Ordinal);

        string stageFlow = Read(root, "HybridCPU_ISE", "CloseToHSL", "Core", "Pipeline",
            "ExecutionFlow", "StageFlow", "CPU_Core.PipelineExecution.StageFlow.cs");
        AssertOrder(stageFlow, "MemoryCyclePlatformOrchestrator.AdvanceCoreObservedPlatformEdge(",
            "RefreshInFlightExplicitMemoryProgress();", "PipelineStage_WriteBack();",
            "PipelineStage_Memory();", "PipelineStage_Execute();", "PipelineStage_Decode();",
            "PipelineStage_Fetch();");
    }

    [Fact]
    public void LedgerAndEvidenceCloseSnapshotOnlyAndDeferFacadeConversion()
    {
        string root = FindRoot();
        string ledger = Read(root, "Documentation", "ArchitectureAuthorityRefactor", "10_RF11", "00_CURRENT_STATUS_AND_LEDGER.md");
        string evidence = Read(root, "Documentation", "ArchitectureAuthorityRefactor", "Evidence", "RF11",
            "rf11.29-diagnostic-snapshot-hardening.md");
        Assert.Contains("RF-11.29 | closed diagnostic snapshot hardening", ledger, StringComparison.Ordinal);
        Assert.Contains("detached", evidence, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("RF-11.30 reference-facade conversion readiness", ledger, StringComparison.Ordinal);
        Assert.Contains("no reference-facade conversion", evidence, StringComparison.OrdinalIgnoreCase);
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
