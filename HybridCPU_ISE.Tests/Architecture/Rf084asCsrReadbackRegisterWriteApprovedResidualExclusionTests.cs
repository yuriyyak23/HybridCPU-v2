namespace HybridCPU_ISE.Tests.Architecture;

public sealed class Rf084asCsrReadbackRegisterWriteApprovedResidualExclusionTests
{
    [Fact]
    public void PaperApprovesExactMainlineDirectEagerGroup()
    {
        string paper = ReadPaper();
        Assert.Contains("RF-08.4as approved CSR-readback `RegisterWrite` C-C residual exclusion", paper, StringComparison.Ordinal);
        Assert.Contains("generated CSR EX/MEM/WB transport", paper, StringComparison.Ordinal);
        Assert.Contains("test-support-only bounded capture", paper, StringComparison.Ordinal);
        Assert.Contains("public eager", paper, StringComparison.Ordinal);
        Assert.Contains("admissible at RF-08", paper, StringComparison.Ordinal);
    }

    [Fact]
    public void DecisionPreservesEnvelopeAndCsrWriteBoundary()
    {
        string paper = ReadPaper();
        Assert.Contains("`CSR_CLEAR` produces no readback", paper, StringComparison.Ordinal);
        Assert.Contains("separately approved", paper, StringComparison.Ordinal);
        Assert.Contains("`CsrWrite` family", paper, StringComparison.Ordinal);
        Assert.Contains("Identity must not be reconstructed from opcode", paper, StringComparison.Ordinal);
        Assert.Contains("old-value/pairing/fault", paper, StringComparison.Ordinal);
    }

    [Fact]
    public void CurrentCodeRetainsMainlineAndDirectSurfaces()
    {
        string root = FindRepositoryRoot();
        string control = File.ReadAllText(Path.Combine(root, "HybridCPU_ISE", "CloseToHSL", "Core",
            "Pipeline", "MicroOps", "Control", "MicroOp.Control.cs"));
        string direct = File.ReadAllText(Path.Combine(root, "HybridCPU_ISE", "CloseToHSL", "Core",
            "Execution", "Dispatch", "ExecutionDispatcherV4.CsrAndSmtVt.cs"));
        Assert.Contains("SetHardPinnedPlacement(SlotClass.SystemSingleton, 7)", control, StringComparison.Ordinal);
        Assert.Contains("RetireRecord.RegisterWrite(vtId, DestRegID, _readValue)", control, StringComparison.Ordinal);
        Assert.Contains("state.WriteRegister(vtId, effect.DestRegId, effect.ReadValue)", direct, StringComparison.Ordinal);
        Assert.Contains("hasRegisterWriteback: false", direct, StringComparison.Ordinal);
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
                return current.FullName;
        }
        throw new DirectoryNotFoundException("Repository root was not found.");
    }
}
