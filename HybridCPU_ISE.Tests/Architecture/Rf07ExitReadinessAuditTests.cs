namespace HybridCPU_ISE.Tests.Architecture;

public sealed class Rf07ExitReadinessAuditTests
{
    [Fact]
    public void ExecutionFlow_NotReadyAssignmentsRemainOwnedAndGenericCatchesFailClosed()
    {
        string root = FindRepositoryRoot();
        string helpers = Read(root, "HybridCPU_ISE/CloseToHSL/Core/Pipeline/ExecutionFlow/Materialization/CPU_Core.PipelineExecution.ExecuteHelpers.cs");
        string materialization = Read(root, "HybridCPU_ISE/CloseToHSL/Core/Pipeline/ExecutionFlow/Materialization/CPU_Core.PipelineExecution.Materialization.cs");
        string stage = Read(root, "HybridCPU_ISE/CloseToHSL/Core/Pipeline/ExecutionFlow/StageFlow/CPU_Core.PipelineExecution.cs");

        Assert.Contains("Preserve the legacy not-ready carrier and timing.", helpers, StringComparison.Ordinal);
        Assert.Contains("pipeEX.ResultReady = false", helpers, StringComparison.Ordinal);
        Assert.Contains("MEM completion", materialization, StringComparison.Ordinal);
        Assert.Contains("lane.ResultReady = false", materialization, StringComparison.Ordinal);
        Assert.Contains("lane.ResultReady = false", stage, StringComparison.Ordinal);
        Assert.Contains("FailCloseExplicitPacketLaneAfterNonFaultExecutionException", stage, StringComparison.Ordinal);
        Assert.Contains("FailCloseSingleLaneExecuteAfterNonFaultException()", helpers, StringComparison.Ordinal);
        Assert.DoesNotContain("catch (Exception ex) when (pipeID.IsVectorOp)\n                {\n                    pipeEX.ResultReady = false", helpers, StringComparison.Ordinal);
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
