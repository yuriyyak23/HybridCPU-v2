namespace HybridCPU_ISE.Tests.Architecture;

public sealed class Rf128tAddressSpaceNestedTlbValidInputParityTests
{
    [Fact]
    public void IommuPathRetainsAddressSpaceCarrierAcrossLookupInsertAndResult()
    {
        string text = File.ReadAllText(Path.Combine(Root(), "HybridCPU_ISE", "CloseToHSL", "Memory", "MMU", "IOMMU.DomainBinding.cs"));
        Assert.Contains("AddressSpaceId addressSpace = domainControl.ToAddressSpaceId", text, StringComparison.Ordinal);
        Assert.Contains("_tlb.TryTranslateNested(", text, StringComparison.Ordinal);
        Assert.Contains("addressSpace,", text, StringComparison.Ordinal);
        Assert.Contains("_tlb.InsertNested(", text, StringComparison.Ordinal);
        Assert.Contains("NestedTranslationResult.Success(", text, StringComparison.Ordinal);
    }

    [Fact]
    public void NestedTagCreationConsumesFullAddressSpaceIdentity()
    {
        string tag = File.ReadAllText(Path.Combine(Root(), "HybridCPU_ISE", "CloseToHSL", "Core", "Runtime", "Memory", "Translation", "NestedTlbTag.cs"));
        string tlb = File.ReadAllText(Path.Combine(Root(), "HybridCPU_ISE", "CloseToHSL", "Memory", "MMU", "TLB.cs"));
        Assert.Contains("NestedTlbTag Create(", tag, StringComparison.Ordinal);
        Assert.Contains("AddressSpaceId addressSpace", tag, StringComparison.Ordinal);
        Assert.Contains("NestedTlbTag.Create(guestVirtualAddress, addressSpace", tlb, StringComparison.Ordinal);
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
