namespace HybridCPU_ISE.Tests.Architecture;

/// <summary>
/// RF-08.3i records that foreground control packet placement is not the
/// existing typed scheduler Stage-B event authorized for issued identity.
/// </summary>
public sealed class Rf083iForegroundControlStageBBlockerAuditTests
{
    [Fact]
    public void ForegroundAuxiliaryControlUsesIssuePacketPlacementNotTypedStageB()
    {
        string root = FindRepositoryRoot();
        string packet = Read(root, "HybridCPU_ISE", "CloseToHSL", "Core", "Pipeline", "Core", "RuntimeClusterAdmissionPreparation.BundleIssuePacket.cs");

        Assert.Contains("byte auxiliarySlotMask = decisionDraft.AuxiliaryReservationMask", packet, StringComparison.Ordinal);
        Assert.Contains("TryAssignToPacket(\n                    slot,", packet, StringComparison.Ordinal);
        Assert.Contains("private static bool TryResolvePacketLane(\n            DecodedBundleSlotDescriptor slot", packet, StringComparison.Ordinal);
        Assert.Contains("SlotClassLaneMap.GetLaneMask(placement.RequiredSlotClass)", packet, StringComparison.Ordinal);
        Assert.DoesNotContain("TryClassAdmission", packet, StringComparison.Ordinal);
        Assert.DoesNotContain("TryMaterializeLane", packet, StringComparison.Ordinal);
        Assert.DoesNotContain("PostStageBIssuedAttempt", packet, StringComparison.Ordinal);
    }

    [Fact]
    public void ForegroundBranchExecutesFromLaneSevenWhileFspStillExcludesControlFlow()
    {
        string root = FindRepositoryRoot();
        string execute = Read(root, "HybridCPU_ISE", "CloseToHSL", "Core", "Pipeline", "ExecutionFlow", "ControlFlow", "CPU_Core.PipelineExecution.ControlFlow.cs");
        string fsp = Read(root, "HybridCPU_ISE", "CloseToHSL", "Core", "Pipeline", "Scheduling", "Fsp", "CPU_Core.PipelineExecution.Fsp.cs");

        Assert.Contains("TryExecuteExplicitPacketLane7Branch", execute, StringComparison.Ordinal);
        Assert.Contains("if (laneIndex != 7", execute, StringComparison.Ordinal);
        Assert.Contains("&& !candidate.IsControlFlow", fsp, StringComparison.Ordinal);
        Assert.Contains("candidate is not Core.ScalarALUMicroOp", fsp, StringComparison.Ordinal);
    }

    [Fact]
    public void PcWritePublicationRemainsAtTheExistingRetireOwner()
    {
        string root = FindRepositoryRoot();
        string control = Read(root, "HybridCPU_ISE", "CloseToHSL", "Core", "Pipeline", "MicroOps", "Control", "MicroOp.Control.cs");
        string coordinator = Read(root, "HybridCPU_ISE", "CloseToHSL", "Core", "Architecture", "Registers", "Retire", "RetireCoordinator.cs");

        Assert.Contains("RetireRecord.PcWrite(vtId, _resolvedRetireTargetAddress)", control, StringComparison.Ordinal);
        Assert.Contains("case RetireRecordKind.PcWrite:", coordinator, StringComparison.Ordinal);
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
