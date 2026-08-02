using YAKSys_Hybrid_CPU;

namespace HybridCPU_ISE.Tests.Architecture;

/// <summary>
/// RF-08.4b proves that every materialized selected-prefix effect kind has both
/// a fail-closed validation disposition and an explicit apply-phase disposition.
/// It does not add effect identity or publication behavior.
/// </summary>
public sealed class Rf084bRetireEffectValidationApplyParityAuditTests
{
    [Fact]
    public void MaterializedEffectVocabularyIsClosedAndStable()
    {
        Processor.CPU_Core.RetireWindowEffectKind[] expected =
        [
            Processor.CPU_Core.RetireWindowEffectKind.None,
            Processor.CPU_Core.RetireWindowEffectKind.DeferredStoreCommit,
            Processor.CPU_Core.RetireWindowEffectKind.Csr,
            Processor.CPU_Core.RetireWindowEffectKind.VectorConfig,
            Processor.CPU_Core.RetireWindowEffectKind.Atomic,
            Processor.CPU_Core.RetireWindowEffectKind.System,
            Processor.CPU_Core.RetireWindowEffectKind.Vmx,
            Processor.CPU_Core.RetireWindowEffectKind.SerializingBoundary,
            Processor.CPU_Core.RetireWindowEffectKind.PipelineEvent,
            Processor.CPU_Core.RetireWindowEffectKind.ScalarMemoryStore,
            Processor.CPU_Core.RetireWindowEffectKind.PredicateState,
            Processor.CPU_Core.RetireWindowEffectKind.VectorStreamDirty,
            Processor.CPU_Core.RetireWindowEffectKind.VectorTransfer,
        ];

        Assert.Equal(expected, Enum.GetValues<Processor.CPU_Core.RetireWindowEffectKind>());
    }

    [Fact]
    public void EveryMaterializedEffectKindHasPrevalidationAndAnExplicitApplyDisposition()
    {
        string source = ReadRetireSource();
        string prevalidation = Slice(
            source,
            "private void PrevalidateRetireWindowBatchForPublication(",
            "private void PrevalidateDeferredStoreEffect(");
        string immediate = Slice(
            source,
            "private void ApplyRetireBatchImmediateEffects(",
            "private bool HasRetiredActiveControlFlowRedirect(");
        string late = Slice(
            source,
            "private void ApplyRetireBatchLateEffectsAndRedirect(",
            "private void HandleRetiredSerializingBoundary(");

        Assert.DoesNotContain(
            "case RetireWindowEffectKind.None",
            prevalidation,
            StringComparison.Ordinal);
        foreach (Processor.CPU_Core.RetireWindowEffectKind kind in
                 Enum.GetValues<Processor.CPU_Core.RetireWindowEffectKind>()
                     .Where(kind => kind != Processor.CPU_Core.RetireWindowEffectKind.None))
        {
            Assert.Contains($"RetireWindowEffectKind.{kind}", prevalidation, StringComparison.Ordinal);
        }
        Assert.Contains("default:", prevalidation, StringComparison.Ordinal);
        Assert.Contains("unsupported effect kind", prevalidation, StringComparison.Ordinal);

        foreach (Processor.CPU_Core.RetireWindowEffectKind kind in new[]
                 {
                     Processor.CPU_Core.RetireWindowEffectKind.DeferredStoreCommit,
                     Processor.CPU_Core.RetireWindowEffectKind.Csr,
                     Processor.CPU_Core.RetireWindowEffectKind.VectorConfig,
                     Processor.CPU_Core.RetireWindowEffectKind.Atomic,
                     Processor.CPU_Core.RetireWindowEffectKind.ScalarMemoryStore,
                     Processor.CPU_Core.RetireWindowEffectKind.PredicateState,
                     Processor.CPU_Core.RetireWindowEffectKind.VectorStreamDirty,
                     Processor.CPU_Core.RetireWindowEffectKind.VectorTransfer,
                 })
        {
            Assert.Contains($"case RetireWindowEffectKind.{kind}", immediate, StringComparison.Ordinal);
        }

        foreach (Processor.CPU_Core.RetireWindowEffectKind kind in new[]
                 {
                     Processor.CPU_Core.RetireWindowEffectKind.System,
                     Processor.CPU_Core.RetireWindowEffectKind.PipelineEvent,
                     Processor.CPU_Core.RetireWindowEffectKind.Vmx,
                     Processor.CPU_Core.RetireWindowEffectKind.SerializingBoundary,
                 })
        {
            Assert.Contains($"RetireWindowEffectKind.{kind}", late, StringComparison.Ordinal);
        }

        Assert.Contains(
            "case RetireWindowEffectKind.VectorStreamDirty:\n                            break;",
            immediate.Replace("\r\n", "\n", StringComparison.Ordinal),
            StringComparison.Ordinal);
    }

    [Fact]
    public void ValidationStillPrecedesAllSelectedPrefixMutationAndBothApplyPhases()
    {
        string stageFlow = File.ReadAllText(Path.Combine(
            FindRepositoryRoot(),
            "HybridCPU_ISE", "CloseToHSL", "Core", "Pipeline", "ExecutionFlow",
            "StageFlow", "CPU_Core.PipelineExecution.cs"));
        string retire = ReadRetireSource();

        int capture = stageFlow.IndexOf(
            "CaptureRetiredWriteBackLaneEffects(ref retireBatch, laneIndex, lane)",
            StringComparison.Ordinal);
        int prevalidate = stageFlow.IndexOf(
            "PrevalidateRetireWindowBatchForPublication(",
            StringComparison.Ordinal);
        int finalize = stageFlow.IndexOf(
            "FinalizeRetiredWriteBackLane(ref retireBatch, laneIndex, lane)",
            StringComparison.Ordinal);
        int immediate = stageFlow.IndexOf(
            "ApplyRetireBatchImmediateEffects(",
            StringComparison.Ordinal);
        int late = retire.IndexOf(
            "ApplyRetireBatchLateEffectsAndRedirect(",
            StringComparison.Ordinal);

        Assert.True(capture >= 0 && capture < prevalidate);
        Assert.True(prevalidate < finalize && finalize < immediate);
        Assert.True(late >= 0);
        Assert.Contains(
            "ClearRetiredWriteBackLanes(retireOrder, retireLaneCount);\n" +
            "                ApplyRetireBatchLateEffectsAndRedirect(",
            retire.Replace("\r\n", "\n", StringComparison.Ordinal),
            StringComparison.Ordinal);
    }

    private static string ReadRetireSource() =>
        File.ReadAllText(Path.Combine(
            FindRepositoryRoot(),
            "HybridCPU_ISE", "CloseToHSL", "Core", "Pipeline", "Retire",
            "Evidence", "CPU_Core.PipelineExecution.Retire.cs"));

    private static string Slice(string source, string startMarker, string endMarker)
    {
        int start = source.IndexOf(startMarker, StringComparison.Ordinal);
        int end = source.IndexOf(endMarker, start + startMarker.Length, StringComparison.Ordinal);
        Assert.True(start >= 0 && end > start);
        return source[start..end];
    }

    private static string FindRepositoryRoot()
    {
        for (DirectoryInfo? current = new(AppContext.BaseDirectory);
             current is not null;
             current = current.Parent)
        {
            if (Directory.Exists(Path.Combine(current.FullName, "HybridCPU_ISE")) &&
                Directory.Exists(Path.Combine(current.FullName, "HybridCPU_ISE.Tests")))
            {
                return current.FullName;
            }
        }

        throw new DirectoryNotFoundException("Repository root was not found.");
    }
}
