namespace HybridCPU_ISE.Tests.Architecture;

public sealed class Rf084alAtomicCommitApprovedResidualExclusionTests
{
    [Fact]
    public void PaperApprovesExactMainlineAndDirectAtomicEnvelopeAtRf08Exit()
    {
        string paper = ReadPaper();

        Assert.Contains("RF-08.4al approved `AtomicCommit` C-C residual exclusion", paper, StringComparison.Ordinal);
        Assert.Contains("mutable `AtomicMicroOp` resolved effect, generated atomic EX/MEM/WB", paper, StringComparison.Ordinal);
        Assert.Contains("`CaptureRetireWindowAtomicEffect` without the mainline trace", paper, StringComparison.Ordinal);
        Assert.Contains("admissible at RF-08 exit", paper, StringComparison.Ordinal);
        Assert.Contains("reviewed only by a separate architecture revision", paper, StringComparison.Ordinal);
    }

    [Fact]
    public void DecisionKeepsReturnedWriteAndStoreFamiliesSeparate()
    {
        string paper = ReadPaper();

        Assert.Contains("published as a separate", paper, StringComparison.Ordinal);
        Assert.Contains("`RegisterWrite` retire record", paper, StringComparison.Ordinal);
        Assert.Contains("`DeferredStoreCommit` and `ScalarMemoryStoreCommit` also remain separate", paper, StringComparison.Ordinal);
        Assert.Contains("Identity must not be", paper, StringComparison.Ordinal);
        Assert.Contains("reconstructed from opcode, lane, slot, core, VT, address, source value, access", paper, StringComparison.Ordinal);
        Assert.Contains("provide differential atomic evidence", paper, StringComparison.Ordinal);
    }

    private static string ReadPaper()
    {
        string root = FindRepositoryRoot();
        return File.ReadAllText(Path.Combine(root, "ResearchPaper", "section", "md base",
            "5_Two_Stage_Admission_and_Bundle_Compositional_SMT_Packing.md"));
    }

    private static string FindRepositoryRoot()
    {
        for (DirectoryInfo? current = new(AppContext.BaseDirectory); current is not null; current = current.Parent)
        {
            if (Directory.Exists(Path.Combine(current.FullName, "HybridCPU_ISE")) &&
                Directory.Exists(Path.Combine(current.FullName, "ResearchPaper")))
            {
                return current.FullName;
            }
        }

        throw new DirectoryNotFoundException("Repository root was not found.");
    }
}
