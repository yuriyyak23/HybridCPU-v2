namespace HybridCPU_ISE.Tests.Architecture;

/// <summary>RF-12.7ax Lane-6 StreamEngineId residual caller closure audit.</summary>
public sealed class Rf127axLane6StreamEngineCallerClosureAuditTests
{
    [Fact]
    public void EveryLane6StreamResourceMaskUsesCheckedSelector()
    {
        string directory = Path.Combine(Root(), "HybridCPU_ISE", "CloseToHSL", "Core", "Pipeline", "MicroOps",
            "Lane6DmaStream");
        string all = string.Join("\n", Directory.EnumerateFiles(directory, "*.cs", SearchOption.AllDirectories)
            .Select(File.ReadAllText));

        Assert.DoesNotContain("ForStreamEngine(0)", all, StringComparison.Ordinal);
        Assert.Equal(3, Count(all, "StreamEngineId streamEngine = StreamEngineId.Zero;"));
        Assert.Equal(3, Count(all, "ResourceMaskBuilder.ForStreamEngine(streamEngine)"));
    }

    [Fact]
    public void Lane6RetainsSeparateDmaAndDomainRawResources()
    {
        string directory = Path.Combine(Root(), "HybridCPU_ISE", "CloseToHSL", "Core", "Pipeline", "MicroOps",
            "Lane6DmaStream");
        string all = string.Join("\n", Directory.EnumerateFiles(directory, "*.cs", SearchOption.AllDirectories)
            .Select(File.ReadAllText));

        Assert.Equal(3, Count(all, "ResourceMaskBuilder.ForDMAChannel(0)"));
        Assert.Equal(3, Count(all, "ResourceMaskBuilder.ForMemoryDomain(resourceDomainBucket)"));
    }

    private static int Count(string source, string text) => source.Split(text, StringSplitOptions.None).Length - 1;

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
