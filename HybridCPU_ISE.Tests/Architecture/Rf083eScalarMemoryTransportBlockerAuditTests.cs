namespace HybridCPU_ISE.Tests.Architecture;

/// <summary>
/// RF-08.3e preserves the scalar-memory blocker as executable evidence.  It
/// does not authorize a synthetic load/store identity or a store cutover.
/// </summary>
public sealed class Rf083eScalarMemoryTransportBlockerAuditTests
{
    [Fact]
    public void ScalarLoadIsRetireVisibleButOutsideTheAuthorizedScalarAluCarrier()
    {
        string root = FindRepositoryRoot();
        string loadStore = Read(root, "HybridCPU_ISE", "CloseToHSL", "Core", "Pipeline", "MicroOps", "Memory", "MicroOp.LoadStore.cs");
        string fsp = Read(root, "HybridCPU_ISE", "CloseToHSL", "Core", "Pipeline", "Scheduling", "Fsp", "CPU_Core.PipelineExecution.Fsp.cs");
        string carrier = Read(root, "HybridCPU_ISE", "CloseToHSL", "Core", "Pipeline", "Scheduling", "PostStageBIssuedAttempt.cs");

        Assert.Contains("public class LoadMicroOp", loadStore, StringComparison.Ordinal);
        Assert.Contains("RetireRecord.RegisterWrite(vtId, DestRegID, _loadedValue)", loadStore, StringComparison.Ordinal);
        Assert.Contains("candidate is not Core.ScalarALUMicroOp", fsp, StringComparison.Ordinal);
        Assert.DoesNotContain("LoadMicroOp", carrier, StringComparison.Ordinal);
        Assert.DoesNotContain("StoreMicroOp", carrier, StringComparison.Ordinal);
    }

    [Fact]
    public void NoAuthorizedExactLoadOrStoreContractProjectionExistsAtTheLiveIngress()
    {
        string root = FindRepositoryRoot();
        string scalarProjection = Read(root, "HybridCPU_ISE", "Legacy", "CloseToHSL", "Core", "Decoder", "Rf06ScalarLegacyProjection.cs");
        string specializedProjection = Read(root, "HybridCPU_ISE", "CloseToHSL", "Core", "Decoder", "Rf06SpecializedCapabilityProjection.cs");
        string fsp = Read(root, "HybridCPU_ISE", "CloseToHSL", "Core", "Pipeline", "Scheduling", "Fsp", "CPU_Core.PipelineExecution.Fsp.cs");

        Assert.Contains("OpcodeValues.ADD or OpcodeValues.SUB or OpcodeValues.AND or OpcodeValues.OR or OpcodeValues.XOR", scalarProjection, StringComparison.Ordinal);
        Assert.Contains("MemoryCapability.None", scalarProjection, StringComparison.Ordinal);
        Assert.DoesNotContain("ProjectLoad", specializedProjection, StringComparison.Ordinal);
        Assert.DoesNotContain("ProjectStore", specializedProjection, StringComparison.Ordinal);
        Assert.Contains("private static MemoryCapability BuildMemoryCapability", specializedProjection, StringComparison.Ordinal);
        Assert.DoesNotContain("Rf06SpecializedCapabilityProjection", fsp, StringComparison.Ordinal);
    }

    [Fact]
    public void StoreVisibilityAndMemoryFaultOwnershipRemainSeparateFromThisAudit()
    {
        string root = FindRepositoryRoot();
        string loadStore = Read(root, "HybridCPU_ISE", "CloseToHSL", "Core", "Pipeline", "MicroOps", "Memory", "MicroOp.LoadStore.cs");
        string execute = Read(root, "HybridCPU_ISE", "CloseToHSL", "Core", "Pipeline", "ExecutionFlow", "Materialization", "CPU_Core.PipelineExecution.ExecuteHelpers.cs");
        string retire = Read(root, "HybridCPU_ISE", "CloseToHSL", "Core", "Pipeline", "Retire", "Evidence", "CPU_Core.PipelineExecution.Retire.cs");

        Assert.Contains("public class StoreMicroOp", loadStore, StringComparison.Ordinal);
        Assert.Contains("ProjectSingleLaneScalarLoadRetryOutcome", execute, StringComparison.Ordinal);
        Assert.Contains("ProjectSingleLaneScalarStoreRetryOutcome", execute, StringComparison.Ordinal);
        Assert.Contains("RetireCoordinator.Retire(retireBatch.RetireRecords)", retire, StringComparison.Ordinal);
        Assert.DoesNotContain("PostStageBIssuedAttempt", loadStore, StringComparison.Ordinal);
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
