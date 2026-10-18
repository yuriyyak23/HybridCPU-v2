namespace HybridCPU_ISE.Tests.Architecture;

public sealed class Rf084akScalarMemoryStoreCommitApprovedResidualExclusionTests
{
    [Fact]
    public void PaperApprovesExactRetainedCompatibilityEnvelopeAtRf08Exit()
    {
        string paper = ReadPaper();

        Assert.Contains("RF-08.4ak approved `ScalarMemoryStoreCommit` C-C residual exclusion", paper, StringComparison.Ordinal);
        Assert.Contains("`ExecutionDispatcherV4.CaptureRetireWindowPublications`", paper, StringComparison.Ordinal);
        Assert.Contains("the explicitly test-support-only core caller", paper, StringComparison.Ordinal);
        Assert.Contains("admissible at RF-08 exit", paper, StringComparison.Ordinal);
        Assert.Contains("retained compatibility limitation, not exact identity coverage", paper, StringComparison.Ordinal);
    }

    [Fact]
    public void DecisionKeepsOtherMemoryFamiliesSeparateAndForbidsReconstruction()
    {
        string paper = ReadPaper();

        Assert.Contains("Mainline scalar stores remain the separately approved `DeferredStoreCommit`", paper, StringComparison.Ordinal);
        Assert.Contains("`AtomicCommit` remains a separate retire-visible", paper, StringComparison.Ordinal);
        Assert.Contains("Identity must not be", paper, StringComparison.Ordinal);
        Assert.Contains("reconstructed from opcode, address, data, size, effective-address resolution,", paper, StringComparison.Ordinal);
        Assert.Contains("authorizes no scheduler or Stage-A/B change", paper, StringComparison.Ordinal);
        Assert.Contains("contour production-reachable", paper, StringComparison.Ordinal);
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
