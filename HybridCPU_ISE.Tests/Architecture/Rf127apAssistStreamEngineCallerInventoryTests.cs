namespace HybridCPU_ISE.Tests.Architecture;

/// <summary>RF-12.7ap Assist stream-engine caller migration inventory decision.</summary>
public sealed class Rf127apAssistStreamEngineCallerInventoryTests
{
    [Fact]
    public void AssistIsOnePairedCheckedMaskAndSafetyCallerContour()
    {
        string source = File.ReadAllText(Path.Combine(Root(), "HybridCPU_ISE", "CloseToHSL", "Core",
            "Pipeline", "MicroOps", "Assist", "AssistMicroOp.cs"));

        Assert.Contains("StreamEngineId streamEngine = StreamEngineId.Zero;", source,
            StringComparison.Ordinal);
        Assert.Equal(1, Count(source, "ResourceMaskBuilder.ForStreamEngine(streamEngine)"));
        Assert.Equal(1, Count(source, "ResourceMaskBuilder.ForStreamEngine128(streamEngine)"));
        Assert.Contains("ResourceMaskBuilder.ForDMAChannel(0)", source, StringComparison.Ordinal);
        Assert.Contains("SafetyMask = ResourceMaskBuilder.ForDMAChannel128(0)", source,
            StringComparison.Ordinal);
    }

    [Fact]
    public void OtherRawCallerFamiliesRemainOutsideThisSelection()
    {
        string root = Root();
        string all = string.Join("\n", Directory.EnumerateFiles(Path.Combine(root, "HybridCPU_ISE"), "*.cs",
            SearchOption.AllDirectories).Select(File.ReadAllText));

        Assert.Contains("DmaStreamComputeMicroOp.cs", string.Join("\n", Directory.EnumerateFiles(
            Path.Combine(root, "HybridCPU_ISE"), "DmaStreamComputeMicroOp.cs", SearchOption.AllDirectories)),
            StringComparison.Ordinal);
        Assert.DoesNotContain("ForStreamEngine(0)", all, StringComparison.Ordinal);
        Assert.DoesNotContain("ForStreamEngine128(0)", all, StringComparison.Ordinal);
        Assert.Equal(3, Count(all, "ForStreamEngine(StreamEngineId.Zero)"));
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
