namespace HybridCPU_ISE.Tests.Architecture;

/// <summary>
/// RF-08.4g freezes the current VectorConfigWrite producer/source blocker
/// without treating mutable VConfigMicroOp state or lane 7 as identity.
/// </summary>
public sealed class Rf084gVectorConfigWriteIdentitySourceBlockerAuditTests
{
    [Fact]
    public void VectorConfigRetireEffectIsTypedPayloadWithoutIssuedAttemptProvenance()
    {
        string root = FindRepositoryRoot();
        string model = Read(root, "HybridCPU_ISE", "CloseToHSL", "Core", "Pipeline", "Retire", "SideEffects", "VectorConfigRetireModel.cs");

        Assert.Contains("public readonly struct VectorConfigRetireEffect", model, StringComparison.Ordinal);
        Assert.Contains("public ulong ActualVectorLength", model, StringComparison.Ordinal);
        Assert.Contains("public ulong VType", model, StringComparison.Ordinal);
        Assert.DoesNotContain("ScheduledOperation", model, StringComparison.Ordinal);
        Assert.DoesNotContain("VliwOperationId", model, StringComparison.Ordinal);
        Assert.DoesNotContain("GeneratedStaticBinding", model, StringComparison.Ordinal);
        Assert.DoesNotContain("SourceOperationProvenance", model, StringComparison.Ordinal);
        Assert.DoesNotContain("WorkingBundleSequence", model, StringComparison.Ordinal);
    }

    [Fact]
    public void MainlineLane7EffectIsReadFromMutableMicroOpOnlyAtRetireCapture()
    {
        string root = FindRepositoryRoot();
        string microOp = Read(root, "HybridCPU_ISE", "CloseToHSL", "Core", "Pipeline", "MicroOps", "Vector", "VectorMicroOps.Data.cs");
        string retire = Read(root, "HybridCPU_ISE", "CloseToHSL", "Core", "Pipeline", "Retire", "Evidence", "CPU_Core.PipelineExecution.Retire.cs");
        string ex = Read(root, "HybridCPU_ISE", "CloseToHSL", "Core", "Pipeline", "Stages", "Execute", "CPU_Core.Pipeline.Stages.ScalarExecuteLaneState.cs");
        string mem = Read(root, "HybridCPU_ISE", "CloseToHSL", "Core", "Pipeline", "Stages", "Memory", "CPU_Core.Pipeline.Stages.ScalarMemoryLaneState.cs");
        string wb = Read(root, "HybridCPU_ISE", "CloseToHSL", "Core", "Pipeline", "Stages", "WriteBack", "CPU_Core.Pipeline.Stages.ScalarWriteBackLaneState.cs");

        Assert.Contains("SetHardPinnedPlacement(SlotClass.SystemSingleton, 7);", microOp, StringComparison.Ordinal);
        Assert.Contains("_resolvedRetireEffect = VectorConfigRetireEffect.Create(", microOp, StringComparison.Ordinal);
        Assert.Contains("public VectorConfigRetireEffect CreateRetireEffect() => _resolvedRetireEffect;", microOp, StringComparison.Ordinal);
        Assert.Contains("lane.MicroOp is Core.VConfigMicroOp vectorConfigMicroOp", retire, StringComparison.Ordinal);
        Assert.Contains("vectorConfigMicroOp.CreateRetireEffect();", retire, StringComparison.Ordinal);
        Assert.Contains("retireBatch.CaptureGeneratedVectorConfigEffect(", retire, StringComparison.Ordinal);
        Assert.DoesNotContain("GeneratedVectorConfigEffect", ex, StringComparison.Ordinal);
        Assert.DoesNotContain("GeneratedVectorConfigEffect", mem, StringComparison.Ordinal);
        Assert.DoesNotContain("GeneratedVectorConfigEffect", wb, StringComparison.Ordinal);
    }

    [Fact]
    public void DirectCompatibilitySurfacesRejectVectorConfigAndPaperNowApprovesCoupledContour()
    {
        string root = FindRepositoryRoot();
        string dispatcher = Read(root, "HybridCPU_ISE", "CloseToHSL", "Core", "Execution", "Dispatch", "ExecutionDispatcherV4.cs");
        string paper = Read(root, "ResearchPaper", "section", "md base", "5_Two_Stage_Admission_and_Bundle_Compositional_SMT_Packing.md");
        string retire = Read(root, "HybridCPU_ISE", "CloseToHSL", "Core", "Pipeline", "Retire", "Evidence", "CPU_Core.PipelineExecution.Retire.cs");

        Assert.Contains("throw CreateUnsupportedVectorConfigEagerExecuteSurfaceException(instr);", dispatcher, StringComparison.Ordinal);
        Assert.Contains("throw CreateUnsupportedVectorConfigRetireWindowPublicationSurfaceException(instr);", dispatcher, StringComparison.Ordinal);
        Assert.Contains("| vector-config coupled `RegisterWrite` / `VectorConfigWrite` |", paper, StringComparison.Ordinal);
        Assert.Contains("RF-08.4x approves C-C", paper, StringComparison.Ordinal);
        Assert.Contains("creates no issued-attempt identity", paper, StringComparison.Ordinal);
        Assert.Contains("PrevalidateVectorConfigEffect(in Core.VectorConfigRetireEffect vectorConfigEffect)", retire, StringComparison.Ordinal);
        Assert.Contains("ApplyRetiredVectorConfigEffect(retireEffect.VectorConfigEffect);", retire, StringComparison.Ordinal);
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
