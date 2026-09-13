namespace HybridCPU_ISE.Tests.Architecture;

public sealed class Rf129nDmaStreamComputeTokenHandleExitAuditTests
{
    [Fact]
    public void FamilyRetainsOnlyLane6StoreOwnedIdentityBoundaries()
    {
        string store = Read("HybridCPU_ISE", "CloseToHSL", "Core", "Execution", "DmaStreamCompute", "DmaStreamComputeTokenStore.cs");
        string paper = Read("ResearchPaper", "section", "md base", "3_Architectural_Overview_and_Frontend_Contract.md");
        Assert.Contains("Dictionary<DmaStreamComputeTokenHandle", store, StringComparison.Ordinal);
        Assert.Contains("QueryStatusByTokenId", store, StringComparison.Ordinal);
        Assert.Contains("DmaStreamComputeStatusQueryRejectKind.InvalidToken", store, StringComparison.Ordinal);
        Assert.Contains("DmaStreamComputeTokenHandle` values keep", paper, StringComparison.Ordinal);
        Assert.DoesNotContain("MemoryBankId", store, StringComparison.Ordinal);
        Assert.DoesNotContain("SlotId", store, StringComparison.Ordinal);
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
