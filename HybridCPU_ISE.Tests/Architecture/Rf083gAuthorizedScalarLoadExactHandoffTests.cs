namespace HybridCPU_ISE.Tests.Architecture;

/// <summary>
/// RF-08.3g freezes authorization only.  The next load-family implementation
/// must still add its decoder-owned source projection and its live proof.
/// </summary>
public sealed class Rf083gAuthorizedScalarLoadExactHandoffTests
{
    [Fact]
    public void PaperAuthorizesOnlyCanonicalScalarLoadTransportWithExactFrozenIdentity()
    {
        string root = FindRepositoryRoot();
        string paper = Read(root, "ResearchPaper", "section", "md base", "5_Two_Stage_Admission_and_Bundle_Compositional_SMT_Packing.md");

        Assert.Contains("RF-08.3g authorized scalar-load exact issued-attempt handoff", paper, StringComparison.Ordinal);
        Assert.Contains("already-frozen `GeneratedStaticBinding` instance", paper, StringComparison.Ordinal);
        Assert.Contains("successful Stage-B\n`ScheduledOperation`", paper, StringComparison.Ordinal);
        Assert.Contains("`LoadMicroOp`", paper, StringComparison.Ordinal);
        Assert.Contains("does not\nauthorize `StoreMicroOp`, atomics", paper, StringComparison.Ordinal);
        Assert.Contains("replay-stored, faulted or\nretrying load", paper, StringComparison.Ordinal);
    }

    [Fact]
    public void AdrKeepsMemoryAuthorityAndPublicationAtTheirExistingOwners()
    {
        string root = FindRepositoryRoot();
        string adr = Read(root, "Documentation", "ArchitectureAuthorityRefactor", "02_Authority", "ADR-009_VLIW_Retirement.md");

        Assert.Contains("RF-08.3g authorised scalar-load transport", adr, StringComparison.Ordinal);
        Assert.Contains("memory timing, MSHR", adr, StringComparison.Ordinal);
        Assert.Contains("StoreCommit", adr, StringComparison.Ordinal);
        Assert.Contains("RetireCoordinator", adr, StringComparison.Ordinal);
        Assert.Contains("does not change FSP owner/donor policy", adr, StringComparison.Ordinal);
    }

    [Fact]
    public void AuthorizationDoesNotSilentlyCreateALiveLoadIngress()
    {
        string root = FindRepositoryRoot();
        string fsp = Read(root, "HybridCPU_ISE", "CloseToHSL", "Core", "Pipeline", "Scheduling", "Fsp", "CPU_Core.PipelineExecution.Fsp.cs");
        string specializedProjection = Read(root, "HybridCPU_ISE", "CloseToHSL", "Core", "Decoder", "Rf06SpecializedCapabilityProjection.cs");

        Assert.Contains("candidate is not Core.ScalarALUMicroOp", fsp, StringComparison.Ordinal);
        Assert.DoesNotContain("ProjectLoad", specializedProjection, StringComparison.Ordinal);
        Assert.DoesNotContain("ProjectStore", specializedProjection, StringComparison.Ordinal);
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
