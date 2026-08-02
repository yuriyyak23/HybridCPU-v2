namespace HybridCPU_ISE.Tests.Architecture;

/// <summary>
/// RF-08.4d freezes the paper-approved PcWrite C-C residual exclusion and its
/// complete current producer inventory without changing production behavior.
/// </summary>
public sealed class Rf084dPcWriteApprovedResidualExclusionTests
{
    [Fact]
    public void PaperApprovesClosedPcWriteCCScopeAtRf08Exit()
    {
        string root = FindRepositoryRoot();
        string paper = Read(root, "ResearchPaper", "section", "md base", "5_Two_Stage_Admission_and_Bundle_Compositional_SMT_Packing.md");

        Assert.Contains("#### RF-08.4d approved `PcWrite` C-C residual exclusion", paper, StringComparison.Ordinal);
        Assert.Contains("The exclusion is admissible at RF-08 exit.", paper, StringComparison.Ordinal);
        Assert.Contains("foreground auxiliary, exact-slot and replay", paper, StringComparison.Ordinal);
        Assert.Contains("direct compatibility dispatcher retire batch", paper, StringComparison.Ordinal);
        Assert.Contains("live-state-adapter PC writeback", paper, StringComparison.Ordinal);
        Assert.Contains("VMX retire-outcome redirects", paper, StringComparison.Ordinal);
        Assert.Contains("does not authorize identity reconstruction from opcode, lane, slot, VT, target", paper, StringComparison.Ordinal);
        Assert.Contains("`RetireCoordinator` remains the only", paper, StringComparison.Ordinal);
    }

    [Fact]
    public void CurrentPcWriteProducerInventoryRemainsClosedAndPublicationOwned()
    {
        string root = FindRepositoryRoot();
        string coreRoot = Path.Combine(root, "HybridCPU_ISE", "CloseToHSL", "Core");
        string[] productionFiles = Directory.GetFiles(coreRoot, "*.cs", SearchOption.AllDirectories);
        string[] producers = productionFiles
            .Where(path => File.ReadAllText(path).Contains("RetireRecord.PcWrite(", StringComparison.Ordinal))
            .Select(path => Path.GetRelativePath(coreRoot, path).Replace('\\', '/'))
            .OrderBy(path => path, StringComparer.Ordinal)
            .ToArray();

        Assert.Equal(
            [
                "Architecture/State/Architectural/CPU_Core.StateData.RuntimeOwnership.cs",
                "Execution/Dispatch/ExecutionDispatcherV4.MemoryAndControl.cs",
                "Pipeline/MicroOps/Control/MicroOp.Control.cs",
                "Pipeline/Retire/Evidence/CPU_Core.PipelineExecution.VmxRetire.cs"
            ],
            producers);

        string coordinator = Read(root, "HybridCPU_ISE", "CloseToHSL", "Core", "Architecture", "Registers", "Retire", "RetireCoordinator.cs");
        Assert.Contains("case RetireRecordKind.PcWrite:", coordinator, StringComparison.Ordinal);
        Assert.Contains("ApplyPcWrite(record);", coordinator, StringComparison.Ordinal);
    }

    private static string Read(string root, params string[] segments) =>
        File.ReadAllText(Path.Combine(new[] { root }.Concat(segments).ToArray()));

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

        throw new DirectoryNotFoundException("Repository root was not found from the test output directory.");
    }
}
