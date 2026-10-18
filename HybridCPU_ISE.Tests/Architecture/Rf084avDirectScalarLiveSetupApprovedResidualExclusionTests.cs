namespace HybridCPU_ISE.Tests.Architecture;

public sealed class Rf084avDirectScalarLiveSetupApprovedResidualExclusionTests
{
    [Fact]
    public void PaperApprovesExactDirectApiGroup()
    {
        string paper = ReadPaper();
        Assert.Contains("RF-08.4av approved direct scalar/live/setup `RegisterWrite` C-C residual exclusion", paper, StringComparison.Ordinal);
        foreach (string surface in new[]
        {
            "public eager `ExecutionDispatcherV4` scalar-ALU execution",
            "TEST-ONLY bounded scalar capture",
            "public `LiveCpuStateAdapter.WriteRegister`",
            "public `CPU_Core.WriteCommittedArch`",
            "retained `LegacyCpuStateAdapter.WriteIntRegister`/`WriteRegister`"
        })
            Assert.Contains(surface, paper, StringComparison.Ordinal);
        Assert.Contains("admissible at RF-08 exit", paper, StringComparison.Ordinal);
    }

    [Fact]
    public void DecisionPreservesReachabilityAndCsrBoundary()
    {
        string paper = ReadPaper();
        Assert.Contains("no production core constructor", paper, StringComparison.Ordinal);
        Assert.Contains("selected-retire users of `LiveCpuStateAdapter` still do not call", paper, StringComparison.Ordinal);
        Assert.Contains("remains outside this", paper, StringComparison.Ordinal);
        Assert.Contains("unconditional fail-closed", paper, StringComparison.Ordinal);
        Assert.Contains("Scalar load, control-link, CSR readback", paper, StringComparison.Ordinal);
        Assert.Contains("Identity must not be reconstructed from", paper, StringComparison.Ordinal);
    }

    [Fact]
    public void CurrentCodeRetainsDirectOwnersAndLegacyDelegation()
    {
        string root = FindRepositoryRoot();
        string scalar = Read(root, "HybridCPU_ISE", "CloseToHSL", "Core", "Execution", "Dispatch",
            "ExecutionDispatcherV4.Scalar.cs");
        string live = Read(root, "HybridCPU_ISE", "CloseToHSL", "Core", "State", "LiveCpuStateAdapter.cs");
        string legacy = Read(root, "HybridCPU_ISE", "Legacy", "CloseToHSL", "Core", "State", "Compat", "LegacyCpuStateAdapter.cs");
        string state = Read(root, "HybridCPU_ISE", "CloseToHSL", "Core", "Architecture", "State",
            "Architectural", "CPU_Core.StateData.cs");

        Assert.Contains("state.WriteRegister(vtId, instr.Rd, result)", scalar, StringComparison.Ordinal);
        Assert.Contains("RetireRecord.RegisterWrite(normalizedVtId", live, StringComparison.Ordinal);
        Assert.Contains("_canonicalState.WriteRegister(_selectedVtId, regID, value)", legacy, StringComparison.Ordinal);
        Assert.Contains("_canonicalState.WriteRegister(vtId, regId, value)", legacy, StringComparison.Ordinal);
        Assert.Contains("RetireCoordinator.Retire(RetireRecord.RegisterWrite(normalizedVtId, archReg, value))", state, StringComparison.Ordinal);
    }

    private static string ReadPaper() =>
        Read(FindRepositoryRoot(), "ResearchPaper", "section", "md base",
            "5_Two_Stage_Admission_and_Bundle_Compositional_SMT_Packing.md");

    private static string Read(string root, params string[] segments) =>
        File.ReadAllText(Path.Combine(new[] { root }.Concat(segments).ToArray()));

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
