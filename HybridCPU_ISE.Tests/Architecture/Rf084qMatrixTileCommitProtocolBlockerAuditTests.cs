namespace HybridCPU_ISE.Tests.Architecture;

/// <summary>
/// RF-08.4q freezes the independent MatrixTile capture/retire/rollback
/// protocol and its pre-batch-publication boundary without changing it.
/// </summary>
public sealed class Rf084qMatrixTileCommitProtocolBlockerAuditTests
{
    [Fact]
    public void MainlineLane6RetireConsumesCaptureBeforeCommonBatchPrevalidation()
    {
        string root = FindRepositoryRoot();
        string matrixMicroOp = Read(root, "HybridCPU_ISE", "CloseToHSL", "Core",
            "Pipeline", "MicroOps", "MatrixTile", "MatrixTileMicroOps.cs");
        string retire = Read(root, "HybridCPU_ISE", "CloseToHSL", "Core", "Pipeline",
            "Retire", "Evidence", "CPU_Core.PipelineExecution.Retire.cs");
        string stageFlow = Read(root, "HybridCPU_ISE", "CloseToHSL", "Core", "Pipeline",
            "ExecutionFlow", "StageFlow", "CPU_Core.PipelineExecution.cs");

        Assert.Contains("public override void EmitWriteBackRetireRecords(", matrixMicroOp, StringComparison.Ordinal);
        Assert.Contains("RetireCapturedResult(ref core, capture)", matrixMicroOp, StringComparison.Ordinal);
        Assert.Contains("MatrixTileReplayRollbackAbi.RetireWithCheckpoint(", matrixMicroOp, StringComparison.Ordinal);
        Assert.Contains("lane.MicroOp is not Core.MatrixTileMicroOp", retire, StringComparison.Ordinal);

        int capture = stageFlow.IndexOf(
            "CaptureRetiredWriteBackLaneEffects(ref retireBatch, laneIndex, lane);",
            StringComparison.Ordinal);
        int prevalidate = stageFlow.IndexOf(
            "PrevalidateRetireWindowBatchForPublication(",
            capture,
            StringComparison.Ordinal);
        Assert.True(capture >= 0 && prevalidate > capture);
    }

    [Fact]
    public void MatrixTileUsesFamilyCorrelationButNoRf08RetireEffectCarrier()
    {
        string root = FindRepositoryRoot();
        string captureAbi = Read(root, "HybridCPU_ISE", "CloseToHSL", "Core", "ISA",
            "Instructions", "NonVmx", "Lanes00_03Vector", "MatrixTile",
            "MatrixTileExecuteCaptureAbi.cs");
        string materializer = Read(root, "HybridCPU_ISE", "CloseToHSL", "Core", "ISA",
            "Instructions", "NonVmx", "Lanes00_03Vector", "MatrixTile",
            "MatrixTileIrProjectionAndMaterializer.cs");
        string matrixRoot = Path.Combine(root, "HybridCPU_ISE", "CloseToHSL", "Core",
            "ISA", "Instructions", "NonVmx", "Lanes00_03Vector", "MatrixTile");
        string types = Read(root, "HybridCPU_ISE", "CloseToHSL", "Core", "Pipeline",
            "ExecutionFlow", "StageFlow", "CPU_Core.PipelineExecution.Types.cs");

        Assert.Contains("public readonly record struct MatrixTileCaptureIdentity(", captureAbi, StringComparison.Ordinal);
        Assert.Contains("uint CoreId,", captureAbi, StringComparison.Ordinal);
        Assert.Contains("int OwnerThreadId,", captureAbi, StringComparison.Ordinal);
        Assert.Contains("uint Opcode,", captureAbi, StringComparison.Ordinal);
        Assert.Contains("ulong CaptureOrdinal,", captureAbi, StringComparison.Ordinal);
        Assert.Contains("ulong CaptureFingerprint)", captureAbi, StringComparison.Ordinal);

        string allMatrixSources = string.Join(
            "\n",
            Directory.GetFiles(matrixRoot, "*.cs", SearchOption.AllDirectories)
                .Select(File.ReadAllText));
        Assert.DoesNotContain("RetireVisibleEffectIdentity", allMatrixSources, StringComparison.Ordinal);
        Assert.DoesNotContain("ScheduledOperation", allMatrixSources, StringComparison.Ordinal);
        Assert.DoesNotContain("VliwOperationId", allMatrixSources, StringComparison.Ordinal);
        Assert.Contains("GeneratedStaticBinding binding = canonical.StaticBinding", materializer, StringComparison.Ordinal);
        string materializedShape = materializer[
            materializer.IndexOf("public readonly record struct MatrixTileMaterializedInstruction(", StringComparison.Ordinal)..
            materializer.IndexOf("public static class MatrixTileIrProjectionAndMaterializer", StringComparison.Ordinal)];
        Assert.DoesNotContain("GeneratedStaticBinding", materializedShape, StringComparison.Ordinal);
        Assert.DoesNotContain("MatrixTile", ExtractRetireWindowEffectKind(types), StringComparison.Ordinal);
    }

    [Fact]
    public void RollbackAndReplayRemainHarnessOnlyCallersAndPaperRowIsSupersededByApprovedDecision()
    {
        string root = FindRepositoryRoot();
        string coreRoot = Path.Combine(root, "HybridCPU_ISE", "CloseToHSL", "Core");
        string[] rollbackCallers = FindCallerFiles(coreRoot, ".RollbackRetiredResult(");
        string[] replayCallers = FindCallerFiles(coreRoot, ".ReplayRolledBackResult(");
        string paper = Read(root, "ResearchPaper", "section", "md base",
            "5_Two_Stage_Admission_and_Bundle_Compositional_SMT_Packing.md");

        Assert.Equal(["Pipeline/MatrixTileFullPipelineHarness.cs"], rollbackCallers);
        Assert.Equal(["Pipeline/MatrixTileFullPipelineHarness.cs"], replayCallers);
        Assert.Contains(
            "| `MatrixTileCommit` | existing lane-6 MatrixTile capture/retire/checkpoint/rollback owner",
            paper,
            StringComparison.Ordinal);
        Assert.Contains(
            "RF-08.4r approves C-C",
            paper,
            StringComparison.Ordinal);
        Assert.Contains("separate architecture revision |", paper, StringComparison.Ordinal);
    }

    private static string[] FindCallerFiles(string coreRoot, string marker) =>
        Directory.GetFiles(coreRoot, "*.cs", SearchOption.AllDirectories)
            .Where(path => File.ReadAllText(path).Contains(marker, StringComparison.Ordinal))
            .Select(path => Path.GetRelativePath(coreRoot, path).Replace('\\', '/'))
            .OrderBy(path => path, StringComparer.Ordinal)
            .ToArray();

    private static string ExtractRetireWindowEffectKind(string types)
    {
        int start = types.IndexOf("internal enum RetireWindowEffectKind", StringComparison.Ordinal);
        int end = types.IndexOf("private enum RetireWindowTypedEffectKind", start, StringComparison.Ordinal);
        return types[start..end];
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
