namespace HybridCPU_ISE.Tests.Architecture;

public sealed class Rf129bsDonorAssistEpochExitAuditTests
{
    [Fact]
    public void ClosedWorldDonorObservationContourRetainsOnlyDocumentedRawSurfaces()
    {
        string runtime = Read("HybridCPU_ISE", "CloseToHSL", "Core", "Pipeline", "Assist", "Runtime", "AssistRuntime.cs");
        string factory = Read("HybridCPU_ISE", "CloseToHSL", "Core", "Pipeline", "MicroOps", "Assist", "AssistMicroOp.cs");
        string scheduler = Read("HybridCPU_ISE", "CloseToHSL", "Core", "Pipeline", "Assist", "InterCore", "MicroOpScheduler.Assist.InterCore.cs");
        string execution = Read("HybridCPU_ISE", "CloseToHSL", "Core", "Pipeline", "Assist", "Runtime", "CPU_Core.Assist.cs");
        string testAssembler = ReadTree("TestAssemblerConsoleApps");
        Assert.Contains("public ulong DonorAssistEpochId", runtime, StringComparison.Ordinal);
        Assert.Contains("public ulong SourceAssistEpochId", runtime, StringComparison.Ordinal);
        Assert.Contains("sourceAssistEpochId: 0", runtime, StringComparison.Ordinal);
        Assert.Contains("donorAssistEpochId: 0", factory, StringComparison.Ordinal);
        Assert.Contains("transport.DonorAssistEpochId", factory, StringComparison.Ordinal);
        Assert.Contains("ownerSnapshot.AssistEpochId != transport.DonorAssistEpochId", scheduler, StringComparison.Ordinal);
        Assert.Contains("donorSnapshot.AssistEpochId != assistMicroOp.DonorSource.SourceAssistEpochId", execution, StringComparison.Ordinal);
        Assert.DoesNotContain("struct DonorAssistEpoch", runtime, StringComparison.Ordinal);
        Assert.DoesNotContain("DonorAssistEpochId", testAssembler, StringComparison.Ordinal);
        Assert.DoesNotContain("SourceAssistEpochId", testAssembler, StringComparison.Ordinal);
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
