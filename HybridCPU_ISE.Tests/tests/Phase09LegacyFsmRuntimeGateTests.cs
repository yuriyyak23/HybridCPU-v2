using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using HybridCPU_ISE.Tests.TestHelpers;
using Xunit;
using YAKSys_Hybrid_CPU;
using YAKSys_Hybrid_CPU.Core;

namespace HybridCPU_ISE.Tests.Phase09;

public sealed class Phase09LegacyFsmRuntimeGateTests
{
    private static readonly string[] RuntimeRoots =
    [
        Path.Combine("HybridCPU_ISE", "Core"),
        Path.Combine("HybridCPU_ISE", "Processor"),
        Path.Combine("CpuInterfaceBridge"),
        Path.Combine("HybridCPU_EnvGUI"),
        Path.Combine("forms"),
        Path.Combine("HybridCPU_ISE", "IseObservationService.cs"),
    ];

    private static readonly string[] LegacyProcessorStateSurfacePatterns =
    [
        "Processor.CPU_Core.ProcessorState",
        "CPU_Core.ProcessorState",
        "ReadLegacyProcessorStateSnapshot(",
        "ResolveLegacyForegroundProcessorStateSnapshot(",
        "CurrentProcessorState",
    ];

    [Fact]
    public void GetCoreState_UsesPerVtPipelineStateProjection()
    {
        var core = new Processor.CPU_Core(0);
        core.PrepareExecutionStart(0x9000, activeVtId: 1);
        core.ActiveVirtualThreadId = 1;

        core.WriteVirtualThreadPipelineState(1, PipelineState.Task);
        CoreStateSnapshot taskSnapshot =
            ObservationServiceTestFactory.CreateSingleCoreService(core).GetCoreState(0);
        Assert.Equal(PipelineState.Task.ToString(), taskSnapshot.CurrentState);

        core.WriteVirtualThreadPipelineState(1, PipelineState.ClockGatedDonor);

        IseObservationService gatedService = ObservationServiceTestFactory.CreateSingleCoreService(core);
        CoreStateSnapshot gatedSnapshot = gatedService.GetCoreState(0);
        FspPowerSnapshot powerSnapshot = gatedService.GetFspPowerSnapshot(0);

        Assert.Equal(PipelineState.ClockGatedDonor.ToString(), gatedSnapshot.CurrentState);
        Assert.True(powerSnapshot.IsClockGated);
    }

    [Fact]
    public void GetPodBarrierState_WhenPerVtBarrierStateIsPublished_UsesPipelineState()
    {
        var core = new Processor.CPU_Core(0);
        core.PrepareExecutionStart(0x9100, activeVtId: 2);
        core.ActiveVirtualThreadId = 2;
        core.WriteVirtualThreadPipelineState(2, PipelineState.WaitForClusterSync);
        IseObservationService service = ObservationServiceTestFactory.CreateSingleCoreService(core);

        bool barrierState = service.GetPodBarrierState(0);

        Assert.True(barrierState);
    }

    [Fact]
    public void CallInterrupt_WhenPipelineStateIsTaskAndNoInterruptFrame_StillRoutesHandler()
    {
        const ushort interruptId = 0x234;
        const ulong handlerAddress = 0x5000;
        const ulong startPc = 0x440;

        ProcessorMode originalMode = Processor.CurrentProcessorMode;
        bool hadPreviousHandler = Processor.InterruptData.GetHandler(
            interruptId,
            out ulong previousHandlerAddress,
            out byte previousPriority);

        try
        {
            _ = new Processor(ProcessorMode.Emulation);
            ref Processor.CPU_Core core = ref Processor.CPU_Cores[0];
            core.PrepareExecutionStart(startPc, activeVtId: 0);
            core.ActiveVirtualThreadId = 0;
            core.WriteVirtualThreadPipelineState(0, PipelineState.Task);

            Assert.True(Processor.InterruptData.RegisterHandler(interruptId, handlerAddress, priority: 7));

            byte result = Processor.InterruptData.CallInterrupt(
                Processor.DeviceType.Timer,
                interruptId,
                core.CoreID);

            Assert.Equal(0, result);
            Assert.Equal(handlerAddress, core.ReadActiveLivePc());
            Assert.Equal(PipelineState.Task, core.ReadVirtualThreadPipelineState(0));
            Assert.Equal(startPc + 32, core.Pop_Interrupt_EntryPoint_Address());
        }
        finally
        {
            if (hadPreviousHandler)
            {
                Assert.True(Processor.InterruptData.RegisterHandler(
                    interruptId,
                    previousHandlerAddress,
                    previousPriority));
            }
            else
            {
                Assert.True(Processor.InterruptData.UnregisterHandler(interruptId));
            }

            Processor.CurrentProcessorMode = originalMode;
        }
    }

