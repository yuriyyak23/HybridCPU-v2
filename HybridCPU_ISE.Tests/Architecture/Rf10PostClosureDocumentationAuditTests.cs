namespace HybridCPU_ISE.Tests.Architecture;

public sealed class Rf10PostClosureDocumentationAuditTests
{
    [Fact]
    public void CurrentLedgerIsClosedCurrentStateWithoutStaleOpenNarrative()
    {
        string root = FindRepositoryRoot();
        string status = Read(root, "Documentation/ArchitectureAuthorityRefactor/09_RF10/00_CURRENT_STATUS_AND_LEDGER.md");

        Assert.Contains("This file is the current-state index", status, StringComparison.Ordinal);
        Assert.Contains("RF-10 overall | closed", status, StringComparison.Ordinal);
        Assert.Contains("RF-11 | closed", status, StringComparison.Ordinal);
        Assert.Contains("../10_RF11/00_CURRENT_STATUS_AND_LEDGER.md", status, StringComparison.Ordinal);
        Assert.DoesNotContain("RF-10 remains open", status, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("вЂ", status, StringComparison.Ordinal);
    }

    [Fact]
    public void AllSliceEvidenceAndPostClosureAuditAreIndexed()
    {
        string root = FindRepositoryRoot();
        string evidenceDirectory = Path.Combine(root, "Documentation", "ArchitectureAuthorityRefactor", "Evidence", "RF10");
        string status = Read(root, "Documentation/ArchitectureAuthorityRefactor/09_RF10/00_CURRENT_STATUS_AND_LEDGER.md");

        for (int slice = 0; slice <= 14; slice++)
        {
            Assert.Contains(
                Directory.EnumerateFiles(evidenceDirectory, $"rf10.{slice}-*.md"),
                static path => File.Exists(path));
        }

        Assert.True(File.Exists(Path.Combine(
            evidenceDirectory,
            "rf10-post-closure-completeness-and-diagnostic-audit.md")));
        Assert.Contains("rf10-post-closure-completeness-and-diagnostic-audit.md", status, StringComparison.Ordinal);
    }

    [Fact]
    public void DiagnosticAuditRecordsPassAndNonParityWithoutPerformanceClaim()
    {
        string root = FindRepositoryRoot();
        string audit = Read(root,
            "Documentation/ArchitectureAuthorityRefactor/Evidence/RF10/rf10-post-closure-completeness-and-diagnostic-audit.md");

        Assert.Contains("20260727_084502_345_matrix", audit, StringComparison.Ordinal);
        Assert.Contains("passes 12/12", audit, StringComparison.Ordinal);
        Assert.Contains("timing regression", audit, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("telemetry continuity gap", audit, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("must not claim", audit, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void SupersededRf11PromptPreservesHistoricalEntryConstraints()
    {
        string root = FindRepositoryRoot();
        string prompt = Read(root, "Documentation/ArchitectureAuthorityRefactor/09_RF10/01_RF11_CONTINUATION_PROMPT.md");

        Assert.StartsWith("# Superseded RF-11 entry prompt", prompt, StringComparison.Ordinal);
        Assert.Contains("RF-11.0 entry", prompt, StringComparison.Ordinal);
        Assert.Contains("не переносить runtime state в этом slice", prompt, StringComparison.Ordinal);
        Assert.Contains("PhysicalRegisterFile", prompt, StringComparison.Ordinal);
        Assert.Contains("RetireCoordinator", prompt, StringComparison.Ordinal);
        Assert.Contains("не исправлять их попутно в RF-11", prompt, StringComparison.Ordinal);
    }

    private static string Read(string root, string relativePath) =>
        File.ReadAllText(Path.Combine(root, relativePath.Replace('/', Path.DirectorySeparatorChar)));

    private static string FindRepositoryRoot()
    {
        DirectoryInfo? current = new(AppContext.BaseDirectory);
        while (current is not null)
        {
            if (File.Exists(Path.Combine(current.FullName, "HybridCPU_ISE.sln")) ||
                Directory.Exists(Path.Combine(current.FullName, "Documentation", "ArchitectureAuthorityRefactor")))
            {
                return current.FullName;
            }

            current = current.Parent;
        }

        throw new DirectoryNotFoundException("Unable to locate HybridCPU ISE repository root.");
    }
}
