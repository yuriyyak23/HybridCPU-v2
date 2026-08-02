namespace HybridCPU_ISE.Tests.Architecture;

public sealed class Rf07ExitExceptionAndReadinessOwnerAuditTests
{
    [Fact]
    public void ExecuteReachableCatches_HaveFrozenTypedDispositionOrPreProjectedAdapter()
    {
        string root = FindRepositoryRoot();
        string helpers = Read(root,
            "HybridCPU_ISE/CloseToHSL/Core/Pipeline/ExecutionFlow/Materialization/CPU_Core.PipelineExecution.ExecuteHelpers.cs");
        string stage = Read(root,
            "HybridCPU_ISE/CloseToHSL/Core/Pipeline/ExecutionFlow/StageFlow/CPU_Core.PipelineExecution.cs");
        string scalarMemory = Read(root,
            "HybridCPU_ISE/CloseToHSL/Core/Pipeline/MicroOps/Memory/MicroOp.LoadStore.cs");
        string singleLane = Between(
            helpers,
            "private bool TryExecuteSingleLaneMicroOp()",
            "private const string ScalarLoadFallbackBackendSurfaceName");

        Assert.Contains("catch (Core.UnsupportedExecutionSurfaceException exception)", singleLane, StringComparison.Ordinal);
        Assert.Contains("catch (Core.Execution.ExecutionOutcomeContractViolationException exception)", singleLane, StringComparison.Ordinal);
        Assert.Contains("catch (Core.PageFaultException pageFaultException)", singleLane, StringComparison.Ordinal);
        Assert.Contains("ProjectSingleLanePageFaultOutcome(pageFaultException)", singleLane, StringComparison.Ordinal);
        Assert.Contains("catch (Core.Memory.MemoryAlignmentException memoryAlignmentException)", singleLane, StringComparison.Ordinal);
        Assert.Contains("ProjectSingleLaneAlignmentFaultOutcome(", singleLane, StringComparison.Ordinal);
        Assert.Contains("catch (Exception ex) when (pipeID.IsVectorOp)", singleLane, StringComparison.Ordinal);
        Assert.Equal(2, CountOccurrences(singleLane, "ProjectSingleLaneNonFaultExceptionOutcome(ex)"));
        Assert.Equal(2, CountOccurrences(singleLane, "FailCloseSingleLaneExecuteAfterNonFaultException()"));

        Assert.Contains("RethrowExplicitPacketExecutePageFault(pageFaultException)", stage, StringComparison.Ordinal);
        Assert.Contains("RethrowExplicitPacketExecuteAlignmentFault(", stage, StringComparison.Ordinal);
        Assert.Contains("catch (Exception exception)", stage, StringComparison.Ordinal);
        Assert.Contains("FailCloseExplicitPacketLaneAfterNonFaultExecutionException(ref lane, exception)", stage, StringComparison.Ordinal);

        Assert.Equal(2, CountOccurrences(scalarMemory, "catch (PageFaultException)"));
        Assert.Equal(2, CountOccurrences(scalarMemory, "catch (Exception ex)"));
        Assert.Equal(6, CountOccurrences(scalarMemory, "this.MarkFaulted();"));
        Assert.Equal(2, CountOccurrences(scalarMemory, "throw new PageFaultException("));
    }

    [Fact]
    public void ResultReadyFalseOwners_AreBoundedAndNeverGenericExceptionDisposition()
    {
        string root = FindRepositoryRoot();
        string helpers = Read(root,
            "HybridCPU_ISE/CloseToHSL/Core/Pipeline/ExecutionFlow/Materialization/CPU_Core.PipelineExecution.ExecuteHelpers.cs");
        string stage = Read(root,
            "HybridCPU_ISE/CloseToHSL/Core/Pipeline/ExecutionFlow/StageFlow/CPU_Core.PipelineExecution.cs");
        string materialization = Read(root,
            "HybridCPU_ISE/CloseToHSL/Core/Pipeline/ExecutionFlow/Materialization/CPU_Core.PipelineExecution.Materialization.cs");

        string singleLane = Between(
            helpers,
            "private bool TryExecuteSingleLaneMicroOp()",
            "private const string ScalarLoadFallbackBackendSurfaceName");
        Assert.Contains("ProjectSingleLaneScalarLoadRetryOutcome", singleLane, StringComparison.Ordinal);
        Assert.Contains("ProjectSingleLaneScalarStoreRetryOutcome", singleLane, StringComparison.Ordinal);
        Assert.Contains("ProjectSingleLaneLoadSegmentRetryOutcome", singleLane, StringComparison.Ordinal);
        Assert.Equal(1, CountOccurrences(singleLane, "pipeEX.ResultReady = false"));
        Assert.Contains("pipeEX.GeneratedEvent = null", singleLane, StringComparison.Ordinal);
        Assert.DoesNotContain("catch (Exception ex)\n                {\n                    pipeEX.ResultReady = false", singleLane, StringComparison.Ordinal);

        Assert.Equal(1, CountOccurrences(stage, "lane.ResultReady = false"));
        Assert.True(
            stage.IndexOf("TryPrepareExplicitPacketExecuteMemoryCarrierLane(", StringComparison.Ordinal) <
            stage.IndexOf("lane.ResultReady = false", StringComparison.Ordinal));

        Assert.Contains("executeLane.LaneIndex is 4 or 5", materialization, StringComparison.Ordinal);
        Assert.Contains("MEM completion", materialization, StringComparison.Ordinal);
        Assert.Equal(1, CountOccurrences(materialization, "lane.ResultReady = false"));

        Assert.Equal(2, CountOccurrences(helpers, "lane.ResultReady = success"));
        Assert.DoesNotContain("return false", Between(
            stage,
            "catch (Exception exception)",
            "ApplyExplicitPacketExecuteEpilogueAccounting"), StringComparison.Ordinal);
    }

    private static string Between(string source, string startMarker, string endMarker)
    {
        int start = source.IndexOf(startMarker, StringComparison.Ordinal);
        Assert.True(start >= 0, startMarker);
        int end = source.IndexOf(endMarker, start + startMarker.Length, StringComparison.Ordinal);
        Assert.True(end > start, endMarker);
        return source[start..end];
    }

    private static int CountOccurrences(string source, string value)
    {
        int count = 0;
        int index = 0;
        while ((index = source.IndexOf(value, index, StringComparison.Ordinal)) >= 0)
        {
            count++;
            index += value.Length;
        }

        return count;
    }

    private static string Read(string root, string relative) =>
        File.ReadAllText(Path.Combine(root, relative.Replace('/', Path.DirectorySeparatorChar)));

    private static string FindRepositoryRoot()
    {
        for (DirectoryInfo? current = new(AppContext.BaseDirectory); current is not null; current = current.Parent)
            if (Directory.Exists(Path.Combine(current.FullName, "HybridCPU_ISE")))
                return current.FullName;
        throw new DirectoryNotFoundException("Repository root was not found.");
    }
}
