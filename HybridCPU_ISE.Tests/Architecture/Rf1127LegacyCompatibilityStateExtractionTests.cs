using System.Reflection;
using System.Text.RegularExpressions;
using YAKSys_Hybrid_CPU;

namespace HybridCPU_ISE.Tests.Architecture;

public sealed class Rf1127LegacyCompatibilityStateExtractionTests
{
    private const BindingFlags Flags = BindingFlags.Instance | BindingFlags.Public |
        BindingFlags.NonPublic | BindingFlags.DeclaredOnly;
    private static readonly string[] Names = ["CycleCounter", "StageCycleCounter", "Stalled"];

    [Fact]
    public void RuntimeContainsExactStorageOnlyLegacyCompatibilityState()
    {
        Type runtime = Required("YAKSys_Hybrid_CPU.Core.CoreRuntimeState");
        Type legacy = Required("YAKSys_Hybrid_CPU.Core.LegacyCompatibilityState");
        Assert.Single(runtime.GetFields(Flags), field => field.FieldType == legacy);
        Assert.Equal(Names, legacy.GetFields(Flags).Select(field => field.Name));
        Assert.Equal(typeof(ulong), legacy.GetField("CycleCounter", Flags)?.FieldType);
        Assert.Equal(typeof(int), legacy.GetField("StageCycleCounter", Flags)?.FieldType);
        Assert.Equal(typeof(bool), legacy.GetField("Stalled", Flags)?.FieldType);
        Assert.DoesNotContain(legacy.GetMethods(Flags), method => method.Name is
            "AdvanceCycle" or "Execute" or "Commit" or "Rollback" or "Publish" or "Remove");
    }

    [Fact]
    public void LegacyDirectFieldsAreRemovedAndForwardByRef()
    {
        Type core = typeof(Processor.CPU_Core);
        foreach (string name in Names)
        {
            Assert.Null(core.GetField(name, Flags));
            Assert.True((core.GetProperty(name, Flags) ??
                throw new InvalidOperationException(name)).PropertyType.IsByRef);
        }
    }

    [Fact]
    public void ConstructionCopiesAndResetPreserveLegacySurface()
    {
#pragma warning disable CS0618
        var core = new Processor.CPU_Core(0);
#pragma warning restore CS0618
        Processor.CPU_Core copy = core;
        Assert.Same(core.Runtime.LegacyCompatibility, copy.Runtime.LegacyCompatibility);
        Assert.Equal(0UL, core.CycleCounter);
        Assert.Equal(0, core.StageCycleCounter);
        Assert.False(core.Stalled);

        core.CycleCounter = 81;
        core.StageCycleCounter = 9;
        core.Stalled = true;
        Assert.Equal(81UL, copy.CycleCounter);
        Assert.Equal(9, copy.StageCycleCounter);
        Assert.True(copy.Stalled);
        copy.ResetCycleCounter();
        Assert.Equal(0UL, core.CycleCounter);
        Assert.Equal(0, core.StageCycleCounter);
        Assert.True(core.Stalled);
    }

    [Fact]
    public void ExistingInitializationAndResetScopeRemainTextuallyFrozen()
    {
        string root = FindRoot();
        string state = Read(root, "HybridCPU_ISE", "CloseToHSL", "Core", "Architecture", "State",
            "Architectural", "CPU_Core.StateData.cs");
        AssertOrder(state, "this.CycleCounter = 0;", "this.StageCycleCounter = 0;", "this.Stalled = false;");
        AssertOrder(state, "private void ResetFreshExecutionTransientState()", "Stalled = false;",
            "ResetVirtualThreadPipelineStates();");
        AssertOrder(state, "private void ResetFreshExecutionPipelineRuntimeState()", "FlushPipeline();",
            "InitializePipeline();", "ResetCycleCounter();");

        string fsm = Read(root, "HybridCPU_ISE", "CloseToHSL", "Core", "Architecture", "State",
            "Pipeline", "CPU_Core.FSM.cs");
        AssertOrder(fsm, "public void ResetCycleCounter()", "CycleCounter = 0;", "StageCycleCounter = 0;");
        Assert.DoesNotContain("Stalled = false;", fsm, StringComparison.Ordinal);
    }