    [Fact]
    public void ProcessorStateLegacySurface_NoRuntimeUsesRemain()
    {
        string repoRoot = FindRepoRoot();
        string[] violations = ScanRuntimeFiles(repoRoot, LegacyProcessorStateSurfacePatterns, allowList: []);

        Assert.True(
            violations.Length == 0,
            "Legacy ProcessorState surface remains in the runtime contour.\n" +
            string.Join("\n", violations));
    }

    [Fact]
    public void TickAndAdvancePipelineState_NoProductionCallSitesRemain()
    {
        string repoRoot = FindRepoRoot();
        string[] violations = ScanRuntimeFiles(
            repoRoot,
            [".Tick(", ".AdvancePipelineState("],
            allowList: []);

        Assert.True(
            violations.Length == 0,
            "Production contour still calls legacy Tick()/AdvancePipelineState().\n" +
            string.Join("\n", violations));
    }

    [Fact]
    public void TickAndAdvancePipelineState_MethodsAreAbsentFromCpuCoreAssembly()
    {
        BindingFlags flags =
            BindingFlags.Instance |
            BindingFlags.Static |
            BindingFlags.Public |
            BindingFlags.NonPublic;

        Assert.Null(typeof(Processor.CPU_Core).GetMethod("Tick", flags));
        Assert.Null(typeof(Processor.CPU_Core).GetMethod("AdvancePipelineState", flags));
    }

    [Fact]
    public void HybridCpuAssembly_ExportsNoPublicTestHooks()
    {
        BindingFlags flags =
            BindingFlags.Instance |
            BindingFlags.Static |
            BindingFlags.Public |
            BindingFlags.DeclaredOnly;

        Assembly assembly = typeof(Processor.CPU_Core).Assembly;
        string[] violations = assembly
            .GetTypes()
            .Where(type => type.IsVisible)
            .SelectMany(type => GetPublicTestHookViolations(type, flags))
            .Distinct(StringComparer.Ordinal)
            .OrderBy(violation => violation, StringComparer.Ordinal)
            .ToArray();

        Assert.True(
            violations.Length == 0,
            "HybridCPU_ISE still exports public Test* hooks.\n" +
            string.Join("\n", violations));
    }

    [Fact]
    public void TestSupportFiles_ContainNoPublicMemberDeclarations()
    {
        string repoRoot = FindRepoRoot();
        string hybridCpuRoot = Path.Combine(repoRoot, "HybridCPU_ISE");
        var violations = new List<string>();

        foreach (string filePath in Directory.EnumerateFiles(
                     hybridCpuRoot,
                     "*.TestSupport.cs",
                     SearchOption.AllDirectories))
        {
            if (IsGeneratedPath(filePath))
            {
                continue;
            }

            string relativePath = Path.GetRelativePath(repoRoot, filePath);
            string[] lines = File.ReadAllLines(filePath);
            for (int i = 0; i < lines.Length; i++)
            {
                string trimmed = lines[i].TrimStart();
                if (!trimmed.StartsWith("public ", StringComparison.Ordinal))
                {
                    continue;
                }

                if (trimmed.StartsWith("public partial class ", StringComparison.Ordinal) ||
                    trimmed.StartsWith("public partial struct ", StringComparison.Ordinal))
                {
                    continue;
                }

                violations.Add($"{relativePath}:{i + 1}: {trimmed}");
            }
        }

        Assert.True(
            violations.Count == 0,
            "Test-support files still export public member declarations.\n" +
            string.Join("\n", violations));
    }

