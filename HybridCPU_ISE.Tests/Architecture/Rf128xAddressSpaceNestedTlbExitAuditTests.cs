namespace HybridCPU_ISE.Tests.Architecture;

public sealed class Rf128xAddressSpaceNestedTlbExitAuditTests
{
    [Fact]
    public void CompositeFamilyRetainsOnlyOwnerLocalIdentityAndFaultBoundaries()
    {
        string paper = Read("ResearchPaper", "section", "md base", "3_Architectural_Overview_and_Frontend_Contract.md");
        string tlb = Read("HybridCPU_ISE", "CloseToHSL", "Memory", "MMU", "TLB.cs");
        string iommu = Read("HybridCPU_ISE", "CloseToHSL", "Memory", "MMU", "IOMMU.DomainBinding.cs");

        Assert.Contains("Address-space and nested-TLB identity boundary", paper, StringComparison.Ordinal);
        Assert.Contains("not a canonical address-space or", paper, StringComparison.Ordinal);
        Assert.Contains("No global carrier `IsValid`", paper, StringComparison.Ordinal);
        Assert.Contains("FlushNestedBySecondStageRoot", tlb, StringComparison.Ordinal);
        Assert.Contains("FlushNestedByAddressSpaceTag", tlb, StringComparison.Ordinal);
        Assert.Contains("NestedTranslationResult.SecondStageMisconfiguration(", iommu, StringComparison.Ordinal);
    }

    [Fact]
    public void NoCrossFamilyOrCompatibilitySurfaceIsIntroduced()
    {
        string addressSpace = Read("HybridCPU_ISE", "CloseToHSL", "Core", "Runtime", "Memory", "AddressSpaces", "AddressSpaceId.cs");
        string tag = Read("HybridCPU_ISE", "CloseToHSL", "Core", "Runtime", "Memory", "Translation", "NestedTlbTag.cs");

        Assert.DoesNotContain("DomainId", addressSpace, StringComparison.Ordinal);
        Assert.DoesNotContain("DomainId", tag, StringComparison.Ordinal);
        Assert.DoesNotContain("MemoryBankId", addressSpace, StringComparison.Ordinal);
        Assert.DoesNotContain("LaneId", tag, StringComparison.Ordinal);
        Assert.DoesNotContain("Replay", addressSpace, StringComparison.Ordinal);
        Assert.DoesNotContain("Telemetry", tag, StringComparison.Ordinal);
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
