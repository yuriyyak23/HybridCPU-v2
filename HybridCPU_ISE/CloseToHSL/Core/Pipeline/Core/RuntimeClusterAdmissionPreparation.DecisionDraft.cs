using System;
using System.Collections.Generic;
using System.Numerics;

namespace YAKSys_Hybrid_CPU.Core
{
    /// <summary>
    /// Internal runtime decision draft consumed from <see cref="RuntimeClusterAdmissionCandidateView"/>.
    /// This normalizes runtime-readable admission facts into one place and keeps three concerns explicit:
    /// diagnostic cluster-probe metadata, whether execution may materialize from the issue packet,
    /// and whether the reference sequential path is still retained alongside that packet state.
    /// </summary>
    internal readonly struct RuntimeClusterAdmissionDecisionDraft
    {
        public RuntimeClusterAdmissionDecisionDraft(
            ulong pc,
            DecodeMode decodeMode,
            RuntimeClusterAdmissionDecisionKind decisionKind,
            RuntimeClusterAdmissionExecutionMode executionMode,
            byte scalarIssueMask,
            byte auxiliaryReservationMask,
            byte blockedScalarCandidateMask,
            RuntimeClusterFallbackReasonMask fallbackReasonMask,
            int advisoryScalarIssueWidth,
            bool shouldProbeClusterPath,
            bool usesIssuePacketAsExecutionSource,
            bool retainsReferenceSequentialPath)
        {
            PC = pc;
            DecodeMode = decodeMode;
            DecisionKind = decisionKind;
            ExecutionMode = executionMode;
            ScalarIssueMask = scalarIssueMask;
            AuxiliaryReservationMask = auxiliaryReservationMask;
            BlockedScalarCandidateMask = blockedScalarCandidateMask;
            FallbackReasonMask = fallbackReasonMask;
            AdvisoryScalarIssueWidth = advisoryScalarIssueWidth;
            ShouldProbeClusterPath = shouldProbeClusterPath;
            UsesIssuePacketAsExecutionSource = usesIssuePacketAsExecutionSource;
            RetainsReferenceSequentialPath = retainsReferenceSequentialPath;
        }

        public ulong PC { get; }
        public DecodeMode DecodeMode { get; }
        public RuntimeClusterAdmissionDecisionKind DecisionKind { get; }
        public RuntimeClusterAdmissionExecutionMode ExecutionMode { get; }
        public byte ScalarIssueMask { get; }
        public byte AuxiliaryReservationMask { get; }
        public byte BlockedScalarCandidateMask { get; }
        public RuntimeClusterFallbackReasonMask FallbackReasonMask { get; }
        public int AdvisoryScalarIssueWidth { get; }

        /// <summary>
        /// Diagnostic flag indicating whether the bundle should still feed cluster-path probe telemetry.
        /// </summary>
        public bool ShouldProbeClusterPath { get; }

        /// <summary>
        /// True when execute-stage materialization may use the issue packet as the live source for
        /// this decode result.
        /// </summary>
        public bool UsesIssuePacketAsExecutionSource { get; }

        /// <summary>
        /// True when the reference sequential path stays available alongside the packetized admission state.
        /// </summary>
        public bool RetainsReferenceSequentialPath { get; }

        /// <summary>
        /// Restricts issue-packet execution to decodes whose current slot actually belongs to the
        /// selected scalar or authoritative auxiliary packet. This prevents unrelated deferred
        /// slots from incorrectly triggering later packet work while preserving bundle-level
        /// cluster diagnostics.
        /// </summary>
        public RuntimeClusterAdmissionDecisionDraft BindToCurrentSlot(byte slotIndex)
        {
            if (!UsesIssuePacketAsExecutionSource || slotIndex >= 8)
                return this;

            byte slotBit = (byte)(1 << slotIndex);
            if (((ScalarIssueMask | AuxiliaryReservationMask) & slotBit) != 0)
                return this;

            return new RuntimeClusterAdmissionDecisionDraft(
                PC,
                DecodeMode,
                DecisionKind,
                ExecutionMode,
                ScalarIssueMask,
                AuxiliaryReservationMask,
                BlockedScalarCandidateMask,
                FallbackReasonMask,
                AdvisoryScalarIssueWidth,
                ShouldProbeClusterPath,
                usesIssuePacketAsExecutionSource: false,
                RetainsReferenceSequentialPath);
        }

        public static RuntimeClusterAdmissionDecisionDraft Create(RuntimeClusterAdmissionCandidateView candidateView)
        {
            return CreateExecutable(candidateView, clusterPreparedModeEnabled: false);
        }

        public static RuntimeClusterAdmissionDecisionDraft CreateExecutable(
            RuntimeClusterAdmissionCandidateView candidateView,
            bool clusterPreparedModeEnabled)
        {
            RuntimeClusterAdmissionDecisionKind decisionKind = ResolveDecisionKind(candidateView);
            RuntimeClusterAdmissionExecutionMode executionMode = ResolveExecutionMode(
                candidateView,
                decisionKind,
                clusterPreparedModeEnabled);
            bool usesIssuePacketAsExecutionSource =
                clusterPreparedModeEnabled &&
                (executionMode == RuntimeClusterAdmissionExecutionMode.ClusterPrepared ||
                 executionMode == RuntimeClusterAdmissionExecutionMode.ClusterPreparedRefined ||
                 candidateView.AuxiliaryReservationMask != 0);
            RuntimeClusterFallbackReasonMask fallbackReasonMask = candidateView.FallbackReasonMask;

            if (executionMode == RuntimeClusterAdmissionExecutionMode.ReferenceSequentialFallback &&
                fallbackReasonMask == RuntimeClusterFallbackReasonMask.None)
            {
                throw new InvalidOperationException(
                    "ReferenceSequentialFallback requires a non-zero fallback reason mask derived from runtime diagnostics.");
            }

            // Stage 7 Phase B: when refined path active, issue from RefinedPreparedScalarMask
            byte scalarIssueMask = executionMode == RuntimeClusterAdmissionExecutionMode.ClusterPreparedRefined
                ? candidateView.RefinedPreparedScalarMask
                : candidateView.PreparedScalarMask;

            return new RuntimeClusterAdmissionDecisionDraft(
                candidateView.PC,
                candidateView.DecodeMode,
                decisionKind,
                executionMode,
                scalarIssueMask,
                candidateView.AuxiliaryReservationMask,
                candidateView.BlockedScalarCandidateMask,
                fallbackReasonMask,
                candidateView.AdvisoryScalarIssueWidth,
                shouldProbeClusterPath: decisionKind == RuntimeClusterAdmissionDecisionKind.AdvisoryClusterCandidate,
                usesIssuePacketAsExecutionSource,
                retainsReferenceSequentialPath: !clusterPreparedModeEnabled);
        }

        public static RuntimeClusterAdmissionDecisionDraft CreateEmpty(ulong pc = 0)
        {
            return new RuntimeClusterAdmissionDecisionDraft(
                pc,
                DecodeMode.ReferenceSequentialMode,
                RuntimeClusterAdmissionDecisionKind.Empty,
                RuntimeClusterAdmissionExecutionMode.Empty,
                scalarIssueMask: 0,
                auxiliaryReservationMask: 0,
                blockedScalarCandidateMask: 0,
                fallbackReasonMask: RuntimeClusterFallbackReasonMask.None,
                advisoryScalarIssueWidth: 0,
                shouldProbeClusterPath: false,
                usesIssuePacketAsExecutionSource: false,
                retainsReferenceSequentialPath: true);
        }

        private static RuntimeClusterAdmissionExecutionMode ResolveExecutionMode(
            RuntimeClusterAdmissionCandidateView candidateView,
            RuntimeClusterAdmissionDecisionKind decisionKind,
            bool clusterPreparedModeEnabled)
        {
            if (decisionKind == RuntimeClusterAdmissionDecisionKind.Empty)
                return RuntimeClusterAdmissionExecutionMode.Empty;

            if (decisionKind == RuntimeClusterAdmissionDecisionKind.AdvisoryAuxiliaryOnly)
                return RuntimeClusterAdmissionExecutionMode.AuxiliaryOnlyReference;

            if (!clusterPreparedModeEnabled)
                return RuntimeClusterAdmissionExecutionMode.ReferenceSequential;

            if (decisionKind != RuntimeClusterAdmissionDecisionKind.AdvisoryClusterCandidate)
                return RuntimeClusterAdmissionExecutionMode.ReferenceSequential;

            if (candidateView.PreparedScalarMask == 0)
            {
                // Stage 7 Phase B: refined-mask recovery — when conservative mask is empty
                // but hazard-triage-aware refined mask has ready candidates, promote to refined path.
                if (candidateView.RefinedPreparedScalarMask != 0)
                    return RuntimeClusterAdmissionExecutionMode.ClusterPreparedRefined;

                return RuntimeClusterAdmissionExecutionMode.ReferenceSequentialFallback;
            }

            return RuntimeClusterAdmissionExecutionMode.ClusterPrepared;
        }

        private static RuntimeClusterAdmissionDecisionKind ResolveDecisionKind(RuntimeClusterAdmissionCandidateView candidateView)
        {
            if (candidateView.ValidNonEmptyMask == 0)
                return RuntimeClusterAdmissionDecisionKind.Empty;

            if (candidateView.HasDraftCandidate || candidateView.HasClusterCandidate)
                return RuntimeClusterAdmissionDecisionKind.AdvisoryClusterCandidate;

            if (candidateView.ScalarCandidateMask == 0 && candidateView.AuxiliaryCandidateMask != 0)
                return RuntimeClusterAdmissionDecisionKind.AdvisoryAuxiliaryOnly;

            return RuntimeClusterAdmissionDecisionKind.AdvisoryReferenceSequential;
        }
    }
}