    [Fact]
    public void ProcessorStateLegacyMembers_AreAbsentFromCpuCoreAssembly()
    {
        BindingFlags flags =
            BindingFlags.Instance |
            BindingFlags.Static |
            BindingFlags.Public |
            BindingFlags.NonPublic;

        Assert.Null(typeof(Processor.CPU_Core).GetNestedType("ProcessorState", flags));
        Assert.Null(typeof(Processor.CPU_Core).GetField("CurrentState", flags));
        Assert.Null(typeof(Processor.CPU_Core).GetMethod("ReadLegacyProcessorStateSnapshot", flags));
        Assert.Null(typeof(Processor.CPU_Core).GetMethod("ResolveLegacyForegroundProcessorStateSnapshot", flags));

        Type? observationSnapshotType =
            typeof(Processor.CPU_Core).GetNestedType("PipelineObservationSnapshot", flags);

        Assert.NotNull(observationSnapshotType);
        Assert.Null(observationSnapshotType!.GetProperty("CurrentProcessorState", flags));
    }

    [Fact]
    public void RawPipelineStatePublishers_NoRuntimeCallersRemainOutsideStateData()
    {
        string repoRoot = FindRepoRoot();
        var allowList = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            Path.Combine("HybridCPU_ISE", "Core", "State", "CPU_Core.StateData.cs"),
            Path.Combine("HybridCPU_ISE", "Core", "State", "CPU_Core.StateData.RuntimeOwnership.cs"),
        };

        string[] violations = ScanRuntimeFiles(
            repoRoot,
            [".WriteVirtualThreadPipelineState("],
            allowList);

