namespace HybridCPU_ISE.Tests.Architecture;

public sealed class Rf084aoPredicateStateWriteApprovedResidualExclusionTests
{
    [Fact]
    public void PaperApprovesExactSplitTopologyWithoutBoundedProductionClaim()
    {
        string paper = ReadPaper();

        Assert.Contains(
            "RF-08.4ao approved `PredicateStateWrite` C-C residual exclusion",
            paper,
            StringComparison.Ordinal);
        Assert.Contains("exact current split", paper, StringComparison.Ordinal);
        Assert.Contains("retain eager `SetPredicateRegister` mutation during Execute", paper, StringComparison.Ordinal);
        Assert.Contains("current core callers are explicitly test", paper, StringComparison.Ordinal);
        Assert.Contains("does not claim that", paper, StringComparison.Ordinal);
        Assert.Contains("production predicate publication is bounded", paper, StringComparison.Ordinal);
        Assert.Contains("admissible at RF-08 exit", paper, StringComparison.Ordinal);
    }

    [Fact]
    public void DecisionPreservesOwnersAndForbidsIdentityReconstruction()
    {
        string paper = ReadPaper();

        Assert.Contains("`VectorStreamDirty` remains a separate family", paper, StringComparison.Ordinal);
        Assert.Contains("Identity must not be reconstructed from", paper, StringComparison.Ordinal);
        Assert.Contains("eager-to-retire owner transfer", paper, StringComparison.Ordinal);
        Assert.Contains("younger/fault suppression and same-instance differential", paper, StringComparison.Ordinal);
    }

    [Fact]
    public void CurrentCodeRetainsClosedWriterAndBoundedProducerSets()
    {
        string root = FindRepositoryRoot();
        string coreRoot = Path.Combine(root, "HybridCPU_ISE", "CloseToHSL", "Core");

        string[] writers = Directory.GetFiles(coreRoot, "*.cs", SearchOption.AllDirectories)
            .Where(path => File.ReadAllText(path).Contains("SetPredicateRegister(", StringComparison.Ordinal))
            .Select(path => Path.GetRelativePath(coreRoot, path).Replace('\\', '/'))
            .Where(path => path is
                "Execution/StreamEngine/Modes/StreamEngine.Execute1D.cs" or
                "Execution/StreamEngine/Modes/StreamEngine.cs" or
                "Pipeline/MicroOps/Vector/VectorMicroOps.Compute.cs")
            .OrderBy(path => path, StringComparer.Ordinal)
            .ToArray();

        Assert.Equal(
            [
                "Execution/StreamEngine/Modes/StreamEngine.Execute1D.cs",
                "Execution/StreamEngine/Modes/StreamEngine.cs",
                "Pipeline/MicroOps/Vector/VectorMicroOps.Compute.cs"
            ],
            writers);

        string[] boundedProducers = Directory.GetFiles(coreRoot, "*.cs", SearchOption.AllDirectories)
            .Where(path => File.ReadAllText(path).Contains(
                "retireBatch.CaptureRetireWindowPredicateState(",
                StringComparison.Ordinal))
            .Select(path => Path.GetRelativePath(coreRoot, path).Replace('\\', '/'))
            .ToArray();

        Assert.Equal(["Execution/StreamEngine/Modes/StreamEngine.Execute1D.cs"], boundedProducers);
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
