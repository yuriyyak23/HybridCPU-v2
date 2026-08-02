using YAKSys_Hybrid_CPU.Core.Legality;

namespace HybridCPU_ISE.Tests.Architecture;

public sealed class Rf123jBundleLegalityRegisterAggregationValidInputCutoverTests
{
    [Fact]
    public void AccumulatorUsesIndependentCheckedPathsAndRetainsBothRawFallbacks()
    {
        string root = FindRepositoryRoot();
        string analyzer = File.ReadAllText(Path.Combine(
            root,
            "HybridCPU_ISE",
            "CloseToHSL",
            "Core",
            "Legality",
            "BundleLegalityAnalyzer.cs"));

        Assert.Equal(2, Count(analyzer, "ArchRegId.TryCreate("));
        Assert.Equal(1, Count(analyzer,
            "ResourceMaskBuilder.ForArchitecturalRegisterRead(architecturalRegisterId)"));
        Assert.Equal(1, Count(analyzer,
            "ResourceMaskBuilder.ForRegisterRead(registerId)"));
        Assert.Equal(1, Count(analyzer,
            "ResourceMaskBuilder.ForArchitecturalRegisterWrite(architecturalRegisterId)"));
        Assert.Equal(1, Count(analyzer,
            "ResourceMaskBuilder.ForRegisterWrite(registerId)"));
        Assert.Equal(2, Count(analyzer, "if ((uint)registerId < 64)"));
        Assert.Equal(1, Count(analyzer, "readRegisterMask |= 1UL << registerId"));
        Assert.Equal(1, Count(analyzer, "writeRegisterMask |= 1UL << registerId"));
        Assert.DoesNotContain("ArchRegId.Create(", analyzer, StringComparison.Ordinal);
        Assert.DoesNotContain("registerId = 0", ExtractAccumulator(analyzer),
            StringComparison.Ordinal);
        Assert.DoesNotContain("Math.Clamp", ExtractAccumulator(analyzer),
            StringComparison.Ordinal);
        Assert.DoesNotContain("%", ExtractAccumulator(analyzer), StringComparison.Ordinal);
    }

    [Fact]
    public void CutoverAddsNoSignatureStorageParserOrPublicSurface()
    {
        Type analyzerType = typeof(BundleLegalityAnalyzer);
        Assert.Single(analyzerType.GetMethods()
            .Where(method => method.Name == nameof(BundleLegalityAnalyzer.Analyze)));
        Assert.Empty(analyzerType.GetMethods()
            .Where(method => method.Name == "AccumulateDependencyInputs"));

        string root = FindRepositoryRoot();
        string production = ReadTree(root, "HybridCPU_ISE");
        string compiler = ReadTree(root, "HybridCPU_Compiler");
        string assembler = ReadTree(root, "TestAssemblerConsoleApps");
        Assert.Equal(1, Count(production,
            "new Core.Legality.BundleLegalityAnalyzer().Analyze(canonicalBundle)"));
        Assert.Equal(0, Count(compiler, "BundleLegalityAnalyzer"));
        Assert.Equal(0, Count(assembler, "BundleLegalityAnalyzer"));
    }

    private static string ExtractAccumulator(string analyzer)
    {
        int start = analyzer.IndexOf(
            "private static void AccumulateDependencyInputs",
            StringComparison.Ordinal);
        Assert.True(start >= 0);
        int end = analyzer.IndexOf(
            "private static DecodedSlotLegality[] BuildSlotLegalities",
            start,
            StringComparison.Ordinal);
        Assert.True(end > start);
        return analyzer[start..end];
    }

    private static string ReadTree(string root, string relativeRoot) =>
        string.Join("\n", Directory.EnumerateFiles(
                Path.Combine(root, relativeRoot), "*.cs", SearchOption.AllDirectories)
            .Where(path => !path.Contains(
                $"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}",
                StringComparison.OrdinalIgnoreCase))
            .Where(path => !path.Contains(
                $"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}",
                StringComparison.OrdinalIgnoreCase))
            .OrderBy(path => path, StringComparer.Ordinal)
            .Select(File.ReadAllText));

    private static int Count(string text, string value)
    {
        int count = 0;
        int offset = 0;
        while ((offset = text.IndexOf(value, offset, StringComparison.Ordinal)) >= 0)
        {
            count++;
            offset += value.Length;
        }

        return count;
    }

    private static string FindRepositoryRoot()
    {
        string? current = AppContext.BaseDirectory;
        while (current is not null)
        {
            if (File.Exists(Path.Combine(current, "HybridCPU v2.slnx")))
                return current;

            current = Directory.GetParent(current)?.FullName;
        }

        throw new DirectoryNotFoundException("HybridCPU repository root was not found.");
    }
}
