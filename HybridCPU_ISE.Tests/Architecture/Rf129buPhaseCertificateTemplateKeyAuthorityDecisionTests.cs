namespace HybridCPU_ISE.Tests.Architecture;

public sealed class Rf129buPhaseCertificateTemplateKeyAuthorityDecisionTests
{
    [Fact]
    public void PaperDefinesLayoutSpecificCacheKeyAbsenceAndFallbackWithoutAuthorityInflation()
    {
        string paper = Read("ResearchPaper", "section", "md base", "7_Replay_Stable_Placement_Replay_Tokens_and_Execution_Boundaries.md");
        string substrate = Read("HybridCPU_ISE", "CloseToHSL", "Core", "Pipeline", "Certificates", "ReplayPhaseSubstrate.Implementations.cs");
        string ledger = Read("Documentation", "Documentation", "ArchitectureAuthorityRefactor", "11_RF12", "00_ENTRY_STATUS_AND_ROADMAP.md");
        Assert.Contains("Legality-template key scope, absence, and authority", paper, StringComparison.Ordinal);
        Assert.Contains("internal, layout-specific equality carrier", paper, StringComparison.Ordinal);
        Assert.Contains("invalid/default key means that no reusable legality template is present", paper, StringComparison.Ordinal);
        Assert.Contains("existing live guard and legality path", paper, StringComparison.Ordinal);
        Assert.Contains("equality substitute or architectural identity", paper, StringComparison.Ordinal);
        Assert.Contains("_template = templateKey.IsValid", substrate, StringComparison.Ordinal);
        Assert.Contains("RF-12.9bu | closed architecture decision", ledger, StringComparison.Ordinal);
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
