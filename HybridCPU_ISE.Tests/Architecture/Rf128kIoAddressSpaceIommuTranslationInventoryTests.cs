namespace HybridCPU_ISE.Tests.Architecture;

/// <summary>RF-12.8k closed-world inventory for I/O translation identity families.</summary>
public sealed class Rf128kIoAddressSpaceIommuTranslationInventoryTests
{
    [Fact]
    public void AddressSpaceAndNestedTlbIdentityKeepTheirOwnCompositeShapes()
    {
        string root = Root();
        string addressSpace = Read(root, "Core", "Runtime", "Memory", "AddressSpaces", "AddressSpaceId.cs");
        string nestedTag = Read(root, "Core", "Runtime", "Memory", "Translation", "NestedTlbTag.cs");
        Assert.Contains("record struct AddressSpaceId", addressSpace, StringComparison.Ordinal);
        Assert.Contains("ulong SecondStageEpoch", addressSpace, StringComparison.Ordinal);
        Assert.Contains("ulong AddressSpaceTagEpoch", addressSpace, StringComparison.Ordinal);
        Assert.Contains("record struct NestedTlbTag", nestedTag, StringComparison.Ordinal);
        Assert.Contains("AddressSpaceId AddressSpace", nestedTag, StringComparison.Ordinal);
        Assert.Contains("TranslationEpoch", nestedTag, StringComparison.Ordinal);
    }

    [Fact]
    public void IommuBindingKeyAndIotlbTagRemainSeparateDictionaryKeys()
    {
        string root = Root();
        string binding = Read(root, "Core", "Runtime", "IO", "Dma", "DmaDomainBinding.cs");
        string iotlb = ReadMemory(root, "IotlbTag.cs");
        string iommu = ReadMemory(root, "IOMMU.DomainBinding.cs");
        Assert.Contains("record struct IommuDomainBinding", binding, StringComparison.Ordinal);
        Assert.Contains("DomainEpoch = epoch == 0 ? 1 : epoch", binding, StringComparison.Ordinal);
        Assert.Contains("record struct IotlbTag", iotlb, StringComparison.Ordinal);
        Assert.Contains("mappingEpoch == 0 ? 1 : mappingEpoch", iotlb, StringComparison.Ordinal);
        Assert.Contains("record struct IoDomainKey", iommu, StringComparison.Ordinal);
        Assert.Contains("Dictionary<IoDomainKey, IommuDomainBinding>", iommu, StringComparison.Ordinal);
        Assert.Contains("Dictionary<IotlbTag, IotlbEntry>", iommu, StringComparison.Ordinal);
    }

    [Fact]
    public void TranslationOwnersKeepInvalidBindingAndPermissionOutcomes()
    {
        string iommu = ReadMemory(Root(), "IOMMU.DomainBinding.cs");
        Assert.Contains("DmaFaultKind.MissingDomainBinding", iommu, StringComparison.Ordinal);
        Assert.Contains("DmaFaultKind.DescriptorFault", iommu, StringComparison.Ordinal);
        Assert.Contains("DmaFaultKind.PermissionFault", iommu, StringComparison.Ordinal);
        Assert.Contains("IotlbTag.Create", iommu, StringComparison.Ordinal);
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
