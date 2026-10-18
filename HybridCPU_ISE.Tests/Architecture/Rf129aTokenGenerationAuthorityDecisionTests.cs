namespace HybridCPU_ISE.Tests.Architecture;

public sealed class Rf129aTokenGenerationAuthorityDecisionTests
{
    [Fact]
    public void PaperRetainsSeparateTokenGenerationAndEpochOwners()
    {
        string paper = File.ReadAllText(Path.Combine(Root(), "ResearchPaper", "section", "md base", "3_Architectural_Overview_and_Frontend_Contract.md"));

        Assert.Contains("#### Token and generation owner boundary", paper, StringComparison.Ordinal);
        Assert.Contains("separate owner-scoped families", paper, StringComparison.Ordinal);
        Assert.Contains("universal invalid value", paper, StringComparison.Ordinal);
        Assert.Contains("The timed-memory controller retains request allocation", paper, StringComparison.Ordinal);
        Assert.Contains("no common `TokenId`/generation type", paper, StringComparison.Ordinal);
    }

    [Fact]
    public void ExistingLocalZeroPoliciesRemainDistinct()
    {
        string memory = File.ReadAllText(Path.Combine(Root(), "HybridCPU_ISE", "CloseToHSL", "Memory", "Timing", "MemoryCycleController.cs"));
        string lane7 = File.ReadAllText(Path.Combine(Root(), "HybridCPU_ISE", "CloseToHSL", "Core", "Runtime", "Lanes", "Lane7", "Lane7StateBlock.cs"));

        Assert.Contains("public bool IsValid => Value != 0", memory, StringComparison.Ordinal);
        Assert.Contains("if (TokenEpoch == 0)", lane7, StringComparison.Ordinal);
        Assert.Contains("TokenEpoch = 1", lane7, StringComparison.Ordinal);
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