    [Fact]
    public void ModernPipelineTelemetryAndSchedulerCountersRemainSeparate()
    {
        Type legacy = Required("YAKSys_Hybrid_CPU.Core.LegacyCompatibilityState");
        Assert.DoesNotContain(legacy.GetFields(Flags), field =>
            field.FieldType.Name.Contains("PipelineControl", StringComparison.Ordinal) ||
            field.FieldType.Name.Contains("Telemetry", StringComparison.Ordinal) ||
            field.FieldType.Name.Contains("Scheduler", StringComparison.Ordinal));

#pragma warning disable CS0618
        var core = new Processor.CPU_Core(0);
#pragma warning restore CS0618
        core.Stalled = true;
        Assert.False(core.Runtime.Execution.Control.Stalled);
        core.Runtime.Execution.Control.Stalled = true;
        Assert.True(core.Stalled);
    }

    [Fact]
    public void ClosedWorldCpuCoreWriterAndMutationSeamsRemainExplicit()
    {
        string root = FindRoot();
        string production = ReadSources(Path.Combine(root, "HybridCPU_ISE"));
        string owners = Read(root, "HybridCPU_ISE", "CloseToHSL", "Core", "Architecture", "State",
                "Architectural", "CPU_Core.StateData.cs") +
            Read(root, "HybridCPU_ISE", "CloseToHSL", "Core", "Architecture", "State",
                "Pipeline", "CPU_Core.FSM.cs");
        Assert.Equal(2, Regex.Matches(owners, @"\b(?:this\.)?CycleCounter\s*=").Count);
        Assert.Equal(2, Regex.Matches(owners, @"\b(?:this\.)?StageCycleCounter\s*=").Count);
        Assert.Equal(2, Regex.Matches(owners, @"\b(?:this\.)?Stalled\s*=").Count);
        Assert.Contains("GlobalCycleCounter++", production, StringComparison.Ordinal);
        Assert.Contains("pipeCtrl.Stalled = true", production, StringComparison.Ordinal);

        string[] mutationBypasses = Directory.GetFiles(Path.Combine(root, "HybridCPU_ISE.Tests"), "*.cs", SearchOption.AllDirectories)
            .Where(file => !file.EndsWith(nameof(Rf1127LegacyCompatibilityStateExtractionTests) + ".cs", StringComparison.Ordinal))
            .Where(file => !file.EndsWith("Rf1116ResidualOwnerAndFacadeReadinessTests.cs", StringComparison.Ordinal))
            .Where(file => !file.EndsWith("Rf1126VirtualThreadControlStateExtractionTests.cs", StringComparison.Ordinal))
            .Where(file =>
            {
                string text = File.ReadAllText(file);
                return Names.Any(name => text.Contains(name, StringComparison.Ordinal)) &&
                    text.Contains("SetValueDirect", StringComparison.Ordinal);
            })
            .ToArray();
        Assert.Empty(mutationBypasses);
        Assert.DoesNotContain("JsonSerializer.Serialize(core.Runtime.LegacyCompatibility", production, StringComparison.Ordinal);
    }

    [Fact]
    public void LedgerAndEvidenceCloseContainmentWithoutRf13Removal()
    {
        string root = FindRoot();
        string ledger = Read(root, "Documentation", "Documentation", "ArchitectureAuthorityRefactor", "10_RF11", "00_CURRENT_STATUS_AND_LEDGER.md");
        string evidence = Read(root, "Documentation", "Documentation", "ArchitectureAuthorityRefactor", "Evidence", "RF11",
            "rf11.27-legacy-compatibility-state-extraction.md");
        Assert.Contains("RF-11.27 | closed LegacyCompatibilityState", ledger, StringComparison.Ordinal);
        Assert.Contains("exactly three", evidence, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("RF-11.28 CoreBindingState", ledger, StringComparison.Ordinal);
        Assert.Contains("RF-13", evidence, StringComparison.Ordinal);
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
    private static string ReadSources(string path) => string.Join('\n', Directory.GetFiles(path, "*.cs", SearchOption.AllDirectories).Select(File.ReadAllText));
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
