namespace HybridCPU_ISE.Tests.Architecture;

public sealed class Rf1212gReproducibilitySnapshotDispositionTests
{
    [Fact]
    public void EvidenceDoesNotClaimAnImmutableSnapshotForTheDirtyTree()
    {
        string evidence = Read("Documentation", "Documentation", "ArchitectureAuthorityRefactor", "Evidence", "RF12",
            "rf12.12g-reproducibility-snapshot-disposition.md");

        Assert.Contains("687 entries", evidence, StringComparison.Ordinal);
        Assert.Contains("**not** a source identity", evidence, StringComparison.Ordinal);
        Assert.Contains("**not** a clean checkout", evidence, StringComparison.Ordinal);
        Assert.Contains("does not publish a tree hash", evidence, StringComparison.Ordinal);
        Assert.Contains("c7efad2aac06169bc0ba00bbd29cdc0ff1395f1dcfc89ddde22265c6824bae50", evidence, StringComparison.Ordinal);
    }

    [Fact]
    public void LedgerRecordsTheLaterCancelledSnapshotDisposition()
    {
        string ledger = Read("Documentation", "Documentation", "ArchitectureAuthorityRefactor", "11_RF12", "00_ENTRY_STATUS_AND_ROADMAP.md");

        Assert.Contains("RF-12 is closed at RF-12.12h", ledger, StringComparison.Ordinal);
        Assert.Contains("RF-12.12g | closed", ledger, StringComparison.Ordinal);
        Assert.Contains("RF-12.12h | closed audit-disposition and stale-wording reconciliation", ledger, StringComparison.Ordinal);
        Assert.Contains("cancelled commit/SHA/immutable-snapshot execution", ledger, StringComparison.Ordinal);
    }

    private static string Read(params string[] parts)
    {
        DirectoryInfo? current = new(AppContext.BaseDirectory);
        while (current is not null)
        {
            if (Directory.Exists(Path.Combine(current.FullName, "HybridCPU_ISE")))
                return File.ReadAllText(Path.Combine(new[] { current.FullName }.Concat(parts).ToArray()));
            current = current.Parent;
        }

        throw new DirectoryNotFoundException();
    }
}
