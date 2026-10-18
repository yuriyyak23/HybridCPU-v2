namespace HybridCPU_ISE.Tests.Architecture;

/// <summary>
/// RF-08.4y distinguishes direct DMA query/status RegisterWrite constructors
/// from production-reachable selected-retire effects.
/// </summary>
public sealed class Rf084yDmaQueryStatusRegisterWriteReachabilityAuditTests
{
    [Fact]
    public void BothCarriersAreHardPinnedLane6AndOwnDormantRegisterEmitters()
    {
        string root = FindRepositoryRoot();
        string query = Read(root, "HybridCPU_ISE", "CloseToHSL", "Core", "Pipeline",
            "MicroOps", "Lane6DmaStream", "DmaStreamComputeQueryCapsMicroOp.cs");
        string status = Read(root, "HybridCPU_ISE", "CloseToHSL", "Core", "Pipeline",
            "MicroOps", "Lane6DmaStream", "DmaStreamComputeStatusMicroOp.cs");

        foreach (string source in new[] { query, status })
        {
            Assert.Contains("SetHardPinnedPlacement(SlotClass.DmaStreamClass, 6)", source, StringComparison.Ordinal);
            Assert.Contains("public override void EmitWriteBackRetireRecords(", source, StringComparison.Ordinal);
            Assert.Contains("RetireRecord.RegisterWrite(", source, StringComparison.Ordinal);
        }

        Assert.Contains("DSC_QUERY_CAPS", query, StringComparison.Ordinal);
        Assert.Contains("DSC_STATUS", status, StringComparison.Ordinal);
        Assert.Contains("QueryStatusByTokenId(", status, StringComparison.Ordinal);
    }

    [Fact]
    public void AuthoritativeSelectedRetireExcludesNonMatrixLane6()
    {
        string root = FindRepositoryRoot();
        string helpers = Read(root, "HybridCPU_ISE", "CloseToHSL", "Core", "Pipeline",
            "ExecutionFlow", "StageFlow", "CPU_Core.Pipeline.Helpers.cs");
        string retire = Read(root, "HybridCPU_ISE", "CloseToHSL", "Core", "Pipeline",
            "Retire", "Evidence", "CPU_Core.PipelineExecution.Retire.cs");
        string stageFlow = Read(root, "HybridCPU_ISE", "CloseToHSL", "Core", "Pipeline",
            "ExecutionFlow", "StageFlow", "CPU_Core.PipelineExecution.cs");

        Assert.Contains(
            "=> laneIndex < 6 || laneIndex == 7;",
            helpers,
            StringComparison.Ordinal);
        Assert.Contains(
            "writeBackStage.Lane6.MicroOp is Core.MatrixTileMicroOp",
            helpers,
            StringComparison.Ordinal);
        Assert.Contains(
            "if (laneIndex >= 6 && laneIndex != 7 &&",
            retire,
            StringComparison.Ordinal);
        Assert.Contains(
            "lane.MicroOp is not Core.MatrixTileMicroOp)",
            retire,
            StringComparison.Ordinal);
        Assert.Contains(
            "CaptureRetiredWriteBackLaneEffects(ref retireBatch, laneIndex, lane);",
            stageFlow,
            StringComparison.Ordinal);
    }

    [Fact]
    public void ReplayEvidenceIsDiagnosticAndNotRf08ExactIdentity()
    {
        string root = FindRepositoryRoot();
        string queryEvidence = Read(root, "HybridCPU_ISE", "CloseToHSL", "Core",
            "Execution", "DmaStreamCompute", "DmaStreamComputeCapabilityQuery.cs");
        string statusEvidence = Read(root, "HybridCPU_ISE", "CloseToHSL", "Core",
            "Execution", "DmaStreamCompute", "DmaStreamComputeTokenStore.cs");
        string fsp = Read(root, "HybridCPU_ISE", "CloseToHSL", "Core", "Pipeline",
            "Scheduling", "Fsp", "CPU_Core.PipelineExecution.Fsp.cs");

        string queryType = Extract(queryEvidence,
            "public readonly record struct DmaStreamComputeCapabilityQueryReplayEvidence",
            "\n    }\n}");
        string statusType = Extract(statusEvidence,
            "public readonly record struct DmaStreamComputeStatusReplayEvidence",
            "\n    public sealed class DmaStreamComputeTokenStore");

        foreach (string evidence in new[] { queryType, statusType })
        {
            Assert.DoesNotContain("ScheduledOperation", evidence, StringComparison.Ordinal);
            Assert.DoesNotContain("GeneratedStaticBinding", evidence, StringComparison.Ordinal);
            Assert.DoesNotContain("AdmissionRecord", evidence, StringComparison.Ordinal);
            Assert.DoesNotContain("PostStageBIssuedAttempt", evidence, StringComparison.Ordinal);
        }

        string attach = Extract(
            fsp,
            "private void AttachRf08PostStageBIdentityTemplate(",
            "private byte ResolveForegroundRunnableVirtualThreadMask()");
        Assert.DoesNotContain("DmaStreamCompute", attach, StringComparison.Ordinal);
        Assert.Contains("ScalarALUMicroOp", attach, StringComparison.Ordinal);
    }

    [Fact]
    public void DirectPublicationCallersRemainTestOnly()
    {
        string root = FindRepositoryRoot();
        string queryTests = Read(root, "HybridCPU_ISE.Tests", "tests",
            "DmaStreamComputeQueryCapsPhase07ATests.cs");
        string statusTests = Read(root, "HybridCPU_ISE.Tests", "tests",
            "DmaStreamComputeStatusPhase07Tests.cs");
        string dispatcher = Read(root, "HybridCPU_ISE", "CloseToHSL", "Core",
            "Execution", "Dispatch", "ExecutionDispatcherV4.cs");

        Assert.Contains("query.EmitWriteBackRetireRecords(", queryTests, StringComparison.Ordinal);
        Assert.Contains("core.RetireCoordinator.Retire(", queryTests, StringComparison.Ordinal);
        Assert.Contains("status.EmitWriteBackRetireRecords(", statusTests, StringComparison.Ordinal);
        Assert.DoesNotContain("DmaStreamComputeQueryCapsMicroOp", dispatcher, StringComparison.Ordinal);
        Assert.DoesNotContain("DmaStreamComputeStatusMicroOp", dispatcher, StringComparison.Ordinal);
    }

    private static string Extract(string source, string startMarker, string endMarker)
    {
        int start = source.IndexOf(startMarker, StringComparison.Ordinal);
        Assert.True(start >= 0, $"Missing start marker: {startMarker}");
        int end = source.IndexOf(endMarker, start, StringComparison.Ordinal);
        Assert.True(end > start, $"Missing end marker: {endMarker}");
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

        throw new DirectoryNotFoundException("Repository root was not found.");
    }
}
