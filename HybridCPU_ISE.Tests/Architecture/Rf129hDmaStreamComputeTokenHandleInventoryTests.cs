namespace HybridCPU_ISE.Tests.Architecture;

public sealed class Rf129hDmaStreamComputeTokenHandleInventoryTests
{
    [Fact]
    public void HandleRemainsAFullOwnerScopedCompositeKey()
    {
        string store = Read("HybridCPU_ISE", "CloseToHSL", "Core", "Execution", "DmaStreamCompute", "DmaStreamComputeTokenStore.cs");
        Assert.Contains("record struct DmaStreamComputeTokenHandle", store, StringComparison.Ordinal);
        Assert.Contains("public bool IsDefault => TokenId == 0 || Generation == 0", store, StringComparison.Ordinal);
        Assert.Contains("Dictionary<DmaStreamComputeTokenHandle", store, StringComparison.Ordinal);
        Assert.Contains("!handle.MatchesOwner(ownerBinding)", store, StringComparison.Ordinal);
    }

    [Fact]
    public void RawStatusQueryAndVirtualEvidenceStaySeparateOwnerContours()
    {
        string store = Read("HybridCPU_ISE", "CloseToHSL", "Core", "Execution", "DmaStreamCompute", "DmaStreamComputeTokenStore.cs");
        string evidence = Read("HybridCPU_ISE", "CloseToHSL", "Core", "Runtime", "Lanes", "Lane6", "Lane6VirtualToken.Evidence.partial.cs");
        Assert.Contains("QueryStatusByTokenId(", store, StringComparison.Ordinal);
        Assert.Contains("DmaStreamComputeStatusQueryRejectKind.OwnerDomainMismatch", store, StringComparison.Ordinal);
        Assert.Contains("!hostHandle.IsDefault", evidence, StringComparison.Ordinal);
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
