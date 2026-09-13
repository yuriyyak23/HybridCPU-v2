namespace HybridCPU_ISE.Tests.Architecture;

public sealed class Rf129iDmaStreamComputeTokenHandleAuthorityDecisionTests
{
    [Fact]
    public void PaperRetainsFullHandleAndOwnerLocalRawStatusDecision()
    {
        string paper = File.ReadAllText(Path.Combine(Root(), "ResearchPaper", "section", "md base", "3_Architectural_Overview_and_Frontend_Contract.md"));
        Assert.Contains("For `DmaStreamComputeTokenHandle`", paper, StringComparison.Ordinal);
        Assert.Contains("complete handle remains the Lane-6", paper, StringComparison.Ordinal);
        Assert.Contains("raw nonzero token ID is a query ingress only", paper, StringComparison.Ordinal);
        Assert.Contains("may not reconstruct", paper, StringComparison.Ordinal);
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
