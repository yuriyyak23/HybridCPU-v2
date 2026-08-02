namespace HybridCPU_ISE.Tests.Architecture;

public sealed class Rf129bqDonorAssistEpochConstructorEligibilityInventoryTests
{
    [Fact]
    public void PublicRawFactoriesConstructedTransportAndDefaultCompatibilitySeamRemainExplicit()
    {
        string runtime = Read("HybridCPU_ISE", "CloseToHSL", "Core", "Pipeline", "Assist", "Runtime", "AssistRuntime.cs");
        string factory = Read("HybridCPU_ISE", "CloseToHSL", "Core", "Pipeline", "MicroOps", "Assist", "AssistMicroOp.cs");
        string producer = Read("HybridCPU_ISE", "CloseToHSL", "Core", "Pipeline", "Assist", "InterCore", "CPU_Core.PipelineExecution.AssistInterCore.cs");
        string defaultTest = Read("HybridCPU_ISE.Tests", "tests", "Phase09CrossPodSchedulerPodBindingSeamTests.cs");
        string testAssembler = ReadTree("TestAssemblerConsoleApps");
        Assert.Contains("public AssistInterCoreTransport(", runtime, StringComparison.Ordinal);
        Assert.Contains("public AssistDonorSourceDescriptor(", runtime, StringComparison.Ordinal);
        Assert.Contains("public static bool TryCreateInterCoreTransportFromSeed(", factory, StringComparison.Ordinal);
        Assert.Contains("donorAssistEpochId: 0", factory, StringComparison.Ordinal);
        Assert.Contains("TryCreateInterCoreTransportFromSeed(", producer, StringComparison.Ordinal);
        Assert.Contains("new AssistInterCoreTransport()", defaultTest, StringComparison.Ordinal);
        Assert.DoesNotContain("TryCreateInterCoreTransportFromSeed(", testAssembler, StringComparison.Ordinal);
        Assert.DoesNotContain("new AssistInterCoreTransport(", testAssembler, StringComparison.Ordinal);
    }

    private static string Read(params string[] path) => File.ReadAllText(Path.Combine(new[] { FindRepositoryRoot() }.Concat(path).ToArray()));
    private static string ReadTree(params string[] path) => string.Join("\n", Directory.EnumerateFiles(Path.Combine(new[] { FindRepositoryRoot() }.Concat(path).ToArray()), "*.cs", SearchOption.AllDirectories).Select(File.ReadAllText));
    private static string FindRepositoryRoot()
    {
        DirectoryInfo? current = new(AppContext.BaseDirectory);
        while (current is not null)
        {
            if (Directory.Exists(Path.Combine(current.FullName, "HybridCPU_ISE"))) return current.FullName;
            current = current.Parent;
        }
        throw new DirectoryNotFoundException();
    }
}
