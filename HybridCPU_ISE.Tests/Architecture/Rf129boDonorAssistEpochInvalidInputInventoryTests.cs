namespace HybridCPU_ISE.Tests.Architecture;

public sealed class Rf129boDonorAssistEpochInvalidInputInventoryTests
{
    [Fact]
    public void InvalidDonorObservationPathsAndWinnersRemainSeparated()
    {
        string transport = Read("HybridCPU_ISE", "CloseToHSL", "Core", "Pipeline", "Assist", "Runtime", "AssistRuntime.cs");
        string factory = Read("HybridCPU_ISE", "CloseToHSL", "Core", "Pipeline", "MicroOps", "Assist", "AssistMicroOp.cs");
        string scheduler = Read("HybridCPU_ISE", "CloseToHSL", "Core", "Pipeline", "Assist", "InterCore", "MicroOpScheduler.Assist.InterCore.cs");
        string execution = Read("HybridCPU_ISE", "CloseToHSL", "Core", "Pipeline", "Assist", "Runtime", "CPU_Core.Assist.cs");
        Assert.Contains("donorAssistEpochId: 0", factory, StringComparison.Ordinal);
        Assert.Contains("if (!IsSupportedInterCoreSeed(seed))\n                return false", factory, StringComparison.Ordinal);
        Assert.Contains("throw new ArgumentException", transport, StringComparison.Ordinal);
        Assert.Contains("return !transport.IsValid", scheduler, StringComparison.Ordinal);
        Assert.Contains("ownerSnapshot.AssistEpochId != transport.DonorAssistEpochId", scheduler, StringComparison.Ordinal);
        Assert.Contains("RecordAssistInterCoreReject(candidateTransport, requestingPodId)", scheduler, StringComparison.Ordinal);
        Assert.Contains("Core.AssistInvalidationReason.InterCoreOwnerDrift", execution, StringComparison.Ordinal);
        Assert.Contains("donorSnapshot.AssistEpochId != assistMicroOp.DonorSource.SourceAssistEpochId", execution, StringComparison.Ordinal);
        Assert.Contains("Core.AssistInvalidationReason.InterCoreBoundaryDrift", execution, StringComparison.Ordinal);
    }

    private static string Read(params string[] path) => File.ReadAllText(Path.Combine(new[] { FindRepositoryRoot() }.Concat(path).ToArray()));
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
