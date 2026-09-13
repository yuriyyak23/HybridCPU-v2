namespace HybridCPU_ISE.Tests.Architecture;

public sealed class Rf07ExitGenericCatchAuditTests
{
    [Fact]
    public void RemainingGenericExecutionCatches_HaveTypedFatalProjectionAndFailClosedCleanup()
    {
        string root = FindRepositoryRoot();
        string helpers = Read(root, "HybridCPU_ISE/CloseToHSL/Core/Pipeline/ExecutionFlow/Materialization/CPU_Core.PipelineExecution.ExecuteHelpers.cs");
        string stage = Read(root, "HybridCPU_ISE/CloseToHSL/Core/Pipeline/ExecutionFlow/StageFlow/CPU_Core.PipelineExecution.cs");

        Assert.Contains("catch (Exception ex) when (pipeID.IsVectorOp)", helpers, StringComparison.Ordinal);
        Assert.Contains("ProjectSingleLaneNonFaultExceptionOutcome(ex)", helpers, StringComparison.Ordinal);
        Assert.Contains("FailCloseSingleLaneExecuteAfterNonFaultException()", helpers, StringComparison.Ordinal);
        Assert.Contains("ExecutionOutcomeKind.FatalInvariantViolation", helpers, StringComparison.Ordinal);
        Assert.Contains("catch (Exception exception)", stage, StringComparison.Ordinal);
        Assert.Contains("FailCloseExplicitPacketLaneAfterNonFaultExecutionException(ref lane, exception)", stage, StringComparison.Ordinal);
        Assert.Contains("ProjectExplicitPacketNonFaultExceptionOutcome(exception)", helpers, StringComparison.Ordinal);
        Assert.DoesNotContain("catch (Exception exception) { return false", stage, StringComparison.Ordinal);
    }

    private static string Read(string root, string relative) =>
        File.ReadAllText(Path.Combine(root, relative.Replace('/', Path.DirectorySeparatorChar)));

    private static string FindRepositoryRoot()
    {
        for (DirectoryInfo? current = new(AppContext.BaseDirectory); current is not null; current = current.Parent)
            if (Directory.Exists(Path.Combine(current.FullName, "HybridCPU_ISE"))) return current.FullName;
        throw new DirectoryNotFoundException("Repository root was not found.");
    }
}
