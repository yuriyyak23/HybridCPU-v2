namespace HybridCPU_ISE.Tests.Architecture;

/// <summary>RF-08.3n: scalar-load identity transport has no current typed Stage-B caller.</summary>
public sealed class Rf083nScalarLoadTypedStageBTopologyBlockerTests
{
    [Fact]
    public void LoadIsOutsideTheExistingScalarClusterHandoffWithoutChangingSchedulerPolicy()
    {
        string root = FindRepositoryRoot();
        string fsp = Read(root, "HybridCPU_ISE", "CloseToHSL", "Core", "Pipeline", "Scheduling", "Fsp", "CPU_Core.PipelineExecution.Fsp.cs");
        string preparation = Read(root, "HybridCPU_ISE", "CloseToHSL", "Core", "Decoder", "ClusterIssuePreparation.cs");

        Assert.Contains("ScalarClusterIssueEntry[] entries", fsp, StringComparison.Ordinal);
        Assert.Contains("if (candidate is not Core.ScalarALUMicroOp)", fsp, StringComparison.Ordinal);
        Assert.DoesNotContain("Core.LoadMicroOp", fsp, StringComparison.Ordinal);
        Assert.Contains("byte preparedScalarMask = admissionPrep.WideReadyScalarMask", preparation, StringComparison.Ordinal);
        Assert.Contains("BuildAuxiliaryReservations(slots, admissionPrep.AuxiliaryOpMask)", preparation, StringComparison.Ordinal);
    }

    private static string Read(string root, params string[] segments) =>
        File.ReadAllText(Path.Combine(new[] { root }.Concat(segments).ToArray()));

    private static string FindRepositoryRoot()
    {
        for (DirectoryInfo? current = new(AppContext.BaseDirectory); current is not null; current = current.Parent)
        {
            if (Directory.Exists(Path.Combine(current.FullName, "HybridCPU_ISE")) && Directory.Exists(Path.Combine(current.FullName, "HybridCPU_ISE.Tests")))
                return current.FullName;
        }
        throw new DirectoryNotFoundException("Repository root was not found from the test output directory.");
    }
}
