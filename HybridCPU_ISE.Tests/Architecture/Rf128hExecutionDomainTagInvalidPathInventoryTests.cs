namespace HybridCPU_ISE.Tests.Architecture;

/// <summary>RF-12.8h inventory of execution DomainTag guard-specific outcomes.</summary>
public sealed class Rf128hExecutionDomainTagInvalidPathInventoryTests
{
    [Fact]
    public void SchedulerGuardRejectsMismatchOnlyForNonzeroRequestedDomain()
    {
        string guards = Read("HybridCPU_ISE", "CloseToHSL", "Core", "Pipeline", "Safety", "SafetyVerifier.Guards.cs");
        Assert.Contains("requestedDomainTag == 0", guards, StringComparison.Ordinal);
        Assert.Contains("CreateGuardReject(RejectKind.DomainMismatch)", guards, StringComparison.Ordinal);

        string nomination = Read("HybridCPU_ISE", "CloseToHSL", "Core", "Pipeline", "Scheduling", "LaneChoice",
            "MicroOpScheduler.Nomination.cs");
        Assert.Contains("if (requestedDomainTag != 0)", nomination, StringComparison.Ordinal);
        Assert.Contains("if (!domainGuard.IsAllowed)", nomination, StringComparison.Ordinal);
        Assert.Contains("continue;", nomination, StringComparison.Ordinal);
    }

    [Fact]
    public void MemoryAndWriteBackOwnersKeepTheirDistinctSquashOutcomes()
    {
        string memory = Read("HybridCPU_ISE", "CloseToHSL", "Core", "Pipeline", "Stages", "Memory",
            "CPU_Core.PipelineExecution.Memory.cs");
        string writeBack = Read("HybridCPU_ISE", "CloseToHSL", "Core", "Pipeline", "ExecutionFlow", "Materialization",
            "CPU_Core.PipelineExecution.Materialization.cs");
        Assert.Contains("pipeEX.DomainTag == 0 || CsrMemDomainCert == 0", memory, StringComparison.Ordinal);
        Assert.Contains("pipeMEM.WritesRegister = false", memory, StringComparison.Ordinal);
        Assert.Contains("pipeWB.DomainTag == 0 || CsrMemDomainCert == 0", writeBack, StringComparison.Ordinal);
        Assert.Contains("pipeWB.WritesRegister = false", writeBack, StringComparison.Ordinal);
    }

    [Fact]
    public void TimedMemoryBankOwnerRetainsSilentSquashSeparately()
    {
        string bank = Read("HybridCPU_ISE", "CloseToHSL", "Memory", "Banks", "MultiBankMemoryArea.cs");
        Assert.Contains("if (_currentDomainTag != 0)", bank, StringComparison.Ordinal);
        Assert.Contains("if (!CheckBankDomainAccess(bankId, _currentDomainTag))", bank, StringComparison.Ordinal);
        Assert.Contains("SilentSquashCount++", bank, StringComparison.Ordinal);
    }

    private static string Read(params string[] parts) => File.ReadAllText(Path.Combine([Root(), .. parts]));
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
