using System;
using System.Collections.Generic;
using System.Numerics;

namespace YAKSys_Hybrid_CPU.Core
{
    internal readonly struct RuntimeClusterAdmissionHandoff
    {
        public RuntimeClusterAdmissionHandoff(
            ulong pc,
            DecodeMode decodeMode,
            DecodedBundleDependencySummary? dependencySummary,
            DecodedBundleAdmissionPrep admissionPrep,
            ClusterIssuePreparation clusterPreparation,
            BundleIssuePacket issuePacket,
            RuntimeClusterAdmissionCandidateView candidateView,
            RuntimeClusterAdmissionDecisionDraft decisionDraft,
            bool isHandoffReady,
            bool canSeedLegalityIntegration,
            bool canSeedClusterIntegration,
            bool retainsReferenceSequentialPath)
        {
            PC = pc;
            DecodeMode = decodeMode;
            DependencySummary = dependencySummary;
            AdmissionPrep = admissionPrep;
            ClusterPreparation = clusterPreparation;
            IssuePacket = issuePacket;
            CandidateView = candidateView;
            DecisionDraft = decisionDraft;
            IsHandoffReady = isHandoffReady;
            CanSeedLegalityIntegration = canSeedLegalityIntegration;
            CanSeedClusterIntegration = canSeedClusterIntegration;
            RetainsReferenceSequentialPath = retainsReferenceSequentialPath;
        }

        public ulong PC { get; }
        public DecodeMode DecodeMode { get; }

        /// <summary>
        /// Bundle-wide dependency matrices (RAW/WAW/WAR/control/memory).
        /// Downstream may call <see cref="DecodedBundleDependencySummary.QuerySlotHazards"/>
        /// and <see cref="DecodedBundleDependencySummary.ComputeRefinedWideReadyScalarMask"/>
        /// for fine-grained hazard analysis. Advisory only.
        /// </summary>
        public DecodedBundleDependencySummary? DependencySummary { get; }

        /// <summary>
        /// Conservative admission prep from Phase 01 decode.
        /// Contains <see cref="DecodedBundleAdmissionPrep.ScalarCandidateMask"/>,
        /// <see cref="DecodedBundleAdmissionPrep.WideReadyScalarMask"/> (conservative),
        /// and <see cref="DecodedBundleAdmissionPrep.SuggestNarrowFallback"/>.
        /// </summary>
        public DecodedBundleAdmissionPrep AdmissionPrep { get; }

        /// <summary>
        /// Decoder-side cluster preparation including scalar group (up to 4 entries)
        /// and auxiliary reservations by class.
        /// </summary>
        public ClusterIssuePreparation ClusterPreparation { get; }

        /// <summary>
        /// Fixed-shape issue packet for the current bundle-step.
        /// Always carries packetized decode/admission metadata and may also become the
        /// live execution-source lane set when <see cref="RuntimeClusterAdmissionDecisionDraft.UsesIssuePacketAsExecutionSource"/>
        /// is true.
        /// </summary>
        public BundleIssuePacket IssuePacket { get; }

        /// <summary>
        /// Runtime advisory candidate view with both conservative and refined scalar masks.
        /// Phase 04 adds <see cref="RuntimeClusterAdmissionCandidateView.RefinedPreparedScalarMask"/>
        /// and <see cref="RuntimeClusterAdmissionCandidateView.RefinedAdvisoryScalarIssueWidth"/>.
        /// </summary>
        public RuntimeClusterAdmissionCandidateView CandidateView { get; }

        /// <summary>
        /// Runtime decision draft. <see cref="RuntimeClusterAdmissionDecisionDraft.ShouldProbeClusterPath"/>
        /// flags bundles viable for cluster-path diagnostics, while
        /// <see cref="RuntimeClusterAdmissionDecisionDraft.UsesIssuePacketAsExecutionSource"/> and
        /// <see cref="RuntimeClusterAdmissionDecisionDraft.RetainsReferenceSequentialPath"/> describe how
        /// execute-stage materialization should treat the packet versus the retained reference path.
        /// </summary>
        public RuntimeClusterAdmissionDecisionDraft DecisionDraft { get; }

        /// <summary>True when the bundle has non-empty valid slots and handoff metadata is populated.</summary>
        public bool IsHandoffReady { get; }

        /// <summary>
        /// True when the bundle has a dependency summary and legality gates
        /// (QuerySlotHazards, ComputeRefinedWideReadyScalarMask) are available for fine-grained query.
        /// </summary>
        public bool CanSeedLegalityIntegration { get; }

        /// <summary>
        /// True when the bundle has a non-empty decision draft and downstream has admission metadata
        /// worth consuming. This does not, by itself, say whether execution stays on the retained
        /// reference path or materializes from the packet; callers must read <see cref="DecisionDraft"/>.
        /// </summary>
        public bool CanSeedClusterIntegration { get; }

        /// <summary>
        /// True when the handoff still keeps the reference sequential path available next to any packetized
        /// execution state. This may be false in cluster-prepared execution modes that intentionally
        /// materialize execute lanes from <see cref="IssuePacket"/>.
        /// </summary>
        public bool RetainsReferenceSequentialPath { get; }

        public static RuntimeClusterAdmissionHandoff Create(
            ulong pc,
            IReadOnlyList<DecodedBundleSlotDescriptor> slots,
            ClusterIssuePreparation clusterPreparation,
            RuntimeClusterAdmissionCandidateView candidateView,
            RuntimeClusterAdmissionDecisionDraft decisionDraft)
        {
            ArgumentNullException.ThrowIfNull(slots);
            ArgumentNullException.ThrowIfNull(clusterPreparation);

            DecodedBundleDependencySummary? dependencySummary = clusterPreparation.DependencySummary;
            BundleIssuePacket issuePacket = BundleIssuePacket.Create(
                clusterPreparation,
                slots,
                candidateView,
                decisionDraft);
            bool isHandoffReady = candidateView.ValidNonEmptyMask != 0;

            return new RuntimeClusterAdmissionHandoff(
                pc,
                candidateView.DecodeMode,
                dependencySummary,
                clusterPreparation.AdmissionPrep,
                clusterPreparation,
                issuePacket,
                candidateView,
                decisionDraft,
                isHandoffReady,
                canSeedLegalityIntegration: isHandoffReady && dependencySummary.HasValue,
                canSeedClusterIntegration: isHandoffReady && decisionDraft.DecisionKind != RuntimeClusterAdmissionDecisionKind.Empty,
                retainsReferenceSequentialPath: decisionDraft.RetainsReferenceSequentialPath);
        }

        public static RuntimeClusterAdmissionHandoff CreateEmpty(ulong pc = 0)
        {
            RuntimeClusterAdmissionCandidateView candidateView = RuntimeClusterAdmissionCandidateView.CreateEmpty(pc);
            RuntimeClusterAdmissionDecisionDraft decisionDraft = RuntimeClusterAdmissionDecisionDraft.CreateEmpty(pc);

            return new RuntimeClusterAdmissionHandoff(
                pc,
                DecodeMode.ReferenceSequentialMode,
                dependencySummary: null,
                admissionPrep: default,
                clusterPreparation: ClusterIssuePreparation.CreateEmpty(pc),
                issuePacket: BundleIssuePacket.CreateEmpty(pc),
                candidateView,
                decisionDraft,
                isHandoffReady: false,
                canSeedLegalityIntegration: false,
                canSeedClusterIntegration: false,
                retainsReferenceSequentialPath: decisionDraft.RetainsReferenceSequentialPath);
        }
    }
}
