namespace HybridCPU_ISE.Tests.Architecture;

public sealed class Rf084atAcceleratorRegisterAbiApprovedResidualExclusionTests
{
    [Fact]
    public void PaperApprovesExactSevenOpcodeContour()
    {
        string paper = ReadPaper();
        Assert.Contains("RF-08.4at approved accelerator command ABI `RegisterWrite` C-C residual exclusion", paper, StringComparison.Ordinal);
        foreach (string opcode in new[] { "ACCEL_QUERY_CAPS", "ACCEL_SUBMIT", "ACCEL_POLL", "ACCEL_STATUS", "ACCEL_WAIT", "ACCEL_CANCEL", "ACCEL_FENCE" })
            Assert.Contains($"`{opcode}`", paper, StringComparison.Ordinal);
        Assert.Contains("rejected-no-write and precise-fault-no-write", paper, StringComparison.Ordinal);
        Assert.Contains("admissible at RF-08 exit", paper, StringComparison.Ordinal);
    }

    [Fact]
    public void DecisionPreservesCommitBoundaryAndForbidsReconstruction()
    {
        string paper = ReadPaper();
        Assert.Contains("separately approved `AcceleratorCommit` protocol", paper, StringComparison.Ordinal);
        Assert.Contains("neither absorbs that protocol", paper, StringComparison.Ordinal);
        Assert.Contains("Identity must not be reconstructed from opcode/kind", paper, StringComparison.Ordinal);
        Assert.Contains("fence/commit-coupling evidence", paper, StringComparison.Ordinal);
    }

    [Fact]
    public void CurrentCodeRetainsSingleCarrierAndAbiOutcomes()
    {
        string root = FindRepositoryRoot();
        string microOp = File.ReadAllText(Path.Combine(root, "HybridCPU_ISE", "CloseToHSL", "Core",
            "Pipeline", "MicroOps", "Lane7Accelerator", "SystemDeviceCommandMicroOp.cs"));
        string abi = File.ReadAllText(Path.Combine(root, "HybridCPU_ISE", "CloseToHSL", "Core",
            "Execution", "ExternalAccelerators", "Tokens", "AcceleratorRegisterAbi.cs"));
        Assert.Contains("SetHardPinnedPlacement(SlotClass.SystemSingleton, 7)", microOp, StringComparison.Ordinal);
        Assert.Contains("RetireRecord.RegisterWrite(vtId, DestinationRegister, abi.RegisterValue)", microOp, StringComparison.Ordinal);
        Assert.Contains("NoWriteRejected = 0", abi, StringComparison.Ordinal);
        Assert.Contains("WriteRegister = 1", abi, StringComparison.Ordinal);
        Assert.Contains("NoWritePreciseFault = 2", abi, StringComparison.Ordinal);
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
