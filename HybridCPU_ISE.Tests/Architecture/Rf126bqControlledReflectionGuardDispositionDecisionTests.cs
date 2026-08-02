namespace HybridCPU_ISE.Tests.Architecture;

/// <summary>RF-12.6bq decision guard for controlled token reflection guards.</summary>
public sealed class Rf126bqControlledReflectionGuardDispositionDecisionTests
{

    [Fact]
    public void PaperRequiresTheseGuardsToBeMigratedBeforeRemovalEligibility()
    {
        string paper = File.ReadAllText(Path.Combine(Root(), "ResearchPaper",
            "section", "md base", "3_Architectural_Overview_and_Frontend_Contract.md"));
        Assert.Contains("reflection/signature consumer", paper, StringComparison.Ordinal);
        Assert.Contains("Until every public compatibility caller is removed or separately",
            paper, StringComparison.Ordinal);
    }

    private static string Root()
    {
        DirectoryInfo? current = new(AppContext.BaseDirectory);
        while (current is not null)
        {
            if (Directory.Exists(Path.Combine(current.FullName, "HybridCPU_ISE")) &&
                Directory.Exists(Path.Combine(current.FullName, "ResearchPaper"))) return current.FullName;
            current = current.Parent;
        }
        throw new DirectoryNotFoundException();
    }
}
