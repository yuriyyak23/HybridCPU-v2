namespace HybridCPU_ISE.Tests.Architecture;

/// <summary>
/// RF-08.4c certifies the current PcWrite C-A/C-C decision gate against the
/// production foreground-control path. It intentionally changes no runtime code.
/// </summary>
public sealed class Rf084cPcWriteCompatibilitySourceCertificationTests
{
    [Fact]
    public void ForegroundControlLaneIsFilteredBeforeDirectExecuteLatchMaterialization()
    {
        string root = FindRepositoryRoot();
        string stageFlow = Read(root, "HybridCPU_ISE", "CloseToHSL", "Core", "Pipeline", "ExecutionFlow", "StageFlow", "CPU_Core.PipelineExecution.StageFlow.cs");
        string materialization = Read(root, "HybridCPU_ISE", "CloseToHSL", "Core", "Pipeline", "ExecutionFlow", "Materialization", "CPU_Core.PipelineExecution.Materialization.cs");

        Assert.Contains("7 => IsExecutableSystemSingletonIssueLaneMicroOp(issueLane.MicroOp)", stageFlow, StringComparison.Ordinal);
        Assert.Contains("HasIssueLaneHazardAgainstSlotMask(", stageFlow, StringComparison.Ordinal);
        Assert.Contains("issueLane = ApplyIssueLaneExecutionSurfaceContract(issueLane, issuePacket.PC);", materialization, StringComparison.Ordinal);
        Assert.Contains("return CanVirtualThreadIssueInForeground(issueLane.OwnerThreadId)", materialization, StringComparison.Ordinal);
        Assert.Contains("pipeEX.Lane7 = CreateExecuteLaneState(", materialization, StringComparison.Ordinal);
        Assert.DoesNotContain("ControlIssuedAttempt", materialization, StringComparison.Ordinal);
        Assert.DoesNotContain("PostCompatibilityMaterializationIssuedAttempt", materialization, StringComparison.Ordinal);
    }

    [Fact]
    public void ExistingIssueAndLatchShapesDoNotCarryRequiredCAIdentityProvenance()
    {
        string root = FindRepositoryRoot();
        string issueLane = Read(root, "HybridCPU_ISE", "CloseToHSL", "Core", "Pipeline", "Core", "RuntimeClusterAdmissionPreparation.IssuePacketLane.cs");
        string executeLane = Read(root, "HybridCPU_ISE", "CloseToHSL", "Core", "Pipeline", "Stages", "Execute", "CPU_Core.Pipeline.Stages.ScalarExecuteLaneState.cs");

        Assert.Contains("public byte SlotIndex { get; }", issueLane, StringComparison.Ordinal);
        Assert.Contains("public MicroOp MicroOp { get; }", issueLane, StringComparison.Ordinal);
        Assert.DoesNotContain("GeneratedStaticBinding", issueLane, StringComparison.Ordinal);
        Assert.DoesNotContain("SourceOperationProvenance", issueLane, StringComparison.Ordinal);
        Assert.DoesNotContain("WorkingBundleSequence", issueLane, StringComparison.Ordinal);
        Assert.DoesNotContain("ControlIssuedAttempt", executeLane, StringComparison.Ordinal);
        Assert.DoesNotContain("PostCompatibilityMaterializationIssuedAttempt", executeLane, StringComparison.Ordinal);
    }

    [Fact]
    public void LaneSevenIsSharedPhysicalPlacementNotSharedIdentityAuthority()
    {
        string root = FindRepositoryRoot();
        string control = Read(root, "HybridCPU_ISE", "CloseToHSL", "Core", "Pipeline", "MicroOps", "Control", "MicroOp.Control.cs");
        string stageFlow = Read(root, "HybridCPU_ISE", "CloseToHSL", "Core", "Pipeline", "ExecutionFlow", "StageFlow", "CPU_Core.PipelineExecution.StageFlow.cs");
        string coordinator = Read(root, "HybridCPU_ISE", "CloseToHSL", "Core", "Architecture", "Registers", "Retire", "RetireCoordinator.cs");

        Assert.Contains("SetHardPinnedPlacement(SlotClass.BranchControl, 7)", control, StringComparison.Ordinal);
        Assert.Contains("return microOp is Core.BranchMicroOp", stageFlow, StringComparison.Ordinal);
        Assert.Contains("|| microOp is Core.SysEventMicroOp", stageFlow, StringComparison.Ordinal);
        Assert.Contains("RetireRecord.PcWrite(vtId, _resolvedRetireTargetAddress)", control, StringComparison.Ordinal);
        Assert.Contains("ApplyPcWrite(record);", coordinator, StringComparison.Ordinal);
    }

    private static string Read(string root, params string[] segments) =>
        File.ReadAllText(Path.Combine(new[] { root }.Concat(segments).ToArray()));

    private static string FindRepositoryRoot()
    {
        for (DirectoryInfo? current = new(AppContext.BaseDirectory); current is not null; current = current.Parent)
        {
            if (Directory.Exists(Path.Combine(current.FullName, "HybridCPU_ISE")) &&
                Directory.Exists(Path.Combine(current.FullName, "HybridCPU_ISE.Tests")))
            {
                return current.FullName;
            }
        }

        throw new DirectoryNotFoundException("Repository root was not found from the test output directory.");
    }
}
