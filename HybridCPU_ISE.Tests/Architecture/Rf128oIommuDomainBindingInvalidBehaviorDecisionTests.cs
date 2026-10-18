namespace HybridCPU_ISE.Tests.Architecture;

/// <summary>RF-12.8o authority against IOMMU binding invalid-result conflation.</summary>
public sealed class Rf128oIommuDomainBindingInvalidBehaviorDecisionTests
{
    [Fact]
    public void PaperRetainsUnboundAndTranslationAbortOutcomesSeparately()
    {
        string paper = File.ReadAllText(Path.Combine(Root(), "ResearchPaper", "section", "md base",
            "3_Architectural_Overview_and_Frontend_Contract.md"));
        Assert.Contains("#### IOMMU binding invalid-behavior ownership boundary", paper, StringComparison.Ordinal);
        Assert.Contains("invalid or default IOMMU binding remains unbound", paper, StringComparison.Ordinal);
        Assert.Contains("missing/stale binding, descriptor-range", paper, StringComparison.Ordinal);
        Assert.Contains("permission denial", paper, StringComparison.Ordinal);
        Assert.Contains("not execution-domain outcomes", paper, StringComparison.Ordinal);
        Assert.Contains("No shared invalid API", paper, StringComparison.Ordinal);
    }

    [Fact]
    public void BindingAndTranslationOwnersRetainExistingDistinctResults()
    {
        string root = Root();
        string iommu = File.ReadAllText(Path.Combine(root, "HybridCPU_ISE", "CloseToHSL", "Memory", "MMU",
            "IOMMU.DomainBinding.cs"));
        Assert.Contains("if (!effective.IsValid)", iommu, StringComparison.Ordinal);
        Assert.Contains("DmaFaultKind.MissingDomainBinding", iommu, StringComparison.Ordinal);
        Assert.Contains("DmaFaultKind.DescriptorFault", iommu, StringComparison.Ordinal);
        Assert.Contains("DmaFaultKind.PermissionFault", iommu, StringComparison.Ordinal);
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
