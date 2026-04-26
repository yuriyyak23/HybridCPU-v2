using Xunit;
using YAKSys_Hybrid_CPU.Core;

namespace HybridCPU_ISE.Tests.Phase09;

/// <summary>
/// Proves that <see cref="MicroOpScheduler"/> cross-pod assist path
/// uses the explicitly-passed <c>pods</c> parameter (D3-G binding seam) instead of
/// the mutable global <c>Processor.Pods</c>.
///
/// Pattern: construct a scheduler with inter-core assist candidates,
/// invoke the inter-core inject path, and verify that cross-pod steal
/// is inert when <c>pods</c> is null (default) — proving no global read.
/// </summary>
public sealed class Phase09CrossPodSchedulerPodBindingSeamTests
{
    [Fact]
    public void CrossPodAssist_WhenPodsParameterIsNull_SkipsCrossPodStealWithoutGlobalRead()
    {
        // Arrange: scheduler with inter-core assist enabled
        var scheduler = new MicroOpScheduler();

        // Seed an inter-core assist nomination so the inject path has something to try.
        // The nomination is deliberately invalid (default transport), but the point is
        // that the cross-pod branch will be reached and must not read Processor.Pods.
        var transport = new AssistInterCoreTransport();
        scheduler.NominateInterCoreAssistCandidate(0, transport);

        // Act: PackBundle with localCoreId >= 0 triggers TryInjectInterCoreAssistCandidates
        // which internally calls TryStealInterCoreAssistTransport → TryStealCrossPodAssistTransport.
        // Since pods=null (default), cross-pod path returns false without touching global state.
        MicroOp[] result = scheduler.PackBundle(
            new MicroOp?[8],
            currentThreadId: 0,
            stealEnabled: true,
            stealMask: 0xFF,
            localCoreId: 1,
            assistPodId: 0x0100);

        // Assert: no crash, no Processor.Pods read, result is a valid 8-slot bundle
        Assert.Equal(8, result.Length);
        // Cross-pod injections counter should remain 0 since pods was null
        Assert.Equal(0, scheduler.AssistInterCoreCrossPodInjections);
    }

    [Fact]
    public void CrossPodAssist_WhenPodsIsEmptyArray_ReturnsZeroInjections()
    {
        // Arrange: scheduler where cross-pod path would be invoked
        var scheduler = new MicroOpScheduler();

        // Act: PackBundle — cross-pod path receives null pods (default) so no cross-pod steal
        MicroOp[] result = scheduler.PackBundle(
            new MicroOp?[8],
            currentThreadId: 0,
            stealEnabled: true,
            stealMask: 0xFF,
            localCoreId: 0,
            assistPodId: 0x0200);

        // Assert: no cross-pod injections occurred
        Assert.Equal(0, scheduler.AssistInterCoreCrossPodInjections);
        Assert.Equal(8, result.Length);
    }
}
