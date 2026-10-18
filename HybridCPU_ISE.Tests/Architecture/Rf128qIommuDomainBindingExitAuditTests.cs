namespace HybridCPU_ISE.Tests.Architecture;

/// <summary>RF-12.8q final closed-world audit of IOMMU domain binding.</summary>
public sealed class Rf128qIommuDomainBindingExitAuditTests
{
    [Fact]
    public void BindingKeysTranslationAndPublicationContoursRemainExplicit()
    {
        string root = Root();
        string binding = Read(root, "Core", "Runtime", "IO", "Dma", "DmaDomainBinding.cs");
        string iommu = ReadMemory(root, "IOMMU.DomainBinding.cs");
        string iotlb = ReadMemory(root, "IotlbTag.cs");
        Assert.Contains("record struct IommuDomainBinding", binding, StringComparison.Ordinal);
        Assert.Contains("record struct DmaTranslationResult", binding, StringComparison.Ordinal);
        Assert.Contains("IotlbTag Tag", binding, StringComparison.Ordinal);
        Assert.Contains("record struct IoDomainKey", iommu, StringComparison.Ordinal);
        Assert.Contains("Dictionary<IoDomainKey, IommuDomainBinding>", iommu, StringComparison.Ordinal);
        Assert.Contains("Dictionary<IotlbTag, IotlbEntry>", iommu, StringComparison.Ordinal);
        Assert.Contains("IotlbTag.Create", iommu, StringComparison.Ordinal);
        Assert.Contains("record struct IotlbTag", iotlb, StringComparison.Ordinal);
    }

    [Fact]
    public void RuntimeDescriptorLaneAndFaultOwnersRemainSeparate()
    {
        string root = Root();
        string descriptor = Read(root, "Core", "Runtime", "Memory", "Iommu", "IommuDomainDescriptor.cs");
        string lane6 = Read(root, "Core", "Runtime", "Lanes", "Lane6", "Lane6QueueRuntime.cs");
        string authority = Read(root, "Core", "Runtime", "IO", "Dma", "DmaAuthorityService.cs");
        Assert.Contains("IommuDomainBinding Binding", descriptor, StringComparison.Ordinal);
        Assert.Contains("IommuDomainBinding binding", lane6, StringComparison.Ordinal);
        Assert.Contains("DmaFaultKind.MissingDomainBinding", authority, StringComparison.Ordinal);
        Assert.False(File.Exists(Path.Combine(root, "HybridCPU_ISE", "CloseToHSL", "Core", "Runtime", "IO", "Dma", "UniversalDomainId.cs")));
    }

    private static string Read(string root, params string[] parts) => File.ReadAllText(Path.Combine([root, "HybridCPU_ISE", "CloseToHSL", .. parts]));
    private static string ReadMemory(string root, string file) => File.ReadAllText(Path.Combine(root, "HybridCPU_ISE", "CloseToHSL", "Memory", "MMU", file));
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
