namespace HybridCPU_ISE.Tests.Architecture;

/// <summary>
/// RF-08.4x freezes the architecture-owner C-C decision for the closed
/// vector-config optional-rd/VectorConfigWrite contour.
/// </summary>
public sealed class Rf084xVectorConfigCoupledApprovedResidualExclusionTests
{
    [Fact]
    public void PaperApprovesClosedCoupledContourAtRf08Exit()
    {
        string paper = ReadPaper();

        Assert.Contains(
            "RF-08.4x approved vector-config coupled C-C residual exclusion",
            paper,
            StringComparison.Ordinal);
        Assert.Contains(
            "exactly `VSETVL`, `VSETVLI` and `VSETIVLI`",
            paper,
            StringComparison.Ordinal);
        Assert.Contains(
            "and explicit-packet execution retain",
            paper,
            StringComparison.Ordinal);
        Assert.Contains(
            "surfaces remain rejected",
            paper,
            StringComparison.Ordinal);
        Assert.Contains(
            "rd=x0 suppresses only the register publication",
            paper,
            StringComparison.Ordinal);
        Assert.Contains(
            "admissible at RF-08 exit",
            paper,
            StringComparison.Ordinal);
    }

    [Fact]
    public void DecisionRetainsDistinctEffectsAndForbidsIdentityReconstruction()
    {
        string paper = ReadPaper();

        Assert.Contains(
            "distinct `RegisterWrite` effect",
            paper,
            StringComparison.Ordinal);
        Assert.Contains(
            "`VectorConfigWrite`; rd=x0",
            paper,
            StringComparison.Ordinal);
        Assert.Contains(
            "not merge the two effect kinds",
            paper,
            StringComparison.Ordinal);
        Assert.Contains(
            "reconstructed from opcode, lane 7, VT, rd, VL, VTYPE",
            paper,
            StringComparison.Ordinal);
        Assert.Contains(
            "reviewed only by a separate architecture revision",
            paper,
            StringComparison.Ordinal);
        Assert.Contains(
            "effect coupling/order contract",
            paper,
            StringComparison.Ordinal);
    }

    private static string ReadPaper()
    {
        string root = FindRepositoryRoot();
        return File.ReadAllText(Path.Combine(
            root,
            "ResearchPaper",
            "section",
            "md base",
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
