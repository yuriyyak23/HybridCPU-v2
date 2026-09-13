namespace HybridCPU_ISE.Tests.Architecture;

public sealed class Rf1212FinalClosedWorldExitAuditTests
{
    [Fact]
    public void CurrentClosureFollowsTheSupersededHistoricalEvidenceAndReconciliation()
    {
        string ledger = Read("Documentation", "Documentation", "ArchitectureAuthorityRefactor", "11_RF12", "00_ENTRY_STATUS_AND_ROADMAP.md");
        string evidence = Read("Documentation", "Documentation", "ArchitectureAuthorityRefactor", "Evidence", "RF12", "rf12.12-final-closed-world-identifier-owner-raw-seam-invalid-path-exit-audit.md");
        string amendment = Read("Documentation", "Documentation", "ArchitectureAuthorityRefactor", "Evidence", "RF12", "rf12.12a-final-exit-audit-evidence-completeness-amendment.md");
        string reconciliation = Read("Documentation", "Documentation", "ArchitectureAuthorityRefactor", "Evidence", "RF12", "rf12.12b-external-audit-reconciliation-and-reopened-handoff.md");
        string currentFinal = Read("Documentation", "Documentation", "ArchitectureAuthorityRefactor", "Evidence", "RF12", "rf12.12c-final-post-reconciliation-closed-world-exit-audit.md");
        string guard = Read("HybridCPU_ISE.Tests", "Architecture", "Rf120ResourceIdIngressGuardTests.cs");

        Assert.Contains("RF-12.9cb | closed exit audit", ledger, StringComparison.Ordinal);
        Assert.Contains("RF-12.10 | closed inventory/freeze", ledger, StringComparison.Ordinal);
        Assert.Contains("RF-12.11 | closed retention audit", ledger, StringComparison.Ordinal);
        Assert.Contains("RF-12.12 | superseded exit audit", ledger, StringComparison.Ordinal);
        Assert.Contains("RF-12.12a | superseded audit amendment", ledger, StringComparison.Ordinal);
        Assert.Contains("RF-12.12b | closed reconciliation", ledger, StringComparison.Ordinal);
        Assert.Contains("RF-12.12c | superseded final exit audit", ledger, StringComparison.Ordinal);
        Assert.Contains("RF-12.12h | closed audit-disposition and stale-wording reconciliation", ledger, StringComparison.Ordinal);
        Assert.Contains("RF-12 overall | closed at RF-12.12h", ledger, StringComparison.Ordinal);
        Assert.Contains("No generic `ChannelId`, `DomainId`, `TokenId`", evidence, StringComparison.Ordinal);
        Assert.Contains("template keys suppress reuse and use live legality", evidence, StringComparison.Ordinal);
        Assert.Contains("paper taxonomy to every family disposition", amendment, StringComparison.Ordinal);
        Assert.Contains("RF-12 is **open**", reconciliation, StringComparison.Ordinal);
        Assert.Contains("RF-12.5d", reconciliation, StringComparison.Ordinal);
        Assert.Contains("RF-12 is **closed**", currentFinal, StringComparison.Ordinal);
        Assert.Contains("ExistingCheckedTypesAndUncheckedPublicConstructionSeamsRemainExplicit", guard, StringComparison.Ordinal);
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
