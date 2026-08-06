namespace HybridCPU_ISE.Tests.Architecture;

public sealed class Rf1212dCurrentStatusDocumentationReconciliationTests
{
    [Fact]
    public void CanonicalCurrentStatusDocumentsNameOnlyRf1212d()
    {
        string root = FindRepositoryRoot();
        string ledger = Read(root, "Documentation", "Documentation", "ArchitectureAuthorityRefactor", "11_RF12", "00_ENTRY_STATUS_AND_ROADMAP.md");
        string overview = Read(root, "Documentation", "Documentation", "ArchitectureAuthorityRefactor", "00_Overview", "00_README.md");
        string evidence = Read(root, "Documentation", "Documentation", "ArchitectureAuthorityRefactor", "Evidence", "README.md");
        string clarification = Read(root, "Documentation", "Documentation", "ArchitectureAuthorityRefactor", "HybridCPU-v2_Ref1_Refactoring_Plan_Clarifications.md");

        const string current = "RF-12 is closed at RF-12.12h";
        Assert.Contains(current, ledger, StringComparison.Ordinal);
        Assert.Contains("RF-12 | closed at RF-12.12h", overview, StringComparison.Ordinal);
        Assert.Contains(current, evidence, StringComparison.Ordinal);
        Assert.Contains("RF-12 closed at RF-12.12h", clarification, StringComparison.Ordinal);

        Assert.DoesNotContain("RF-12 | open at RF-12.5j", overview, StringComparison.Ordinal);
        Assert.DoesNotContain("RF-12 is open through closed RF-12.4a", evidence, StringComparison.Ordinal);
        Assert.DoesNotContain("current ledger reopens RF-12 at RF-12.5d", clarification, StringComparison.Ordinal);
    }

    [Fact]
    public void LedgerSupersedesPrematureFinalVerdictsAndStaleQueueRows()
    {
        string ledger = Read(FindRepositoryRoot(), "Documentation", "Documentation", "ArchitectureAuthorityRefactor", "11_RF12", "00_ENTRY_STATUS_AND_ROADMAP.md");

        Assert.Contains("RF-12.12c | superseded final exit audit", ledger, StringComparison.Ordinal);
        Assert.Contains("RF-12.12d | closed current-status reconciliation", ledger, StringComparison.Ordinal);
        Assert.Contains("RF-12.12e | closed residual RM-1 reconciliation", ledger, StringComparison.Ordinal);
        Assert.Contains("RF-12.12h | closed audit-disposition and stale-wording reconciliation", ledger, StringComparison.Ordinal);
        Assert.Contains("RF-12 overall | closed at RF-12.12h", ledger, StringComparison.Ordinal);
        Assert.DoesNotContain("RF-12.6bf — **next**", ledger, StringComparison.Ordinal);
        Assert.DoesNotContain("RF-12.6bg — **next**", ledger, StringComparison.Ordinal);
        Assert.DoesNotContain("RF-12.6bh — **next**", ledger, StringComparison.Ordinal);
        Assert.DoesNotContain("RF-12.6bi — **next**", ledger, StringComparison.Ordinal);
    }

    [Fact]
    public void ReconciliationEvidencePreservesPaperAuthorityAndSeparatesLaterGaps()
    {
        string evidence = Read(FindRepositoryRoot(), "Documentation", "Documentation", "ArchitectureAuthorityRefactor", "Evidence", "RF12",
            "rf12.12d-current-status-documentation-reconciliation.md");

        Assert.Contains("not architectural authority", evidence, StringComparison.Ordinal);
        Assert.Contains("Paper section 3.7", evidence, StringComparison.Ordinal);
        Assert.Contains("authority for identifier taxonomy", evidence, StringComparison.Ordinal);
        Assert.Contains("reproducible immutable evidence, residual RM-1 proof", evidence, StringComparison.Ordinal);
        Assert.Contains("until a family-local invalid-input decision", evidence, StringComparison.Ordinal);
        Assert.Contains("RF-12.12d is closed", evidence, StringComparison.Ordinal);
    }

    private static string Read(string root, params string[] parts) =>
        File.ReadAllText(Path.Combine(new[] { root }.Concat(parts).ToArray()));

    private static string FindRepositoryRoot()
    {
        DirectoryInfo? current = new(AppContext.BaseDirectory);
        while (current is not null)
        {
            if (Directory.Exists(Path.Combine(current.FullName, "HybridCPU_ISE")))
                return current.FullName;
            current = current.Parent;
        }

        throw new DirectoryNotFoundException("Repository root was not found.");
    }
}
