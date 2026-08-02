namespace HybridCPU_ISE.Tests.Architecture;

/// <summary>
/// RF-08.4r freezes the architecture-owner C-C decision for the closed
/// MatrixTileCommit independent protocol. It authorizes no production change.
/// </summary>
public sealed class Rf084rMatrixTileCommitApprovedResidualExclusionTests
{
    [Fact]
    public void PaperApprovesClosedContourAtRf08ExitWithSeparateRevisionExpiry()
    {
        string root = FindRepositoryRoot();
        string paper = File.ReadAllText(Path.Combine(
            root,
            "ResearchPaper",
            "section",
            "md base",
            "5_Two_Stage_Admission_and_Bundle_Compositional_SMT_Packing.md"));

        Assert.Contains(
            "RF-08.4r approved `MatrixTileCommit` C-C residual exclusion",
            paper,
            StringComparison.Ordinal);
        Assert.Contains(
            "may remain as approved residual",
            paper,
            StringComparison.Ordinal);
        Assert.Contains(
            "`RetireWindowEffect` union",
            paper,
            StringComparison.Ordinal);
        Assert.Contains(
            "before common retire-record/effect prevalidation",
            paper,
            StringComparison.Ordinal);
        Assert.Contains(
            "reviewed only by a separate architecture revision",
            paper,
            StringComparison.Ordinal);
        Assert.Contains(
            "| separate architecture revision |",
            paper,
            StringComparison.Ordinal);
    }

    [Fact]
    public void DecisionForbidsIdentityReconstructionAndOwnerMigration()
    {
        string root = FindRepositoryRoot();
        string paper = File.ReadAllText(Path.Combine(
            root,
            "ResearchPaper",
            "section",
            "md base",
            "5_Two_Stage_Admission_and_Bundle_Compositional_SMT_Packing.md"));

        Assert.Contains(
            "must not reconstruct",
            paper,
            StringComparison.Ordinal);
        Assert.Contains(
            "identity from core, owner VT, opcode, operation kind, lane 6, capture ordinal",
            paper,
            StringComparison.Ordinal);
        Assert.Contains(
            "must not change MatrixTile publication, checkpoint,",
            paper,
            StringComparison.Ordinal);
        Assert.Contains(
            "cross-family",
            paper,
            StringComparison.Ordinal);
        Assert.Contains(
            "order/fault contract and differential evidence",
            paper,
            StringComparison.Ordinal);
        Assert.Contains(
            "must be enumerated there as a limitation",
            paper,
            StringComparison.Ordinal);
    }

    private static string FindRepositoryRoot()
    {
        DirectoryInfo? directory = new(AppContext.BaseDirectory);
        while (directory != null)
        {
            if (Directory.Exists(Path.Combine(directory.FullName, "ResearchPaper")) &&
                Directory.Exists(Path.Combine(directory.FullName, "HybridCPU_ISE")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException("Repository root not found.");
    }
}
