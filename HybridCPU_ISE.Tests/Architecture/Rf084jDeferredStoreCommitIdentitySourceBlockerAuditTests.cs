namespace HybridCPU_ISE.Tests.Architecture;

/// <summary>
/// RF-08.4j freezes the current DeferredStoreCommit producer and application
/// contour without treating a WB lane index or mutable lane payload as exact
/// issued-attempt identity.
/// </summary>
public sealed class Rf084jDeferredStoreCommitIdentitySourceBlockerAuditTests
{
    [Fact]
    public void DeferredStoreEffectCarriesOnlyMutableWriteBackLaneReference()
    {
        string root = FindRepositoryRoot();
        string types = Read(root, "HybridCPU_ISE", "CloseToHSL", "Core", "Pipeline", "ExecutionFlow", "StageFlow", "CPU_Core.PipelineExecution.Types.cs");
        string factory = Slice(
            types,
            "public static RetireWindowEffect DeferredStoreCommit(byte laneIndex)",
            "public static RetireWindowEffect Csr(");

        Assert.Contains("RetireWindowEffectKind.DeferredStoreCommit", factory, StringComparison.Ordinal);
        Assert.Contains("laneIndex", factory, StringComparison.Ordinal);
        Assert.DoesNotContain("ScheduledOperation", factory, StringComparison.Ordinal);
        Assert.DoesNotContain("VliwOperationId", factory, StringComparison.Ordinal);
        Assert.DoesNotContain("GeneratedStaticBinding", factory, StringComparison.Ordinal);
        Assert.DoesNotContain("SourceOperationProvenance", factory, StringComparison.Ordinal);
        Assert.DoesNotContain("PostStageBIssuedAttempt", factory, StringComparison.Ordinal);
    }

    [Fact]
    public void MainlineStoreReadinessFlowsThroughExistingMutableMemAndWriteBackLane()
    {
        string root = FindRepositoryRoot();
        string memory = Read(root, "HybridCPU_ISE", "CloseToHSL", "Core", "Pipeline", "Stages", "Memory", "CPU_Core.PipelineExecution.Memory.cs");
        string materialization = Read(root, "HybridCPU_ISE", "CloseToHSL", "Core", "Pipeline", "ExecutionFlow", "Materialization", "CPU_Core.PipelineExecution.Materialization.cs");
        string retire = Read(root, "HybridCPU_ISE", "CloseToHSL", "Core", "Pipeline", "Retire", "Evidence", "CPU_Core.PipelineExecution.Retire.cs");

        Assert.Equal(
            3,
            CountOccurrences(memory, "lane.DefersStoreCommitToWriteBack = true;"));
        Assert.Contains(
            "TryAcceptExplicitPacketScalarStore",
            memory,
            StringComparison.Ordinal);
        Assert.Contains(
            "lane.PendingMemoryControllerRequestId = admission.RequestId;",
            memory,
            StringComparison.Ordinal);
        Assert.DoesNotContain(
            "deferPhysicalWriteUntilRetire: true",
            memory,
            StringComparison.Ordinal);
        Assert.Contains(
            "lane.DefersStoreCommitToWriteBack = memoryLane.DefersStoreCommitToWriteBack;",
            materialization,
            StringComparison.Ordinal);
        Assert.Contains(
            "retireBatch.AppendDeferredStoreLane(laneIndex);",
            retire,
            StringComparison.Ordinal);
    }

    [Fact]
    public void SelectedRetireRevalidatesLiveLaneAndKeepsDirectStoreAndAtomicSeparate()
    {
        string root = FindRepositoryRoot();
        string retire = Read(root, "HybridCPU_ISE", "CloseToHSL", "Core", "Pipeline", "Retire", "Evidence", "CPU_Core.PipelineExecution.Retire.cs");
        string types = Read(root, "HybridCPU_ISE", "CloseToHSL", "Core", "Pipeline", "ExecutionFlow", "StageFlow", "CPU_Core.PipelineExecution.Types.cs");
        string dispatcher = Read(root, "HybridCPU_ISE", "CloseToHSL", "Core", "Execution", "Dispatch", "ExecutionDispatcherV4.MemoryAndControl.cs");
        string prevalidation = Slice(
            retire,
            "private void PrevalidateDeferredStoreEffect(byte laneIndex)",
            "private void PrevalidateCsrEffect(");

        Assert.Contains("ScalarWriteBackLaneState lane = pipeWB.GetLane(laneIndex);", prevalidation, StringComparison.Ordinal);
        Assert.Contains("!lane.IsOccupied || !lane.DefersStoreCommitToWriteBack", prevalidation, StringComparison.Ordinal);
        Assert.Contains("!lane.IsMemoryOp || lane.IsLoad", prevalidation, StringComparison.Ordinal);
        Assert.Contains("ApplyRetiredScalarStoreCommit(", retire, StringComparison.Ordinal);
        Assert.Contains(
            "pipeWB.GetLane(retireEffect.DeferredStoreLaneIndex)",
            retire,
            StringComparison.Ordinal);

        Assert.Contains("RetireWindowEffectKind.ScalarMemoryStore", types, StringComparison.Ordinal);
        Assert.Contains("RetireWindowEffectKind.Atomic", types, StringComparison.Ordinal);
        Assert.Contains("retireBatch.CaptureRetireWindowScalarMemoryStore(", dispatcher, StringComparison.Ordinal);
        Assert.DoesNotContain("AppendDeferredStoreLane", dispatcher, StringComparison.Ordinal);

        string paper = Read(root, "ResearchPaper", "section", "md base", "5_Two_Stage_Admission_and_Bundle_Compositional_SMT_Packing.md");
        Assert.Contains("RF-08.4aj approves C-C for this closed mainline contour", paper, StringComparison.Ordinal);
        Assert.Contains("Direct compatibility `ScalarMemoryStoreCommit` and mainline/direct", paper, StringComparison.Ordinal);
    }

    private static int CountOccurrences(string source, string value)
    {
        int count = 0;
        int offset = 0;
        while ((offset = source.IndexOf(value, offset, StringComparison.Ordinal)) >= 0)
        {
            count++;
            offset += value.Length;
        }

        return count;
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
