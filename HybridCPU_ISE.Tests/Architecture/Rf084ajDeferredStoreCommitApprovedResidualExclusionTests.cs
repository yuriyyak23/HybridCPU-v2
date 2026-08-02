namespace HybridCPU_ISE.Tests.Architecture;

public sealed class Rf084ajDeferredStoreCommitApprovedResidualExclusionTests
{
    [Fact]
    public void PaperApprovesExactMainlineCompletionEnvelopeAtRf08Exit()
    {
        string paper = ReadPaper();

        Assert.Contains("RF-08.4aj approved `DeferredStoreCommit` C-C residual exclusion", paper, StringComparison.Ordinal);
        Assert.Contains("single-lane synchronous scalar-store", paper, StringComparison.Ordinal);
        Assert.Contains("explicit-packet successful completed-token branch", paper, StringComparison.Ordinal);
        Assert.Contains("explicit-packet no-memory-subsystem synchronous fallback", paper, StringComparison.Ordinal);
        Assert.Contains("admissible at RF-08 exit", paper, StringComparison.Ordinal);
        Assert.Contains("reviewed only by a separate architecture revision", paper, StringComparison.Ordinal);
    }

    [Fact]
    public void DecisionKeepsMemoryFamiliesSeparateAndForbidsReconstruction()
    {
        string paper = ReadPaper();

        Assert.Contains("Direct compatibility `ScalarMemoryStoreCommit` and mainline/direct", paper, StringComparison.Ordinal);
        Assert.Contains("Identity must not be", paper, StringComparison.Ordinal);
        Assert.Contains("reconstructed from opcode, lane, VT, address, data, size, bank, request token,", paper, StringComparison.Ordinal);
        Assert.Contains("authorizes no scheduler or Stage-A/B change", paper, StringComparison.Ordinal);
        Assert.Contains("provide differential scalar-store evidence", paper, StringComparison.Ordinal);
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
