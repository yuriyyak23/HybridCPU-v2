namespace HybridCPU_ISE.Tests.Architecture;

public sealed class Rf084aqAcceleratorCommitApprovedResidualExclusionTests
{
    [Fact]
    public void PaperApprovesExactIndependentProtocolAndNamesLimitation()
    {
        string paper = ReadPaper();
        Assert.Contains(
            "RF-08.4aq approved `AcceleratorCommit` independent-protocol C-C residual exclusion",
            paper,
            StringComparison.Ordinal);
        Assert.Contains("only production", paper, StringComparison.Ordinal);
        Assert.Contains("commit-before-common-prevalidation", paper, StringComparison.Ordinal);
        Assert.Contains("cross-family atomicity and precise-fault limitation", paper, StringComparison.Ordinal);
        Assert.Contains("admissible at RF-08 exit", paper, StringComparison.Ordinal);
    }

    [Fact]
    public void PaperPreservesOwnersFamilyBoundaryAndForbidsReconstruction()
    {
        string paper = ReadPaper();
        Assert.Contains("owners remain unchanged", paper, StringComparison.Ordinal);
        Assert.Contains("Lane-6 `DmaStreamCompute` remains a separate", paper, StringComparison.Ordinal);
        Assert.Contains("Identity must", paper, StringComparison.Ordinal);
        Assert.Contains("not be reconstructed from token handle/ID/state", paper, StringComparison.Ordinal);
        Assert.Contains("full-union prevalidation", paper, StringComparison.Ordinal);
    }

    [Fact]
    public void CurrentCodeRetainsSingleProductionFenceCommitCaller()
    {
        string root = FindRepositoryRoot();
        string coreRoot = Path.Combine(root, "HybridCPU_ISE", "CloseToHSL", "Core");
        string[] callers = Directory.GetFiles(coreRoot, "*.cs", SearchOption.AllDirectories)
            .Where(path => File.ReadAllText(path).Contains(".FenceCommit(", StringComparison.Ordinal))
            .Select(path => Path.GetRelativePath(coreRoot, path).Replace('\\', '/'))
            .Where(path => path != "Execution/ExternalAccelerators/ExternalAcceleratorRuntime.cs")
            .OrderBy(path => path, StringComparer.Ordinal)
            .ToArray();
        Assert.Equal(["Pipeline/MicroOps/Lane7Accelerator/SystemDeviceCommandMicroOp.cs"], callers);

        string microOp = File.ReadAllText(Path.Combine(coreRoot, callers[0].Replace('/', Path.DirectorySeparatorChar)));
        Assert.Contains("ExecuteFenceObserve(runtime, ReadTokenHandle", microOp, StringComparison.Ordinal);
        Assert.Contains(".FenceCommit(_capturedFenceHandle)", microOp, StringComparison.Ordinal);
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
