namespace HybridCPU_ISE.Tests.Architecture;

/// <summary>
/// RF-08.4v freezes CSR readback RegisterWrite as a family separate from
/// the CSR-A CsrWrite exclusion.
/// </summary>
public sealed class Rf084vCsrReadbackRegisterWriteSourceBlockerAuditTests
{
    [Fact]
    public void MainlineCsrReadbackIsHardPinnedAndEmitsSeparateRegisterWrite()
    {
        string root = FindRepositoryRoot();
        string control = Read(root, "HybridCPU_ISE", "CloseToHSL", "Core",
            "Pipeline", "MicroOps", "Control", "MicroOp.Control.cs");

        Assert.Contains("public abstract class CSRMicroOp : MicroOp", control, StringComparison.Ordinal);
        Assert.Contains("IsStealable = false", control, StringComparison.Ordinal);
        Assert.Contains("SetHardPinnedPlacement(SlotClass.SystemSingleton, 7)", control, StringComparison.Ordinal);
        Assert.Contains("bool publishesRegisterWriteback = ReadsCsr", control, StringComparison.Ordinal);
        Assert.Contains("RetireRecord.RegisterWrite(vtId, DestRegID, _readValue)", control, StringComparison.Ordinal);
        Assert.Contains("public sealed class CsrReadCounterMicroOp : CSRMicroOp", control, StringComparison.Ordinal);
    }

    [Fact]
    public void MainlineGeneratedEffectTransportHasNoExactRf08Carrier()
    {
        string root = FindRepositoryRoot();
        string helpers = Read(root, "HybridCPU_ISE", "CloseToHSL", "Core",
            "Pipeline", "ExecutionFlow", "Materialization",
            "CPU_Core.PipelineExecution.ExecuteHelpers.cs");
        string materialization = Read(root, "HybridCPU_ISE", "CloseToHSL", "Core",
            "Pipeline", "ExecutionFlow", "Materialization",
            "CPU_Core.PipelineExecution.Materialization.cs");
        string retire = Read(root, "HybridCPU_ISE", "CloseToHSL", "Core",
            "Pipeline", "Retire", "Evidence", "CPU_Core.PipelineExecution.Retire.cs");
        string fsp = Read(root, "HybridCPU_ISE", "CloseToHSL", "Core",
            "Pipeline", "Scheduling", "Fsp", "CPU_Core.PipelineExecution.Fsp.cs");

        Assert.Contains("GeneratedCsrEffect = MaterializeLaneCsrEffect", helpers, StringComparison.Ordinal);
        Assert.Contains("lane.GeneratedCsrEffect = executeLane.GeneratedCsrEffect", materialization, StringComparison.Ordinal);
        Assert.Contains("lane.GeneratedCsrEffect = memoryLane.GeneratedCsrEffect", materialization, StringComparison.Ordinal);
        Assert.Contains("retireBatch.CaptureGeneratedCsrEffect(", retire, StringComparison.Ordinal);
        Assert.Contains("EmitGeneratedCsrRetireRecords(", retire, StringComparison.Ordinal);
        Assert.Contains("RetireRecord.RegisterWrite(", ExtractGeneratedCsrEmitter(retire), StringComparison.Ordinal);
        Assert.DoesNotContain("CSRMicroOp", ExtractIdentityTemplateMethod(fsp), StringComparison.Ordinal);
    }

    [Fact]
    public void DirectDispatcherSeparatesBoundedReadbackFromEagerMutation()
    {
        string root = FindRepositoryRoot();
        string direct = Read(root, "HybridCPU_ISE", "CloseToHSL", "Core",
            "Execution", "Dispatch", "ExecutionDispatcherV4.CsrAndSmtVt.cs");
        string coreRoot = Path.Combine(root, "HybridCPU_ISE", "CloseToHSL", "Core");

        Assert.Contains("private ExecutionResult ExecuteCsr(", direct, StringComparison.Ordinal);
        Assert.Contains("ApplyCsrEffect(effect, state, vtId)", direct, StringComparison.Ordinal);
        Assert.Contains("state.WriteRegister(vtId, effect.DestRegId, effect.ReadValue)", direct, StringComparison.Ordinal);
        Assert.Contains("private void CaptureCsrRetireWindowPublications(", direct, StringComparison.Ordinal);
        Assert.Contains("RetireRecord.RegisterWrite(", direct, StringComparison.Ordinal);
        Assert.Contains("hasRegisterWriteback: false", direct, StringComparison.Ordinal);

        string[] directCaptureCallers = FindCallerFiles(
            coreRoot,
            "dispatcher.CaptureRetireWindowPublications(");
        Assert.Equal(["Pipeline/Core/CPU_Core.TestSupport.cs"], directCaptureCallers);
    }

    [Fact]
    public void PaperApprovesReadbackSeparatelyFromCsrA()
    {
        string root = FindRepositoryRoot();
        string paper = Read(root, "ResearchPaper", "section", "md base",
            "5_Two_Stage_Admission_and_Bundle_Compositional_SMT_Packing.md");

        Assert.Contains(
            "CSR readback to a general register is a separate residual",
            paper,
            StringComparison.Ordinal);
        Assert.Contains(
            "`RegisterWrite` producer and is not absorbed into this exclusion",
            paper,
            StringComparison.Ordinal);
        Assert.Contains(
            "approved CSR-readback `RegisterWrite`",
            paper,
            StringComparison.Ordinal);
    }

    private static string ExtractGeneratedCsrEmitter(string retire)
    {
        int start = retire.IndexOf("private static void EmitGeneratedCsrRetireRecords(", StringComparison.Ordinal);
        int end = retire.IndexOf(
            "private static void EmitGeneratedVectorConfigRetireRecords(",
            start,
            StringComparison.Ordinal);
        return retire[start..end];
    }

    private static string ExtractIdentityTemplateMethod(string fsp)
    {
        int start = fsp.IndexOf("private void AttachRf08PostStageBIdentityTemplate(", StringComparison.Ordinal);
        int end = fsp.IndexOf(
            "private byte ResolveForegroundRunnableVirtualThreadMask()",
            start,
            StringComparison.Ordinal);
        return fsp[start..end];
    }

    private static string[] FindCallerFiles(string coreRoot, string marker) =>
        Directory.GetFiles(coreRoot, "*.cs", SearchOption.AllDirectories)
            .Where(path => File.ReadAllText(path).Contains(marker, StringComparison.Ordinal))
            .Select(path => Path.GetRelativePath(coreRoot, path).Replace('\\', '/'))
            .OrderBy(path => path, StringComparer.Ordinal)
            .ToArray();

    private static string Read(string root, params string[] segments) =>
        File.ReadAllText(Path.Combine(new[] { root }.Concat(segments).ToArray()));

    private static string FindRepositoryRoot()
    {
        DirectoryInfo? directory = new(AppContext.BaseDirectory);
        while (directory != null)
        {
            if (Directory.Exists(Path.Combine(directory.FullName, "ResearchPaper")) &&
                Directory.Exists(Path.Combine(directory.FullName, "HybridCPU_ISE")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException("Repository root not found.");
    }
}
