using System.Reflection;
using YAKSys_Hybrid_CPU;
using YAKSys_Hybrid_CPU.Core;

namespace HybridCPU_ISE.Tests.Architecture;

public sealed class Rf1126VirtualThreadControlStateExtractionTests
{
    private const BindingFlags Flags = BindingFlags.Instance | BindingFlags.Public |
        BindingFlags.NonPublic | BindingFlags.DeclaredOnly;

    private static readonly string[] LegacyNames =
        ["IsVMXRoot", "VirtualThreadPipelineStates", "_vmxExecutionPlaneWired"];

    [Fact]
    public void RuntimeContainsExactStorageOnlyVirtualThreadControlState()
    {
        Type runtime = Required("YAKSys_Hybrid_CPU.Core.CoreRuntimeState");
        Type control = Required("YAKSys_Hybrid_CPU.Core.VirtualThreadControlState");
        Assert.Single(runtime.GetFields(Flags), field => field.FieldType == control);
        Assert.Equal(new[] { "IsVmxRoot", "PipelineStates", "VmxExecutionPlaneWired" },
            control.GetFields(Flags).Select(field => field.Name).OrderBy(name => name, StringComparer.Ordinal));
        Assert.Equal(2, control.GetFields(Flags).Count(field => field.FieldType == typeof(bool)));
        Assert.Single(control.GetFields(Flags), field => field.FieldType == typeof(PipelineState[]));
        Assert.DoesNotContain(control.GetMethods(Flags), method => method.Name is
            "Execute" or "Commit" or "Rollback" or "Publish" or "Transition" or "Migrate");
    }

    [Fact]
    public void LegacyDirectFieldsAreRemovedAndForwardByRef()
    {
        Type core = typeof(Processor.CPU_Core);
        foreach (string name in LegacyNames)
        {
            Assert.Null(core.GetField(name, Flags));
            Assert.True((core.GetProperty(name, Flags) ??
                throw new InvalidOperationException(name)).PropertyType.IsByRef);
        }
    }

    [Fact]
    public void ConstructionAndCopiesPreserveVtControlIdentityAndDefaults()
    {
#pragma warning disable CS0618
        var core = new Processor.CPU_Core(0);
#pragma warning restore CS0618
        Processor.CPU_Core copy = core;
        Assert.Same(core.Runtime.VirtualThreadControl, copy.Runtime.VirtualThreadControl);
        Assert.False(core.IsVMXRoot);
        Assert.True(core.HasWiredVmxExecutionPlane);
        Assert.Equal(Processor.CPU_Core.SmtWays, core.VirtualThreadPipelineStates.Length);
        Assert.All(core.VirtualThreadPipelineStates, state => Assert.Equal(PipelineState.Task, state));

        core.IsVMXRoot = true;
        Assert.True(copy.IsVMXRoot);
        copy.WriteVirtualThreadPipelineState(2, PipelineState.WaitForEvent);
        Assert.Equal(PipelineState.WaitForEvent, core.ReadVirtualThreadPipelineState(2));
        copy.SetVmxExecutionPlaneWiredForTesting(false);
        Assert.False(core.HasWiredVmxExecutionPlane);
    }

    [Fact]
    public void ExistingInitializationResetAndGuardedTransitionOrderRemainInPlace()
    {
        string root = FindRoot();
        string state = Read(root, "HybridCPU_ISE", "CloseToHSL", "Core", "Architecture", "State",
            "Architectural", "CPU_Core.StateData.cs");
        AssertOrder(state, "this.IsVMXRoot = false;", "this._vmxExecutionPlaneWired = true;",
            "this.VirtualThreadPipelineStates = new PipelineState[SmtWays];",
            "this.VirtualThreadPipelineStates[vt] = PipelineState.Task;");
        AssertOrder(state, "private void ResetVirtualThreadPipelineStates()",
            "EnsureVirtualThreadPipelineStatesInitialized();",
            "VirtualThreadPipelineStates[vt] = PipelineState.Task;");

        string ownership = Read(root, "HybridCPU_ISE", "CloseToHSL", "Core", "Architecture", "State",
            "Architectural", "CPU_Core.StateData.RuntimeOwnership.cs");
        AssertOrder(ownership, "internal void ApplyVirtualThreadPipelineTransition(",
            "PipelineState currentState = ReadVirtualThreadPipelineState(normalizedVtId);",
            "PipelineFsmGuard.Transition(currentState, trigger)");
        Assert.Contains("ApplyRetiredVmxPipelineStateOwnership", ownership, StringComparison.Ordinal);
        Assert.Contains("PipelineTransitionTrigger.VmLaunch", ownership, StringComparison.Ordinal);
        Assert.Contains("PipelineTransitionTrigger.VmxOff", ownership, StringComparison.Ordinal);
    }

