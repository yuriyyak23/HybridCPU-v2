namespace HybridCPU_ISE.Tests.Architecture;

public sealed class Rf07ExitPublicExceptionAdapterLedgerTests
{
    [Fact]
    public void RetainedPublicExecuteAdapters_HaveOneOwnerTypedReplacementAndRf07Expiry()
    {
        string root = FindRepositoryRoot();
        string helpers = Read(root,
            "HybridCPU_ISE/CloseToHSL/Core/Pipeline/ExecutionFlow/Materialization/CPU_Core.PipelineExecution.ExecuteHelpers.cs");
        string stage = Read(root,
            "HybridCPU_ISE/CloseToHSL/Core/Pipeline/ExecutionFlow/StageFlow/CPU_Core.PipelineExecution.cs");
        string faults = Read(root,
            "HybridCPU_ISE/CloseToHSL/Core/Pipeline/ExecutionFlow/Faults/CPU_Core.PipelineExecution.Exceptions.cs");
        string scalarMemory = Read(root,
            "HybridCPU_ISE/CloseToHSL/Core/Pipeline/MicroOps/Memory/MicroOp.LoadStore.cs");

        string rawFallback = MemberBody(helpers, "private void RejectSingleLaneReferenceRawFallbackEntry");
        Assert.Contains("CreateWrappedException(", rawFallback, StringComparison.Ordinal);
        Assert.Contains("ExecutionFaultCategory.InvalidInternalOp", rawFallback, StringComparison.Ordinal);
        Assert.Contains("FailCloseSingleLaneExecuteAfterNonFaultException()", rawFallback, StringComparison.Ordinal);

        string loadDenial = MemberBody(helpers, "private void RejectSingleLaneScalarLoadFallbackBackend");
        Assert.Contains("ProjectSingleLaneScalarLoadBackendUnavailableOutcome", loadDenial, StringComparison.Ordinal);
        Assert.Contains("FailCloseSingleLaneExecuteAfterNonFaultException()", loadDenial, StringComparison.Ordinal);
        Assert.Contains("throw new Core.UnsupportedExecutionSurfaceException(", loadDenial, StringComparison.Ordinal);

        string storeDenial = MemberBody(helpers, "private void RejectSingleLaneScalarStoreFallbackBackend");
        Assert.Contains("ProjectSingleLaneScalarStoreBackendUnavailableOutcome", storeDenial, StringComparison.Ordinal);
        Assert.Contains("FailCloseSingleLaneExecuteAfterNonFaultException()", storeDenial, StringComparison.Ordinal);
        Assert.Contains("throw new Core.UnsupportedExecutionSurfaceException(", storeDenial, StringComparison.Ordinal);

        string invalidSize = MemberBody(helpers, "private void RejectSingleLaneScalarStoreInvalidSize");
        Assert.Contains("ProjectSingleLaneScalarStoreInvalidSizeOutcome", invalidSize, StringComparison.Ordinal);
        Assert.Contains("FailCloseSingleLaneExecuteAfterNonFaultException()", invalidSize, StringComparison.Ordinal);
        Assert.Contains("ScalarStoreInvalidSizeDispositionMarker", invalidSize, StringComparison.Ordinal);
        Assert.Contains("throw new Core.Execution.ExecutionOutcomeContractViolationException(", invalidSize, StringComparison.Ordinal);

        string singleLane = MemberBody(helpers, "private bool TryExecuteSingleLaneMicroOp");
        Assert.Equal(2, CountOccurrences(singleLane, "CreateWrappedException("));
        Assert.Contains("ProjectSingleLanePageFaultOutcome(pageFaultException)", singleLane, StringComparison.Ordinal);
        Assert.Contains("DeliverSingleLanePageFaultOutcome(outcome, pageFaultException)", singleLane, StringComparison.Ordinal);
        Assert.Contains("ProjectSingleLaneAlignmentFaultOutcome(", singleLane, StringComparison.Ordinal);
        Assert.Contains("DeliverSingleLaneAlignmentFaultOutcome(outcome, alignmentFault)", singleLane, StringComparison.Ordinal);
        Assert.Contains("catch (Core.UnsupportedExecutionSurfaceException exception)", singleLane, StringComparison.Ordinal);
        Assert.Contains("catch (Core.Execution.ExecutionOutcomeContractViolationException exception)", singleLane, StringComparison.Ordinal);

        string explicitPageFault = MemberBody(faults, "private void RethrowExplicitPacketExecutePageFault");
        Assert.Contains("ProjectExplicitPacketPageFaultOutcome", explicitPageFault, StringComparison.Ordinal);
        Assert.Contains("DeliverExplicitPacketPageFaultOutcome", explicitPageFault, StringComparison.Ordinal);
        string explicitAlignment = MemberBody(faults, "private void RethrowExplicitPacketExecuteAlignmentFault");
        Assert.Contains("ProjectExplicitPacketAlignmentFaultOutcome", explicitAlignment, StringComparison.Ordinal);
        Assert.Contains("DeliverExplicitPacketAlignmentFaultOutcome", explicitAlignment, StringComparison.Ordinal);

        string explicitExecute = MemberBody(stage, "private void ExecuteExplicitPacketLanes");
        Assert.Equal(1, CountOccurrences(explicitExecute, "CreateWrappedException("));
        string explicitGenericCatch = Between(explicitExecute, "catch (Exception exception)", "ApplyExplicitPacketExecuteEpilogueAccounting");
        Assert.Contains("FailCloseExplicitPacketLaneAfterNonFaultExecutionException(ref lane, exception)", explicitGenericCatch, StringComparison.Ordinal);
        Assert.DoesNotContain("lane.ResultReady = false", explicitGenericCatch, StringComparison.Ordinal);

        Assert.Equal(2, CountOccurrences(scalarMemory, "throw new PageFaultException("));
        Assert.Equal(2, CountOccurrences(scalarMemory, "catch (Exception ex)"));
        Assert.Equal(6, CountOccurrences(scalarMemory, "if (this.IsSpeculative)"));
        Assert.Contains("throw new PageFaultException($\"Memory access error", scalarMemory, StringComparison.Ordinal);
        Assert.Contains("throw new PageFaultException($\"Memory write error", scalarMemory, StringComparison.Ordinal);
    }

