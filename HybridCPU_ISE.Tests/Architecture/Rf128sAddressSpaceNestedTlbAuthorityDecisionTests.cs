namespace HybridCPU_ISE.Tests.Architecture;

public sealed class Rf128sAddressSpaceNestedTlbAuthorityDecisionTests
{
    [Fact]
    public void PaperRetainsFullAddressSpaceAndDerivedNestedTlbIdentities()
    {
        string paper = File.ReadAllText(Path.Combine(Root(), "ResearchPaper", "section", "md base", "3_Architectural_Overview_and_Frontend_Contract.md"));
        Assert.Contains("#### Address-space and nested-TLB identity boundary", paper, StringComparison.Ordinal);
        Assert.Contains("complete second-stage address-space composite", paper, StringComparison.Ordinal);
        Assert.Contains("`NestedTlbTag` derives", paper, StringComparison.Ordinal);
        Assert.Contains("Neither may be narrowed", paper, StringComparison.Ordinal);
        Assert.Contains("raw address-space tag, root identity", paper, StringComparison.Ordinal);
        Assert.Contains("no new checked wrapper", paper, StringComparison.Ordinal);
    }

    [Fact]
    public void ExistingFlushSelectorsRemainTlbOwnerLocal()
    {
        string tlb = File.ReadAllText(Path.Combine(Root(), "HybridCPU_ISE", "CloseToHSL", "Memory", "MMU", "TLB.cs"));
        Assert.Contains("FlushNestedBySecondStageRoot", tlb, StringComparison.Ordinal);
        Assert.Contains("FlushNestedByAddressSpaceTag", tlb, StringComparison.Ordinal);
        Assert.Contains("FlushNestedSingleAddress(AddressSpaceId addressSpace", tlb, StringComparison.Ordinal);
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
