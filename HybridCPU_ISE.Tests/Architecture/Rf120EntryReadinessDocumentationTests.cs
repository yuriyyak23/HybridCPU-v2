namespace HybridCPU_ISE.Tests.Architecture;

public sealed class Rf120EntryReadinessDocumentationTests
{
    [Fact]
    public void CurrentLedgerOwnsTheSingleRf12Handoff()
    {
        string root = FindRepositoryRoot();
        string ledger = Read(root, "Documentation", "ArchitectureAuthorityRefactor", "11_RF12",
            "00_ENTRY_STATUS_AND_ROADMAP.md");
        string roadmap = Read(root, "Documentation", "ArchitectureAuthorityRefactor", "04_CoreMigration",
            "04_RF07_RF13_Core_Migration.md");

        Assert.Contains("RF-12 is closed at RF-12.12h", ledger, StringComparison.Ordinal);
        Assert.Contains("RF-12.12 | superseded exit audit", ledger,
            StringComparison.Ordinal);
        Assert.Contains("RF-12.5d | closed inventory/freeze", ledger, StringComparison.Ordinal);
        Assert.Contains("RF-12.5e | closed architecture decision", ledger, StringComparison.Ordinal);
        Assert.Contains("RF-12.5f | closed core valid-input contract", ledger, StringComparison.Ordinal);
        Assert.Contains("RF-12.5g | closed valid-input signature parity", ledger, StringComparison.Ordinal);
        Assert.Contains("RF-12.5h | closed invalid-input behavior", ledger, StringComparison.Ordinal);
        Assert.Contains("RF-12.5i | closed raw-pinning compatibility/removal eligibility inventory", ledger, StringComparison.Ordinal);
        Assert.Contains("RF-12.5j | closed raw-pinning compatibility retention decision", ledger, StringComparison.Ordinal);
        Assert.Contains("RF-12.0 | closed inventory/freeze", ledger, StringComparison.Ordinal);
        Assert.Contains("RF-12.1 | closed architecture decision", ledger, StringComparison.Ordinal);
        Assert.Contains("RF-12.2a | closed valid-input contract", ledger, StringComparison.Ordinal);
        Assert.Contains("RF-12.2b | closed valid-input contract", ledger, StringComparison.Ordinal);
        Assert.Contains("RF-12.2c | closed valid-input contract", ledger, StringComparison.Ordinal);
        Assert.Contains("RF-12.3a | closed valid-input contract", ledger, StringComparison.Ordinal);
        Assert.Contains("RF-12.3b | closed valid-input caller cutover", ledger,
            StringComparison.Ordinal);
        Assert.Contains("RF-12.3c | closed architecture decision", ledger,
            StringComparison.Ordinal);
        Assert.Contains("RF-12.3d | closed invalid-input behavior", ledger,
            StringComparison.Ordinal);
        Assert.Contains("RF-12.3e | closed invalid-input behavior", ledger,
            StringComparison.Ordinal);
        Assert.Contains("RF-12.3f | closed compatibility removal", ledger,
            StringComparison.Ordinal);
        Assert.Contains("RF-12.3g | closed architecture decision", ledger,
            StringComparison.Ordinal);
        Assert.Contains("RF-12.3h | closed valid-input caller cutover", ledger,
            StringComparison.Ordinal);
        Assert.Contains("RF-12.3i | closed architecture decision", ledger,
            StringComparison.Ordinal);
        Assert.Contains("RF-12.3j | closed valid-input caller cutover", ledger,
            StringComparison.Ordinal);
        Assert.Contains("RF-12.3k | closed architecture decision", ledger,
            StringComparison.Ordinal);
        Assert.Contains("RF-12.3l | closed valid-input caller cutover", ledger,
            StringComparison.Ordinal);
        Assert.Contains("RF-12.3m | closed architecture decision", ledger,
            StringComparison.Ordinal);
        Assert.Contains("RF-12.3n | closed valid-input caller cutover", ledger,
            StringComparison.Ordinal);
        Assert.Contains("RF-12.3o | closed architecture decision", ledger,
            StringComparison.Ordinal);
        Assert.Contains("RF-12.3p | closed valid-input caller cutover", ledger,
            StringComparison.Ordinal);
        Assert.Contains("RF-12.3q | closed architecture decision", ledger,
            StringComparison.Ordinal);
        Assert.Contains("RF-12.3r | closed valid-input caller cutover", ledger,
            StringComparison.Ordinal);
        Assert.Contains("RF-12.3s | closed architecture decision", ledger,
            StringComparison.Ordinal);
        Assert.Contains("RF-12.3t | closed valid-input caller cutover", ledger,
            StringComparison.Ordinal);
        Assert.Contains("RF-12.3u | closed architecture decision", ledger,
            StringComparison.Ordinal);
        Assert.Contains("RF-12.3v | closed valid-input caller cutover", ledger,
            StringComparison.Ordinal);
        Assert.Contains("RF-12.3w | closed architecture decision", ledger,
            StringComparison.Ordinal);
        Assert.Contains("RF-12.3x | closed valid-input caller cutover", ledger,
            StringComparison.Ordinal);
        Assert.Contains("RF-12.3y | closed architecture decision", ledger,
            StringComparison.Ordinal);
        Assert.Contains("RF-12.3z | closed valid-input caller cutover", ledger,
            StringComparison.Ordinal);
        Assert.Contains("RF-12.3aa | closed architecture decision", ledger,
            StringComparison.Ordinal);
        Assert.Contains("RF-12.3ab | closed valid-input caller cutover", ledger,
            StringComparison.Ordinal);
        Assert.Contains("RF-12.3ac | closed architecture decision", ledger,
            StringComparison.Ordinal);
        Assert.Contains("RF-12.3ad | closed valid-input caller cutover", ledger,
            StringComparison.Ordinal);
        Assert.Contains("RF-12.3ae | closed architecture decision", ledger,
            StringComparison.Ordinal);
        Assert.Contains("RF-12.3af | closed valid-input caller cutover", ledger,
            StringComparison.Ordinal);
        Assert.Contains("RF-12.3ag | closed architecture decision", ledger,
            StringComparison.Ordinal);
        Assert.Contains("RF-12.3ah | closed valid-input caller cutover", ledger,
            StringComparison.Ordinal);
        Assert.Contains("RF-12.3ai | closed architecture decision", ledger,
            StringComparison.Ordinal);
        Assert.Contains("RF-12.3aj | closed valid-input caller cutover", ledger,
            StringComparison.Ordinal);
        Assert.Contains("RF-12.3ak | closed caller closure audit", ledger,
            StringComparison.Ordinal);
        Assert.Contains("RF-12.4 | closed inventory/freeze", ledger,
            StringComparison.Ordinal);
        Assert.Contains("RF-12.4a | closed architecture decision", ledger,
            StringComparison.Ordinal);
        Assert.Contains("RF-12.4b | closed valid-input contract", ledger,
            StringComparison.Ordinal);
        Assert.Contains("RF-12.4c | closed architecture decision", ledger,
            StringComparison.Ordinal);
        Assert.Contains("RF-12.4d | closed valid-input caller cutover", ledger,
            StringComparison.Ordinal);
        Assert.Contains("RF-12.4e | closed architecture decision", ledger,
            StringComparison.Ordinal);
        Assert.Contains("RF-12.4f | closed valid-input caller cutover", ledger,
            StringComparison.Ordinal);
        Assert.Contains("RF-12.4g | closed architecture decision", ledger,
            StringComparison.Ordinal);
        Assert.Contains("RF-12.4h | closed valid-input caller cutover", ledger,
            StringComparison.Ordinal);
        Assert.Contains("RF-12.4i | closed architecture decision", ledger,
            StringComparison.Ordinal);
        Assert.Contains("RF-12.4j | closed valid-input caller cutover", ledger,
            StringComparison.Ordinal);
        Assert.Contains("RF-12.4k | closed architecture decision", ledger,
            StringComparison.Ordinal);
        Assert.Contains("RF-12.4l | closed valid-input caller cutover", ledger,
            StringComparison.Ordinal);
        Assert.Contains("RF-12.4m | closed architecture decision", ledger,
            StringComparison.Ordinal);
        Assert.Contains("RF-12.4n | closed valid-input caller cutover", ledger,
            StringComparison.Ordinal);
        Assert.Contains("RF-12.4o | closed architecture decision", ledger,
            StringComparison.Ordinal);
        Assert.Contains("RF-12.4p | closed valid-input caller cutover", ledger,
            StringComparison.Ordinal);
        Assert.Contains("RF-12.5 | closed inventory/freeze", ledger,
            StringComparison.Ordinal);
        Assert.Contains("RF-12.5a | closed valid-input contract", ledger,
            StringComparison.Ordinal);
        Assert.Contains("RF-12.5b | closed architecture decision", ledger,
            StringComparison.Ordinal);
        Assert.Contains("RF-12.6a | closed architecture decision", ledger,
            StringComparison.Ordinal);
        Assert.Contains("RF-12.6b | closed valid-input contract", ledger,
            StringComparison.Ordinal);
        Assert.Contains("RF-12.6c | closed architecture decision", ledger,
            StringComparison.Ordinal);
        Assert.Contains("RF-12.6d | closed valid-input caller cutover", ledger,
            StringComparison.Ordinal);
        Assert.Contains("RF-12.12b | closed reconciliation", ledger,
            StringComparison.Ordinal);
        Assert.Contains("source-provenance slot consumer", roadmap,
            StringComparison.Ordinal);
        Assert.Contains("RF-12.5", roadmap,
            StringComparison.Ordinal);
        Assert.DoesNotContain("ids: add BankId, LaneId, ChannelId and DomainId contracts",
            roadmap, StringComparison.Ordinal);
    }

