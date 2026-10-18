namespace HybridCPU_ISE.Tests.Architecture;

public sealed class Rf129sAcceleratorTokenHandleInvalidBehaviorDecisionTests
{
    [Fact]
    public void PaperKeepsInvalidHandleAndGuardEvidenceOutcomesSeparate()
    {
        string paper = File.ReadAllText(Path.Combine(Root(), "ResearchPaper", "section", "md base", "3_Architectural_Overview_and_Frontend_Contract.md"));
        Assert.Contains("Default, forged and owner-evidence-mismatched `AcceleratorTokenHandle`", paper, StringComparison.Ordinal);
        Assert.Contains("zero and unknown native handles cannot", paper, StringComparison.Ordinal);
        Assert.Contains("guard's owner/domain or epoch", paper, StringComparison.Ordinal);
        Assert.Contains("may not substitute a virtual token", paper, StringComparison.Ordinal);
        Assert.Contains("no shared invalid result", paper, StringComparison.Ordinal);
    }

    private static string Root()
    {
        DirectoryInfo? current = new(AppContext.BaseDirectory);
        while (current is not null)
        {
            if (Directory.Exists(Path.Combine(current.FullName, "ResearchPaper"))) return current.FullName;
            current = current.Parent;
        }
        throw new DirectoryNotFoundException();
    }
}
