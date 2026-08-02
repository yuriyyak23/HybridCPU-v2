namespace HybridCPU_ISE.Tests.Architecture;

public sealed class Rf084auVectorMaskPopCountApprovedResidualExclusionTests
{
    [Fact]
    public void PaperApprovesExactVpopcProducerGroup()
    {
        string paper = ReadPaper();
        Assert.Contains("RF-08.4au approved scalar-result vector `VPOPC` `RegisterWrite` C-C residual exclusion", paper, StringComparison.Ordinal);
        Assert.Contains("`VectorMaskPopCountMicroOp`", paper, StringComparison.Ordinal);
        Assert.Contains("retained raw `VectorALUMicroOp` generated-record", paper, StringComparison.Ordinal);
        Assert.Contains("direct StreamEngine bounded-capture surfaces remain test-only adapters", paper, StringComparison.Ordinal);
        Assert.Contains("admissible at RF-08 exit", paper, StringComparison.Ordinal);
    }

    [Fact]
    public void DecisionKeepsPredicateDirtyAndX0Boundaries()
    {
        string paper = ReadPaper();
        Assert.Contains("Destination x0 remains an architectural no-op owned by `RetireCoordinator`", paper, StringComparison.Ordinal);
        Assert.Contains("writes no predicate state", paper, StringComparison.Ordinal);
        Assert.Contains("remains excluded from", paper, StringComparison.Ordinal);
        Assert.Contains("neither absorbs", paper, StringComparison.Ordinal);
        Assert.Contains("Identity must not be reconstructed from opcode", paper, StringComparison.Ordinal);
    }

    [Fact]
    public void CurrentCodeRetainsCanonicalCarrierAndSeparateDirtyPolicy()
    {
        string root = FindRepositoryRoot();
        string compute = File.ReadAllText(Path.Combine(root, "HybridCPU_ISE", "CloseToHSL", "Core",
            "Pipeline", "MicroOps", "Vector", "VectorMicroOps.Compute.cs"));
        string retire = File.ReadAllText(Path.Combine(root, "HybridCPU_ISE", "CloseToHSL", "Core",
            "Pipeline", "Retire", "Evidence", "CPU_Core.PipelineExecution.Retire.cs"));
        Assert.Contains("public sealed class VectorMaskPopCountMicroOp", compute, StringComparison.Ordinal);
        Assert.Contains("RetireRecord.RegisterWrite(vtId, DestRegID, _result)", compute, StringComparison.Ordinal);
        Assert.Contains("IsaOpcodeValues.VPOPC", retire, StringComparison.Ordinal);
        Assert.Contains("return false;", retire, StringComparison.Ordinal);
    }

    private static string ReadPaper() =>
        File.ReadAllText(Path.Combine(FindRepositoryRoot(), "ResearchPaper", "section", "md base",
            "5_Two_Stage_Admission_and_Bundle_Compositional_SMT_Packing.md"));

    private static string FindRepositoryRoot()
    {
        for (DirectoryInfo? current = new(AppContext.BaseDirectory); current is not null; current = current.Parent)
        {
            if (Directory.Exists(Path.Combine(current.FullName, "HybridCPU_ISE")) &&
                Directory.Exists(Path.Combine(current.FullName, "ResearchPaper")))
                return current.FullName;
        }
        throw new DirectoryNotFoundException("Repository root was not found.");
    }
}
