namespace HybridCPU_ISE.Tests.Architecture;

public sealed class Rf109CanonicalVectorTransferAuthorityDecisionTests
{
    [Fact]
    public void PaperDefinesOneImmutablePayloadAndExistingSelectedRetireOwner()
    {
        string root = FindRepositoryRoot();
        string paper = NormalizeWhitespace(Read(root,
            "ResearchPaper/section/md base/7_Replay_Stable_Placement_Replay_Tokens_and_Execution_Boundaries.md"));

        Assert.Contains("RF-10.9 authorizes one future immutable `VectorTransferRetireEffect`", paper, StringComparison.Ordinal);
        Assert.Contains("does not alter the selected retire subset or order", paper, StringComparison.Ordinal);
        Assert.Contains("must be prevalidated as part of the complete `RetireWindowBatch`", paper, StringComparison.Ordinal);
        Assert.Contains("`RetireCoordinator` remains the publication owner", paper, StringComparison.Ordinal);
        Assert.Contains("must not publish from `EmitWriteBackRetireRecords`", paper, StringComparison.Ordinal);
    }

    [Fact]
    public void DecisionFreezesControllerAdmissionCompletionAndCancellationLifetime()
    {
        string root = FindRepositoryRoot();
        string paper = NormalizeWhitespace(Read(root,
            "ResearchPaper/section/md base/7_Replay_Stable_Placement_Replay_Tokens_and_Execution_Boundaries.md"));

        Assert.Contains("independent finite capacity of eight", paper, StringComparison.Ordinal);
        Assert.Contains("joins the existing controller-native read FIFO", paper, StringComparison.Ordinal);
        Assert.Contains("backpressure allocates no identity", paper, StringComparison.Ordinal);
        Assert.Contains("exactly-once completion or terminal EX-flush cancellation", paper, StringComparison.Ordinal);
        Assert.Contains("never writes destination memory", paper, StringComparison.Ordinal);
    }

    [Fact]
    public void DecisionEvidenceRecordsThatItsOwnSliceDidNotChangeRuntime()
    {
        string root = FindRepositoryRoot();
        string evidence = NormalizeWhitespace(Read(root,
            "Documentation/Documentation/ArchitectureAuthorityRefactor/Evidence/RF10/rf10.9-canonical-vector-transfer-authority-decision.md"));

        Assert.Contains("No production or timing source changed", evidence, StringComparison.Ordinal);
        Assert.Contains("RF-10.10 is authorized", evidence, StringComparison.Ordinal);
    }

    [Fact]
    public void LedgerClosesDecisionOnlyAndNamesImplementationSlice()
    {
        string root = FindRepositoryRoot();
        string status = NormalizeWhitespace(Read(root,
            "Documentation/Documentation/ArchitectureAuthorityRefactor/09_RF10/00_CURRENT_STATUS_AND_LEDGER.md"));
        string evidence = NormalizeWhitespace(Read(root,
            "Documentation/Documentation/ArchitectureAuthorityRefactor/Evidence/RF10/rf10.9-canonical-vector-transfer-authority-decision.md"));

        Assert.Contains("RF-10.9 | closed architecture decision", status, StringComparison.Ordinal);
        Assert.Contains("RF-10.10 | closed", status, StringComparison.Ordinal);
        Assert.Contains("No production or timing source changed", evidence, StringComparison.Ordinal);
        Assert.Contains("RF-10.10 is authorized", evidence, StringComparison.Ordinal);
    }

    private static string Read(string root, string relativePath) =>
        File.ReadAllText(Path.Combine(root, relativePath.Replace('/', Path.DirectorySeparatorChar)));

    private static string NormalizeWhitespace(string value) =>
        string.Join(' ', value.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));

    private static string FindRepositoryRoot()
    {
        string? current = Directory.GetCurrentDirectory();
        while (current != null)
        {
            if (Directory.Exists(Path.Combine(current, "Documentation")) &&
                Directory.Exists(Path.Combine(current, "HybridCPU_ISE")) &&
                Directory.Exists(Path.Combine(current, "HybridCPU_ISE.Tests")))
            {
                return current;
            }

            current = Directory.GetParent(current)?.FullName;
        }

        throw new DirectoryNotFoundException("Could not locate repository root.");
    }
}
