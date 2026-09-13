namespace HybridCPU_ISE.Tests.Architecture;

public sealed class Rf129kDmaStreamComputeTokenHandleInvalidInputInventoryTests
{
    [Fact]
    public void StoreKeepsDefaultAndOwnerMismatchAsLocalLookupCancellationOutcomes()
    {
        string store = Read("HybridCPU_ISE", "CloseToHSL", "Core", "Execution", "DmaStreamCompute", "DmaStreamComputeTokenStore.cs");
        Assert.Contains("if (handle.IsDefault ||", store, StringComparison.Ordinal);
        Assert.Contains("!handle.MatchesOwner(ownerBinding)", store, StringComparison.Ordinal);
        Assert.Contains("return false;", store, StringComparison.Ordinal);
        Assert.Contains("if (!TryGet(handle, ownerBinding", store, StringComparison.Ordinal);
    }

    [Fact]
    public void RawStatusAndVirtualizationKeepSeparateInvalidOutcomes()
    {
        string store = Read("HybridCPU_ISE", "CloseToHSL", "Core", "Execution", "DmaStreamCompute", "DmaStreamComputeTokenStore.cs");
        string queue = Read("HybridCPU_ISE", "CloseToHSL", "Core", "Runtime", "Lanes", "Lane6", "Lane6QueueRuntime.cs");
        Assert.Contains("if (tokenId == 0)", store, StringComparison.Ordinal);
        Assert.Contains("DmaStreamComputeStatusQueryRejectKind.InvalidToken", store, StringComparison.Ordinal);
        Assert.Contains("DmaStreamComputeStatusQueryRejectKind.OwnerDomainMismatch", store, StringComparison.Ordinal);
        Assert.Contains("hostHandle.IsDefault", queue, StringComparison.Ordinal);
        Assert.Contains("DmaFaultKind.QueueOwnershipFault", queue, StringComparison.Ordinal);
    }

    private static string Read(params string[] parts) => File.ReadAllText(Path.Combine(new[] { Root() }.Concat(parts).ToArray()));
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
