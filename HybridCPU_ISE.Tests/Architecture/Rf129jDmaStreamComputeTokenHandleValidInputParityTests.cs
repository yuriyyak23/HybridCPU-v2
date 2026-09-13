namespace HybridCPU_ISE.Tests.Architecture;

public sealed class Rf129jDmaStreamComputeTokenHandleValidInputParityTests
{
    [Fact]
    public void FullHandleFlowsFromIssueAllocationIntoActiveStoreAndCancellation()
    {
        string store = File.ReadAllText(Path.Combine(Root(), "HybridCPU_ISE", "CloseToHSL", "Core", "Execution", "DmaStreamCompute", "DmaStreamComputeTokenStore.cs"));
        Assert.Contains("DmaStreamComputeTokenHandle handle = AllocateHandle", store, StringComparison.Ordinal);
        Assert.Contains("_activeTokens.Add(handle, entry)", store, StringComparison.Ordinal);
        Assert.Contains("TryGet(handle, ownerBinding", store, StringComparison.Ordinal);
        Assert.Contains("entry.Token.Cancel(reason)", store, StringComparison.Ordinal);
        Assert.Contains("SnapshotActiveHandles", store, StringComparison.Ordinal);
    }

    [Fact]
    public void MicroOpRetainsCompositeHandleAsItsValidResultCarrier()
    {
        string microOp = File.ReadAllText(Path.Combine(Root(), "HybridCPU_ISE", "CloseToHSL", "Core", "Pipeline", "MicroOps", "Lane6DmaStream", "DmaStreamComputeMicroOp.cs"));
        Assert.Contains("DmaStreamComputeTokenHandle LastExecutionTokenHandle", microOp, StringComparison.Ordinal);
        Assert.Contains("_lastExecutionResult?.TokenHandle", microOp, StringComparison.Ordinal);
    }

    private static string Root()
    {
        DirectoryInfo? current = new(AppContext.BaseDirectory);
        while (current is not null)
        {
            if (Directory.Exists(Path.Combine(current.FullName, "HybridCPU_ISE")) && Directory.Exists(Path.Combine(current.FullName, "ResearchPaper"))) return current.FullName;
            current = current.Parent;
        }
        throw new DirectoryNotFoundException();
    }
}
