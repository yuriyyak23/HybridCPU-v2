namespace HybridCPU_ISE.Tests.Architecture;

public sealed class Rf129pAcceleratorTokenHandleAuthorityDecisionTests
{
    [Fact]
    public void PaperRetainsNativeHandleAndHostOwnedVirtualizationBoundary()
    {
        string paper = File.ReadAllText(Path.Combine(Root(), "ResearchPaper", "section", "md base", "3_Architectural_Overview_and_Frontend_Contract.md"));
        Assert.Contains("For `AcceleratorTokenHandle`", paper, StringComparison.Ordinal);
        Assert.Contains("zero is the Lane-7 native handle's absent state", paper, StringComparison.Ordinal);
        Assert.Contains("without exposing or", paper, StringComparison.Ordinal);
        Assert.Contains("No generic token conversion", paper, StringComparison.Ordinal);
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
