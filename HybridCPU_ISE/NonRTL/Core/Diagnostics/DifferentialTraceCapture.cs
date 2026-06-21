using System;
using System.Collections.Generic;

namespace YAKSys_Hybrid_CPU.Core
{
    /// <summary>
    /// Immutable snapshot of decode-stage advisory chain metadata captured at one bundle decode point.
    /// Used for differential verification between reference sequential and cluster-prepared paths.
    /// All fields are derived from decode-time facts — no runtime mutable state.
    /// </summary>
    public readonly struct DifferentialTraceEntry
    {
        public DifferentialTraceEntry(
            ulong pc,
            DecodeMode decodeMode,
            byte preparedScalarMask,
            byte refinedPreparedScalarMask,
            int advisoryScalarIssueWidth,
            int refinedAdvisoryScalarIssueWidth,
            bool shouldProbeClusterPath,
            bool suggestsFallbackDiagnostics,
            bool retainsReferenceSequentialPath,
            byte scalarCandidateMask,
            byte blockedScalarCandidateMask,
            byte registerHazardMask,
            byte orderingHazardMask)
        {
            PC = pc;
            DecodeMode = decodeMode;
            PreparedScalarMask = preparedScalarMask;
            RefinedPreparedScalarMask = refinedPreparedScalarMask;
            AdvisoryScalarIssueWidth = advisoryScalarIssueWidth;
            RefinedAdvisoryScalarIssueWidth = refinedAdvisoryScalarIssueWidth;
            ShouldProbeClusterPath = shouldProbeClusterPath;
            SuggestsFallbackDiagnostics = suggestsFallbackDiagnostics;
            RetainsReferenceSequentialPath = retainsReferenceSequentialPath;
            ScalarCandidateMask = scalarCandidateMask;
            BlockedScalarCandidateMask = blockedScalarCandidateMask;
            RegisterHazardMask = registerHazardMask;
            OrderingHazardMask = orderingHazardMask;
        }

        /// <summary>Program counter of the decoded bundle.</summary>
        public ulong PC { get; }

        /// <summary>Decode mode active at capture time.</summary>
        public DecodeMode DecodeMode { get; }

        /// <summary>Conservative prepared scalar mask from Phase 01 flat blocking.</summary>
        public byte PreparedScalarMask { get; }

        /// <summary>Refined prepared scalar mask from Phase 03 hazard-triage-aware readiness.</summary>
        public byte RefinedPreparedScalarMask { get; }

        /// <summary>Advisory scalar issue width based on conservative mask.</summary>
        public int AdvisoryScalarIssueWidth { get; }

        /// <summary>Advisory scalar issue width based on refined mask.</summary>
        public int RefinedAdvisoryScalarIssueWidth { get; }

        /// <summary>Whether the decision draft flagged this bundle for cluster-path probing.</summary>
        public bool ShouldProbeClusterPath { get; }

        /// <summary>Whether the admission prep suggests narrow fallback.</summary>
        public bool SuggestsFallbackDiagnostics { get; }

        /// <summary>Whether the reference sequential path is retained (must always be true).</summary>
        public bool RetainsReferenceSequentialPath { get; }

        /// <summary>Scalar candidate mask from admission prep.</summary>
        public byte ScalarCandidateMask { get; }

        /// <summary>Blocked scalar candidate mask (candidates excluded from prepared group).</summary>
        public byte BlockedScalarCandidateMask { get; }

        /// <summary>Register hazard mask from dependency analysis.</summary>
        public byte RegisterHazardMask { get; }

        /// <summary>Ordering hazard mask from dependency analysis.</summary>
        public byte OrderingHazardMask { get; }

        /// <summary>
        /// Create a trace entry from the advisory chain objects materialized during decode.
        /// </summary>
        internal static DifferentialTraceEntry FromAdvisoryChain(
            RuntimeClusterAdmissionCandidateView candidateView,
            RuntimeClusterAdmissionDecisionDraft decisionDraft)
        {
            return new DifferentialTraceEntry(
                candidateView.PC,
                candidateView.DecodeMode,
                candidateView.PreparedScalarMask,
                candidateView.RefinedPreparedScalarMask,
                candidateView.AdvisoryScalarIssueWidth,
                candidateView.RefinedAdvisoryScalarIssueWidth,
                decisionDraft.ShouldProbeClusterPath,
                candidateView.SuggestsFallbackDiagnostics,
                decisionDraft.RetainsReferenceSequentialPath,
                candidateView.ScalarCandidateMask,
                candidateView.BlockedScalarCandidateMask,
                candidateView.RegisterHazardMask,
                candidateView.OrderingHazardMask);
        }
    }

    /// <summary>
    /// Append-only trace capture for differential verification.
    /// Collects <see cref="DifferentialTraceEntry"/> snapshots from decode-stage advisory chain
    /// materialization. This is a diagnostic facility — it does not modify pipeline execution.
    /// </summary>
    public sealed class DifferentialTraceCapture
    {
        private readonly List<DifferentialTraceEntry> _entries = [];

        /// <summary>Number of captured entries.</summary>
        public int Count => _entries.Count;

        /// <summary>
        /// Append a trace entry captured from advisory chain materialization.
        /// </summary>
        public void AddEntry(DifferentialTraceEntry entry)
        {
            _entries.Add(entry);
        }

        /// <summary>
        /// Return a read-only view of all captured entries.
        /// </summary>
        public IReadOnlyList<DifferentialTraceEntry> GetEntries() => _entries;

        /// <summary>
        /// Remove all captured entries and reset the trace to empty state.
        /// </summary>
        public void Clear()
        {
            _entries.Clear();
        }
    }
}
