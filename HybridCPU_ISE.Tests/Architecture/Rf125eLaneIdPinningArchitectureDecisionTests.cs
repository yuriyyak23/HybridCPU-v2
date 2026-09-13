namespace HybridCPU_ISE.Tests.Architecture;

public sealed class Rf125eLaneIdPinningArchitectureDecisionTests
{
    [Fact]
    public void PaperMakesStageBTheInvalidHardPinOwnerBeforeAnyLaneShift()
    {
        string root = FindRepositoryRoot();
        string paper = Read(root, "ResearchPaper", "section", "md base", "3_Architectural_Overview_and_Frontend_Contract.md");
        string scheduler = Read(root, "HybridCPU_ISE", "CloseToHSL", "Core", "Pipeline", "Scheduling", "Admission", "MicroOpScheduler.Admission.cs");

        Assert.Contains("`Flexible` carries no lane and `HardPinned` carries exactly one `LaneId`", paper, StringComparison.Ordinal);
        Assert.Contains("distinct `InvalidPinnedLane` rejection before any lane", paper, StringComparison.Ordinal);
        Assert.Contains("does not create an issued attempt, execution, completion, replay state", paper, StringComparison.Ordinal);
        Assert.Contains("1 << lane", scheduler, StringComparison.Ordinal);
    }

    [Fact]
    public void DecisionPreservesRawCompatibilityUntilTheNextValidInputSlice()
    {
        string root = FindRepositoryRoot();
        string ledger = Read(root, "Documentation", "Documentation", "ArchitectureAuthorityRefactor", "11_RF12", "00_ENTRY_STATUS_AND_ROADMAP.md");
        string evidence = Read(root, "Documentation", "Documentation", "ArchitectureAuthorityRefactor", "Evidence", "RF12", "rf12.5e-laneid-pinning-architecture-decision.md");

        Assert.Contains("RF-12.5e | closed architecture decision", ledger, StringComparison.Ordinal);
        Assert.Contains("RF-12.5h | closed invalid-input behavior", ledger, StringComparison.Ordinal);
        Assert.Contains("RF-12 is closed at RF-12.12h", ledger, StringComparison.Ordinal);
        Assert.Contains("RF-12.5f core valid-input contract", evidence, StringComparison.Ordinal);
        Assert.Contains("No clamp, modulo, zero substitution", evidence, StringComparison.Ordinal);
        Assert.Contains("Production/runtime change: none", evidence, StringComparison.Ordinal);
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
