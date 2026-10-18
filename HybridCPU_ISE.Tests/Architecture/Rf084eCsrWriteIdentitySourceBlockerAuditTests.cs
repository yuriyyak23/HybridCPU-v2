namespace HybridCPU_ISE.Tests.Architecture;

/// <summary>
/// RF-08.4e freezes the current CsrWrite producer/source blocker without
/// authorizing a synthetic identity or changing CSR execution/retirement.
/// </summary>
public sealed class Rf084eCsrWriteIdentitySourceBlockerAuditTests
{
    [Fact]
    public void CsrRetireEffectIsTypedPayloadWithoutIssuedAttemptProvenance()
    {
        string root = FindRepositoryRoot();
        string control = Read(root, "HybridCPU_ISE", "CloseToHSL", "Core", "Pipeline", "MicroOps", "Control", "MicroOp.Control.cs");
        string csrEffect = Slice(control, "public readonly struct CsrRetireEffect", "public abstract class CSRMicroOp");

        Assert.Contains("CsrStorageSurface storageSurface", csrEffect, StringComparison.Ordinal);
        Assert.Contains("bool hasCsrWrite", csrEffect, StringComparison.Ordinal);
        Assert.DoesNotContain("ScheduledOperation", csrEffect, StringComparison.Ordinal);
        Assert.DoesNotContain("VliwOperationId", csrEffect, StringComparison.Ordinal);
        Assert.DoesNotContain("GeneratedStaticBinding", csrEffect, StringComparison.Ordinal);
        Assert.DoesNotContain("SourceOperationProvenance", csrEffect, StringComparison.Ordinal);
        Assert.DoesNotContain("WorkingBundleSequence", csrEffect, StringComparison.Ordinal);
    }

    [Fact]
    public void MainlineAndDirectCsrContoursConvergeOnlyAtTypedRetireBatch()
    {
        string root = FindRepositoryRoot();
        string execute = Read(root, "HybridCPU_ISE", "CloseToHSL", "Core", "Pipeline", "ExecutionFlow", "Materialization", "CPU_Core.PipelineExecution.ExecuteHelpers.cs");
        string retire = Read(root, "HybridCPU_ISE", "CloseToHSL", "Core", "Pipeline", "Retire", "Evidence", "CPU_Core.PipelineExecution.Retire.cs");
        string dispatcher = Read(root, "HybridCPU_ISE", "CloseToHSL", "Core", "Execution", "Dispatch", "ExecutionDispatcherV4.CsrAndSmtVt.cs");

        Assert.Contains("bool success = ExecuteMicroOpWithStableCoreIdentity(pipeEX.MicroOp);", execute, StringComparison.Ordinal);
        Assert.Contains("pipeEX.GeneratedCsrEffect = MaterializeLaneCsrEffect(pipeEX.MicroOp);", execute, StringComparison.Ordinal);
        Assert.Contains("MaterializeCsrEffectWithStableCoreIdentity(csrMicroOp);", retire, StringComparison.Ordinal);
        Assert.Contains("effect.ClearsArchitecturalExceptionState ||", retire, StringComparison.Ordinal);
        Assert.Contains("effect.HasCsrWrite ||", retire, StringComparison.Ordinal);
        Assert.Contains("effect.HasRegisterWriteback", retire, StringComparison.Ordinal);
        Assert.Contains("retireBatch.CaptureGeneratedCsrEffect(", retire, StringComparison.Ordinal);
        Assert.Contains("CaptureCsrRetireWindowPublications(", dispatcher, StringComparison.Ordinal);
        Assert.Contains("retireBatch.CaptureRetireWindowCsrEffect(effect);", dispatcher, StringComparison.Ordinal);
        Assert.DoesNotContain("PostStageBIssuedAttempt", dispatcher, StringComparison.Ordinal);
    }

    [Fact]
    public void PaperNowSupersedesTemporaryCsrWriteRowWhilePublicationOwnersStayUnchanged()
    {
        string root = FindRepositoryRoot();
        string paper = Read(root, "ResearchPaper", "section", "md base", "5_Two_Stage_Admission_and_Bundle_Compositional_SMT_Packing.md");
        string retire = Read(root, "HybridCPU_ISE", "CloseToHSL", "Core", "Pipeline", "Retire", "Evidence", "CPU_Core.PipelineExecution.Retire.cs");

        Assert.Contains("| `CsrWrite` | successful mainline single-lane/explicit-packet", paper, StringComparison.Ordinal);
        Assert.Contains("RF-08.4f CSR-A approves this closed two-contour residual exclusion", paper, StringComparison.Ordinal);
        Assert.Contains("| `SystemCommit` |", paper, StringComparison.Ordinal);
        Assert.Contains("| `VmxCommit` |", paper, StringComparison.Ordinal);
        Assert.Contains("RF-08.4ai approves C-C for this closed two-contour family", paper, StringComparison.Ordinal);
        Assert.Contains("PrevalidateCsrEffect(in Core.CsrRetireEffect csrEffect)", retire, StringComparison.Ordinal);
        Assert.Contains("ApplyRetiredCsrEffect(retireEffect.CsrEffect);", retire, StringComparison.Ordinal);
    }

    private static string Slice(string source, string startMarker, string endMarker)
    {
        int start = source.IndexOf(startMarker, StringComparison.Ordinal);
        int end = source.IndexOf(endMarker, start, StringComparison.Ordinal);
        Assert.True(start >= 0 && end > start);
        return source[start..end];
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
