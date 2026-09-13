namespace HybridCPU_ISE.Tests.Architecture;

public sealed class Rf128rAddressSpaceNestedTlbInventoryTests
{
    [Fact]
    public void AddressSpaceAndTagRetainCompositeConstructionAndEpochShape()
    {
        string root = Root();
        string address = Read(root, "Core", "Runtime", "Memory", "AddressSpaces", "AddressSpaceId.cs");
        string tag = Read(root, "Core", "Runtime", "Memory", "Translation", "NestedTlbTag.cs");
        Assert.Contains("ushort DomainTag", address, StringComparison.Ordinal);
        Assert.Contains("ulong SecondStageEpoch", address, StringComparison.Ordinal);
        Assert.Contains("ulong AddressSpaceGeneration", address, StringComparison.Ordinal);
        Assert.Contains("AddressSpaceId AddressSpace", tag, StringComparison.Ordinal);
        Assert.Contains("TranslationEpoch", tag, StringComparison.Ordinal);
        Assert.Contains("addressSpace.SecondStageEpoch ^", tag, StringComparison.Ordinal);
    }

    [Fact]
    public void TlbStorageLookupFlushAndPublicationKeepDistinctOwners()
    {
        string root = Root();
        string tlb = ReadMemory(root, "TLB.cs");
        string iommu = ReadMemory(root, "IOMMU.DomainBinding.cs");
        string result = Read(root, "Core", "Runtime", "Memory", "Translation", "NestedTranslationResult.cs");
        Assert.Contains("public NestedTlbTag NestedTag", tlb, StringComparison.Ordinal);
        Assert.Contains("TryTranslateNested(", tlb, StringComparison.Ordinal);
        Assert.Contains("InsertNested(", tlb, StringComparison.Ordinal);
        Assert.Contains("FlushNestedBySecondStageRoot", tlb, StringComparison.Ordinal);
        Assert.Contains("FlushNestedByAddressSpaceTag", tlb, StringComparison.Ordinal);
        Assert.Contains("domainControl.ToAddressSpaceId", iommu, StringComparison.Ordinal);
        Assert.Contains("NestedTlbTag TlbTag", result, StringComparison.Ordinal);
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