    [Fact]
    public void EntryContractForbidsGenericTaxonomyAndAuthorityInflation()
    {
        string root = FindRepositoryRoot();
        string ledger = Read(root, "Documentation", "ArchitectureAuthorityRefactor", "11_RF12",
            "00_ENTRY_STATUS_AND_ROADMAP.md");
        string proposal = Read(root, "Documentation", "ArchitectureAuthorityRefactor", "02_Authority",
            "02_Target_Architecture_and_Authority.md");

        Assert.Contains("RF-12.0 changes no production declaration", ledger,
            StringComparison.Ordinal);
        Assert.Contains("introducing `VirtualThreadId` alongside the existing `VtId`", ledger,
            StringComparison.Ordinal);
        Assert.Contains("universal `ChannelId`, `DomainId` or `TokenId`", ledger,
            StringComparison.Ordinal);
        Assert.Contains("Checked identifiers provide representational validation only", ledger,
            StringComparison.Ordinal);
        Assert.Contains("RF-12 is closed at RF-12.12h", ledger,
            StringComparison.Ordinal);
        Assert.Contains("implementation proposal, not paper authority", proposal,
            StringComparison.Ordinal);
    }

    [Fact]
    public void HistoricalContinuationPromptIsNotTheCurrentRf12Queue()
    {
        string root = FindRepositoryRoot();
        string prompt = Read(root, "Documentation", "ArchitectureAuthorityRefactor", "11_RF12",
            "01_RF12_CONTINUATION_PROMPT.md");
        Assert.Contains("RF-12.0 inventory, RF-12.1 architecture", prompt, StringComparison.Ordinal);
        Assert.Contains("Open only RF-12.6at", prompt,
            StringComparison.Ordinal);
        string ledger = Read(root, "Documentation", "ArchitectureAuthorityRefactor", "11_RF12",
            "00_ENTRY_STATUS_AND_ROADMAP.md");
        Assert.Contains("RF-12.12b", ledger, StringComparison.Ordinal);
        Assert.Contains("RF-12 is closed at RF-12.12h", ledger, StringComparison.Ordinal);
        string evidence = Read(root, "Documentation", "ArchitectureAuthorityRefactor", "Evidence", "RF12",
            "rf12-entry-readiness-and-testassembler-comparison.md");

        Assert.Contains("Начать только с RF-12.0", prompt, StringComparison.Ordinal);
        Assert.Contains("--iterations 200 --minimal-logs", prompt, StringComparison.Ordinal);
        Assert.Contains("без extended telemetry и полной", prompt, StringComparison.Ordinal);
        Assert.Contains("twelve child artifacts", evidence, StringComparison.Ordinal);
        Assert.Contains("no new post-RF11 TestAssembler regression", evidence,
            StringComparison.Ordinal);
        Assert.Contains("there is not timing/counter parity", evidence,
            StringComparison.Ordinal);
    }

    private static string Read(string root, params string[] components) =>
        File.ReadAllText(components.Aggregate(root, Path.Combine));

    private static string FindRepositoryRoot()
    {
        string? current = AppContext.BaseDirectory;
        while (current is not null)
        {
            if (File.Exists(Path.Combine(current, "HybridCPU v2.slnx")))
            {
                return current;
            }

            current = Directory.GetParent(current)?.FullName;
        }

        throw new DirectoryNotFoundException("HybridCPU repository root was not found.");
    }
}
