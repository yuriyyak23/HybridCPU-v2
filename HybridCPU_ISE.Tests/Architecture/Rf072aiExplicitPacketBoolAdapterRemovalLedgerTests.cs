namespace HybridCPU_ISE.Tests.Architecture;

public sealed class Rf072aiExplicitPacketBoolAdapterRemovalLedgerTests
{
    [Fact]
    public void GenericExplicitPacketBoolAdapter_HasOneOwnerAndNoImplicitOutcomeReplacement()
    {
        string root = FindRepositoryRoot();
        string source = File.ReadAllText(Path.Combine(root,
            "HybridCPU_ISE", "CloseToHSL", "Core", "Pipeline", "ExecutionFlow",
            "Materialization", "CPU_Core.PipelineExecution.ExecuteHelpers.cs"));
        string stageFlow = File.ReadAllText(Path.Combine(root,
            "HybridCPU_ISE", "CloseToHSL", "Core", "Pipeline", "ExecutionFlow",
            "StageFlow", "CPU_Core.PipelineExecution.cs"));

        string execute = Between(source,
            "private bool TryExecuteExplicitPacketGenericMicroOpLane",
            "private void FailCloseSingleLaneExecuteAfterNonFaultException");
        string apply = Between(source,
            "private void ApplyExplicitPacketGenericMicroOpExecutionOutcome",
            "private bool TryExecuteExplicitPacketGenericMicroOpLane");

        Assert.Contains("bool success = ExecuteMicroOpWithStableCoreIdentity(lane.MicroOp!)", execute, StringComparison.Ordinal);
        Assert.Contains("ApplyExplicitPacketGenericMicroOpExecutionOutcome(", execute, StringComparison.Ordinal);
        Assert.Contains("lane.ResultReady = success", apply, StringComparison.Ordinal);
        Assert.DoesNotContain("ExecutionOutcomeKind.Retryable", execute, StringComparison.Ordinal);
        Assert.DoesNotContain("ExecutionOutcomeKind.BackendUnavailable", execute, StringComparison.Ordinal);
        Assert.DoesNotContain("ExecutionRecord", execute, StringComparison.Ordinal);
        Assert.DoesNotContain("RetireCoordinator", apply, StringComparison.Ordinal);
        Assert.Equal(1, CountOccurrences(
            stageFlow,
            "TryExecuteExplicitPacketGenericMicroOpLane("));
        Assert.DoesNotContain("ExecutionRecord", stageFlow, StringComparison.Ordinal);
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

    private static string FindRepositoryRoot()
    {
        for (DirectoryInfo? current = new(AppContext.BaseDirectory); current is not null; current = current.Parent)
            if (Directory.Exists(Path.Combine(current.FullName, "HybridCPU_ISE"))) return current.FullName;
        throw new DirectoryNotFoundException("Repository root was not found.");
    }
}
