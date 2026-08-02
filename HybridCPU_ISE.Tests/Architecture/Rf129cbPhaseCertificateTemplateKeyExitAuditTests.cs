namespace HybridCPU_ISE.Tests.Architecture;

public sealed class Rf129cbPhaseCertificateTemplateKeyExitAuditTests
{
    [Fact]
    public void FamilyRemainsInternalLayoutSpecificAndFreeOfExternalEscapeSeams()
    {
        string interCore = Read("HybridCPU_ISE", "CloseToHSL", "Core", "Pipeline", "Certificates", "ReplayPhaseSubstrate.cs");
        string source = Read("HybridCPU_ISE", "CloseToHSL", "Core", "Pipeline", "Certificates", "ReplayPhaseSubstrate.Implementations.cs");
        string paper = Read("ResearchPaper", "section", "md base", "7_Replay_Stable_Placement_Replay_Tokens_and_Execution_Boundaries.md");
        string testSupport = Read("HybridCPU_ISE", "CloseToHSL", "Core", "Pipeline", "Core", "CPU_Core.TestSupport.cs");
        string ledger = Read("Documentation", "ArchitectureAuthorityRefactor", "11_RF12", "00_ENTRY_STATUS_AND_ROADMAP.md");

        Assert.Contains("internal readonly struct PhaseCertificateTemplateKey", interCore, StringComparison.Ordinal);
        Assert.Contains("internal readonly struct PhaseCertificateTemplateKey4Way", source, StringComparison.Ordinal);
        Assert.Contains("CreateInterCoreTemplateKey", source, StringComparison.Ordinal);
        Assert.Contains("CreateSmtTemplateKey", source, StringComparison.Ordinal);
        Assert.Contains("if (!templateKey.IsValid)", source, StringComparison.Ordinal);
        Assert.Contains("EvaluateSmtBoundaryGuard", source, StringComparison.Ordinal);
        Assert.DoesNotContain("PhaseCertificateTemplateKey", testSupport, StringComparison.Ordinal);
        Assert.Contains("internal, layout-specific equality carrier", paper, StringComparison.Ordinal);
        Assert.Contains("wire form follows from this internal cache taxonomy", paper, StringComparison.Ordinal);
        Assert.Contains("RF-12.9cb | closed exit audit", ledger, StringComparison.Ordinal);
    }

    private static string Read(params string[] path) => File.ReadAllText(Path.Combine(new[] { FindRepositoryRoot() }.Concat(path).ToArray()));

    private static string FindRepositoryRoot()
    {
        DirectoryInfo? current = new(AppContext.BaseDirectory);
        while (current is not null)
        {
            if (Directory.Exists(Path.Combine(current.FullName, "HybridCPU_ISE"))) return current.FullName;
            current = current.Parent;
        }
        throw new DirectoryNotFoundException();
    }
}
