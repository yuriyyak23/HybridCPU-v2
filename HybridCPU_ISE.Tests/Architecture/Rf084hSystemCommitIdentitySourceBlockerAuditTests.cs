namespace HybridCPU_ISE.Tests.Architecture;

/// <summary>
/// RF-08.4h freezes the SystemCommit source boundary without conflating the
/// mainline typed System effect with direct PipelineEventPublication.
/// </summary>
public sealed class Rf084hSystemCommitIdentitySourceBlockerAuditTests
{
    [Fact]
    public void MainlineSystemEffectPayloadHasNoIssuedAttemptProvenance()
    {
        string root = FindRepositoryRoot();
        string types = Read(root, "HybridCPU_ISE", "CloseToHSL", "Core", "Pipeline", "ExecutionFlow", "StageFlow", "CPU_Core.PipelineExecution.Types.cs");
        string systemFactory = Slice(
            types,
            "public static RetireWindowEffect System(",
            "public static RetireWindowEffect PipelineEvent(");

        Assert.Contains("Core.SystemEventKind systemEventKind", systemFactory, StringComparison.Ordinal);
        Assert.Contains("Core.SystemEventOrderGuarantee orderGuarantee", systemFactory, StringComparison.Ordinal);
        Assert.Contains("ulong retiredPc", systemFactory, StringComparison.Ordinal);
        Assert.Contains("int virtualThreadId", systemFactory, StringComparison.Ordinal);
        Assert.DoesNotContain("ScheduledOperation", systemFactory, StringComparison.Ordinal);
        Assert.DoesNotContain("VliwOperationId", systemFactory, StringComparison.Ordinal);
        Assert.DoesNotContain("GeneratedStaticBinding", systemFactory, StringComparison.Ordinal);
        Assert.DoesNotContain("SourceOperationProvenance", systemFactory, StringComparison.Ordinal);
        Assert.DoesNotContain("WorkingBundleSequence", systemFactory, StringComparison.Ordinal);
    }

    [Fact]
    public void MainlineSysEventUsesGeneratedEventSidebandAndTypedSystemCapture()
    {
        string root = FindRepositoryRoot();
        string microOp = Read(root, "HybridCPU_ISE", "CloseToHSL", "Core", "Pipeline", "MicroOps", "System", "MicroOp.System.cs");
        string execute = Read(root, "HybridCPU_ISE", "CloseToHSL", "Core", "Pipeline", "ExecutionFlow", "Materialization", "CPU_Core.PipelineExecution.ExecuteHelpers.cs");
        string materialization = Read(root, "HybridCPU_ISE", "CloseToHSL", "Core", "Pipeline", "ExecutionFlow", "Materialization", "CPU_Core.PipelineExecution.Materialization.cs");
        string retire = Read(root, "HybridCPU_ISE", "CloseToHSL", "Core", "Pipeline", "Retire", "Evidence", "CPU_Core.PipelineExecution.Retire.cs");

        Assert.Contains("SetHardPinnedPlacement(SlotClass.SystemSingleton, 7);", microOp, StringComparison.Ordinal);
        Assert.Contains("public Pipeline.PipelineEvent? CreatePipelineEvent(", microOp, StringComparison.Ordinal);
        Assert.Contains("pipeEX.GeneratedEvent = MaterializeLaneGeneratedEvent(pipeEX.MicroOp);", execute, StringComparison.Ordinal);
        Assert.Contains("lane.GeneratedEvent = executeLane.GeneratedEvent;", materialization, StringComparison.Ordinal);
        Assert.Contains("lane.GeneratedEvent = memoryLane.GeneratedEvent;", materialization, StringComparison.Ordinal);
        Assert.Contains("retireBatch.CaptureGeneratedSystemEvent(", retire, StringComparison.Ordinal);
        Assert.Contains("typedSystemEventMicroOp.EventKind", retire, StringComparison.Ordinal);
        Assert.Contains("ApplyRetiredSystemEvent(", retire, StringComparison.Ordinal);
    }

    [Fact]
    public void DirectSystemContourRemainsSeparateAfterNarrowMainlineDecision()
    {
        string root = FindRepositoryRoot();
        string dispatcher = Read(root, "HybridCPU_ISE", "CloseToHSL", "Core", "Execution", "Dispatch", "ExecutionDispatcherV4.System.cs");
        string paper = Read(root, "ResearchPaper", "section", "md base", "5_Two_Stage_Admission_and_Bundle_Compositional_SMT_Packing.md");

        Assert.Contains("CaptureSystemRetireWindowPublications(", dispatcher, StringComparison.Ordinal);
        Assert.Contains("retireBatch.CaptureRetireWindowPipelineEvent(", dispatcher, StringComparison.Ordinal);
        Assert.DoesNotContain("CaptureGeneratedSystemEvent(", dispatcher, StringComparison.Ordinal);
        Assert.Contains("| `SystemCommit` | canonical lane-7 `SysEventMicroOp`", paper, StringComparison.Ordinal);
        Assert.Contains("RF-08.4ah approves C-C", paper, StringComparison.Ordinal);
        Assert.Contains("Direct/eager and bounded compatibility event publication remains the separate", paper, StringComparison.Ordinal);
        Assert.Contains("| `VmxCommit` |", paper, StringComparison.Ordinal);
    }

    private static string Slice(string source, string startMarker, string endMarker)
    {
        int start = source.IndexOf(startMarker, StringComparison.Ordinal);
        int end = source.IndexOf(endMarker, start, StringComparison.Ordinal);
        Assert.True(start >= 0 && end > start);
        return source[start..end];
    }

    private static string Read(string root, params string[] segments) =>
        File.ReadAllText(Path.Combine(new[] { root }.Concat(segments).ToArray()));

    private static string FindRepositoryRoot()
    {
        for (DirectoryInfo? current = new(AppContext.BaseDirectory); current is not null; current = current.Parent)
        {
            if (Directory.Exists(Path.Combine(current.FullName, "HybridCPU_ISE")) &&
                Directory.Exists(Path.Combine(current.FullName, "ResearchPaper")))
            {
                return current.FullName;
            }
        }

        throw new DirectoryNotFoundException("Repository root was not found from the test output directory.");
    }
}
