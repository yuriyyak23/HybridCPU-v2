namespace HybridCPU_ISE.Tests.Architecture;

/// <summary>
/// RF-08.3a is an entry audit, not a live cutover.  It freezes the factual
/// scalar ingress boundary so later work cannot fabricate an issued-attempt
/// identity in EX, MEM, WB, or the retire owner.
/// </summary>
public sealed class Rf083aScalarLinkageIngressAuditTests
{
    [Fact]
    public void LiveScalarIngressCarriesSlotVtAndLaneButNoIssuedAttemptObject()
    {
        string root = FindRepositoryRoot();
        string issuePacketLane = Read(root,
            "HybridCPU_ISE", "CloseToHSL", "Core", "Pipeline", "Core",
            "RuntimeClusterAdmissionPreparation.IssuePacketLane.cs");
        string execute = Read(root,
            "HybridCPU_ISE", "CloseToHSL", "Core", "Pipeline", "Stages", "Execute",
            "CPU_Core.Pipeline.Stages.ScalarExecuteLaneState.cs");
        string memory = Read(root,
            "HybridCPU_ISE", "CloseToHSL", "Core", "Pipeline", "Stages", "Memory",
            "CPU_Core.Pipeline.Stages.ScalarMemoryLaneState.cs");
        string writeBack = Read(root,
            "HybridCPU_ISE", "CloseToHSL", "Core", "Pipeline", "Stages", "WriteBack",
            "CPU_Core.Pipeline.Stages.ScalarWriteBackLaneState.cs");

        Assert.Contains("PhysicalLaneIndex", issuePacketLane, StringComparison.Ordinal);
        Assert.Contains("SlotIndex", issuePacketLane, StringComparison.Ordinal);
        Assert.Contains("VirtualThreadId", issuePacketLane, StringComparison.Ordinal);
        Assert.Contains("OwnerThreadId", issuePacketLane, StringComparison.Ordinal);

        foreach (string source in new[] { issuePacketLane, execute, memory, writeBack })
        {
            Assert.DoesNotContain("ScheduledOperation", source, StringComparison.Ordinal);
            Assert.DoesNotContain("ExecutionRecord", source, StringComparison.Ordinal);
            Assert.DoesNotContain("VliwOperationId", source, StringComparison.Ordinal);
            Assert.DoesNotContain("GeneratedStaticBinding", source, StringComparison.Ordinal);
        }
    }

    [Fact]
    public void IssuedAttemptFactoryIsLimitedToRf06AndTheAuthorizedPostStageBCarrier()
    {
        string root = FindRepositoryRoot();
        string coreRoot = Path.Combine(root, "HybridCPU_ISE", "CloseToHSL", "Core");
        string routingPath = Path.Combine(coreRoot, "Pipeline", "Scheduling", "Rf06ScalarSchedulerRouting.cs");
        string routing = File.ReadAllText(routingPath);

        Assert.Contains("ScheduledOperation.CreateAfterStageB(", routing, StringComparison.Ordinal);
        Assert.Contains("PackBundleIntraCoreSmt(", routing, StringComparison.Ordinal);

        string[] liveFactorySites = Directory.GetFiles(coreRoot, "*.cs", SearchOption.AllDirectories)
            .Where(path => File.ReadAllText(path).Contains(
                "ScheduledOperation.CreateAfterStageB(", StringComparison.Ordinal))
            .ToArray();
        string carrierPath = Path.Combine(coreRoot, "Pipeline", "Scheduling", "PostStageBIssuedAttempt.cs");
        Assert.Equal(2, liveFactorySites.Length);
        Assert.Contains(liveFactorySites, path => PathComparer.Equals(path, carrierPath));
        Assert.Contains(liveFactorySites, path => PathComparer.Equals(path, routingPath));
    }

    [Fact]
    public void ExecuteAndRetireStagesCannotFabricateIdentityFromLegacyLaneFields()
    {
        string root = FindRepositoryRoot();
        string materialization = Read(root,
            "HybridCPU_ISE", "CloseToHSL", "Core", "Pipeline", "ExecutionFlow", "Materialization",
            "CPU_Core.PipelineExecution.Materialization.cs");
        string retire = Read(root,
            "HybridCPU_ISE", "CloseToHSL", "Core", "Pipeline", "Retire", "Evidence",
            "CPU_Core.PipelineExecution.Retire.cs");
        string stageFlowTypes = Read(root,
            "HybridCPU_ISE", "CloseToHSL", "Core", "Pipeline", "ExecutionFlow", "StageFlow",
            "CPU_Core.PipelineExecution.Types.cs");

        foreach (string source in new[] { materialization, stageFlowTypes })
        {
            Assert.DoesNotContain("ScheduledOperation.CreateAfterStageB(", source, StringComparison.Ordinal);
            Assert.DoesNotContain("ExecutionRecord.Create(", source, StringComparison.Ordinal);
            Assert.DoesNotContain("RetireVisibleEffectIdentity.Freeze(", source, StringComparison.Ordinal);
            Assert.DoesNotContain("ScalarRegisterWriteRetireEffect.Freeze(", source, StringComparison.Ordinal);
        }

        Assert.DoesNotContain("ScheduledOperation.CreateAfterStageB(", retire, StringComparison.Ordinal);
        Assert.DoesNotContain("ExecutionRecord.Create(", retire, StringComparison.Ordinal);
        Assert.Contains("CompleteScalarRegisterWrite(retireRecord)", retire, StringComparison.Ordinal);

        Assert.Contains("EmitWriteBackRetireRecords(", retire, StringComparison.Ordinal);
        Assert.Contains("RetireCoordinator.Retire(retireBatch.RetireRecords)", retire, StringComparison.Ordinal);
        Assert.Contains("EmitWriteBackRetireRecords(", stageFlowTypes, StringComparison.Ordinal);
    }

    private static readonly IEqualityComparer<string> PathComparer =
        OperatingSystem.IsWindows() ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal;

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
