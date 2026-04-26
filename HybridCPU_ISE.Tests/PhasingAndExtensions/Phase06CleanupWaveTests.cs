using Xunit;
using YAKSys_Hybrid_CPU.Core;

public class Phase06CleanupWaveTests
{
    [Fact]
    public void CreateExecutable_IgnoresFallbackDiagnosticsHint_WhenPreparedScalarMaskExists()
    {
        ClusterFallbackDiagnosticsSnapshot fallbackDiagnosticsSnapshot = new(0b0000_0001, true);

        RuntimeClusterAdmissionCandidateView candidateView = new(
            pc: 0x1000UL,
            decodeMode: DecodeMode.ClusterPreparedMode,
            validNonEmptyMask: 0b0000_0001,
            scalarCandidateMask: 0b0000_0001,
            preparedScalarMask: 0b0000_0001,
            refinedPreparedScalarMask: 0b0000_0001,
            blockedScalarCandidateMask: 0,
            auxiliaryCandidateMask: 0,
            auxiliaryReservationMask: 0,
            fallbackDiagnosticsSnapshot: fallbackDiagnosticsSnapshot,
            registerHazardMask: 0,
            orderingHazardMask: 0,
            advisoryScalarIssueWidth: 1,
            refinedAdvisoryScalarIssueWidth: 1,
            hasDraftCandidate: true,
            shouldConsiderClusterAdmission: true);

        RuntimeClusterAdmissionDecisionDraft decision = RuntimeClusterAdmissionDecisionDraft.CreateExecutable(
            candidateView,
            clusterPreparedModeEnabled: true);

        Assert.Equal(RuntimeClusterAdmissionDecisionKind.AdvisoryClusterCandidate, decision.DecisionKind);
        Assert.Equal(RuntimeClusterAdmissionExecutionMode.ClusterPrepared, decision.ExecutionMode);
        Assert.True(decision.UsesIssuePacketAsExecutionSource);
    }

    [Fact]
    public void CreateExecutable_AuxiliaryOnlyReservation_UsesIssuePacketExecutionSourceWhenClusterPreparedModeEnabled()
    {
        ClusterFallbackDiagnosticsSnapshot fallbackDiagnosticsSnapshot = new(0, false);

        RuntimeClusterAdmissionCandidateView candidateView = new(
            pc: 0x1800UL,
            decodeMode: DecodeMode.ClusterPreparedMode,
            validNonEmptyMask: 0b0000_0001,
            scalarCandidateMask: 0,
            preparedScalarMask: 0,
            refinedPreparedScalarMask: 0,
            blockedScalarCandidateMask: 0,
            auxiliaryCandidateMask: 0b0000_0001,
            auxiliaryReservationMask: 0b0000_0001,
            fallbackDiagnosticsSnapshot: fallbackDiagnosticsSnapshot,
            registerHazardMask: 0,
            orderingHazardMask: 0,
            advisoryScalarIssueWidth: 0,
            refinedAdvisoryScalarIssueWidth: 0,
            hasDraftCandidate: false,
            shouldConsiderClusterAdmission: true);

        RuntimeClusterAdmissionDecisionDraft decision = RuntimeClusterAdmissionDecisionDraft.CreateExecutable(
            candidateView,
            clusterPreparedModeEnabled: true);

        Assert.Equal(RuntimeClusterAdmissionDecisionKind.AdvisoryAuxiliaryOnly, decision.DecisionKind);
        Assert.Equal(RuntimeClusterAdmissionExecutionMode.AuxiliaryOnlyReference, decision.ExecutionMode);
        Assert.True(decision.UsesIssuePacketAsExecutionSource);
        Assert.Equal((byte)0b0000_0001, decision.AuxiliaryReservationMask);

        RuntimeClusterAdmissionDecisionDraft boundDecision = decision.BindToCurrentSlot(0);
        Assert.True(boundDecision.UsesIssuePacketAsExecutionSource);
    }
}
