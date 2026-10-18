namespace HybridCPU_ISE.Tests.Architecture;

public sealed class Rf129dMemoryRequestIdInvalidBehaviorDecisionTests
{
    [Fact]
    public void PaperForbidsDefaultRequestAsCancellationAuthority()
    {
        string paper = File.ReadAllText(Path.Combine(Root(), "ResearchPaper", "section", "md base", "3_Architectural_Overview_and_Frontend_Contract.md"));
        Assert.Contains("an absent/default carrier is not a cancellation request", paper, StringComparison.Ordinal);
        Assert.Contains("gate cancellation", paper, StringComparison.Ordinal);
        Assert.Contains("default-cancellation compatibility bypass", paper, StringComparison.Ordinal);
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
