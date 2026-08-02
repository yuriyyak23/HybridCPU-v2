namespace HybridCPU_ISE.Tests.Architecture;

/// <summary>RF-12.7bc residual vector StreamEngineId caller closure audit.</summary>
public sealed class Rf127bcVectorStreamEngineCallerClosureAuditTests
{
    [Fact]
    public void VectorCallersUseTheCheckedFixedStreamSelector()
    {
        string vectorDirectory = Path.Combine(Root(), "HybridCPU_ISE", "CloseToHSL", "Core", "Pipeline",
            "MicroOps", "Vector");
        string source = string.Join("\n", Directory.GetFiles(vectorDirectory, "*.cs")
            .Select(File.ReadAllText));

        Assert.DoesNotContain("ForStreamEngine(0)", source, StringComparison.Ordinal);
        Assert.Equal(2, Count(source, "StreamEngineId streamEngine = StreamEngineId.Zero;"));
        Assert.Equal(2, Count(source, "ForStreamEngine(streamEngine)"));
        Assert.Contains("if (readsMemory || writesMemory)", source, StringComparison.Ordinal);
        Assert.Contains("Math.Clamp(OwnerThreadId", source, StringComparison.Ordinal);
    }

    [Fact]
    public void RemainingRawSelectorBelongsOnlyToTheSeparateMatrixTileContour()
    {
        string core = Path.Combine(Root(), "HybridCPU_ISE", "CloseToHSL", "Core");
        string[] files = Directory.GetFiles(core, "*.cs", SearchOption.AllDirectories);
        string[] rawSelectorFiles = files
            .Where(path => File.ReadAllText(path).Contains(
                "ForStreamEngine(MatrixTileResourceContour.StreamEngineChannel)", StringComparison.Ordinal))
            .Select(path => Path.GetFileName(path) ?? throw new InvalidOperationException())
            .OrderBy(name => name, StringComparer.Ordinal)
            .ToArray();

        Assert.Empty(rawSelectorFiles);
    }

    private static int Count(string text, string value) => text.Split(value, StringSplitOptions.None).Length - 1;

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
