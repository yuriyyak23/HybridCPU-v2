namespace HybridCPU_ISE.Tests.Architecture;

/// <summary>RF-12.8e paper boundary between execution and other raw DomainTag names.</summary>
public sealed class Rf128eDomainTagCrossFamilyAuthorityDecisionTests
{
    [Fact]
    public void PaperSeparatesExecutionDomainTagFromNoCCacheIommuAndSecurity()
    {
        string paper = File.ReadAllText(Path.Combine(Root(), "ResearchPaper", "section", "md base",
            "3_Architectural_Overview_and_Frontend_Contract.md"));
        Assert.Contains("#### Execution domain-tag cross-family boundary", paper, StringComparison.Ordinal);
        Assert.Contains("distinct from a NoC flit", paper, StringComparison.Ordinal);
        Assert.Contains("cache-isolation tag", paper, StringComparison.Ordinal);
        Assert.Contains("IOMMU binding/translation identity", paper, StringComparison.Ordinal);
        Assert.Contains("No raw equality, cast, copy, fallback", paper, StringComparison.Ordinal);
        Assert.Contains("Execution zero remains its valid baseline", paper, StringComparison.Ordinal);
        Assert.Contains("`DomainTag` type", paper, StringComparison.Ordinal);
    }

    [Fact]
    public void ExistingOwnerSurfacesRemainSeparate()
    {
        string root = Root();
        string cache = File.ReadAllText(Path.Combine(root, "HybridCPU_ISE", "CloseToHSL", "Core", "Execution",
            "Memory", "Cache", "CPU_Core.Cache.cs"));
        string noc = File.ReadAllText(Path.Combine(root, "HybridCPU_ISE", "CloseToHSL", "Core", "Execution",
            "StreamEngine", "BurstIO", "NoC_XY_Router.cs"));
        Assert.Contains("domainTag != 0", cache, StringComparison.Ordinal);
        Assert.Contains("DomainTag", noc, StringComparison.Ordinal);
    }

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
