namespace HybridCPU_ISE.Tests.Architecture;

/// <summary>RF-12.7as main Lane-6 StreamEngineId valid-input cutover.</summary>
public sealed class Rf127asDmaStreamComputeStreamEngineValidInputCutoverTests
{
    [Fact]
    public void MainLane6MaskUsesCheckedStreamZeroWithoutChangingOtherRoles()
    {
        string source = Read("DmaStreamComputeMicroOp.cs");

        Assert.Contains("StreamEngineId streamEngine = StreamEngineId.Zero;", source,
            StringComparison.Ordinal);
        Assert.Contains("ResourceMaskBuilder.ForStreamEngine(streamEngine)", source,
            StringComparison.Ordinal);
        Assert.DoesNotContain("ForStreamEngine(0)", source, StringComparison.Ordinal);
        Assert.Contains("ResourceMaskBuilder.ForDMAChannel(0)", source, StringComparison.Ordinal);
        Assert.Contains("ResourceMaskBuilder.ForLoad()", source, StringComparison.Ordinal);
        Assert.Contains("ResourceMaskBuilder.ForStore()", source, StringComparison.Ordinal);
        Assert.Contains("ResourceMaskBuilder.ForMemoryDomain(resourceDomainBucket)", source,
            StringComparison.Ordinal);
    }


    private static string Read(string file) => File.ReadAllText(Path.Combine(Root(), "HybridCPU_ISE", "CloseToHSL",
        "Core", "Pipeline", "MicroOps", "Lane6DmaStream", file));

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
