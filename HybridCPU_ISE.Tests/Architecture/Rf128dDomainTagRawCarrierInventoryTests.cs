namespace HybridCPU_ISE.Tests.Architecture;

/// <summary>RF-12.8d raw DomainTag carrier and cross-family inventory.</summary>
public sealed class Rf128dDomainTagRawCarrierInventoryTests
{
    [Fact]
    public void ExecutionAndNonExecutionDomainTagUsersAreBothExplicit()
    {
        string root = Root();
        string production = ReadAll(root, "HybridCPU_ISE");
        Assert.Contains("SlotPlacementMetadata", production, StringComparison.Ordinal);
        Assert.Contains("BundleResourceCertificate", production, StringComparison.Ordinal);
        Assert.Contains("CPU_Core.Cache", production, StringComparison.Ordinal);
        Assert.Contains("NoC_XY_Router", production, StringComparison.Ordinal);
        Assert.Contains("IOMMU.DomainBinding", production, StringComparison.Ordinal);
        Assert.Contains("DomainTagManager", production, StringComparison.Ordinal);
    }

    [Fact]
    public void BaselineZeroAndOwnerSpecificNonzeroPoliciesAreNotAliased()
    {
        string root = Root();
        string cache = Read(root, "HybridCPU_ISE", "CloseToHSL", "Core", "Execution", "Memory", "Cache",
            "CPU_Core.Cache.cs");
        string legacy = Read(root, "HybridCPU_ISE", "Legacy", "CloseToHSL", "Core", "Decoder",
            "DecodedBundleTransportProjector.cs");
        Assert.Contains("domainTag != 0", cache, StringComparison.Ordinal);
        Assert.Contains("canonicalAdmissionMetadata.DomainTag != 0", legacy, StringComparison.Ordinal);
    }

    private static string ReadAll(string root, string directory) => string.Join("\n", Directory.EnumerateFiles(
        Path.Combine(root, directory), "*.cs", SearchOption.AllDirectories).Select(File.ReadAllText));
    private static string Read(string root, params string[] parts) => File.ReadAllText(Path.Combine([root, .. parts]));
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
