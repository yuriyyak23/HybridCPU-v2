namespace HybridCPU_ISE.Tests.Architecture;

/// <summary>
/// RF-07.2af freezes the explicit-packet eligibility and generic exception
/// caller boundary for the already-published indexed vector carriers.
/// It is inventory evidence only; it does not migrate the bool adapter.
/// </summary>
public sealed class Rf072afExplicitPacketIndexedVectorEligibilityInventoryTests
{
    [Fact]
    public void IndexedVectorCarriers_AreGenericExplicitPacketCandidates_NotScalarMemoryCarriers()
    {
        string root = FindRepositoryRoot();
        string vectorMemory = Read(root,
            "HybridCPU_ISE/CloseToHSL/Core/Pipeline/MicroOps/Vector/VectorMicroOps.Memory.cs");
        string helpers = Read(root,
            "HybridCPU_ISE/CloseToHSL/Core/Pipeline/ExecutionFlow/Materialization/CPU_Core.PipelineExecution.ExecuteHelpers.cs");
        string memory = Read(root,
            "HybridCPU_ISE/CloseToHSL/Core/Pipeline/Stages/Memory/CPU_Core.PipelineExecution.Memory.cs");

        string gatherConstructor = Between(vectorMemory, "public GatherMicroOp()", "public override void InitializeMetadata()");
        string scatterConstructor = Between(vectorMemory, "public StoreScatterMicroOp()", "public void SetStoreBuffer");
        string eligibility = Between(
            helpers,
            "private static bool ShouldUseExplicitPacketGenericMicroOpExecutionContour",
            "private void ApplyExplicitPacketGenericMicroOpExecutionOutcome");
        string memoryCarrier = Between(
            memory,
            "private bool TryPrepareExplicitPacketExecuteMemoryCarrierLane",
            "// RF-07.2t intentionally owns");

        Assert.Contains("IsMemoryOp = false", gatherConstructor, StringComparison.Ordinal);
        Assert.Contains("IsMemoryOp = false", scatterConstructor, StringComparison.Ordinal);
        Assert.Contains("return laneIndex < 4", eligibility, StringComparison.Ordinal);
        Assert.Contains("laneIndex == 7", eligibility, StringComparison.Ordinal);
        Assert.Contains("!microOp.IsMemoryOp", eligibility, StringComparison.Ordinal);
        Assert.Contains("lane.MicroOp is Core.LoadMicroOp", memoryCarrier, StringComparison.Ordinal);
        Assert.Contains("lane.MicroOp is Core.StoreMicroOp", memoryCarrier, StringComparison.Ordinal);
        Assert.Contains("lane.MicroOp is Core.AtomicMicroOp", memoryCarrier, StringComparison.Ordinal);
        Assert.DoesNotContain("GatherMicroOp", memoryCarrier, StringComparison.Ordinal);
        Assert.DoesNotContain("StoreScatterMicroOp", memoryCarrier, StringComparison.Ordinal);
    }

    [Fact]
    public void GenericExplicitPacketPath_RetainsBoolReadinessAdapterForIndexedVectorCandidates()
    {
        string helpers = Read(FindRepositoryRoot(),
            "HybridCPU_ISE/CloseToHSL/Core/Pipeline/ExecutionFlow/Materialization/CPU_Core.PipelineExecution.ExecuteHelpers.cs");
        string generic = Between(
            helpers,
            "private bool TryExecuteExplicitPacketGenericMicroOpLane",
            "private void FailCloseSingleLaneExecuteAfterNonFaultException");
        string apply = Between(
            helpers,
            "private void ApplyExplicitPacketGenericMicroOpExecutionOutcome",
            "private bool TryExecuteExplicitPacketGenericMicroOpLane");

        AssertOrdered(generic, "bool success = ExecuteMicroOpWithStableCoreIdentity(lane.MicroOp!)", "ApplyExplicitPacketGenericMicroOpExecutionOutcome(");
        Assert.Contains("lane.ResultReady = success", apply, StringComparison.Ordinal);
        Assert.Contains("lane.GeneratedRetireRecordCount = 0", apply, StringComparison.Ordinal);
        Assert.DoesNotContain("ExecutionOutcomeKind.Retryable", generic, StringComparison.Ordinal);
        Assert.DoesNotContain("ExecutionRecord", generic, StringComparison.Ordinal);
    }

