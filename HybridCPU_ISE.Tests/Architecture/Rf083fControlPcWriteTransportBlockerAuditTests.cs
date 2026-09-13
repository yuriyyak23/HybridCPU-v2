namespace HybridCPU_ISE.Tests.Architecture;

/// <summary>
/// RF-08.3f records the control/PcWrite blocker without manufacturing an
/// issued attempt or extending the scalar RegisterWrite carrier.
/// </summary>
public sealed class Rf083fControlPcWriteTransportBlockerAuditTests
{
    [Fact]
    public void BranchPcWriteRemainsAnExistingRetireCoordinatorPayload()
    {
        string root = FindRepositoryRoot();
        string control = Read(root, "HybridCPU_ISE", "CloseToHSL", "Core", "Pipeline", "MicroOps", "Control", "MicroOp.Control.cs");
        string coordinator = Read(root, "HybridCPU_ISE", "CloseToHSL", "Core", "Architecture", "Registers", "Retire", "RetireCoordinator.cs");

        Assert.Contains("public sealed class BranchMicroOp", control, StringComparison.Ordinal);
        Assert.Contains("RetireRecord.PcWrite(vtId, _resolvedRetireTargetAddress)", control, StringComparison.Ordinal);
        Assert.Contains("case RetireRecordKind.PcWrite:", coordinator, StringComparison.Ordinal);
        Assert.Contains("ApplyPcWrite(record);", coordinator, StringComparison.Ordinal);
    }

    [Fact]
    public void ApprovedScalarCarrierCannotBeAttachedToControlFlow()
    {
        string root = FindRepositoryRoot();
        string fsp = Read(root, "HybridCPU_ISE", "CloseToHSL", "Core", "Pipeline", "Scheduling", "Fsp", "CPU_Core.PipelineExecution.Fsp.cs");
        string carrier = Read(root, "HybridCPU_ISE", "CloseToHSL", "Core", "Pipeline", "Scheduling", "PostStageBIssuedAttempt.cs");

        Assert.Contains("candidate is not Core.ScalarALUMicroOp", fsp, StringComparison.Ordinal);
        Assert.Contains("&& !candidate.IsControlFlow", fsp, StringComparison.Ordinal);
        Assert.DoesNotContain("BranchMicroOp", carrier, StringComparison.Ordinal);
        Assert.DoesNotContain("PcWrite", carrier, StringComparison.Ordinal);
    }

    [Fact]
    public void ControlCapabilityProjectionIsNotALiveExactIngress()
    {
        string root = FindRepositoryRoot();
        string specializedProjection = Read(root, "HybridCPU_ISE", "CloseToHSL", "Core", "Decoder", "Rf06SpecializedCapabilityProjection.cs");
        string fsp = Read(root, "HybridCPU_ISE", "CloseToHSL", "Core", "Pipeline", "Scheduling", "Fsp", "CPU_Core.PipelineExecution.Fsp.cs");
        string routing = Read(root, "HybridCPU_ISE", "CloseToHSL", "Core", "Pipeline", "Scheduling", "Rf06ScalarSchedulerRouting.cs");

        Assert.Contains("internal static Rf06ControlCapability ProjectControl(", specializedProjection, StringComparison.Ordinal);
        Assert.Contains("GeneratedStaticBinding binding", specializedProjection, StringComparison.Ordinal);
        Assert.DoesNotContain("Rf06SpecializedCapabilityProjection", fsp, StringComparison.Ordinal);
        Assert.DoesNotContain("ProjectControl(", routing, StringComparison.Ordinal);
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
