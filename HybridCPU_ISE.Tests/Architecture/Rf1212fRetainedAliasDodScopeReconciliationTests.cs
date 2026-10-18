namespace HybridCPU_ISE.Tests.Architecture;

public sealed class Rf1212fRetainedAliasDodScopeReconciliationTests
{
    [Fact]
    public void GovernanceAndLedgerUseThePaperDefinedBoundary()
    {
        string root = Root();
        string dod = Read(root, "Documentation", "Documentation", "ArchitectureAuthorityRefactor", "05_Governance", "05_Invariants_Dependency_Risks_DoD.md");
        string ledger = Read(root, "Documentation", "Documentation", "ArchitectureAuthorityRefactor", "11_RF12", "00_ENTRY_STATUS_AND_ROADMAP.md");

        Assert.Contains("checked or owner-validated RF-12 resource boundary", dod, StringComparison.Ordinal);
        Assert.Contains("paper-listed legacy raw compatibility", dod, StringComparison.Ordinal);
        Assert.Contains("is not validation", dod, StringComparison.Ordinal);
        Assert.Contains("checked or owner-validated resource boundaries", ledger, StringComparison.Ordinal);
        Assert.Contains("pending its own", ledger, StringComparison.Ordinal);
        Assert.Contains("invalid-input decision", ledger, StringComparison.Ordinal);
    }

    [Fact]
    public void EvidenceSeparatesRetainedRawCompatibilityFromCheckedAuthority()
    {
        string evidence = Read(Root(), "Documentation", "Documentation", "ArchitectureAuthorityRefactor", "Evidence", "RF12",
            "rf12.12f-retained-alias-dod-scope-reconciliation.md");

        Assert.Contains("Paper section 3.7", evidence, StringComparison.Ordinal);
        Assert.Contains("no invalid-to-zero alias at a checked or", evidence, StringComparison.Ordinal);
        Assert.Contains("owner-validated resource boundary", evidence, StringComparison.Ordinal);
        Assert.Contains("No legacy alias is newly introduced", evidence, StringComparison.Ordinal);
    }

    private static string Root()
    {
        DirectoryInfo? current = new(AppContext.BaseDirectory);
        while (current is not null)
        {
            if (Directory.Exists(Path.Combine(current.FullName, "HybridCPU_ISE"))) return current.FullName;
            current = current.Parent;
        }
        throw new DirectoryNotFoundException();
    }

    private static string Read(string root, params string[] parts) =>
        File.ReadAllText(Path.Combine(new[] { root }.Concat(parts).ToArray()));
}
