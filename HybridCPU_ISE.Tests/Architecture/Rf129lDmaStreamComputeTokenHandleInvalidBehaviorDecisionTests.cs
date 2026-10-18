namespace HybridCPU_ISE.Tests.Architecture;

public sealed class Rf129lDmaStreamComputeTokenHandleInvalidBehaviorDecisionTests
{
    [Fact]
    public void PaperRetainsDistinctInvalidOwners()
    {
        string paper = File.ReadAllText(Path.Combine(Root(), "ResearchPaper", "section", "md base", "3_Architectural_Overview_and_Frontend_Contract.md"));
        Assert.Contains("Default, forged and owner-mismatched `DmaStreamComputeTokenHandle`", paper, StringComparison.Ordinal);
        Assert.Contains("raw status query distinguishes missing from", paper, StringComparison.Ordinal);
        Assert.Contains("No owner may substitute another's", paper, StringComparison.Ordinal);
        Assert.Contains("no behavior change, common invalid result", paper, StringComparison.Ordinal);
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
