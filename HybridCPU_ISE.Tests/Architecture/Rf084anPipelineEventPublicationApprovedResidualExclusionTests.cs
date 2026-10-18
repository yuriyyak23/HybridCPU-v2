namespace HybridCPU_ISE.Tests.Architecture;

public sealed class Rf084anPipelineEventPublicationApprovedResidualExclusionTests
{
    [Fact]
    public void PaperApprovesExactClosedProducerGroup()
    {
        string paper = ReadPaper();

        Assert.Contains(
            "RF-08.4an approved `PipelineEventPublication` C-C residual exclusion",
            paper,
            StringComparison.Ordinal);
        Assert.Contains("mainline contour is exactly `TrapMicroOp`/`TrapEntryEvent`", paper, StringComparison.Ordinal);
        Assert.Contains("System and SmtVt dispatcher", paper, StringComparison.Ordinal);
        Assert.Contains("explicitly test-support core caller", paper, StringComparison.Ordinal);
        Assert.Contains("admissible at RF-08 exit", paper, StringComparison.Ordinal);
    }

    [Fact]
    public void DecisionPreservesLateOwnerAndFamilyBoundaries()
    {
        string paper = ReadPaper();

        Assert.Contains("selected-retire boundary plus pipeline-FSM application", paper, StringComparison.Ordinal);
        Assert.Contains("`SystemCommit` family", paper, StringComparison.Ordinal);
        Assert.Contains("`TrapCommit` family", paper, StringComparison.Ordinal);
        Assert.Contains("neither merges those families", paper, StringComparison.Ordinal);
        Assert.Contains("Identity must not be", paper, StringComparison.Ordinal);
        Assert.Contains("event-order, flush/invalidation, per-VT FSM and failure evidence", paper, StringComparison.Ordinal);
    }

    [Fact]
    public void CurrentCodeStillHasExactAuditedProducerAndCallerSet()
    {
        string root = FindRepositoryRoot();
        string coreRoot = Path.Combine(root, "HybridCPU_ISE", "CloseToHSL", "Core");

        string[] producers = Directory.GetFiles(coreRoot, "*.cs", SearchOption.AllDirectories)
            .Where(path => File.ReadAllText(path).Contains(
                "retireBatch.CaptureRetireWindowPipelineEvent(",
                StringComparison.Ordinal))
            .Select(path => Path.GetRelativePath(coreRoot, path).Replace('\\', '/'))
            .OrderBy(path => path, StringComparer.Ordinal)
            .ToArray();

        Assert.Equal(
            [
                "Execution/Dispatch/ExecutionDispatcherV4.CsrAndSmtVt.cs",
                "Execution/Dispatch/ExecutionDispatcherV4.System.cs"
            ],
            producers);

        string[] callers = Directory.GetFiles(coreRoot, "*.cs", SearchOption.AllDirectories)
            .Where(path => File.ReadAllText(path).Contains(
                "dispatcher.CaptureRetireWindowPublications(",
                StringComparison.Ordinal))
            .Select(path => Path.GetRelativePath(coreRoot, path).Replace('\\', '/'))
            .ToArray();

        Assert.Equal(["Pipeline/Core/CPU_Core.TestSupport.cs"], callers);
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