        Assert.True(
            violations.Length == 0,
            "Runtime contour still calls WriteVirtualThreadPipelineState(...) directly outside core-owned state helpers.\n" +
            string.Join("\n", violations));
    }

    [Fact]
    public void ResolveFinalPipelineState_NoRuntimeCallersRemainOutsideVmxExecutionUnit()
    {
        string repoRoot = FindRepoRoot();
        var allowList = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            Path.Combine("HybridCPU_ISE", "Core", "System", "VmxExecutionUnit.cs"),
        };

        string[] violations = ScanRuntimeFiles(
            repoRoot,
            ["ResolveFinalPipelineState("],
            allowList);

        Assert.True(
            violations.Length == 0,
            "Runtime contour still depends on VmxExecutionUnit.ResolveFinalPipelineState(...) instead of guarded core-owned FSM application.\n" +
            string.Join("\n", violations));
    }

    [Fact]
    public void LiveCpuStateAdapter_NoLongerOwnsDirectPcOrFsmPublication()
    {
        string repoRoot = FindRepoRoot();
        string filePath = Path.Combine(
            repoRoot,
            "HybridCPU_ISE",
            "Core",
            "State",
            "LiveCpuStateAdapter.cs");

        string[] lines = File.ReadAllLines(filePath);
        var violations = new List<string>();

        string[] forbiddenPatterns =
        [
            ".WriteVirtualThreadPipelineState(",
            ".WriteActiveLivePc(",
            "RetireRecord.PcWrite(",
        ];

        for (int i = 0; i < lines.Length; i++)
        {
            string line = lines[i];
            foreach (string pattern in forbiddenPatterns)
            {
                if (line.Contains(pattern, StringComparison.Ordinal))
                {
                    violations.Add($"{Path.GetRelativePath(repoRoot, filePath)}:{i + 1}: {line.Trim()}");
                }
            }
        }

        Assert.True(
            violations.Count == 0,
            "LiveCpuStateAdapter still owns direct FSM or PC publication instead of core-owned helpers.\n" +
            string.Join("\n", violations));
    }

    [Fact]
    public void ExecuteBranchHelpers_NoLongerInvokeDirectPipelineRedirect()
    {
        string repoRoot = FindRepoRoot();
        string filePath = Path.Combine(
            repoRoot,
            "HybridCPU_ISE",
            "Core",
            "Pipeline",
            "Core",
            "CPU_Core.PipelineExecution.ControlFlow.cs");

        string[] lines = File.ReadAllLines(filePath);
        var redirectMentions = new List<string>();

        for (int i = 0; i < lines.Length; i++)
        {
            string line = lines[i];
            if (!line.Contains("ApplyPipelineControlFlowRedirect(", StringComparison.Ordinal))
            {
                continue;
            }

            redirectMentions.Add($"{Path.GetRelativePath(repoRoot, filePath)}:{i + 1}: {line.Trim()}");
        }

        Assert.True(
            redirectMentions.Count == 1,
            "Execute control-flow contour still contains direct redirect call-sites instead of a single helper declaration.\n" +
            string.Join("\n", redirectMentions));
    }

    private static string[] ScanRuntimeFiles(
        string repoRoot,
        string[] patterns,
        HashSet<string> allowList)
    {
        var violations = new List<string>();

        foreach (string runtimeRoot in RuntimeRoots)
        {
            string fullPath = Path.Combine(repoRoot, runtimeRoot);
            if (Directory.Exists(fullPath))
            {
                foreach (string filePath in Directory.EnumerateFiles(fullPath, "*.cs", SearchOption.AllDirectories))
                {
                    ScanFile(filePath, repoRoot, patterns, allowList, violations);
                }

                continue;
            }

            if (File.Exists(fullPath))
            {
                ScanFile(fullPath, repoRoot, patterns, allowList, violations);
            }
        }

        return violations.ToArray();
    }

    private static void ScanFile(
        string filePath,
        string repoRoot,
        string[] patterns,
        HashSet<string> allowList,
        List<string> violations)
    {
        if (IsGeneratedPath(filePath))
        {
            return;
        }

        string relativePath = Path.GetRelativePath(repoRoot, filePath);
        if (allowList.Contains(relativePath))
        {
            return;
        }

        string[] lines = File.ReadAllLines(filePath);
        for (int i = 0; i < lines.Length; i++)
        {
            string line = lines[i];
            foreach (string pattern in patterns)
            {
                if (line.Contains(pattern, StringComparison.Ordinal))
                {
                    violations.Add($"{relativePath}:{i + 1}: {line.Trim()}");
                }
            }
        }
    }

    private static IEnumerable<string> GetPublicTestHookViolations(Type type, BindingFlags flags)
    {
        if (type.Name.StartsWith("Test", StringComparison.Ordinal))
        {
            yield return $"{type.FullName} (type)";
        }

        foreach (MethodInfo method in type.GetMethods(flags))
        {
            if (!method.IsSpecialName && method.Name.StartsWith("Test", StringComparison.Ordinal))
            {
                yield return $"{type.FullName}.{method.Name}()";
            }
        }

        foreach (PropertyInfo property in type.GetProperties(flags))
        {
            if (property.Name.StartsWith("Test", StringComparison.Ordinal))
            {
                yield return $"{type.FullName}.{property.Name}";
            }
        }

        foreach (FieldInfo field in type.GetFields(flags))
        {
            if (field.Name.StartsWith("Test", StringComparison.Ordinal))
            {
                yield return $"{type.FullName}.{field.Name}";
            }
        }

        foreach (EventInfo eventInfo in type.GetEvents(flags))
        {
            if (eventInfo.Name.StartsWith("Test", StringComparison.Ordinal))
            {
                yield return $"{type.FullName}.{eventInfo.Name}";
            }
        }
    }

    private static bool IsGeneratedPath(string filePath) =>
        filePath.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.Ordinal) ||
        filePath.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.Ordinal);

    private static string FindRepoRoot()
    {
        DirectoryInfo? current = new DirectoryInfo(AppContext.BaseDirectory);
        while (current is not null)
        {
            bool hasRepoLayout =
                Directory.Exists(Path.Combine(current.FullName, "Documentation")) &&
                Directory.Exists(Path.Combine(current.FullName, "HybridCPU_ISE")) &&
                Directory.Exists(Path.Combine(current.FullName, "HybridCPU_ISE.Tests"));
            if (hasRepoLayout)
            {
                return current.FullName;
            }

            current = current.Parent;
        }

        throw new DirectoryNotFoundException(
            "Could not locate HybridCPU ISE repository root from test output directory.");
    }
}
