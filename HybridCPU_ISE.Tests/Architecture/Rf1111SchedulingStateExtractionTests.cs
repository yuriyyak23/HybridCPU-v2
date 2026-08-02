using System.Reflection;
using YAKSys_Hybrid_CPU;

namespace HybridCPU_ISE.Tests.Architecture;

public sealed class Rf1111SchedulingStateExtractionTests
{
    private const BindingFlags Flags = BindingFlags.Instance | BindingFlags.Public |
        BindingFlags.NonPublic | BindingFlags.DeclaredOnly;

    [Fact]
    public void RuntimeContainsExactSchedulingBindingAndVtSelectionContour()
    {
        Type runtime = Required("YAKSys_Hybrid_CPU.Core.CoreRuntimeState");
        Type scheduling = Required("YAKSys_Hybrid_CPU.Core.SchedulingState");
        Assert.Single(runtime.GetFields(Flags), field => field.FieldType == scheduling);
        Assert.Equal(new[] { "ActiveVirtualThreadId", "Scheduler", "VirtualThreadStalled" },
            scheduling.GetFields(Flags).Select(field => field.Name).OrderBy(name => name, StringComparer.Ordinal));
        Assert.Empty(scheduling.GetMethods(Flags).Where(method =>
            method.Name is "Schedule" or "Issue" or "Execute" or "Commit" or "Rollback"));
    }

    [Fact]
    public void LegacyStoragesAreRemovedAndForwardersAreByRef()
    {
        Type core = typeof(Processor.CPU_Core);
        foreach (string name in new[] { "_fspScheduler", "VirtualThreadStalled", "ActiveVirtualThreadId" })
        {
            Assert.Null(core.GetField(name, Flags));
            Assert.True((core.GetProperty(name, Flags) ?? throw new InvalidOperationException(name)).PropertyType.IsByRef);
        }
    }

    [Fact]
    public void FacadeCopiesAliasSchedulerBindingAndVtSelectionState()
    {
#pragma warning disable CS0618
        var core = new Processor.CPU_Core(0);
#pragma warning restore CS0618
        core.InitializePipeline();
        Processor.CPU_Core copy = core;
        copy.ActiveVirtualThreadId = 3;
        copy.VirtualThreadStalled[2] = true;

        Assert.Same(core.Runtime.Scheduling, copy.Runtime.Scheduling);
        Assert.Same(core.Runtime.Scheduling.Scheduler, copy.Runtime.Scheduling.Scheduler);
        Assert.Equal(3, core.ReadActiveVirtualThreadId());
        Assert.True(core.VirtualThreadStalled[2]);
    }

    [Fact]
    public void PodBindingFallbackAndFspOwnerChecksRemainExact()
    {
        string root = FindRoot();
        string pipeline = Read(root, "HybridCPU_ISE", "CloseToHSL", "Core", "Pipeline", "Stages", "Issue", "CPU_Core.Pipeline.cs");
        string fsp = Read(root, "HybridCPU_ISE", "CloseToHSL", "Core", "Pipeline", "Scheduling", "Fsp", "CPU_Core.PipelineExecution.Fsp.cs");
        Assert.Contains("_fspScheduler = pod?.Scheduler ?? new Core.MicroOpScheduler();", pipeline, StringComparison.Ordinal);
        Assert.Contains("if (!object.ReferenceEquals(podScheduler, _fspScheduler))", fsp, StringComparison.Ordinal);
        Assert.Contains("for (int vt = 0; vt < SmtWays; vt++)", fsp, StringComparison.Ordinal);
    }

    [Fact]
    public void SchedulerInternalsAndAdjacentAuthoritiesRemainOutsideContainer()
    {
        Type scheduling = Required("YAKSys_Hybrid_CPU.Core.SchedulingState");
        Assert.DoesNotContain(scheduling.GetFields(Flags), field =>
            field.Name.Contains("Queue", StringComparison.Ordinal) ||
            field.Name.Contains("Scoreboard", StringComparison.Ordinal) ||
            field.Name.Contains("Mshr", StringComparison.Ordinal) ||
            field.Name.Contains("Replay", StringComparison.Ordinal) ||
            field.Name.Contains("Assist", StringComparison.Ordinal));

        Type core = typeof(Processor.CPU_Core);
        Assert.True((core.GetProperty("pipeEX", Flags) ?? throw new InvalidOperationException("pipeEX")).PropertyType.IsByRef);
        Assert.True((core.GetProperty("pipeMEM", Flags) ?? throw new InvalidOperationException("pipeMEM")).PropertyType.IsByRef);
        Assert.True((core.GetProperty("pipeWB", Flags) ?? throw new InvalidOperationException("pipeWB")).PropertyType.IsByRef);
        foreach (string name in new[] { "PhysicalRegisters", "ArchRenameMap", "ArchCommitMap", "PhysRegFreeList" })
            Assert.True((core.GetProperty(name, Flags) ?? throw new InvalidOperationException(name)).PropertyType.IsByRef);
    }

    [Fact]
    public void LedgerAndEvidenceCloseOnlySchedulingState()
    {
        string root = FindRoot();
        string ledger = Read(root, "Documentation", "ArchitectureAuthorityRefactor", "10_RF11", "00_CURRENT_STATUS_AND_LEDGER.md");
        string evidence = Read(root, "Documentation", "ArchitectureAuthorityRefactor", "Evidence", "RF11", "rf11.11-scheduling-state-extraction.md");
        Assert.Contains("RF-11.11 | closed SchedulingState", ledger, StringComparison.Ordinal);
        Assert.Contains("RF-11.12 ExecutionState", ledger, StringComparison.Ordinal);
        Assert.Contains("pod-shared", evidence, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("VT0...VT3", evidence, StringComparison.Ordinal);
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
