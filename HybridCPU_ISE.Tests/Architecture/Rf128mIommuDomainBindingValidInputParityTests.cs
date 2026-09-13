namespace HybridCPU_ISE.Tests.Architecture;

/// <summary>RF-12.8m valid-input signature parity for IOMMU domain binding.</summary>
public sealed class Rf128mIommuDomainBindingValidInputParityTests
{
    [Fact]
    public void CreateBindingAndTranslationKeepTheCompositeBindingCarrier()
    {
        string root = Root();
        string binding = Read(root, "Core", "Runtime", "IO", "Dma", "DmaDomainBinding.cs");
        string iommu = ReadMemory(root, "IOMMU.DomainBinding.cs");
        Assert.Contains("IommuDomainBinding Create(", binding, StringComparison.Ordinal);
        Assert.Contains("ushort ioDomainTag", binding, StringComparison.Ordinal);
        Assert.Contains("uint domainId", binding, StringComparison.Ordinal);
        Assert.Contains("ulong domainTag", binding, StringComparison.Ordinal);
        Assert.Contains("uint deviceId", binding, StringComparison.Ordinal);
        Assert.Contains("IOMMUAccessPermissions permissions", binding, StringComparison.Ordinal);
        Assert.Contains("BindIoDomain(IommuDomainBinding binding)", iommu, StringComparison.Ordinal);
        Assert.Contains("TryTranslateDma(", iommu, StringComparison.Ordinal);
        Assert.Contains("IommuDomainBinding binding", iommu, StringComparison.Ordinal);
    }

    [Fact]
    public void LiveCallersDoNotDecomposeTheBindingAtSignatureBoundary()
    {
        string root = Root();
        string hostBackend = ReadMemory(root, "IoVirtualizationHostBackend.cs");
        string descriptor = Read(root, "Core", "Execution", "DmaStreamCompute", "VmxDmaDescriptorValidator.cs");
        Assert.Contains("BindDomain(IommuDomainBinding binding)", hostBackend, StringComparison.Ordinal);
        Assert.Contains("IOMMU.TryTranslateDma(", descriptor, StringComparison.Ordinal);
        Assert.Contains("binding,", descriptor, StringComparison.Ordinal);
    }

    private static string Read(string root, params string[] parts) => File.ReadAllText(Path.Combine([root, "HybridCPU_ISE", "CloseToHSL", .. parts]));
    private static string ReadMemory(string root, string file) => File.ReadAllText(Path.Combine(root, "HybridCPU_ISE", "CloseToHSL", "Memory", "MMU", file));
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
