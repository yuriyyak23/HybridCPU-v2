namespace HybridCPU_ISE.Tests.Architecture;

/// <summary>
/// RF-08.4ai freezes the architecture-owner C-C decision for the closed
/// two-contour VmxCommit family. It authorizes no production change.
/// </summary>
public sealed class Rf084aiVmxCommitApprovedResidualExclusionTests
{
    [Fact]
    public void PaperApprovesExactMainlineAndDirectContoursAtRf08Exit()
    {
        string paper = ReadPaper();

        Assert.Contains(
            "RF-08.4ai approved `VmxCommit` C-C residual exclusion",
            paper,
            StringComparison.Ordinal);
        Assert.Contains(
            "lane-7 `VmxMicroOp`, its mutable resolved retire effect, generated VMX",
            paper,
            StringComparison.Ordinal);
        Assert.Contains(
            "`CreateRemovedFrontendFaultEffect` and `CaptureRetireWindowVmxEffect` without",
            paper,
            StringComparison.Ordinal);
        Assert.Contains(
            "admissible at RF-08 exit",
            paper,
            StringComparison.Ordinal);
        Assert.Contains(
            "reviewed only by a separate architecture revision",
            paper,
            StringComparison.Ordinal);
    }

    [Fact]
    public void DecisionKeepsDerivedFamiliesSeparateAndForbidsReconstruction()
    {
        string paper = ReadPaper();

        Assert.Contains(
            "derived VMX `RegisterWrite` outcome adapter and the separately approved",
            paper,
            StringComparison.Ordinal);
        Assert.Contains(
            "Identity must not be",
            paper,
            StringComparison.Ordinal);
        Assert.Contains(
            "reconstructed from opcode, lane, VT, trace, payload, VMCS data, function, root",
            paper,
            StringComparison.Ordinal);
        Assert.Contains(
            "authorizes no scheduler or Stage-A/B change",
            paper,
            StringComparison.Ordinal);
        Assert.Contains(
            "provide differential VMX evidence",
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
        for (DirectoryInfo? current = new(AppContext.BaseDirectory);
             current is not null;
             current = current.Parent)
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
