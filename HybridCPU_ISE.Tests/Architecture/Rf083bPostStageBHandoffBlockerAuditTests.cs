namespace HybridCPU_ISE.Tests.Architecture;

/// <summary>
/// RF-08.3b records the pre-authorization topology fact. RF-08.3d may consume
/// that fact only through the authorized typed Stage-B carrier, never by a
/// second RF-06 scheduler invocation or later reconstruction.
/// </summary>
public sealed class Rf083bPostStageBHandoffBlockerAuditTests
{
    [Fact]
    public void LiveFspRetainsRestoreBoundaryAndCannotCreateAnIssuedAttempt()
    {
        string root = FindRepositoryRoot();
        string fsp = Read(root,
            "HybridCPU_ISE", "CloseToHSL", "Core", "Pipeline", "Scheduling", "Fsp",
            "CPU_Core.PipelineExecution.Fsp.cs");

        int schedulerCall = fsp.IndexOf(
            "packedBundle = pod.PackBundleIntraCoreSmt(",
            StringComparison.Ordinal);
        int candidateRestore = fsp.IndexOf(
            "RestoreUnmaterializedSmtCandidates(packedBundle, nominatedCandidates, nominatedSlots);",
            StringComparison.Ordinal);

        Assert.True(schedulerCall >= 0, "The live FSP caller must remain inventoried.");
        Assert.True(candidateRestore > schedulerCall,
            "The live carrier array is post-processed after scheduling and is not an exact accepted-attempt ledger.");
        Assert.Contains("out Core.MicroOp[] packedBundle", fsp, StringComparison.Ordinal);
        Assert.Contains("PostStageBIdentityTemplate", fsp, StringComparison.Ordinal);
        Assert.DoesNotContain("PostStageBIssuedAttempt.CreateAfterSuccessfulStageB", fsp, StringComparison.Ordinal);
    }

    [Fact]
    public void ExistingSchedulerStageBStillReturnsThePackedArrayAndOwnsTheSingleAuthorizedFactory()
    {
        string root = FindRepositoryRoot();
        string scheduler = Read(root,
            "HybridCPU_ISE", "CloseToHSL", "Core", "Pipeline", "Scheduling", "Smt",
            "MicroOpScheduler.SMT.cs");
        string admission = Read(root,
            "HybridCPU_ISE", "CloseToHSL", "Core", "Pipeline", "Scheduling", "Admission",
            "MicroOpScheduler.Admission.cs");

        Assert.Contains("public MicroOp[] PackBundleIntraCoreSmt(", scheduler, StringComparison.Ordinal);
        Assert.Contains("TryClassAdmission(candidate", scheduler, StringComparison.Ordinal);
        Assert.Contains("TryMaterializeLane(candidate, bundleOccupancy, out int lane", scheduler, StringComparison.Ordinal);
        Assert.Contains("result[lane] = candidate;", scheduler, StringComparison.Ordinal);
        Assert.Contains("private bool TryMaterializeLane(", admission, StringComparison.Ordinal);

        Assert.Contains("PostStageBIssuedAttempt.CreateAfterSuccessfulStageB(template, lane)", scheduler, StringComparison.Ordinal);
        Assert.DoesNotContain("CreateAfterSuccessfulStageB(template, slot)", scheduler, StringComparison.Ordinal);
        Assert.DoesNotContain("ScheduledOperation", admission, StringComparison.Ordinal);
        Assert.DoesNotContain("ExecutionRecord", admission, StringComparison.Ordinal);
        Assert.DoesNotContain("GeneratedStaticBinding", admission, StringComparison.Ordinal);
    }

    [Fact]
    public void LiveIssuePacketReprojectsLanesWithoutAnExactAttemptOrBinding()
    {
        string root = FindRepositoryRoot();
        string handoff = Read(root,
            "HybridCPU_ISE", "CloseToHSL", "Core", "Pipeline", "Core",
            "RuntimeClusterAdmissionPreparation.Handoff.cs");
        string packet = Read(root,
            "HybridCPU_ISE", "CloseToHSL", "Core", "Pipeline", "Core",
            "RuntimeClusterAdmissionPreparation.BundleIssuePacket.cs");
        string materialization = Read(root,
            "HybridCPU_ISE", "CloseToHSL", "Core", "Pipeline", "ExecutionFlow", "Materialization",
            "CPU_Core.PipelineExecution.Materialization.cs");

        Assert.Contains("BundleIssuePacket.Create(", handoff, StringComparison.Ordinal);
        Assert.Contains("TryResolvePacketLane(", packet, StringComparison.Ordinal);
        Assert.Contains("IssuePacketLane.Create(physicalLaneIndex", packet, StringComparison.Ordinal);
        Assert.Contains("CreateExecuteLaneState(issuePacket.PC", materialization, StringComparison.Ordinal);

        foreach (string source in new[] { handoff, packet, materialization })
        {
            Assert.DoesNotContain("ScheduledOperation", source, StringComparison.Ordinal);
            Assert.DoesNotContain("ExecutionRecord", source, StringComparison.Ordinal);
            Assert.DoesNotContain("VliwOperationId", source, StringComparison.Ordinal);
            Assert.DoesNotContain("GeneratedStaticBinding", source, StringComparison.Ordinal);
        }
    }

    [Fact]
    public void FrozenCanonicalBindingExistsUpstreamButTheRf06AttemptSeamHasNoLiveCaller()
    {
        string root = FindRepositoryRoot();
        string canonical = Read(root,
            "HybridCPU_ISE", "CloseToHSL", "Core", "Decoder",
            "CanonicalDecodedContracts.cs");
        string runtimeState = Read(root,
            "HybridCPU_ISE", "CloseToHSL", "Core", "Decoder",
            "DecodedBundleDescriptor.cs");
        string coreRoot = Path.Combine(root, "HybridCPU_ISE", "CloseToHSL", "Core");
        string routingPath = Path.Combine(
            coreRoot, "Pipeline", "Scheduling", "Rf06ScalarSchedulerRouting.cs");

        Assert.Contains("public GeneratedStaticBinding? StaticBinding", canonical, StringComparison.Ordinal);
        Assert.Contains("public Decoder.DecodedInstructionBundle CanonicalDecode", runtimeState, StringComparison.Ordinal);

        string[] productionRoutingReferences = Directory.GetFiles(coreRoot, "*.cs", SearchOption.AllDirectories)
            .Where(path => File.ReadAllText(path).Contains(
                "Rf06ScalarSchedulerRouting", StringComparison.Ordinal))
            .ToArray();
        Assert.Equal(new[] { routingPath }, productionRoutingReferences, PathComparer);

        string routing = File.ReadAllText(routingPath);
        Assert.Contains("ScheduledOperation.CreateAfterStageB(", routing, StringComparison.Ordinal);
        Assert.Contains("internal ScheduledOperation? ScheduledOperation", routing, StringComparison.Ordinal);
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
