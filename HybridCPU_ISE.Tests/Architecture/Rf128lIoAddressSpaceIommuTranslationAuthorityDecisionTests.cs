namespace HybridCPU_ISE.Tests.Architecture;

/// <summary>RF-12.8l authority against I/O/address-space identity conflation.</summary>
public sealed class Rf128lIoAddressSpaceIommuTranslationAuthorityDecisionTests
{
    [Fact]
    public void PaperSeparatesAddressSpaceBindingAndIotlbCompositeIdentities()
    {
        string paper = File.ReadAllText(Path.Combine(Root(), "ResearchPaper", "section", "md base",
            "3_Architectural_Overview_and_Frontend_Contract.md"));
        Assert.Contains("#### I/O address-space and translation-identity boundary", paper, StringComparison.Ordinal);
        Assert.Contains("`AddressSpaceId` and `NestedTlbTag`", paper, StringComparison.Ordinal);
        Assert.Contains("`IommuDomainBinding` and its private `IoDomainKey`", paper, StringComparison.Ordinal);
        Assert.Contains("`IotlbTag` identifies", paper, StringComparison.Ordinal);
        Assert.Contains("no universal `DomainId`", paper, StringComparison.Ordinal);
        Assert.Contains("no common type", paper, StringComparison.Ordinal);
    }

    [Fact]
    public void ExistingOwnersKeepTheirLocalNormalizationAndFaultAuthority()
    {
        string root = Root();
        string binding = File.ReadAllText(Path.Combine(root, "HybridCPU_ISE", "CloseToHSL", "Core", "Runtime", "IO",
            "Dma", "DmaDomainBinding.cs"));
        string iommu = File.ReadAllText(Path.Combine(root, "HybridCPU_ISE", "CloseToHSL", "Memory", "MMU",
            "IOMMU.DomainBinding.cs"));
        Assert.Contains("DomainEpoch = epoch == 0 ? 1 : epoch", binding, StringComparison.Ordinal);
        Assert.Contains("DmaFaultKind.MissingDomainBinding", iommu, StringComparison.Ordinal);
        Assert.Contains("DmaFaultKind.PermissionFault", iommu, StringComparison.Ordinal);
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