    [Fact]
    public void AdapterLedger_DoesNotCreateOutcomeOrRetirementAuthorityOutsideExistingProjection()
    {
        string root = FindRepositoryRoot();
        string helpers = Read(root,
            "HybridCPU_ISE/CloseToHSL/Core/Pipeline/ExecutionFlow/Materialization/CPU_Core.PipelineExecution.ExecuteHelpers.cs");
        string stage = Read(root,
            "HybridCPU_ISE/CloseToHSL/Core/Pipeline/ExecutionFlow/StageFlow/CPU_Core.PipelineExecution.cs");

        Assert.DoesNotContain("new Core.Execution.ExecutionRecord", helpers, StringComparison.Ordinal);
        Assert.DoesNotContain("new Core.Execution.ExecutionRecord", stage, StringComparison.Ordinal);
        Assert.DoesNotContain("RetireCoordinator", helpers, StringComparison.Ordinal);
        Assert.DoesNotContain("RetireCoordinator", stage, StringComparison.Ordinal);
        Assert.DoesNotContain("Retryable", MemberBody(helpers, "private static Core.Execution.ExecutionOutcome ProjectSingleLaneNonFaultExceptionOutcome"), StringComparison.Ordinal);
        Assert.DoesNotContain("Retryable", MemberBody(helpers, "private static Core.Execution.ExecutionOutcome ProjectExplicitPacketNonFaultExceptionOutcome"), StringComparison.Ordinal);
    }

    private static string MemberBody(string source, string marker)
    {
        int start = source.IndexOf(marker, StringComparison.Ordinal);
        Assert.True(start >= 0, marker);
        int brace = source.IndexOf('{', start);
        Assert.True(brace > start, marker);
        int depth = 0;
        for (int index = brace; index < source.Length; index++)
        {
            if (source[index] == '{')
                depth++;
            else if (source[index] == '}' && --depth == 0)
                return source[start..(index + 1)];
        }

        throw new InvalidOperationException($"Unterminated member: {marker}");
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
