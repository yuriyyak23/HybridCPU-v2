namespace HybridCPU_ISE.Tests.Architecture;

public sealed class Rf084arControlLinkRegisterWriteApprovedResidualExclusionTests
{
    [Fact]
    public void PaperApprovesExactThreeSurfaceContour()
    {
        string paper = ReadPaper();
        Assert.Contains("RF-08.4ar approved control-link `RegisterWrite` C-C residual exclusion", paper, StringComparison.Ordinal);
        Assert.Contains("single-lane, explicit-packet and", paper, StringComparison.Ordinal);
        Assert.Contains("test-support-only bounded capture", paper, StringComparison.Ordinal);
        Assert.Contains("public", paper, StringComparison.Ordinal);
        Assert.Contains("dispatcher retains eager", paper, StringComparison.Ordinal);
        Assert.Contains("admissible at RF-08 exit", paper, StringComparison.Ordinal);
    }

    [Fact]
    public void DecisionPreservesPcWriteBoundaryAndForbidsReconstruction()
    {
        string paper = ReadPaper();
        Assert.Contains("separately approved `PcWrite` family", paper, StringComparison.Ordinal);
        Assert.Contains("does not merge the effects", paper, StringComparison.Ordinal);
        Assert.Contains("Identity must not be reconstructed from opcode", paper, StringComparison.Ordinal);
        Assert.Contains("explicit eager-compatibility disposition", paper, StringComparison.Ordinal);
    }

    [Fact]
    public void CurrentCodeRetainsMainlineAndDirectSurfaces()
    {
        string root = FindRepositoryRoot();
        string control = File.ReadAllText(Path.Combine(root, "HybridCPU_ISE", "CloseToHSL", "Core",
            "Pipeline", "MicroOps", "Control", "MicroOp.Control.cs"));
        string direct = File.ReadAllText(Path.Combine(root, "HybridCPU_ISE", "CloseToHSL", "Core",
            "Execution", "Dispatch", "ExecutionDispatcherV4.MemoryAndControl.cs"));
        Assert.Contains("SetHardPinnedPlacement(SlotClass.BranchControl, 7)", control, StringComparison.Ordinal);
        Assert.Contains("RetireRecord.RegisterWrite(vtId, DestRegID, _capturedPrimaryWriteBackResult)", control, StringComparison.Ordinal);
        Assert.Contains("state.WriteRegister(vtId, instr.Rd, result.Value)", direct, StringComparison.Ordinal);
        Assert.Contains("CaptureControlFlowRetireWindowPublications", direct, StringComparison.Ordinal);
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
