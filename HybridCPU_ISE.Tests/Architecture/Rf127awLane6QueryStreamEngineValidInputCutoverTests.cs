namespace HybridCPU_ISE.Tests.Architecture;

/// <summary>RF-12.7aw Lane-6 query StreamEngineId valid-input cutover.</summary>
public sealed class Rf127awLane6QueryStreamEngineValidInputCutoverTests
{
    [Fact]
    public void QueryMaskUsesCheckedStreamZeroAndKeepsDmaDomainRoles()
    {
        string source = File.ReadAllText(Path.Combine(Root(), "HybridCPU_ISE", "CloseToHSL", "Core", "Pipeline",
            "MicroOps", "Lane6DmaStream", "DmaStreamComputeQueryCapsMicroOp.cs"));

        Assert.Contains("StreamEngineId streamEngine = StreamEngineId.Zero;", source,
            StringComparison.Ordinal);
        Assert.Contains("ResourceMaskBuilder.ForStreamEngine(streamEngine)", source,
            StringComparison.Ordinal);
        Assert.DoesNotContain("ForStreamEngine(0)", source, StringComparison.Ordinal);
        Assert.Contains("ResourceMaskBuilder.ForDMAChannel(0)", source, StringComparison.Ordinal);
        Assert.Contains("ResourceMaskBuilder.ForMemoryDomain(resourceDomainBucket)", source,
            StringComparison.Ordinal);
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
