namespace HybridCPU_ISE.Tests.Architecture;

/// <summary>
/// RF-08.4s freezes the lane-7 external-accelerator fence/commit topology and
/// its pre-common-prevalidation publication boundary without changing it.
/// </summary>
public sealed class Rf084sAcceleratorCommitProtocolBlockerAuditTests
{
    [Fact]
    public void FenceCommitPublishesDuringBatchMaterializationBeforeCommonPrevalidation()
    {
        string root = FindRepositoryRoot();
        string microOp = Read(root, "HybridCPU_ISE", "CloseToHSL", "Core",
            "Pipeline", "MicroOps", "Lane7Accelerator", "SystemDeviceCommandMicroOp.cs");
        string runtime = Read(root, "HybridCPU_ISE", "CloseToHSL", "Core",
            "Execution", "ExternalAccelerators", "ExternalAcceleratorRuntime.cs");
        string fence = Read(root, "HybridCPU_ISE", "CloseToHSL", "Core",
            "Execution", "ExternalAccelerators", "Fences", "AcceleratorFenceModel.cs");
        string commit = Read(root, "HybridCPU_ISE", "CloseToHSL", "Core",
            "Execution", "ExternalAccelerators", "Commit", "AcceleratorCommitModel.cs");
        string stageFlow = Read(root, "HybridCPU_ISE", "CloseToHSL", "Core",
            "Pipeline", "ExecutionFlow", "StageFlow", "CPU_Core.PipelineExecution.cs");

        Assert.Contains("ExecuteFenceObserve(runtime, ReadTokenHandle", microOp, StringComparison.Ordinal);
        Assert.Contains("ResolveRetireResult(ref core)", microOp, StringComparison.Ordinal);
        Assert.Contains(".FenceCommit(_capturedFenceHandle)", microOp, StringComparison.Ordinal);
        Assert.Contains("commitCompletedTokens: true", runtime, StringComparison.Ordinal);
        Assert.Contains("commitCoordinator.TryCommit(", fence, StringComparison.Ordinal);
        Assert.Contains("token.MarkCommitPendingFromCommitCoordinator", commit, StringComparison.Ordinal);
        Assert.Contains("mainMemory.TryWritePhysicalRange(stagedWrite.Address", commit, StringComparison.Ordinal);
        Assert.Contains("token.MarkCommittedFromCommitCoordinator", commit, StringComparison.Ordinal);

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
    public void CommitCallersAreClosedAndNoCommonAcceleratorEffectExists()
    {
        string root = FindRepositoryRoot();
        string coreRoot = Path.Combine(root, "HybridCPU_ISE", "CloseToHSL", "Core");
        string[] fenceCommitCallers = FindCallerFiles(
            coreRoot,
            ".FenceCommit(",
            "Execution/ExternalAccelerators/ExternalAcceleratorRuntime.cs");
        string[] tryCommitCallers = FindCallerFiles(
            coreRoot,
            "commitCoordinator.TryCommit(");
        string types = Read(root, "HybridCPU_ISE", "CloseToHSL", "Core",
            "Pipeline", "ExecutionFlow", "StageFlow", "CPU_Core.PipelineExecution.Types.cs");

        Assert.Equal(
            ["Pipeline/MicroOps/Lane7Accelerator/SystemDeviceCommandMicroOp.cs"],
            fenceCommitCallers);
        Assert.Equal(
            ["Execution/ExternalAccelerators/Fences/AcceleratorFenceModel.cs"],
            tryCommitCallers);
        Assert.DoesNotContain(
            "Accelerator",
            ExtractRetireWindowEffectKind(types),
            StringComparison.Ordinal);
    }

    [Fact]
    public void TokenCorrelationIsNotRf08IdentityAndLane6RemainsSeparate()
    {
        string root = FindRepositoryRoot();
        string token = Read(root, "HybridCPU_ISE", "CloseToHSL", "Core",
            "Execution", "ExternalAccelerators", "Tokens", "AcceleratorToken.cs");
        string handle = Read(root, "HybridCPU_ISE", "CloseToHSL", "Core",
            "Execution", "ExternalAccelerators", "Tokens", "AcceleratorTokenHandle.cs");
        string routing = Read(root, "HybridCPU_ISE", "CloseToHSL", "Core",
            "Runtime", "Completion", "Routing", "LaneCompletionRouting.cs");

        Assert.Contains("public ulong TokenId", token, StringComparison.Ordinal);
        Assert.Contains("public AcceleratorTokenHandle Handle", token, StringComparison.Ordinal);
        Assert.Contains("public AcceleratorCommandDescriptor Descriptor", token, StringComparison.Ordinal);
        Assert.Contains("public readonly record struct AcceleratorTokenHandle(ulong Value)", handle, StringComparison.Ordinal);
        Assert.DoesNotContain("ScheduledOperation", token + handle, StringComparison.Ordinal);
        Assert.DoesNotContain("VliwOperationId", token + handle, StringComparison.Ordinal);
        Assert.DoesNotContain("RetireVisibleEffectIdentity", token + handle, StringComparison.Ordinal);
        Assert.Contains("DmaStreamComputeLane6 = 1", routing, StringComparison.Ordinal);
        Assert.Contains("ExternalAcceleratorLane7 = 2", routing, StringComparison.Ordinal);
    }

    [Fact]
    public void PaperApprovesDedicatedAcceleratorDisposition()
    {
        string root = FindRepositoryRoot();
        string paper = Read(root, "ResearchPaper", "section", "md base",
            "5_Two_Stage_Admission_and_Bundle_Compositional_SMT_Packing.md");

        Assert.Contains(
            "| `AcceleratorCommit` | lane-7 `ACCEL_FENCE` observe",
            paper,
            StringComparison.Ordinal);
        Assert.Contains(
            "RF-08.4aq approves C-C",
            paper,
            StringComparison.Ordinal);
        Assert.Contains(
            "approved `AcceleratorCommit`",
            paper,
            StringComparison.Ordinal);
    }

    private static string[] FindCallerFiles(
        string coreRoot,
        string marker,
        params string[] excludedRelativePaths) =>
        Directory.GetFiles(coreRoot, "*.cs", SearchOption.AllDirectories)
            .Where(path => File.ReadAllText(path).Contains(marker, StringComparison.Ordinal))
            .Select(path => Path.GetRelativePath(coreRoot, path).Replace('\\', '/'))
            .Where(path => !excludedRelativePaths.Contains(path, StringComparer.Ordinal))
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
