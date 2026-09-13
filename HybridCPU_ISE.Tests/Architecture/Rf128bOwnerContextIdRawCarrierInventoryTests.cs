namespace HybridCPU_ISE.Tests.Architecture;

/// <summary>RF-12.8b OwnerContextId declaration and carrier inventory.</summary>
public sealed class Rf128bOwnerContextIdRawCarrierInventoryTests
{
    [Fact]
    public void PipelineExternalAndTestSupportContoursRemainSeparate()
    {
        string root = Root();
        string production = ReadAll(root, "HybridCPU_ISE");
        string tests = ReadAll(root, "HybridCPU_ISE.Tests");
        Assert.Contains("ScalarWriteBackLaneState", production, StringComparison.Ordinal);
        Assert.Contains("DmaStreamComputeReplayEvidence", production, StringComparison.Ordinal);
        Assert.Contains("AcceleratorOwnerDomainGuard", production, StringComparison.Ordinal);
        Assert.True(File.Exists(Path.Combine(root, "HybridCPU_ISE", "CloseToHSL", "Core", "Pipeline", "Core",
            "CPU_Core.TestSupport.cs")));
        Assert.Contains("OwnerContextId = 0", tests, StringComparison.Ordinal);
        Assert.DoesNotContain("typeof(OwnerContextId)", production, StringComparison.Ordinal);
    }

    private static string ReadAll(string root, string directory) => string.Join("\n", Directory.EnumerateFiles(
        Path.Combine(root, directory), "*.cs", SearchOption.AllDirectories).Select(File.ReadAllText));
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
