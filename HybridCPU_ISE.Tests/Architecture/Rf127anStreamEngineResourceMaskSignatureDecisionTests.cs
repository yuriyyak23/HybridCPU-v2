using System.Text.RegularExpressions;

namespace HybridCPU_ISE.Tests.Architecture;

/// <summary>RF-12.7an StreamEngineId ResourceMaskBuilder signature decision.</summary>
public sealed class Rf127anStreamEngineResourceMaskSignatureDecisionTests
{

    [Fact]
    public void ValidInputCallerContourIsResourceMaskOnlyAndAllActiveProductionSelectorsAreFixedZero()
    {
        string root = Root();
        string production = Path.Combine(root, "HybridCPU_ISE");
        string[] callers = Directory.EnumerateFiles(production, "*.cs", SearchOption.AllDirectories)
            .Where(path => !path.EndsWith("MicroOps\\Types\\MicroOp.cs", StringComparison.Ordinal))
            .Where(path => File.ReadAllText(path).Contains("ForStreamEngine(", StringComparison.Ordinal))
            .Select(path => Path.GetFileName(path)!)
            .OrderBy(name => name, StringComparer.Ordinal)
            .ToArray();

        Assert.Equal(
        [
            "AssistMicroOp.cs", "BundleLegalityAnalyzer.cs", "DmaStreamComputeMicroOp.cs",
            "DmaStreamComputeQueryCapsMicroOp.cs", "DmaStreamComputeStatusMicroOp.cs",
            "MatrixTileMicroOps.cs", "MicroOp.Compute.cs", "VectorMicroOps.cs"
        ], callers);
        Assert.DoesNotContain("StreamEngineId", string.Join("\n", callers), StringComparison.Ordinal);
        string allCallers = string.Join("\n", Directory.EnumerateFiles(production, "*.cs", SearchOption.AllDirectories)
            .Where(path => !path.EndsWith("MicroOps\\Types\\MicroOp.cs", StringComparison.Ordinal))
            .Where(path => File.ReadAllText(path).Contains("ForStreamEngine(", StringComparison.Ordinal))
            .Select(File.ReadAllText));
        Assert.DoesNotMatch(new Regex(@"ForStreamEngine\([1-9]"), allCallers);
    }

    [Fact]
    public void MatrixTileByteWireIsNotAResourceMaskSignatureCaller()
    {
        string transfer = File.ReadAllText(Path.Combine(Root(), "HybridCPU_ISE", "CloseToHSL", "Core", "ISA",
            "Instructions", "NonVmx", "Lanes00_03Vector", "MatrixTile", "MatrixTileStreamTransferAbi.cs"));

        Assert.Contains("byte StreamEngineChannel", transfer, StringComparison.Ordinal);
        Assert.DoesNotContain("ForStreamEngine(", transfer, StringComparison.Ordinal);
    }

    private static string ReadBuilder() => File.ReadAllText(Path.Combine(Root(), "HybridCPU_ISE", "CloseToHSL",
        "Core", "Pipeline", "MicroOps", "Types", "MicroOp.cs"));

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
