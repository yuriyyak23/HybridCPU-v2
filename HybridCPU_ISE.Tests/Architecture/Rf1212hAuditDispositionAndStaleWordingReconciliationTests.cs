namespace HybridCPU_ISE.Tests.Architecture;

public sealed class Rf1212hAuditDispositionAndStaleWordingReconciliationTests
{
    [Fact]
    public void CurrentDocumentsCloseRf12WithoutTreatingDirtyCountsAsIdentity()
    {
        string root = Root();
        string ledger = Read(root, "Documentation", "Documentation", "ArchitectureAuthorityRefactor", "11_RF12", "00_ENTRY_STATUS_AND_ROADMAP.md");
        string overview = Read(root, "Documentation", "Documentation", "ArchitectureAuthorityRefactor", "00_Overview", "00_README.md");
        string index = Read(root, "Documentation", "Documentation", "ArchitectureAuthorityRefactor", "Evidence", "README.md");

        Assert.Contains("RF-12 is closed at RF-12.12h", ledger, StringComparison.Ordinal);
        Assert.Contains("Historical entry snapshot and authority", ledger, StringComparison.Ordinal);
        Assert.Contains("RF-12 overall | closed at RF-12.12h", ledger, StringComparison.Ordinal);
        Assert.Contains("RF-12 | closed at RF-12.12h", overview, StringComparison.Ordinal);
        Assert.Contains("after RF-12.12h audit-disposition closure", overview, StringComparison.Ordinal);
        Assert.Contains("RF-12 is closed at RF-12.12h", index, StringComparison.Ordinal);
        Assert.DoesNotContain("pending reproducibility disposition", index, StringComparison.Ordinal);
    }

    [Fact]
    public void EvidenceSeparatesVolatileCountsFromCancelledSnapshotWork()
    {
        string evidence = Read(Root(), "Documentation", "Documentation", "ArchitectureAuthorityRefactor", "Evidence", "RF12",
            "rf12.12h-audit-disposition-and-stale-wording-reconciliation.md");

        Assert.Contains("687 entries", evidence, StringComparison.Ordinal);
        Assert.Contains("689 entries", evidence, StringComparison.Ordinal);
        Assert.Contains("neither a source identity nor a", evidence, StringComparison.Ordinal);
        Assert.Contains("snapshot proof", evidence, StringComparison.Ordinal);
        Assert.Contains("null-delimited status manifest", evidence, StringComparison.Ordinal);
        Assert.Contains("explicit cancellation", evidence, StringComparison.Ordinal);
        Assert.Contains("does not claim a reproducible release snapshot", evidence, StringComparison.Ordinal);
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
