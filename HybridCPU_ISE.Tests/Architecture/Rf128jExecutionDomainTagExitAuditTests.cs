namespace HybridCPU_ISE.Tests.Architecture;

/// <summary>RF-12.8j final closed-world audit for execution DomainTag.</summary>
public sealed class Rf128jExecutionDomainTagExitAuditTests
{
    [Fact]
    public void ExecutionCarrierSchedulerAndIdentityContoursRemainExplicit()
    {
        string root = Root();
        string placement = Read(root, "Core", "Pipeline", "Scheduling", "SlotPlacementMetadata.cs");
        string materialization = Read(root, "Core", "Pipeline", "ExecutionFlow", "Materialization",
            "CPU_Core.PipelineExecution.Materialization.cs");
        string guard = Read(root, "Core", "Pipeline", "Safety", "SafetyVerifier.Guards.cs");
        string replay = Read(root, "Core", "Pipeline", "Certificates", "ReplayPhaseSubstrate.Implementations.cs");
        Assert.Contains("public ulong DomainTag", placement, StringComparison.Ordinal);
        Assert.Contains("lane.DomainTag = issueLane.MicroOp?.Placement.DomainTag ?? 0", materialization, StringComparison.Ordinal);
        Assert.Contains("CreateGuardReject(RejectKind.DomainMismatch)", guard, StringComparison.Ordinal);
        Assert.Contains("opHasher.Compress(op.Placement.DomainTag)", replay, StringComparison.Ordinal);
    }

    [Fact]
    public void TimedMemoryAndTestSupportRemainOwnerLocalAndNoCheckedExecutionTypeExists()
    {
        string root = Root();
        string bank = File.ReadAllText(Path.Combine(root, "HybridCPU_ISE", "CloseToHSL", "Memory", "Banks",
            "MultiBankMemoryArea.cs"));
        string support = Read(root, "Core", "Pipeline", "Core", "CPU_Core.TestSupport.cs");
        Assert.Contains("SetAccessDomainTag(ulong domainTag)", bank, StringComparison.Ordinal);
        Assert.Contains("SilentSquashCount++", bank, StringComparison.Ordinal);
        Assert.Contains("TryApplyWriteBackStageDomainSquash()", support, StringComparison.Ordinal);
        Assert.False(File.Exists(Path.Combine(root, "HybridCPU_ISE", "CloseToHSL", "Core", "Pipeline", "ExecutionDomainTag.cs")));
    }

    private static string Read(string root, params string[] parts) => File.ReadAllText(Path.Combine([root, "HybridCPU_ISE", "CloseToHSL", .. parts]));
    private static string Root()
    {
        DirectoryInfo? current = new(AppContext.BaseDirectory);
        while (current is not null)
        {
            if (Directory.Exists(Path.Combine(current.FullName, "HybridCPU_ISE")) &&
                Directory.Exists(Path.Combine(current.FullName, "ResearchPaper"))) return current.FullName;
            current = current.Parent;
        }
        throw new DirectoryNotFoundException();
    }
}
