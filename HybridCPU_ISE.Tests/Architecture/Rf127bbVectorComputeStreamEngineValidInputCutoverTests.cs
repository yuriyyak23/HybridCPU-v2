namespace HybridCPU_ISE.Tests.Architecture;

/// <summary>RF-12.7bb vector compute StreamEngineId valid-input cutover.</summary>
public sealed class Rf127bbVectorComputeStreamEngineValidInputCutoverTests
{
    [Fact]
    public void ComputeMaskUsesCheckedStreamZeroBeforeUnchangedSafetyAndAdmission()
    {
        string source = File.ReadAllText(Path.Combine(Root(), "HybridCPU_ISE", "CloseToHSL", "Core", "Pipeline",
            "MicroOps", "Vector", "MicroOp.Compute.cs"));

        Assert.Contains("StreamEngineId streamEngine = StreamEngineId.Zero;", source,
            StringComparison.Ordinal);
        Assert.Contains("ResourceMask = ResourceMaskBuilder.ForStreamEngine(streamEngine)", source,
            StringComparison.Ordinal);
        Assert.DoesNotContain("ResourceMaskBuilder.ForStreamEngine(0)", source, StringComparison.Ordinal);
        Assert.Contains("ResourceMask |= ResourceMaskBuilder.ForLoad()", source, StringComparison.Ordinal);
        Assert.Contains("ResourceMask |= ResourceMaskBuilder.ForStore()", source, StringComparison.Ordinal);
        Assert.Contains("PublishExplicitStructuralSafetyMask", source, StringComparison.Ordinal);
        Assert.Contains("Math.Clamp(OwnerThreadId", source, StringComparison.Ordinal);
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
