namespace HybridCPU_ISE.Tests.Architecture;

public sealed class Rf084apVectorStreamDirtyApprovedResidualExclusionTests
{
    [Fact]
    public void PaperApprovesExactMarkerEagerSplitWithoutPublicationClaim()
    {
        string paper = ReadPaper();

        Assert.Contains(
            "RF-08.4ap approved `VectorStreamDirty` marker/eager split C-C residual exclusion",
            paper,
            StringComparison.Ordinal);
        Assert.Contains("selected application remains a no-op", paper, StringComparison.Ordinal);
        Assert.Contains("not an architectural dirty-state", paper, StringComparison.Ordinal);
        Assert.Contains("publication, is not proof", paper, StringComparison.Ordinal);
        Assert.Contains("not proof of bounded dirty-state coverage", paper, StringComparison.Ordinal);
        Assert.Contains("admissible at RF-08 exit", paper, StringComparison.Ordinal);
    }

    [Fact]
    public void DecisionPreservesOwnersAndForbidsReconstruction()
    {
        string paper = ReadPaper();

        Assert.Contains("actual architectural dirty-state owner remains eager", paper, StringComparison.Ordinal);
        Assert.Contains("Identity must not be reconstructed from opcode", paper, StringComparison.Ordinal);
        Assert.Contains("WB classifier or", paper, StringComparison.Ordinal);
        Assert.Contains("marker-policy change", paper, StringComparison.Ordinal);
        Assert.Contains("differential opcode classification, per-VT", paper, StringComparison.Ordinal);
    }

    [Fact]
    public void CurrentCodeRetainsClosedProducerAndOwnerTopology()
    {
        string root = FindRepositoryRoot();
        string coreRoot = Path.Combine(root, "HybridCPU_ISE", "CloseToHSL", "Core");
        string[] boundedProducers = Directory.GetFiles(coreRoot, "*.cs", SearchOption.AllDirectories)
            .Where(path => File.ReadAllText(path).Contains(
                "retireBatch.CaptureRetireWindowVectorStreamDirty(",
                StringComparison.Ordinal))
            .Select(path => Path.GetRelativePath(coreRoot, path).Replace('\\', '/'))
            .OrderBy(path => path, StringComparer.Ordinal)
            .ToArray();

        Assert.Equal(
            [
                "Execution/StreamEngine/Modes/StreamEngine.Execute1D.cs",
                "Pipeline/Retire/Evidence/CPU_Core.PipelineExecution.Retire.cs"
            ],
            boundedProducers);

        string retire = File.ReadAllText(Path.Combine(coreRoot, "Pipeline", "Retire", "Evidence",
            "CPU_Core.PipelineExecution.Retire.cs"));
        string vectorAlu = File.ReadAllText(Path.Combine(coreRoot, "Execution", "Vector", "ALU", "VectorALU.cs"));
        Assert.Contains("if (IsVectorStreamDirtyRetireOpcode(lane.OpCode))", retire, StringComparison.Ordinal);
        Assert.Contains("case RetireWindowEffectKind.VectorStreamDirty:", retire, StringComparison.Ordinal);
        Assert.Contains("core.ExceptionStatus.VectorDirty = 1;", vectorAlu, StringComparison.Ordinal);
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
