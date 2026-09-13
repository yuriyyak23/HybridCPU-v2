namespace HybridCPU_ISE.Tests.Architecture;

/// <summary>
/// RF-08.4w freezes optional-rd vector-config RegisterWrite as a residual
/// family coupled to, but distinct from, VectorConfigWrite.
/// </summary>
public sealed class Rf084wVectorConfigOptionalRdRegisterWriteBlockerAuditTests
{
    [Fact]
    public void VConfigMicroOpCreatesCoupledEffectWithOptionalRd()
    {
        string root = FindRepositoryRoot();
        string microOp = Read(root, "HybridCPU_ISE", "CloseToHSL", "Core",
            "Pipeline", "MicroOps", "Vector", "VectorMicroOps.Data.cs");
        string model = Read(root, "HybridCPU_ISE", "CloseToHSL", "Core",
            "Pipeline", "Retire", "SideEffects", "VectorConfigRetireModel.cs");

        Assert.Contains("public class VConfigMicroOp : MicroOp", microOp, StringComparison.Ordinal);
        Assert.Contains("IsStealable = false", microOp, StringComparison.Ordinal);
        Assert.Contains("SetHardPinnedPlacement(SlotClass.SystemSingleton, 7)", microOp, StringComparison.Ordinal);
        Assert.Contains("bool hasRegisterWriteback = HasArchitecturalDestinationRegister()", microOp, StringComparison.Ordinal);
        Assert.Contains("_resolvedRetireEffect = VectorConfigRetireEffect.Create(", microOp, StringComparison.Ordinal);
        Assert.Contains("public bool HasRegisterWriteback", model, StringComparison.Ordinal);
        Assert.Contains("public ushort DestinationRegister", model, StringComparison.Ordinal);
    }

    [Fact]
    public void BoundedCaptureEmitsSeparateRegisterWriteFromSameMutableEffect()
    {
        string root = FindRepositoryRoot();
        string retire = Read(root, "HybridCPU_ISE", "CloseToHSL", "Core",
            "Pipeline", "Retire", "Evidence", "CPU_Core.PipelineExecution.Retire.cs");
        string types = Read(root, "HybridCPU_ISE", "CloseToHSL", "Core",
            "Pipeline", "ExecutionFlow", "StageFlow", "CPU_Core.PipelineExecution.Types.cs");

        Assert.Contains("lane.MicroOp is Core.VConfigMicroOp vectorConfigMicroOp", retire, StringComparison.Ordinal);
        Assert.Contains("vectorConfigMicroOp.CreateRetireEffect()", retire, StringComparison.Ordinal);
        Assert.Contains("retireBatch.CaptureGeneratedVectorConfigEffect(", retire, StringComparison.Ordinal);
        Assert.Contains("CPU_Core.EmitGeneratedVectorConfigRetireRecords(", types, StringComparison.Ordinal);
        Assert.Contains("RetireRecord.RegisterWrite(", ExtractVectorConfigEmitter(retire), StringComparison.Ordinal);
        Assert.Contains("AppendEffect(RetireWindowEffect.VectorConfig(vectorConfigEffect))", types, StringComparison.Ordinal);
    }

    [Fact]
    public void NoDirectOrExactCarrierSourceExists()
    {
        string root = FindRepositoryRoot();
        string dispatcher = Read(root, "HybridCPU_ISE", "CloseToHSL", "Core",
            "Execution", "Dispatch", "ExecutionDispatcherV4.cs");
        string fsp = Read(root, "HybridCPU_ISE", "CloseToHSL", "Core",
            "Pipeline", "Scheduling", "Fsp", "CPU_Core.PipelineExecution.Fsp.cs");

        Assert.Contains("throw CreateUnsupportedVectorConfigEagerExecuteSurfaceException(instr)", dispatcher, StringComparison.Ordinal);
        Assert.Contains("throw CreateUnsupportedVectorConfigRetireWindowPublicationSurfaceException(instr)", dispatcher, StringComparison.Ordinal);
        Assert.DoesNotContain("VConfigMicroOp", ExtractIdentityTemplateMethod(fsp), StringComparison.Ordinal);
    }

    [Fact]
    public void SourceAuditKeepsBothEffectKindsExplicitAcrossTheLaterDecision()
    {
        string root = FindRepositoryRoot();
        string paper = Read(root, "ResearchPaper", "section", "md base",
            "5_Two_Stage_Admission_and_Bundle_Compositional_SMT_Packing.md");

        Assert.Contains("| vector-config coupled `RegisterWrite` / `VectorConfigWrite` |", paper, StringComparison.Ordinal);
        Assert.Contains("not merge the two effect kinds", paper, StringComparison.Ordinal);
        Assert.Contains("creates no issued-attempt identity", paper, StringComparison.Ordinal);
        Assert.Contains("| `SystemCommit` |", paper, StringComparison.Ordinal);
        Assert.Contains("| `VmxCommit` |", paper, StringComparison.Ordinal);
    }

    private static string ExtractVectorConfigEmitter(string retire)
    {
        int start = retire.IndexOf("private static void EmitGeneratedVectorConfigRetireRecords(", StringComparison.Ordinal);
        int end = retire.IndexOf(
            "private static void AppendRetireRecord(",
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

        throw new DirectoryNotFoundException("Repository root was not found.");
    }
}
