namespace HybridCPU_ISE.Tests.Architecture;

public sealed class Rf08Rf09PostClosureAcceptanceAuditTests
{
    [Fact]
    public void CurrentLedgersAreClosedUniqueAndFreeOfSupersededOpenClaims()
    {
        string root = FindRepositoryRoot();
        string status = Read(root, "Documentation/Documentation/ArchitectureAuthorityRefactor/08_RF08_RF09/00_CURRENT_STATUS_AND_READING_ORDER.md");
        string exit = Read(root, "Documentation/Documentation/ArchitectureAuthorityRefactor/08_RF08_RF09/03_RF08_EXIT_READINESS_LEDGER.md");
        string gate = Read(root, "Documentation/Documentation/ArchitectureAuthorityRefactor/08_RF08_RF09/02_RF09_ENTRY_GATE.md");

        Assert.Contains("| RF-08 | closed |", status, StringComparison.Ordinal);
        Assert.Contains("| RF-09 | closed; RF-09.0 through RF-09.4 complete |", status, StringComparison.Ordinal);
        Assert.Contains("RF-09: closed by RF-09.4", exit, StringComparison.Ordinal);
        Assert.DoesNotContain("slice but has not started", exit, StringComparison.Ordinal);
        Assert.Contains("RF-08.4bb", gate, StringComparison.Ordinal);
        Assert.Contains("RF-09 execution status: closed", gate, StringComparison.Ordinal);

        string[] rf08Rows = status.Split('\n')
            .Where(line => line.StartsWith("| RF-08.", StringComparison.Ordinal))
            .Select(line => line.Split('|')[1].Trim())
            .ToArray();
        string[] rf09Rows = status.Split('\n')
            .Where(line => line.StartsWith("| RF-09.", StringComparison.Ordinal))
            .Select(line => line.Split('|')[1].Trim())
            .ToArray();

        Assert.Equal(66, rf08Rows.Length);
        Assert.Equal(rf08Rows.Length, rf08Rows.Distinct(StringComparer.Ordinal).Count());
        Assert.Equal(5, rf09Rows.Length);
        Assert.Equal(rf09Rows.Length, rf09Rows.Distinct(StringComparer.Ordinal).Count());
    }

    [Fact]
    public void ReadinessEvidenceIsNotPromotedToPaperAuthority()
    {
        string root = FindRepositoryRoot();
        string paper = Read(root,
            "ResearchPaper/section/md base/5_Two_Stage_Admission_and_Bundle_Compositional_SMT_Packing.md");
        string adr = Read(root, "Documentation/Documentation/ArchitectureAuthorityRefactor/02_Authority/ADR-009_VLIW_Retirement.md");
        string audit = Read(root,
            "Documentation/Documentation/ArchitectureAuthorityRefactor/08_RF08_RF09/05_RF08_RF09_POST_CLOSURE_ACCEPTANCE_AUDIT.md");

        Assert.True(File.Exists(Path.Combine(root, "Documentation", "Documentation", "ArchitectureAuthorityRefactor", "Evidence",
            "RF08", "rf08.4ax-atomic-returned-result-decision-readiness-audit.md")));
        Assert.True(File.Exists(Path.Combine(root, "Documentation", "Documentation", "ArchitectureAuthorityRefactor", "Evidence",
            "RF08", "rf08.4ay-streamengine-scalar-register-decision-readiness-audit.md")));
        Assert.Contains("At RF-08.4aw both rows still expired", paper, StringComparison.Ordinal);
        Assert.Contains("RF-08.4az", paper, StringComparison.Ordinal);
        Assert.Contains("RF-08.4ba", paper, StringComparison.Ordinal);
        Assert.Contains("Historical residual RegisterWrite", adr, StringComparison.Ordinal);
        Assert.Contains("supersede the open status", adr, StringComparison.Ordinal);
        Assert.Contains("4ax/4ay are evidence-only decision-readiness audits", audit, StringComparison.Ordinal);
        Assert.Contains("Paper approvals are RF-08.4az and RF-08.4ba", audit, StringComparison.Ordinal);
    }

