namespace HybridCPU_ISE.Tests.Architecture;

/// <summary>
/// RF-08.4i freezes the two VmxCommit producer contours without treating the
/// VMX payload, trace snapshot, or derived RegisterWrite/PcWrite as identity.
/// </summary>
public sealed class Rf084iVmxCommitIdentitySourceBlockerAuditTests
{
    [Fact]
    public void VmxRetireEffectPayloadHasNoIssuedAttemptProvenance()
    {
        string root = FindRepositoryRoot();
        string model = Read(root, "HybridCPU_ISE", "CloseToHSL", "Core", "Virtualization", "Compatibility", "Frontend", "Retire", "VmxRetireModel.cs");
        string effect = Slice(model, "public readonly struct VmxRetireEffect", "public readonly record struct VmxRetireOutcome");

        Assert.Contains("public VmxOperationKind Operation", effect, StringComparison.Ordinal);
        Assert.Contains("public VmExitReason FailureReason", effect, StringComparison.Ordinal);
        Assert.Contains("public VmxRootDescriptorReference RootDescriptor", effect, StringComparison.Ordinal);
        Assert.DoesNotContain("ScheduledOperation", effect, StringComparison.Ordinal);
        Assert.DoesNotContain("VliwOperationId", effect, StringComparison.Ordinal);
        Assert.DoesNotContain("GeneratedStaticBinding", effect, StringComparison.Ordinal);
        Assert.DoesNotContain("SourceOperationProvenance", effect, StringComparison.Ordinal);
        Assert.DoesNotContain("WorkingBundleSequence", effect, StringComparison.Ordinal);
    }

    [Fact]
    public void CurrentVmxCommitProducersAreMainlineAndDirectCompatibilityOnly()
    {
        string root = FindRepositoryRoot();
        string coreRoot = Path.Combine(root, "HybridCPU_ISE", "CloseToHSL", "Core");
        string[] producerFiles = Directory.GetFiles(coreRoot, "*.cs", SearchOption.AllDirectories)
            .Where(path => File.ReadAllText(path).Contains("VmxRetireEffect.", StringComparison.Ordinal))
            .Select(path => Path.GetRelativePath(coreRoot, path).Replace('\\', '/'))
            .OrderBy(path => path, StringComparer.Ordinal)
            .ToArray();

        Assert.Equal(
            [
                "Execution/Dispatch/ExecutionDispatcherV4.VmxCompatibility.cs",
                "Pipeline/MicroOps/Types/MicroOp.IO.cs"
            ],
            producerFiles);

        string execute = Read(root, "HybridCPU_ISE", "CloseToHSL", "Core", "Pipeline", "ExecutionFlow", "Materialization", "CPU_Core.PipelineExecution.ExecuteHelpers.cs");
        string materialization = Read(root, "HybridCPU_ISE", "CloseToHSL", "Core", "Pipeline", "ExecutionFlow", "Materialization", "CPU_Core.PipelineExecution.Materialization.cs");
        string retire = Read(root, "HybridCPU_ISE", "CloseToHSL", "Core", "Pipeline", "Retire", "Evidence", "CPU_Core.PipelineExecution.Retire.cs");
        string direct = Read(root, "HybridCPU_ISE", "CloseToHSL", "Core", "Execution", "Dispatch", "ExecutionDispatcherV4.VmxCompatibility.cs");

        Assert.Contains("pipeEX.GeneratedVmxEffect = MaterializeLaneVmxEffect(pipeEX.MicroOp);", execute, StringComparison.Ordinal);
        Assert.Contains("lane.GeneratedVmxEffect = executeLane.GeneratedVmxEffect;", materialization, StringComparison.Ordinal);
        Assert.Contains("lane.GeneratedVmxEffect = memoryLane.GeneratedVmxEffect;", materialization, StringComparison.Ordinal);
        Assert.Contains("retireBatch.CaptureGeneratedVmxEffect(", retire, StringComparison.Ordinal);
        Assert.Contains("retireBatch.CaptureRetireWindowVmxEffect(effect, vtId);", direct, StringComparison.Ordinal);
    }

    [Fact]
    public void VmxOutcomeWritesRemainSeparateFamiliesAndPaperRowIsApproved()
    {
        string root = FindRepositoryRoot();
        string vmxRetire = Read(root, "HybridCPU_ISE", "CloseToHSL", "Core", "Pipeline", "Retire", "Evidence", "CPU_Core.PipelineExecution.VmxRetire.cs");
        string paper = Read(root, "ResearchPaper", "section", "md base", "5_Two_Stage_Admission_and_Bundle_Compositional_SMT_Packing.md");

        Assert.Contains("RetireRecord.RegisterWrite(", vmxRetire, StringComparison.Ordinal);
        Assert.Contains("RetireRecord.PcWrite(", vmxRetire, StringComparison.Ordinal);
        Assert.Contains("| `VmxCommit` |", paper, StringComparison.Ordinal);
        Assert.Contains("RF-08.4ai approves C-C for this closed two-contour family", paper, StringComparison.Ordinal);
        Assert.Contains("derived VMX `RegisterWrite` and `PcWrite` remain separate", paper, StringComparison.Ordinal);
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
