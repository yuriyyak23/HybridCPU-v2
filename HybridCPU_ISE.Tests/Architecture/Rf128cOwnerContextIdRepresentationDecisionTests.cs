namespace HybridCPU_ISE.Tests.Architecture;

/// <summary>RF-12.8c authority against cross-representation OwnerContextId migration.</summary>
public sealed class Rf128cOwnerContextIdRepresentationDecisionTests
{
    [Fact]
    public void PaperRetainsSignedAndUnsignedCarriersWithoutACommonCheckedType()
    {
        string paper = File.ReadAllText(Path.Combine(Root(), "ResearchPaper", "section", "md base",
            "3_Architectural_Overview_and_Frontend_Contract.md"));
        Assert.Contains("#### Owner-context raw-carrier representation boundary", paper, StringComparison.Ordinal);
        Assert.Contains("signed execution carriers and unsigned external", paper, StringComparison.Ordinal);
        Assert.Contains("negative no-bundle/no-owner control sentinel", paper, StringComparison.Ordinal);
        Assert.Contains("no common checked `OwnerContextId` CLR type", paper, StringComparison.Ordinal);
        Assert.Contains("no valid-input signature migration", paper, StringComparison.Ordinal);
    }

    [Fact]
    public void ExistingPipelineSentinelAndExternalCastsRemainDistinctRawBoundaries()
    {
        string root = Root();
        string scheduler = File.ReadAllText(Path.Combine(root, "HybridCPU_ISE", "CloseToHSL", "Core", "Pipeline",
            "Scheduling", "BundlePacking", "MicroOpScheduler.PackBundle.cs"));
        string lane6 = File.ReadAllText(Path.Combine(root, "HybridCPU_ISE", "CloseToHSL", "Core", "Pipeline",
            "MicroOps", "Lane6DmaStream", "DmaStreamComputeMicroOp.cs"));
        Assert.Contains("int bundleOwnerContextId = -1", scheduler, StringComparison.Ordinal);
        Assert.Contains("ConvertOwnerContextId(uint ownerContextId)", lane6, StringComparison.Ordinal);
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