    [Fact]
    public void ConsolidatedCountsSourcesTriggersAndArtifactsAreExplicit()
    {
        string root = FindRepositoryRoot();
        string evidence = Read(root,
            "Documentation/Documentation/ArchitectureAuthorityRefactor/Evidence/RF08/rf08.4bb-consolidated-exit-evidence.md");
        string audit = Read(root,
            "Documentation/Documentation/ArchitectureAuthorityRefactor/08_RF08_RF09/05_RF08_RF09_POST_CLOSURE_ACCEPTANCE_AUDIT.md");

        string residualSection = Slice(evidence, "## Approved residual exclusions",
            "## Retained-unreachable constructor sources and exact triggers");
        Assert.Equal(23, residualSection.Split('\n').Count(line => line.StartsWith("| RF-08.", StringComparison.Ordinal)));

        string unreachableSection = Slice(evidence,
            "## Retained-unreachable constructor sources and exact triggers",
            "## Closed-world absence proof");
        Assert.Equal(7, unreachableSection.Split('\n').Count(line =>
            line.StartsWith("| `", StringComparison.Ordinal)));
        Assert.Contains("Exact re-audit trigger", unreachableSection, StringComparison.Ordinal);

        foreach (string artifact in new[]
        {
            "20260726-181745-Baseline",
            "20260726-182201-Retire",
            "20260726-203549-Baseline",
            "20260726-205250-Retire",
            "20260726-205019-ReplayIdentity",
            "20260726-205435-Documentation",
            "20260726_174051_527_matrix"
        })
        {
            Assert.Contains(artifact, audit, StringComparison.Ordinal);
        }
    }

    [Fact]
    public void PredicateDispositionAndNextPromptAreCurrent()
    {
        string root = FindRepositoryRoot();
        string status = Read(root, "Documentation/Documentation/ArchitectureAuthorityRefactor/08_RF08_RF09/00_CURRENT_STATUS_AND_READING_ORDER.md");
        string governance = Read(root, "Documentation/Documentation/ArchitectureAuthorityRefactor/05_Governance/05_Invariants_Dependency_Risks_DoD.md");
        string prompt = Read(root, "Documentation/Documentation/ArchitectureAuthorityRefactor/08_RF08_RF09/06_RF10_CONTINUATION_PROMPT.md");

        Assert.Contains("| RF-08.4ao | complete architecture decision C-C |", status, StringComparison.Ordinal);
        Assert.Contains("PredicateStateWrite` approved split-topology exclusion", governance, StringComparison.Ordinal);
        Assert.DoesNotContain("serving gate open", governance, StringComparison.Ordinal);
        Assert.Contains("Первая открытая фазовая задача — RF-10.0", prompt, StringComparison.Ordinal);
        Assert.Contains("не выполнять RF-10 migration в inventory slice", prompt, StringComparison.Ordinal);
        Assert.Contains("RF-08 закрыт RF-08.4bb", prompt, StringComparison.Ordinal);
        Assert.Contains("RF-09.0…09.4 закрыты", prompt, StringComparison.Ordinal);
    }

    private static string Slice(string source, string startMarker, string endMarker)
    {
        int start = source.IndexOf(startMarker, StringComparison.Ordinal);
        int end = source.IndexOf(endMarker, start, StringComparison.Ordinal);
        Assert.True(start >= 0 && end > start);
        return source[start..end];
    }

    private static string Read(string root, string relativePath) =>
        File.ReadAllText(Path.Combine(root,
            relativePath.Replace('/', Path.DirectorySeparatorChar)));

    private static string FindRepositoryRoot()
    {
        for (DirectoryInfo? current = new(AppContext.BaseDirectory);
             current is not null;
             current = current.Parent)
        {
            if (Directory.Exists(Path.Combine(current.FullName, "HybridCPU_ISE")) &&
                Directory.Exists(Path.Combine(current.FullName, "Documentation")))
            {
                return current.FullName;
            }
        }

        throw new DirectoryNotFoundException("Repository root was not found.");
    }
}
