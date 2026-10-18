namespace HybridCPU_ISE.Tests.Architecture;

public sealed class Rf128wAddressSpaceNestedTlbCompatibilityEligibilityTests
{
    [Fact]
    public void ProductionCallerInventoryForCompositeCarriersIsNonzero()
    {
        string tlb = Read("HybridCPU_ISE", "CloseToHSL", "Memory", "MMU", "TLB.cs");
        string iommu = Read("HybridCPU_ISE", "CloseToHSL", "Memory", "MMU", "IOMMU.DomainBinding.cs");
        string composition = Read("HybridCPU_ISE", "CloseToHSL", "Core", "Runtime", "Nested", "MemoryComposition", "NestedMemoryCompositionService.cs");
        string walker = Read("HybridCPU_ISE", "CloseToHSL", "Core", "Runtime", "Memory", "Translation", "NestedPageWalker.Translate.partial.cs");

        Assert.Contains("TryTranslateNested(", tlb, StringComparison.Ordinal);
        Assert.Contains("InsertNested(", tlb, StringComparison.Ordinal);
        Assert.Contains("domainControl.ToAddressSpaceId", iommu, StringComparison.Ordinal);
        Assert.Contains("context.ToAddressSpaceId", composition, StringComparison.Ordinal);
        Assert.Contains("NestedTlbTag.Create(", walker, StringComparison.Ordinal);
    }

    [Fact]
    public void PublicCompositeDeclarationsRemainRequiredByCurrentCallers()
    {
        string addressSpace = Read("HybridCPU_ISE", "CloseToHSL", "Core", "Runtime", "Memory", "AddressSpaces", "AddressSpaceId.cs");
        string tag = Read("HybridCPU_ISE", "CloseToHSL", "Core", "Runtime", "Memory", "Translation", "NestedTlbTag.cs");

        Assert.Contains("public readonly record struct AddressSpaceId(", addressSpace, StringComparison.Ordinal);
        Assert.Contains("public readonly record struct NestedTlbTag(", tag, StringComparison.Ordinal);
        Assert.Contains("public static NestedTlbTag Create(", tag, StringComparison.Ordinal);
    }

    private static string Read(params string[] parts) => File.ReadAllText(Path.Combine(new[] { Root() }.Concat(parts).ToArray()));

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
