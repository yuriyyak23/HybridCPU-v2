using YAKSys_Hybrid_CPU.Core.Registers.Retire;

namespace HybridCPU_ISE.Tests.Architecture;

/// <summary>
/// RF-08.4m freezes the current split trap topology without converting stage
/// winner metadata or a pipeline-event payload into issued-attempt identity.
/// </summary>
public sealed class Rf084mTrapCommitIdentitySourceBlockerAuditTests
{
    [Fact]
    public void TrapCommitHasNoDedicatedRetireWindowEffectKindOrIdentityCarrier()
    {
        string root = FindRepositoryRoot();
        string types = Read(root, "HybridCPU_ISE", "CloseToHSL", "Core", "Pipeline",
            "ExecutionFlow", "StageFlow", "CPU_Core.PipelineExecution.Types.cs");
        string helpers = Read(root, "HybridCPU_ISE", "CloseToHSL", "Core", "Pipeline",
            "ExecutionFlow", "StageFlow", "CPU_Core.Pipeline.Helpers.cs");
        string identity = Read(root, "HybridCPU_ISE", "CloseToHSL", "Core",
            "Architecture", "Registers", "Retire", "Rf08RetireEffectIdentityContracts.cs");

        Assert.Contains("TrapCommit = 10", identity, StringComparison.Ordinal);
        Assert.DoesNotContain("RetireWindowEffectKind.Trap", types, StringComparison.Ordinal);
        int winnerMetadata = helpers.IndexOf("StageAwareExceptionWinnerMetadata", StringComparison.Ordinal);
        Assert.True(winnerMetadata >= 0);
        Assert.DoesNotContain("PostStageBIssuedAttempt", helpers[winnerMetadata..], StringComparison.Ordinal);
    }

    [Fact]
    public void TrapEntryEventUsesPipelineEventWhileFaultDeliveryUsesStageWinnerMetadata()
    {
        string root = FindRepositoryRoot();
        string microOp = Read(root, "HybridCPU_ISE", "CloseToHSL", "Core", "Pipeline",
            "MicroOps", "Types", "MicroOp.IO.cs");
        string types = Read(root, "HybridCPU_ISE", "CloseToHSL", "Core", "Pipeline",
            "ExecutionFlow", "StageFlow", "CPU_Core.PipelineExecution.Types.cs");
        string retire = Read(root, "HybridCPU_ISE", "CloseToHSL", "Core", "Pipeline",
            "Retire", "Evidence", "CPU_Core.PipelineExecution.Retire.cs");
        string faults = Read(root, "HybridCPU_ISE", "CloseToHSL", "Core", "Pipeline",
            "ExecutionFlow", "Faults", "CPU_Core.PipelineExecution.Faults.cs");

        Assert.Contains("TrapEntryEvent CreatePipelineEvent", microOp, StringComparison.Ordinal);
        Assert.Contains("RetireWindowEffect.PipelineEvent(", types, StringComparison.Ordinal);
        Assert.Contains("pipelineEvent is Core.Pipeline.TrapEntryEvent", types, StringComparison.Ordinal);
        Assert.Contains("case RetireWindowEffectKind.PipelineEvent", retire, StringComparison.Ordinal);
        Assert.Contains("TryResolveStageAwareExceptionWinnerMetadata(", faults, StringComparison.Ordinal);
        Assert.Contains("FlushPipeline(Core.AssistInvalidationReason.Trap)", faults, StringComparison.Ordinal);
        Assert.Contains("throw new Core.PageFaultException(", faults, StringComparison.Ordinal);
    }

    [Fact]
    public void OlderSelectedPrefixPublishesBeforeFaultAndPaperDefinesNarrowTrapFamily()
    {
        string root = FindRepositoryRoot();
        string stageFlow = Read(root, "HybridCPU_ISE", "CloseToHSL", "Core", "Pipeline",
            "ExecutionFlow", "StageFlow", "CPU_Core.PipelineExecution.cs");
        string paper = Read(root, "ResearchPaper", "section", "md base",
            "5_Two_Stage_Admission_and_Bundle_Compositional_SMT_Packing.md");

        int truncate = stageFlow.IndexOf("TruncateRetireOrderBeforeWriteBackFaultWinner(", StringComparison.Ordinal);
        int prevalidate = stageFlow.IndexOf("PrevalidateRetireWindowBatchForPublication(", StringComparison.Ordinal);
        int apply = stageFlow.IndexOf("ApplyRetireBatchImmediateEffects(", StringComparison.Ordinal);
        int finalize = stageFlow.IndexOf("FinalizeWriteBackRetireWindow(", StringComparison.Ordinal);

        Assert.True(truncate >= 0 && truncate < prevalidate);
        Assert.True(prevalidate < apply && apply < finalize);
        Assert.Contains("| `TrapCommit` |", paper, StringComparison.Ordinal);
        Assert.Contains("RF-08.4am defines this family as page-fault delivery only", paper, StringComparison.Ordinal);
        Assert.Contains("| `PipelineEventPublication` | mainline `TrapMicroOp`/`TrapEntryEvent`", paper, StringComparison.Ordinal);
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
