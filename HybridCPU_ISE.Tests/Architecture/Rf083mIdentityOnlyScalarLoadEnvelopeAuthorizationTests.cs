namespace HybridCPU_ISE.Tests.Architecture;

/// <summary>
/// RF-08.3m is authority-only: a NoMemory static load contract can later be
/// used as the identity envelope required by ScheduledOperation, but cannot
/// become memory admission or a live FSP route in this slice.
/// </summary>
public sealed class Rf083mIdentityOnlyScalarLoadEnvelopeAuthorizationTests
{
    [Fact]
    public void PaperAndAdrAuthorizeOnlyTheIdentityEnvelopeException()
    {
        string root = FindRepositoryRoot();
        string paper = Read(root, "ResearchPaper", "section", "md base",
            "5_Two_Stage_Admission_and_Bundle_Compositional_SMT_Packing.md");
        string adr = Read(root, "Documentation", "ArchitectureAuthorityRefactor", "02_Authority",
            "ADR-009_VLIW_Retirement.md");

        Assert.Contains("RF-08.3m authorised identity-only scalar-load envelope", paper, StringComparison.Ordinal);
        Assert.Contains("identity-only", paper, StringComparison.Ordinal);
        Assert.Contains("`MemoryCapability` remains `None`", paper, StringComparison.Ordinal);
        Assert.Contains("`Rf06MemoryCapabilityAdmission`", paper, StringComparison.Ordinal);
        Assert.Contains("only future consumer authorized by this paragraph is the existing typed", paper, StringComparison.Ordinal);
        Assert.Contains("RF-08.3m identity-only scalar-load envelope", adr, StringComparison.Ordinal);
        Assert.Contains("must not be passed to", adr, StringComparison.Ordinal);
    }

    [Fact]
    public void ExistingLiveMemoryAndFspOwnersRemainUnwired()
    {
        string root = FindRepositoryRoot();
        string fsp = Read(root, "HybridCPU_ISE", "CloseToHSL", "Core", "Pipeline", "Scheduling", "Fsp",
            "CPU_Core.PipelineExecution.Fsp.cs");
        string admission = Read(root, "HybridCPU_ISE", "CloseToHSL", "Core", "Decoder",
            "Rf06MemoryCapabilityAdmission.cs");
        string projection = Read(root, "HybridCPU_ISE", "CloseToHSL", "Core", "Decoder",
            "Rf08ScalarLoadContractProjection.cs");

        Assert.Contains("if (candidate is not Core.ScalarALUMicroOp)", fsp, StringComparison.Ordinal);
        Assert.DoesNotContain("Rf08ScalarLoadContractProjection", fsp, StringComparison.Ordinal);
        Assert.DoesNotContain("StaticMemoryAccessPlan", fsp, StringComparison.Ordinal);
        Assert.Contains("memory.Kind == MemoryCapabilityKind.NoMemory", admission, StringComparison.Ordinal);
        Assert.DoesNotContain("AdmissionRecord", projection, StringComparison.Ordinal);
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
