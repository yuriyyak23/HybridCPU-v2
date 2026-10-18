namespace HybridCPU_ISE.Tests.Architecture;

public sealed class Rf072ahExplicitPacketBoolAdapterFamilyInventoryTests
{
    [Fact]
    public void GenericExplicitPacketEligibleFamilies_HaveOnlyAlreadyLedgeredExecuteFalseContours()
    {
        string root = FindRepositoryRoot();
        string microOps = Path.Combine(root, "HybridCPU_ISE", "CloseToHSL", "Core", "Pipeline", "MicroOps");
        string[] excluded = ["VectorMicroOps.Memory.cs", "VectorMicroOps.Data.cs"];

        string[] candidates = Directory.GetFiles(microOps, "*.cs", SearchOption.AllDirectories)
            .Where(path => !excluded.Contains(Path.GetFileName(path), StringComparer.Ordinal))
            .Where(path => path.Contains($"{Path.DirectorySeparatorChar}Vector{Path.DirectorySeparatorChar}", StringComparison.Ordinal) ||
                           path.Contains($"{Path.DirectorySeparatorChar}Lane6DmaStream{Path.DirectorySeparatorChar}", StringComparison.Ordinal) ||
                           path.Contains($"{Path.DirectorySeparatorChar}Lane7Accelerator{Path.DirectorySeparatorChar}", StringComparison.Ordinal) ||
                           path.Contains($"{Path.DirectorySeparatorChar}MatrixTile{Path.DirectorySeparatorChar}", StringComparison.Ordinal))
            .ToArray();

        foreach (string path in candidates)
        {
            string source = File.ReadAllText(path);
            Assert.DoesNotContain("return false;", ExtractExecuteBodies(source), StringComparison.Ordinal);
        }

        string memory = File.ReadAllText(Path.Combine(microOps, "Vector", "VectorMicroOps.Memory.cs"));
        Assert.Equal(2, Count(memory, "return false; //"));
        Assert.Contains("TryAcceptVectorSegmentLoad", memory, StringComparison.Ordinal);
        Assert.Contains("TryTakeCompletion", memory, StringComparison.Ordinal);
        Assert.Contains("StoreSegmentMicroOp.Execute()", memory, StringComparison.Ordinal);

        string vectorData = File.ReadAllText(Path.Combine(microOps, "Vector", "VectorMicroOps.Data.cs"));
        Assert.Contains("public sealed class VectorTransferMicroOp", vectorData, StringComparison.Ordinal);
        Assert.Contains("TryAcceptCanonicalVectorTransfer", vectorData, StringComparison.Ordinal);
        Assert.Contains("TryTakeCompletion", vectorData, StringComparison.Ordinal);
        Assert.Contains("return false;", vectorData, StringComparison.Ordinal);
    }

    private static string ExtractExecuteBodies(string source)
    {
        int start = 0;
        var result = new System.Text.StringBuilder();
        const string marker = "override bool Execute";
        while ((start = source.IndexOf(marker, start, StringComparison.Ordinal)) >= 0)
        {
            int next = source.IndexOf("public override", start + marker.Length, StringComparison.Ordinal);
            result.Append(next < 0 ? source[start..] : source[start..next]);
            start += marker.Length;
        }
        return result.ToString();
    }

    private static int Count(string source, string value)
    {
        int count = 0, index = 0;
        while ((index = source.IndexOf(value, index, StringComparison.Ordinal)) >= 0) { count++; index += value.Length; }
        return count;
    }

    private static string FindRepositoryRoot()
    {
        for (DirectoryInfo? current = new(AppContext.BaseDirectory); current is not null; current = current.Parent)
            if (Directory.Exists(Path.Combine(current.FullName, "HybridCPU_ISE"))) return current.FullName;
        throw new DirectoryNotFoundException("Repository root was not found.");
    }
}