    [Fact]
    public void ClosedWorldLegacyNameInventoryHasNoReflectionSerializationOrTestBypass()
    {
        string root = FindRoot();
        string production = Path.Combine(root, "HybridCPU_ISE");
        string[] expected =
        [
            Path.Combine("CloseToHSL", "Core", "Architecture", "State", "Architectural", "CPU_Core.StateData.cs"),
            Path.Combine("CloseToHSL", "Core", "Frontend", "Decode", "CPU_Core.ReplayDecodeContext.cs"),
            Path.Combine("CloseToHSL", "Core", "State", "CPU_Core.RuntimeState.cs"),
            Path.Combine("Machine", "IseObservationService.cs")
        ];
        string[] actual = Directory.GetFiles(production, "*.cs", SearchOption.AllDirectories)
            .Where(file => LegacyNames.Any(name => (File.ReadAllText(file) ?? string.Empty).Contains(name, StringComparison.Ordinal)))
            .Select(file => file[(production.Length + 1)..])
            .OrderBy(file => file, StringComparer.Ordinal)
            .ToArray();
        Assert.Equal(expected.OrderBy(file => file, StringComparer.Ordinal), actual);

        string[] bypasses = Directory.GetFiles(Path.Combine(root, "HybridCPU_ISE.Tests"), "*.cs", SearchOption.AllDirectories)
            .Where(file => !file.EndsWith(nameof(Rf1126VirtualThreadControlStateExtractionTests) + ".cs", StringComparison.Ordinal))
            .Where(file => !file.EndsWith("Rf1116ResidualOwnerAndFacadeReadinessTests.cs", StringComparison.Ordinal))
            .Where(file =>
            {
                string text = File.ReadAllText(file);
                return LegacyNames.Any(name => System.Text.RegularExpressions.Regex.IsMatch(
                        text, $@"\b{System.Text.RegularExpressions.Regex.Escape(name)}\b")) &&
                    (text.Contains("SetValueDirect", StringComparison.Ordinal) ||
                     text.Contains("JsonSerializer.Serialize", StringComparison.Ordinal));
            })
            .ToArray();
        Assert.Empty(bypasses);
    }

    [Fact]
    public void VmxNeutralVirtualizationRetireReplayAndLegacyCountersRemainSeparate()
    {
        Type control = Required("YAKSys_Hybrid_CPU.Core.VirtualThreadControlState");
        Assert.DoesNotContain(control.GetFields(Flags), field =>
            field.FieldType.Name.Contains("Vmcs", StringComparison.OrdinalIgnoreCase) ||
            field.FieldType.Name.Contains("Virtualization", StringComparison.OrdinalIgnoreCase) ||
            field.FieldType.Name.Contains("Retire", StringComparison.OrdinalIgnoreCase) ||
            field.FieldType.Name.Contains("Replay", StringComparison.OrdinalIgnoreCase));
        Type core = typeof(Processor.CPU_Core);
        foreach (string name in new[] { "CycleCounter", "StageCycleCounter", "Stalled" })
            Assert.True((core.GetProperty(name, Flags) ?? throw new InvalidOperationException(name)).PropertyType.IsByRef);
    }

    [Fact]
    public void LedgerAndEvidenceCloseOnlyVirtualThreadControlStorage()
    {
        string root = FindRoot();
        string ledger = Read(root, "Documentation", "Documentation", "ArchitectureAuthorityRefactor", "10_RF11", "00_CURRENT_STATUS_AND_LEDGER.md");
        string evidence = Read(root, "Documentation", "Documentation", "ArchitectureAuthorityRefactor", "Evidence", "RF11",
            "rf11.26-virtual-thread-control-state-extraction.md");
        Assert.Contains("RF-11.26 | closed VirtualThreadControlState", ledger, StringComparison.Ordinal);
        Assert.Contains("exactly three", evidence, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("RF-11.27 LegacyCompatibilityState", ledger, StringComparison.Ordinal);
    }

    private static Type Required(string name) => typeof(Processor.CPU_Core).Assembly.GetType(name) ??
        throw new InvalidOperationException(name);
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
    private static string Read(string root, params string[] parts) =>
        File.ReadAllText(parts.Aggregate(root, Path.Combine));
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
