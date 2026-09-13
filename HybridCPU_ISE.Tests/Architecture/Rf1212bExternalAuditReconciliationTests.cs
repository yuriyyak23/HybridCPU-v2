namespace HybridCPU_ISE.Tests.Architecture;

public sealed class Rf1212bExternalAuditReconciliationTests
{
    [Fact]
    public void CurrentLedgerIsClosedAndHistoricalHandoffsAreLabelled()
    {
        string root = FindRepositoryRoot();
        string ledger = Read(root, "Documentation", "Documentation", "ArchitectureAuthorityRefactor", "11_RF12", "00_ENTRY_STATUS_AND_ROADMAP.md");
        string migration = Read(root, "Documentation", "Documentation", "ArchitectureAuthorityRefactor", "04_CoreMigration", "04_RF07_RF13_Core_Migration.md");
        string clarification = Read(root, "Documentation", "Documentation", "ArchitectureAuthorityRefactor", "HybridCPU-v2_Ref1_Refactoring_Plan_Clarifications.md");
        string reconciliation = Read(root, "Documentation", "Documentation", "ArchitectureAuthorityRefactor", "Evidence", "RF12",
            "rf12.12b-external-audit-reconciliation-and-reopened-handoff.md");

        Assert.Contains("RF-12 is closed at RF-12.12h", ledger, StringComparison.Ordinal);
        Assert.Contains("historical chronology", migration, StringComparison.Ordinal);
        Assert.Contains("preceding RF-12.6 handoffs and this reconciliation are historical", clarification, StringComparison.Ordinal);
        Assert.Contains("RF-12.10a is", reconciliation, StringComparison.Ordinal);
        Assert.Contains("not started until this independently reversible LaneId inventory has closed", reconciliation, StringComparison.Ordinal);
    }

    [Fact]
    public void ReconciliationSeparatesMissingLaneWorkFromCurrentFailClosedResourceMasks()
    {
        string root = FindRepositoryRoot();
        string scheduler = Read(root, "HybridCPU_ISE", "CloseToHSL", "Core", "Pipeline", "Scheduling", "Admission", "MicroOpScheduler.Admission.cs");
        string masks = Read(root, "HybridCPU_ISE", "CloseToHSL", "Core", "Pipeline", "MicroOps", "Types", "MicroOp.cs");
        string reconciliation = Read(root, "Documentation", "Documentation", "ArchitectureAuthorityRefactor", "Evidence", "RF12",
            "rf12.12b-external-audit-reconciliation-and-reopened-handoff.md");

        Assert.Contains("1 << lane", scheduler, StringComparison.Ordinal);
        Assert.Contains("RequireResourceId", masks, StringComparison.Ordinal);
        Assert.Contains("RF-12.5d: LaneId and pinning closed-world", reconciliation, StringComparison.Ordinal);
        Assert.Contains("rejects out-of-range", reconciliation, StringComparison.Ordinal);
    }

    private static string Read(string root, params string[] path) =>
        File.ReadAllText(Path.Combine(new[] { root }.Concat(path).ToArray()));

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
