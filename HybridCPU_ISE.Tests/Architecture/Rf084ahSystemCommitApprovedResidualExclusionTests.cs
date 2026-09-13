namespace HybridCPU_ISE.Tests.Architecture;

/// <summary>
/// RF-08.4ah freezes the architecture-owner C-C decision for the closed
/// mainline typed-System contour. It authorizes no production change.
/// </summary>
public sealed class Rf084ahSystemCommitApprovedResidualExclusionTests
{
    [Fact]
    public void PaperApprovesExactTypedSystemProducerEnvelopeAtRf08Exit()
    {
        string paper = ReadPaper();

        Assert.Contains(
            "RF-08.4ah approved `SystemCommit` C-C residual exclusion",
            paper,
            StringComparison.Ordinal);
        Assert.Contains(
            "`Fence`, `FenceI`, `Ecall`, `Ebreak`, `Mret`, `Sret`, `Wfi`, `Wfe`, `Sev`,",
            paper,
            StringComparison.Ordinal);
        Assert.Contains(
            "`CaptureGeneratedSystemEvent`; `RetireWindowEffectKind.System`",
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
    public void DecisionKeepsPipelineEventFamilySeparateAndForbidsReconstruction()
    {
        string paper = ReadPaper();

        Assert.Contains(
            "remains the separate",
            paper,
            StringComparison.Ordinal);
        Assert.Contains(
            "`PipelineEventPublication` family",
            paper,
            StringComparison.Ordinal);
        Assert.Contains(
            "Identity must not be",
            paper,
            StringComparison.Ordinal);
        Assert.Contains(
            "reconstructed from opcode, lane 7, event type or kind, order guarantee, PC,",
            paper,
            StringComparison.Ordinal);
        Assert.Contains(
            "authorizes no scheduler or Stage-A/B change",
            paper,
            StringComparison.Ordinal);
        Assert.Contains(
            "preserve the `PipelineEventPublication` family boundary",
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
