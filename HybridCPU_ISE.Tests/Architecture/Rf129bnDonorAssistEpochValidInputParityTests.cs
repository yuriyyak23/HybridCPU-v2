namespace HybridCPU_ISE.Tests.Architecture;

public sealed class Rf129bnDonorAssistEpochValidInputParityTests
{
    [Fact]
    public void ValidRawDonorSnapshotKeepsExactSignaturesCopyAndBothRevalidations()
    {
        string transport = Read("HybridCPU_ISE", "CloseToHSL", "Core", "Pipeline", "Assist", "Runtime", "AssistRuntime.cs");
        string microOp = Read("HybridCPU_ISE", "CloseToHSL", "Core", "Pipeline", "MicroOps", "Assist", "AssistMicroOp.cs");
        string scheduler = Read("HybridCPU_ISE", "CloseToHSL", "Core", "Pipeline", "Assist", "InterCore", "MicroOpScheduler.Assist.InterCore.cs");
        string execution = Read("HybridCPU_ISE", "CloseToHSL", "Core", "Pipeline", "Assist", "Runtime", "CPU_Core.Assist.cs");
        Assert.Contains("int donorCoreId,\n            ushort donorPodId,\n            ulong donorAssistEpochId", transport, StringComparison.Ordinal);
        Assert.Contains("ulong sourceAssistEpochId = 0", transport, StringComparison.Ordinal);
        Assert.Contains("DonorAssistEpochId = donorAssistEpochId", transport, StringComparison.Ordinal);
        Assert.Contains("transport.DonorAssistEpochId", microOp, StringComparison.Ordinal);
        Assert.Contains("transport.DonorCoreId,\n                transport.DonorPodId,\n                transport.DonorAssistEpochId", microOp, StringComparison.Ordinal);
        Assert.Contains("ownerSnapshot.AssistEpochId != transport.DonorAssistEpochId", scheduler, StringComparison.Ordinal);
        Assert.Contains("donorSnapshot.AssistEpochId != assistMicroOp.DonorSource.SourceAssistEpochId", execution, StringComparison.Ordinal);
        Assert.DoesNotContain("DonorAssistEpoch.Create", microOp, StringComparison.Ordinal);
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