    [Fact]
    public void ExplicitPacketGenericCatch_ProjectsUnknownExceptionFatalThenClearsLaneBeforeLegacyDelivery()
    {
        string root = FindRepositoryRoot();
        string stage = Read(root,
            "HybridCPU_ISE/CloseToHSL/Core/Pipeline/ExecutionFlow/StageFlow/CPU_Core.PipelineExecution.cs");
        string helpers = Read(root,
            "HybridCPU_ISE/CloseToHSL/Core/Pipeline/ExecutionFlow/Materialization/CPU_Core.PipelineExecution.ExecuteHelpers.cs");
        string loop = Between(stage, "private void ExecuteExplicitPacketLanes()", "/// <summary>\n            /// Stage 3");
        string failClose = Between(
            helpers,
            "private void FailCloseExplicitPacketLaneAfterNonFaultExecutionException",
            "// RF-07.2e owns only");
        string projection = Between(
            helpers,
            "private static Core.Execution.ExecutionOutcome ProjectExplicitPacketNonFaultExceptionOutcome",
            "}\n        }\n    }\n}");

        AssertOrdered(loop, "catch (Core.PageFaultException pageFaultException)", "catch (Exception exception)");
        AssertOrdered(loop, "catch (Core.Memory.MemoryAlignmentException memoryAlignmentException)", "catch (Exception exception)");
        AssertOrdered(loop, "FailCloseExplicitPacketLaneAfterNonFaultExecutionException(ref lane, exception)", "CreateWrappedException(");
        Assert.Contains("Rf07LegacyOutcomeProjection.ProjectException(exception)", projection, StringComparison.Ordinal);
        Assert.Contains("ExecutionOutcomeKind.FatalInvariantViolation", projection, StringComparison.Ordinal);
        AssertOrdered(failClose, "ReleaseScalarLaneBookkeeping(lane)", "clearedLane.Clear(laneIndex)");
        AssertOrdered(failClose, "clearedLane.Clear(laneIndex)", "pipeEX.SetLane(laneIndex, clearedLane)");
        Assert.DoesNotContain("ExecutionOutcomeKind.Retryable", loop, StringComparison.Ordinal);
        Assert.DoesNotContain("ExecutionRecord", loop, StringComparison.Ordinal);
    }

    private static string Between(string source, string startMarker, string endMarker)
    {
        int start = source.IndexOf(startMarker, StringComparison.Ordinal);
        Assert.True(start >= 0, startMarker);
        int end = source.IndexOf(endMarker, start + startMarker.Length, StringComparison.Ordinal);
        Assert.True(end > start, endMarker);
        return source[start..end];
    }

    private static void AssertOrdered(string source, string first, string second)
    {
        int firstIndex = source.IndexOf(first, StringComparison.Ordinal);
        int secondIndex = source.IndexOf(second, StringComparison.Ordinal);
        Assert.True(firstIndex >= 0, first);
        Assert.True(secondIndex > firstIndex, second);
    }

    private static string Read(string root, string relativePath) =>
        File.ReadAllText(Path.Combine(root, relativePath.Replace('/', Path.DirectorySeparatorChar)));

    private static string FindRepositoryRoot()
    {
        for (DirectoryInfo? current = new(AppContext.BaseDirectory); current is not null; current = current.Parent)
        {
            if (Directory.Exists(Path.Combine(current.FullName, "HybridCPU_ISE")) &&
                Directory.Exists(Path.Combine(current.FullName, "HybridCPU_ISE.Tests")))
            {
                return current.FullName;
            }
        }

        throw new DirectoryNotFoundException("Repository root was not found from the test output directory.");
    }
}
