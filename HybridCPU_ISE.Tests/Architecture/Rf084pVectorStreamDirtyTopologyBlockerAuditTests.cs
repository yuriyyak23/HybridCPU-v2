namespace HybridCPU_ISE.Tests.Architecture;

/// <summary>
/// RF-08.4p freezes the current VectorStreamDirty marker topology without
/// turning an opcode-derived/no-op retire marker into identity or ownership.
/// </summary>
public sealed class Rf084pVectorStreamDirtyTopologyBlockerAuditTests
{
    [Fact]
    public void MainlineCaptureIsOpcodeDerivedAndPreservesTheClosedExclusionSet()
    {
        string root = FindRepositoryRoot();
        string retire = Read(root, "HybridCPU_ISE", "CloseToHSL", "Core", "Pipeline",
            "Retire", "Evidence", "CPU_Core.PipelineExecution.Retire.cs");

        Assert.Contains("if (IsVectorStreamDirtyRetireOpcode(lane.OpCode))", retire, StringComparison.Ordinal);
        Assert.Contains("retireBatch.CaptureRetireWindowVectorStreamDirty(lane.VirtualThreadId)", retire, StringComparison.Ordinal);
        Assert.Contains("IsaOpcodeValues.VSTORE or", retire, StringComparison.Ordinal);
        Assert.Contains("IsaOpcodeValues.VSCATTER or", retire, StringComparison.Ordinal);
        Assert.Contains("IsaOpcodeValues.VPOPC or", retire, StringComparison.Ordinal);
        Assert.Contains("IsaOpcodeValues.STREAM_WAIT", retire, StringComparison.Ordinal);
        Assert.Contains("return OpcodeRegistry.IsVectorOp(opcode);", retire, StringComparison.Ordinal);
    }

    [Fact]
    public void DirectCaptureIsPairedWithPredicateCompatibilityAndCoreCalledOnlyByTestSupport()
    {
        string root = FindRepositoryRoot();
        string coreRoot = Path.Combine(root, "HybridCPU_ISE", "CloseToHSL", "Core");
        string[] directProducerFiles = Directory.GetFiles(coreRoot, "*.cs", SearchOption.AllDirectories)
            .Where(path =>
            {
                string relative = Path.GetRelativePath(coreRoot, path).Replace('\\', '/');
                return !relative.StartsWith("Pipeline/Retire/", StringComparison.Ordinal) &&
                       File.ReadAllText(path).Contains(
                           "retireBatch.CaptureRetireWindowVectorStreamDirty(", StringComparison.Ordinal);
            })
            .Select(path => Path.GetRelativePath(coreRoot, path).Replace('\\', '/'))
            .ToArray();

        Assert.Equal(
            ["Execution/StreamEngine/Modes/StreamEngine.Execute1D.cs"],
            directProducerFiles);

        string direct = Read(root, "HybridCPU_ISE", "CloseToHSL", "Core",
            "Execution", "StreamEngine", "Modes", "StreamEngine.Execute1D.cs");
        Assert.Equal(2, Count(direct, "retireBatch.CaptureRetireWindowVectorStreamDirty(executionVtId)"));
        Assert.Contains("retireBatch.CaptureRetireWindowPredicateState(", direct, StringComparison.Ordinal);

        string[] captureCallers = Directory.GetFiles(coreRoot, "*.cs", SearchOption.AllDirectories)
            .Where(path => File.ReadAllText(path).Contains(
                "StreamEngine.CaptureRetireWindowPublications(", StringComparison.Ordinal))
            .Select(path => Path.GetRelativePath(coreRoot, path).Replace('\\', '/'))
            .ToArray();

        Assert.Equal(["Pipeline/Core/CPU_Core.TestSupport.cs"], captureCallers);
    }

    [Fact]
    public void MarkerHasNoApplyMutationOrExactIdentityAndPaperRowRemainsTemporary()
    {
        string root = FindRepositoryRoot();
        string types = Read(root, "HybridCPU_ISE", "CloseToHSL", "Core", "Pipeline",
            "ExecutionFlow", "StageFlow", "CPU_Core.PipelineExecution.Types.cs");
        string retire = Read(root, "HybridCPU_ISE", "CloseToHSL", "Core", "Pipeline",
            "Retire", "Evidence", "CPU_Core.PipelineExecution.Retire.cs");
        string vectorAlu = Read(root, "HybridCPU_ISE", "CloseToHSL", "Core",
            "Execution", "Vector", "ALU", "VectorALU.cs");
        string paper = Read(root, "ResearchPaper", "section", "md base",
            "5_Two_Stage_Admission_and_Bundle_Compositional_SMT_Packing.md");

        int factory = types.IndexOf("public static RetireWindowEffect VectorStreamDirty(", StringComparison.Ordinal);
        int factoryOwnerEnd = types.IndexOf("internal ref struct RetireWindowBatch", factory, StringComparison.Ordinal);
        string factoryBody = types[factory..factoryOwnerEnd];

        Assert.DoesNotContain("ScheduledOperation", factoryBody, StringComparison.Ordinal);
        Assert.DoesNotContain("VliwOperationId", factoryBody, StringComparison.Ordinal);
        Assert.DoesNotContain("GeneratedStaticBinding", factoryBody, StringComparison.Ordinal);
        Assert.Contains("PrevalidateVirtualThreadId(", retire, StringComparison.Ordinal);
        Assert.Contains(
            "case RetireWindowEffectKind.VectorStreamDirty:\r\n                            break;",
            retire.Replace("\n", "\r\n").Replace("\r\r\n", "\r\n"),
            StringComparison.Ordinal);
        Assert.Contains("core.ExceptionStatus.VectorDirty = 1;", vectorAlu, StringComparison.Ordinal);
        Assert.Contains("| `TrapCommit` |", paper, StringComparison.Ordinal);
        Assert.Contains("| `VectorStreamDirty` | mainline WB opcode-derived", paper, StringComparison.Ordinal);
        Assert.Contains("RF-08.4ap approves C-C", paper, StringComparison.Ordinal);
        Assert.Contains("| separate architecture revision |", paper, StringComparison.Ordinal);
    }

    private static int Count(string value, string marker)
    {
        int count = 0;
        int index = 0;
        while ((index = value.IndexOf(marker, index, StringComparison.Ordinal)) >= 0)
        {
            count++;
            index += marker.Length;
        }

        return count;
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
