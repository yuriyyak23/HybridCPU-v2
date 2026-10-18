namespace HybridCPU_ISE.Tests.Architecture;

/// <summary>RF-12.8n inventory of IOMMU domain-binding invalid-input outcomes.</summary>
public sealed class Rf128nIommuDomainBindingInvalidInputInventoryTests
{
    [Fact]
    public void BindingValidityAndEpochNormalizationRemainExplicit()
    {
        string binding = ReadBinding();
        Assert.Contains("IoDomainTag != 0", binding, StringComparison.Ordinal);
        Assert.Contains("DomainId != 0", binding, StringComparison.Ordinal);
        Assert.Contains("DomainTag != 0", binding, StringComparison.Ordinal);
        Assert.Contains("DeviceId != 0", binding, StringComparison.Ordinal);
        Assert.Contains("Permissions != IOMMUAccessPermissions.None", binding, StringComparison.Ordinal);
        Assert.Contains("DomainEpoch != 0", binding, StringComparison.Ordinal);
        Assert.Contains("DomainEpoch = epoch == 0 ? 1 : epoch", binding, StringComparison.Ordinal);
    }

    [Fact]
    public void BindAndTranslateKeepDistinctInvalidOutcomes()
    {
        string iommu = ReadIommu();
        Assert.Contains("if (!effective.IsValid)", iommu, StringComparison.Ordinal);
        Assert.Contains("return default;", iommu, StringComparison.Ordinal);
        Assert.Contains("DmaFaultKind.MissingDomainBinding", iommu, StringComparison.Ordinal);
        Assert.Contains("DmaFaultKind.DescriptorFault", iommu, StringComparison.Ordinal);
        Assert.Contains("DmaFaultKind.PermissionFault", iommu, StringComparison.Ordinal);
        Assert.Contains("mappingEpoch == 0 ? 1 : mappingEpoch", ReadIotlb(), StringComparison.Ordinal);
    }

    private static string ReadBinding() => File.ReadAllText(Path.Combine(Root(), "HybridCPU_ISE", "CloseToHSL", "Core", "Runtime", "IO", "Dma", "DmaDomainBinding.cs"));
    private static string ReadIommu() => File.ReadAllText(Path.Combine(Root(), "HybridCPU_ISE", "CloseToHSL", "Memory", "MMU", "IOMMU.DomainBinding.cs"));
    private static string ReadIotlb() => File.ReadAllText(Path.Combine(Root(), "HybridCPU_ISE", "CloseToHSL", "Memory", "MMU", "IotlbTag.cs"));
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
