namespace HybridCPU_ISE.Tests.Architecture;

/// <summary>
/// RF-08.4o freezes the split eager-mainline/direct-bounded predicate topology
/// without moving architectural mutation between execution and retirement.
/// </summary>
public sealed class Rf084oPredicateStateWriteTopologyBlockerAuditTests
{
    [Fact]
    public void MainlinePredicateWritersMutateDuringExecutionAcrossThreeSourceFiles()
    {
        string root = FindRepositoryRoot();
        string coreRoot = Path.Combine(root, "HybridCPU_ISE", "CloseToHSL", "Core");
        string[] writerFiles = Directory.GetFiles(coreRoot, "*.cs", SearchOption.AllDirectories)
            .Where(path =>
            {
                string relative = Path.GetRelativePath(coreRoot, path).Replace('\\', '/');
                return (relative.StartsWith("Execution/StreamEngine/", StringComparison.Ordinal) ||
                        relative.StartsWith("Pipeline/MicroOps/Vector/", StringComparison.Ordinal)) &&
                       File.ReadAllText(path).Contains("SetPredicateRegister(", StringComparison.Ordinal);
            })
            .Select(path => Path.GetRelativePath(coreRoot, path).Replace('\\', '/'))
            .OrderBy(path => path, StringComparer.Ordinal)
            .ToArray();

        Assert.Equal(
            [
                "Execution/StreamEngine/Modes/StreamEngine.Execute1D.cs",
                "Execution/StreamEngine/Modes/StreamEngine.cs",
                "Pipeline/MicroOps/Vector/VectorMicroOps.Compute.cs"
            ],
            writerFiles);

        string vectorCompute = Read(root, "HybridCPU_ISE", "CloseToHSL", "Core",
            "Pipeline", "MicroOps", "Vector", "VectorMicroOps.Compute.cs");
        Assert.Contains("PublishPredicateMask(ref Processor.CPU_Core core)", vectorCompute, StringComparison.Ordinal);
        Assert.Contains("core.SetPredicateRegister(destPred, resultMask)", vectorCompute, StringComparison.Ordinal);
    }

    [Fact]
    public void BoundedPredicateEffectIsDirectCompatibilityAndTestSupportCalled()
    {
        string root = FindRepositoryRoot();
        string coreRoot = Path.Combine(root, "HybridCPU_ISE", "CloseToHSL", "Core");
        string[] effectProducerFiles = Directory.GetFiles(coreRoot, "*.cs", SearchOption.AllDirectories)
            .Where(path => File.ReadAllText(path).Contains(
                "retireBatch.CaptureRetireWindowPredicateState(", StringComparison.Ordinal))
            .Select(path => Path.GetRelativePath(coreRoot, path).Replace('\\', '/'))
            .ToArray();

        Assert.Equal(
            ["Execution/StreamEngine/Modes/StreamEngine.Execute1D.cs"],
            effectProducerFiles);

        string[] captureCallers = Directory.GetFiles(coreRoot, "*.cs", SearchOption.AllDirectories)
            .Where(path => File.ReadAllText(path).Contains(
                "StreamEngine.CaptureRetireWindowPublications(", StringComparison.Ordinal))
            .Select(path => Path.GetRelativePath(coreRoot, path).Replace('\\', '/'))
            .ToArray();

        Assert.Equal(["Pipeline/Core/CPU_Core.TestSupport.cs"], captureCallers);

        string genericVector = Read(root, "HybridCPU_ISE", "CloseToHSL", "Core",
            "Pipeline", "MicroOps", "Vector", "MicroOp.Compute.cs");
        Assert.Contains("StreamEngine.Execute(ref core, Instruction, vtId)", genericVector, StringComparison.Ordinal);
        Assert.DoesNotContain("CaptureRetireWindowPublications", genericVector, StringComparison.Ordinal);
    }

    [Fact]
    public void PredicateEffectPayloadHasNoIdentityAndPaperRowRemainsTemporary()
    {
        string root = FindRepositoryRoot();
        string types = Read(root, "HybridCPU_ISE", "CloseToHSL", "Core", "Pipeline",
            "ExecutionFlow", "StageFlow", "CPU_Core.PipelineExecution.Types.cs");
        string retire = Read(root, "HybridCPU_ISE", "CloseToHSL", "Core", "Pipeline",
            "Retire", "Evidence", "CPU_Core.PipelineExecution.Retire.cs");
        string paper = Read(root, "ResearchPaper", "section", "md base",
            "5_Two_Stage_Admission_and_Bundle_Compositional_SMT_Packing.md");

        int factory = types.IndexOf("public static RetireWindowEffect PredicateState(", StringComparison.Ordinal);
        int nextFactory = types.IndexOf("public static RetireWindowEffect", factory + 1, StringComparison.Ordinal);
        string factoryBody = types[factory..nextFactory];

        Assert.DoesNotContain("ScheduledOperation", factoryBody, StringComparison.Ordinal);
        Assert.DoesNotContain("VliwOperationId", factoryBody, StringComparison.Ordinal);
        Assert.DoesNotContain("GeneratedStaticBinding", factoryBody, StringComparison.Ordinal);
        Assert.Contains("case RetireWindowEffectKind.PredicateState:", retire, StringComparison.Ordinal);
        Assert.Contains("SetPredicateRegister(", retire, StringComparison.Ordinal);
        Assert.Contains("| `TrapCommit` |", paper, StringComparison.Ordinal);
        Assert.Contains("| `PredicateStateWrite` | production comparison/mask micro-ops", paper, StringComparison.Ordinal);
        Assert.Contains("RF-08.4ao approves C-C for this exact split topology", paper, StringComparison.Ordinal);
        Assert.Contains("| separate architecture revision |", paper, StringComparison.Ordinal);
    }

    private static string Read(string root, params string[] segments) =>
        File.ReadAllText(Path.Combine(new[] { root }.Concat(segments).ToArray()));

    private static string FindRepositoryRoot()
    {
        DirectoryInfo? directory = new(AppContext.BaseDirectory);
        while (directory != null)
        {
            if (Directory.Exists(Path.Combine(directory.FullName, "ResearchPaper")) &&
                Directory.Exists(Path.Combine(directory.FullName, "HybridCPU_ISE")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException("Repository root not found.");
    }
}
