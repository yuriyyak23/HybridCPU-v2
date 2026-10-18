namespace HybridCPU_ISE.Tests.Architecture;

public sealed class Rf128uAddressSpaceNestedTlbInvalidInputInventoryTests
{
    [Fact]
    public void InvalidTranslationControlsRemainOwnerLocalSecondStageFaults()
    {
        string iommu = Read("HybridCPU_ISE", "CloseToHSL", "Memory", "MMU", "IOMMU.DomainBinding.cs");
        string composition = Read("HybridCPU_ISE", "CloseToHSL", "Core", "Runtime", "Nested", "MemoryComposition", "NestedMemoryCompositionService.cs");

        Assert.Contains("if (!domainControl.IsValid)", iommu, StringComparison.Ordinal);
        Assert.Contains("NestedTranslationResult.SecondStageMisconfiguration(", iommu, StringComparison.Ordinal);
        Assert.Contains("if (!context.IsValid)", composition, StringComparison.Ordinal);
        Assert.Contains("NestedTranslationResult.SecondStageMisconfiguration(", composition, StringComparison.Ordinal);
    }

    [Fact]
    public void PublicCompositeCarriersHaveNoIndependentValidityOrZeroNormalization()
    {
        string addressSpace = Read("HybridCPU_ISE", "CloseToHSL", "Core", "Runtime", "Memory", "AddressSpaces", "AddressSpaceId.cs");
        string nestedTag = Read("HybridCPU_ISE", "CloseToHSL", "Core", "Runtime", "Memory", "Translation", "NestedTlbTag.cs");

        Assert.DoesNotContain("bool IsValid", addressSpace, StringComparison.Ordinal);
        Assert.DoesNotContain("== 0 ?", addressSpace, StringComparison.Ordinal);
        Assert.DoesNotContain("bool IsValid", nestedTag, StringComparison.Ordinal);
        Assert.Contains("AddressSpaceId AddressSpace", nestedTag, StringComparison.Ordinal);
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
