namespace HybridCPU_ISE.Tests.Architecture;

/// <summary>RF-12.8p caller-inventory guard against premature IOMMU binding API removal.</summary>
public sealed class Rf128pIommuDomainBindingCompatibilityEligibilityTests
{
    [Fact]
    public void RawCreateAndCompositeBindingIngressStillHaveLiveCallers()
    {
        string root = Root();
        string binding = Read(root, "Core", "Runtime", "IO", "Dma", "DmaDomainBinding.cs");
        string ioBlock = Read(root, "Core", "Runtime", "Domains", "Descriptors", "IoDomain", "IoVirtualizationBlock.cs");
        string backend = ReadMemory(root, "IoVirtualizationHostBackend.cs");
        string vmx = File.ReadAllText(Path.Combine(root, "HybridCPU_ISE.Tests", "VmxRefactoring", "IoDomainAuthorityBoundaryTests.cs"));
        Assert.Contains("public static IommuDomainBinding Create(", binding, StringComparison.Ordinal);
        Assert.Contains("IommuDomainBinding BindDomain(IommuDomainBinding binding)", ioBlock, StringComparison.Ordinal);
        Assert.Contains("BindDomain(IommuDomainBinding binding)", backend, StringComparison.Ordinal);
        Assert.Contains("IommuDomainBinding.Create(", vmx, StringComparison.Ordinal);
    }

    [Fact]
    public void NoReplacementCheckedBindingSurfaceIsPresent()
    {
        string root = Root();
        Assert.False(File.Exists(Path.Combine(root, "HybridCPU_ISE", "CloseToHSL", "Core", "Runtime", "IO", "Dma", "CheckedIommuDomainBinding.cs")));
        Assert.False(File.Exists(Path.Combine(root, "HybridCPU_ISE", "CloseToHSL", "Memory", "MMU", "IommuBindingId.cs")));
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
