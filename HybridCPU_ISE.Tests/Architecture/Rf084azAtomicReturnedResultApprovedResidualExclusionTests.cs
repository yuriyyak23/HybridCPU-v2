namespace HybridCPU_ISE.Tests.Architecture;

public sealed class Rf084azAtomicReturnedResultApprovedResidualExclusionTests
{
    [Fact]
    public void PaperApprovesOnlyTheDecisionReadyAtomicResultEnvelope()
    {
        string paper = ReadPaper();

        Assert.Contains("RF-08.4az approved atomic returned-result", paper, StringComparison.Ordinal);
        Assert.Contains("exact 22 published word/doubleword LR, SC and AMO opcodes", paper, StringComparison.Ordinal);
        Assert.Contains("does not absorb", paper, StringComparison.Ordinal);
        Assert.Contains("`AtomicCommit`", paper, StringComparison.Ordinal);
        Assert.Contains("separate architecture revision", paper, StringComparison.Ordinal);
    }

    [Fact]
    public void ResultRemainsPostApplyAndSelectedRetireOwned()
    {
        string root = FindRepositoryRoot();
        string retire = Read(root, "HybridCPU_ISE", "CloseToHSL", "Core", "Pipeline",
            "Retire", "Evidence", "CPU_Core.PipelineExecution.Retire.cs");

        int prevalidate = retire.IndexOf("PrevalidateAtomicEffect(retireEffect.AtomicEffect)", StringComparison.Ordinal);
        int apply = retire.IndexOf("ApplyRetiredAtomicEffect(retireEffect.AtomicEffect)", StringComparison.Ordinal);
        int publish = retire.IndexOf("RetireRecord.RegisterWrite(", apply, StringComparison.Ordinal);
        Assert.True(prevalidate >= 0 && apply > prevalidate && publish > apply);
        int coordinator = retire.IndexOf("RetireCoordinator.Retire(", apply, StringComparison.Ordinal);
        Assert.True(coordinator > apply && coordinator < publish);
    }

    [Fact]
    public void CurrentLedgersTreatAtomicResultAsApprovedNotExact()
    {
        string root = FindRepositoryRoot();
        string ledger = Read(root, "Documentation", "Documentation", "ArchitectureAuthorityRefactor", "08_RF08_RF09",
            "03_RF08_EXIT_READINESS_LEDGER.md");
        string status = Read(root, "Documentation", "Documentation", "ArchitectureAuthorityRefactor", "08_RF08_RF09",
            "00_CURRENT_STATUS_AND_READING_ORDER.md");

        Assert.Contains("RF-08.4az", ledger, StringComparison.Ordinal);
        Assert.Contains("exit-admissible approved residual exclusion", ledger, StringComparison.Ordinal);
        Assert.Contains("RF-08.4az", status, StringComparison.Ordinal);
        Assert.DoesNotContain("RF-08.4az | complete exact", status, StringComparison.Ordinal);
    }

    private static string ReadPaper() =>
        Read(FindRepositoryRoot(), "ResearchPaper", "section", "md base",
            "5_Two_Stage_Admission_and_Bundle_Compositional_SMT_Packing.md");

    private static string Read(string root, params string[] segments) =>
        File.ReadAllText(Path.Combine(new[] { root }.Concat(segments).ToArray()));

    private static string FindRepositoryRoot()
    {
        for (DirectoryInfo? current = new(AppContext.BaseDirectory); current is not null; current = current.Parent)
        {
            if (Directory.Exists(Path.Combine(current.FullName, "HybridCPU_ISE")) &&
                Directory.Exists(Path.Combine(current.FullName, "ResearchPaper")))
                return current.FullName;
        }

        throw new DirectoryNotFoundException("Repository root was not found.");
    }
}
