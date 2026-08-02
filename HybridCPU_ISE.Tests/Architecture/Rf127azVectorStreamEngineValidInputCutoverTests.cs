namespace HybridCPU_ISE.Tests.Architecture;

/// <summary>RF-12.7az common vector StreamEngineId valid-input cutover.</summary>
public sealed class Rf127azVectorStreamEngineValidInputCutoverTests
{
    [Fact]
    public void CommonMemoryGateCreatesCheckedStreamZeroInsideExistingGate()
    {
        string source = File.ReadAllText(Path.Combine(Root(), "HybridCPU_ISE", "CloseToHSL", "Core", "Pipeline",
            "MicroOps", "Vector", "VectorMicroOps.cs"));

        Assert.Contains("if (readsMemory || writesMemory)", source, StringComparison.Ordinal);
        Assert.Contains("StreamEngineId streamEngine = StreamEngineId.Zero;", source,
            StringComparison.Ordinal);
        Assert.Contains("ResourceMaskBuilder.ForStreamEngine(streamEngine)", source,
            StringComparison.Ordinal);
        Assert.DoesNotContain("ForStreamEngine(0)", source, StringComparison.Ordinal);
        Assert.Contains("RefreshAdmissionMetadata", source, StringComparison.Ordinal);
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
