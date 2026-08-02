namespace HybridCPU_ISE.Tests.Architecture;

/// <summary>RF-12.7aq Assist StreamEngineId paired valid-input cutover.</summary>
public sealed class Rf127aqAssistStreamEngineValidInputCutoverTests
{
    [Fact]
    public void DmaAssistUsesOneCheckedZeroForPairedStreamMasks()
    {
        string source = File.ReadAllText(Path.Combine(Root(), "HybridCPU_ISE", "CloseToHSL", "Core",
            "Pipeline", "MicroOps", "Assist", "AssistMicroOp.cs"));

        Assert.Contains("StreamEngineId streamEngine = StreamEngineId.Zero;", source,
            StringComparison.Ordinal);
        Assert.Contains("ResourceMaskBuilder.ForStreamEngine(streamEngine)", source,
            StringComparison.Ordinal);
        Assert.Contains("ResourceMaskBuilder.ForStreamEngine128(streamEngine)", source,
            StringComparison.Ordinal);
        Assert.DoesNotContain("ForStreamEngine(0)", source, StringComparison.Ordinal);
        Assert.DoesNotContain("ForStreamEngine128(0)", source, StringComparison.Ordinal);
    }

    [Fact]
    public void OtherRawCallerContoursAndDmaMasksRemainUnchanged()
    {
        string root = Root();
        string assist = File.ReadAllText(Path.Combine(root, "HybridCPU_ISE", "CloseToHSL", "Core", "Pipeline",
            "MicroOps", "Assist", "AssistMicroOp.cs"));
        string all = string.Join("\n", Directory.EnumerateFiles(Path.Combine(root, "HybridCPU_ISE"), "*.cs",
            SearchOption.AllDirectories).Select(File.ReadAllText));

        Assert.Contains("ResourceMaskBuilder.ForDMAChannel(0)", assist, StringComparison.Ordinal);
        Assert.Contains("ResourceMaskBuilder.ForDMAChannel128(0)", assist, StringComparison.Ordinal);
        Assert.DoesNotContain("ForStreamEngine(0)", all, StringComparison.Ordinal);
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
