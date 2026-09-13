namespace HybridCPU_ISE.Tests.Architecture;

public sealed class Rf128vAddressSpaceNestedTlbInvalidBehaviorDecisionTests
{
    [Fact]
    public void PaperLeavesInvalidOutcomeWithTranslationAndTlbOwners()
    {
        string paper = File.ReadAllText(Path.Combine(Root(), "ResearchPaper", "section", "md base", "3_Architectural_Overview_and_Frontend_Contract.md"));

        Assert.Contains("independently constructed absent `AddressSpaceId` or `NestedTlbTag`", paper, StringComparison.Ordinal);
        Assert.Contains("cannot admit a translation", paper, StringComparison.Ordinal);
        Assert.Contains("owners retain their existing invalid-control, permission and entry-fault", paper, StringComparison.Ordinal);
        Assert.Contains("equality comparison does not reinterpret", paper, StringComparison.Ordinal);
        Assert.Contains("No global carrier `IsValid`", paper, StringComparison.Ordinal);
    }

    [Fact]
    public void RuntimeStillHasNoCarrierLevelInvalidBehaviorImplementation()
    {
        string addressSpace = File.ReadAllText(Path.Combine(Root(), "HybridCPU_ISE", "CloseToHSL", "Core", "Runtime", "Memory", "AddressSpaces", "AddressSpaceId.cs"));
        string tag = File.ReadAllText(Path.Combine(Root(), "HybridCPU_ISE", "CloseToHSL", "Core", "Runtime", "Memory", "Translation", "NestedTlbTag.cs"));

        Assert.DoesNotContain("bool IsValid", addressSpace, StringComparison.Ordinal);
        Assert.DoesNotContain("bool IsValid", tag, StringComparison.Ordinal);
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
