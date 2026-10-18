namespace HybridCPU_ISE.Tests.Architecture;

/// <summary>
/// RF-08.4f freezes CSR-A: the two existing CsrWrite producer contours remain
/// a closed residual exclusion without synthetic identity or owner transfer.
/// </summary>
public sealed class Rf084fCsrWriteApprovedResidualExclusionTests
{
    [Fact]
    public void PaperApprovesClosedCsrATwoContourScopeAtRf08Exit()
    {
        string root = FindRepositoryRoot();
        string paper = Read(root, "ResearchPaper", "section", "md base", "5_Two_Stage_Admission_and_Bundle_Compositional_SMT_Packing.md");

        Assert.Contains("#### RF-08.4f CSR-A approved `CsrWrite` residual exclusion", paper, StringComparison.Ordinal);
        Assert.Contains("successful mainline single-lane and explicit-packet `CSRMicroOp` execution", paper, StringComparison.Ordinal);
        Assert.Contains("direct compatibility `InstructionIR` handling", paper, StringComparison.Ordinal);
        Assert.Contains("CSR readback to a general register is a separate residual", paper, StringComparison.Ordinal);
        Assert.Contains("does not authorize reconstruction", paper, StringComparison.Ordinal);
        Assert.Contains("The exclusion is admissible at RF-08 exit without proving complete", paper, StringComparison.Ordinal);
        Assert.Contains("reviewed only by a separate", paper, StringComparison.Ordinal);
        Assert.Contains("RF-09 cannot supply or reconstruct the missing identity", paper, StringComparison.Ordinal);
    }

    [Fact]
    public void CurrentCsrProducersRemainMainlineGeneratedEffectAndDirectCaptureOnly()
    {
        string root = FindRepositoryRoot();
        string execute = Read(root, "HybridCPU_ISE", "CloseToHSL", "Core", "Pipeline", "ExecutionFlow", "Materialization", "CPU_Core.PipelineExecution.ExecuteHelpers.cs");
        string materialization = Read(root, "HybridCPU_ISE", "CloseToHSL", "Core", "Pipeline", "ExecutionFlow", "Materialization", "CPU_Core.PipelineExecution.Materialization.cs");
        string retire = Read(root, "HybridCPU_ISE", "CloseToHSL", "Core", "Pipeline", "Retire", "Evidence", "CPU_Core.PipelineExecution.Retire.cs");
        string dispatcher = Read(root, "HybridCPU_ISE", "CloseToHSL", "Core", "Execution", "Dispatch", "ExecutionDispatcherV4.CsrAndSmtVt.cs");

        Assert.Contains("pipeEX.GeneratedCsrEffect = MaterializeLaneCsrEffect(pipeEX.MicroOp);", execute, StringComparison.Ordinal);
        Assert.Contains("lane.GeneratedCsrEffect = executeLane.GeneratedCsrEffect;", materialization, StringComparison.Ordinal);
        Assert.Contains("lane.GeneratedCsrEffect = memoryLane.GeneratedCsrEffect;", materialization, StringComparison.Ordinal);
        Assert.Contains("retireBatch.CaptureGeneratedCsrEffect(", retire, StringComparison.Ordinal);
        Assert.Contains("retireBatch.CaptureRetireWindowCsrEffect(effect);", dispatcher, StringComparison.Ordinal);
        Assert.DoesNotContain("PostStageBIssuedAttempt", dispatcher, StringComparison.Ordinal);
    }

    [Fact]
    public void ExistingSelectedPrefixOwnersAndSeparateReadbackRemainExplicit()
    {
        string root = FindRepositoryRoot();
        string retire = Read(root, "HybridCPU_ISE", "CloseToHSL", "Core", "Pipeline", "Retire", "Evidence", "CPU_Core.PipelineExecution.Retire.cs");

        Assert.Contains("PrevalidateCsrEffect(in Core.CsrRetireEffect csrEffect)", retire, StringComparison.Ordinal);
        Assert.Contains("ApplyRetiredCsrEffect(retireEffect.CsrEffect);", retire, StringComparison.Ordinal);
        Assert.Contains("RetireRecord.RegisterWrite(", retire, StringComparison.Ordinal);
    }

    private static string Read(string root, params string[] segments) =>
        File.ReadAllText(Path.Combine(new[] { root }.Concat(segments).ToArray()));

    private static string FindRepositoryRoot()
    {
        for (DirectoryInfo? current = new(AppContext.BaseDirectory); current is not null; current = current.Parent)
        {
            if (Directory.Exists(Path.Combine(current.FullName, "HybridCPU_ISE")) &&
                Directory.Exists(Path.Combine(current.FullName, "ResearchPaper")))
            {
                return current.FullName;
            }
        }

        throw new DirectoryNotFoundException("Repository root was not found from the test output directory.");
    }
}
