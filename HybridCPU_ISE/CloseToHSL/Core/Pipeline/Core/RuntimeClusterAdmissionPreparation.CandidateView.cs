using System;
using System.Collections.Generic;
using System.Numerics;

namespace YAKSys_Hybrid_CPU.Core
{
    /// <summary>
    /// Internal advisory admission draft derived from the decoder-owned residual slot/admission carrier and
    /// the runtime-readable cluster preparation seam. This is preparation metadata only and must
    /// not alter the reference sequential execution path.
    /// </summary>
    internal readonly struct RuntimeClusterAdmissionCandidateView
    {
        private const int BundleWidth = 8;
        private const int ScalarIssueWidthCeiling = 4;

        public RuntimeClusterAdmissionCandidateView(
            ulong pc,
            DecodeMode decodeMode,
            byte validNonEmptyMask,
            byte scalarCandidateMask,
            byte preparedScalarMask,
            byte refinedPreparedScalarMask,
            byte blockedScalarCandidateMask,
            byte auxiliaryCandidateMask,
            byte auxiliaryReservationMask,
            ClusterFallbackDiagnosticsSnapshot fallbackDiagnosticsSnapshot,
            byte registerHazardMask,
            byte orderingHazardMask,
            int advisoryScalarIssueWidth,
            int refinedAdvisoryScalarIssueWidth,
            bool hasDraftCandidate,
            bool shouldConsiderClusterAdmission)
        {
            PC = pc;
            DecodeMode = decodeMode;
            ValidNonEmptyMask = validNonEmptyMask;
            ScalarCandidateMask = scalarCandidateMask;
            PreparedScalarMask = preparedScalarMask;
            RefinedPreparedScalarMask = refinedPreparedScalarMask;
            BlockedScalarCandidateMask = blockedScalarCandidateMask;
            AuxiliaryCandidateMask = auxiliaryCandidateMask;
            AuxiliaryReservationMask = auxiliaryReservationMask;
            FallbackDiagnosticsSnapshot = fallbackDiagnosticsSnapshot;
            RegisterHazardMask = registerHazardMask;
            OrderingHazardMask = orderingHazardMask;
            AdvisoryScalarIssueWidth = advisoryScalarIssueWidth;
            RefinedAdvisoryScalarIssueWidth = refinedAdvisoryScalarIssueWidth;
            HasDraftCandidate = hasDraftCandidate;
            ShouldConsiderClusterAdmission = shouldConsiderClusterAdmission;
        }

        public ulong PC { get; }
        public DecodeMode DecodeMode { get; }
        public byte ValidNonEmptyMask { get; }
        public byte ScalarCandidateMask { get; }

        /// <summary>
        /// Conservative prepared scalar mask from Phase 01 flat blocking.
        /// A slot is blocked if it participates in ANY dependency pair.
        /// Preserved for backward compatibility and differential verification.
        /// </summary>
        public byte PreparedScalarMask { get; }

        /// <summary>
        /// Refined prepared scalar mask from Phase 03 hazard-triage-aware readiness.
        /// A slot is blocked only when a <see cref="HazardTriageClass.HardReject"/> peer
        /// exists within the scalar group. This is a structural projection, not a scheduling decision.
        /// Invariant: <c>(RefinedPreparedScalarMask &amp; PreparedScalarMask) == PreparedScalarMask</c>
        /// (refined mask is a superset of conservative mask).
        /// </summary>
        public byte RefinedPreparedScalarMask { get; }

        public byte BlockedScalarCandidateMask { get; }
        public byte AuxiliaryCandidateMask { get; }
        public byte AuxiliaryReservationMask { get; }
        public ClusterFallbackDiagnosticsSnapshot FallbackDiagnosticsSnapshot { get; }
        public byte RegisterHazardMask { get; }
        public byte OrderingHazardMask { get; }

        /// <summary>
        /// Advisory scalar issue width based on conservative <see cref="PreparedScalarMask"/>.
        /// Preserved for backward compatibility.
        /// </summary>
        public int AdvisoryScalarIssueWidth { get; }

        /// <summary>
        /// Advisory scalar issue width based on refined <see cref="RefinedPreparedScalarMask"/>.
        /// Uses hazard-triage-aware readiness; never exceeds 4-way scalar ceiling.
        /// This is a structural projection for diagnostics, not a scheduling decision.
        /// </summary>
        public int RefinedAdvisoryScalarIssueWidth { get; }

        public bool HasDraftCandidate { get; }
        public bool ShouldConsiderClusterAdmission { get; }
        public bool HasClusterCandidate => ShouldConsiderClusterAdmission && ScalarCandidateMask != 0;
        public bool SuggestsFallbackDiagnostics => FallbackDiagnosticsSnapshot.SuggestsFallbackDiagnostics;
        public byte FallbackDiagnosticsMask => FallbackDiagnosticsSnapshot.FallbackDiagnosticsMask;
        public RuntimeClusterFallbackReasonMask FallbackReasonMask => FallbackDiagnosticsSnapshot.FallbackReasonMask;

        /// <summary>
        /// Stage 7 Phase B: true when conservative PreparedScalarMask is empty but
        /// hazard-triage-aware RefinedPreparedScalarMask has ready candidates.
        /// </summary>
        public bool HasRefinedWideReadyCandidates => RefinedPreparedScalarMask != 0 && PreparedScalarMask == 0;

        public static RuntimeClusterAdmissionCandidateView Create(
            ulong pc,
            IReadOnlyList<DecodedBundleSlotDescriptor> slots,
            ClusterIssuePreparation clusterPreparation,
            RuntimeClusterAdmissionPreparation runtimePreparation)
        {
            ArgumentNullException.ThrowIfNull(slots);
            ArgumentNullException.ThrowIfNull(clusterPreparation);

            byte validNonEmptyMask = BuildValidNonEmptyMask(slots);
            DecodedBundleAdmissionPrep admissionPrep = clusterPreparation.AdmissionPrep;
            byte auxiliaryReservationMask = AggregateAuxiliaryReservationMask(clusterPreparation.AuxiliaryReservations);
            byte registerHazardMask = 0;
            byte orderingHazardMask = 0;

            DecodedBundleDependencySummary? dependencySummary = clusterPreparation.DependencySummary;
            if (dependencySummary.HasValue)
            {
                DecodedBundleDependencySummary summary = dependencySummary.Value;
                registerHazardMask = ExtractParticipatingSlotMask(
                    summary.RawDependencyMask |
                    summary.WawDependencyMask |
                    summary.WarDependencyMask);
                orderingHazardMask = ExtractParticipatingSlotMask(
                    summary.ControlConflictMask |
                    summary.MemoryConflictMask);
            }

            byte preparedScalarMask = runtimePreparation.PreparedScalarMask;
            byte blockedScalarCandidateMask = (byte)(admissionPrep.ScalarCandidateMask & ~preparedScalarMask);
            int advisoryScalarIssueWidth = runtimePreparation.ShouldConsiderClusterAdmission
                ? Math.Min(ScalarIssueWidthCeiling, runtimePreparation.PreparedScalarCount)
                : 0;

            // Phase 04: Compute refined readiness from hazard-triage-aware projection
            byte refinedPreparedScalarMask = preparedScalarMask;
            if (dependencySummary.HasValue)
            {
                byte scalarEligibleMask = admissionPrep.ScalarCandidateMask;
                refinedPreparedScalarMask = dependencySummary.Value.ComputeRefinedWideReadyScalarMask(scalarEligibleMask);
            }

            int refinedScalarCount = System.Numerics.BitOperations.PopCount((uint)refinedPreparedScalarMask);
            int refinedAdvisoryScalarIssueWidth = runtimePreparation.ShouldConsiderClusterAdmission
                ? Math.Min(ScalarIssueWidthCeiling, refinedScalarCount)
                : 0;

            RuntimeClusterFallbackReasonMask fallbackReasonMask =
                ClusterFallbackDiagnosticsSnapshot.DeriveReasonMask(
                    runtimePreparation.FallbackDiagnosticsMask,
                    blockedScalarCandidateMask,
                    registerHazardMask,
                    orderingHazardMask);

            bool hasDraftCandidate = runtimePreparation.ShouldConsiderClusterAdmission
                && admissionPrep.ScalarCandidateMask != 0;

            return new RuntimeClusterAdmissionCandidateView(
                pc,
                runtimePreparation.DecodeMode,
                validNonEmptyMask,
                admissionPrep.ScalarCandidateMask,
                preparedScalarMask,
                refinedPreparedScalarMask,
                blockedScalarCandidateMask,
                admissionPrep.AuxiliaryOpMask,
                auxiliaryReservationMask,
                new ClusterFallbackDiagnosticsSnapshot(
                    runtimePreparation.FallbackDiagnosticsMask,
                    runtimePreparation.SuggestsFallbackDiagnostics,
                    fallbackReasonMask),
                registerHazardMask,
                orderingHazardMask,
                advisoryScalarIssueWidth,
                refinedAdvisoryScalarIssueWidth,
                hasDraftCandidate,
                runtimePreparation.ShouldConsiderClusterAdmission);
        }

        public static RuntimeClusterAdmissionCandidateView CreateEmpty(ulong pc = 0)
        {
            return new RuntimeClusterAdmissionCandidateView(
                pc,
                DecodeMode.ReferenceSequentialMode,
                validNonEmptyMask: 0,
                scalarCandidateMask: 0,
                preparedScalarMask: 0,
                refinedPreparedScalarMask: 0,
                blockedScalarCandidateMask: 0,
                auxiliaryCandidateMask: 0,
                auxiliaryReservationMask: 0,
                fallbackDiagnosticsSnapshot: new ClusterFallbackDiagnosticsSnapshot(0, false),
                registerHazardMask: 0,
                orderingHazardMask: 0,
                advisoryScalarIssueWidth: 0,
                refinedAdvisoryScalarIssueWidth: 0,
                hasDraftCandidate: false,
                shouldConsiderClusterAdmission: false);
        }

        private static byte BuildValidNonEmptyMask(IReadOnlyList<DecodedBundleSlotDescriptor> slots)
        {
            byte validNonEmptyMask = 0;
            int slotCount = Math.Min(BundleWidth, slots.Count);

            for (int slotIndex = 0; slotIndex < slotCount; slotIndex++)
            {
                DecodedBundleSlotDescriptor slot = slots[slotIndex];
                if (slot.IsValid && !slot.GetRuntimeExecutionIsEmptyOrNop())
                {
                    validNonEmptyMask |= (byte)(1 << slotIndex);
                }
            }

            return validNonEmptyMask;
        }

        private static byte AggregateAuxiliaryReservationMask(AuxiliaryClusterReservation[] auxiliaryReservations)
        {
            byte reservationMask = 0;
            for (int i = 0; i < auxiliaryReservations.Length; i++)
            {
                reservationMask |= auxiliaryReservations[i].SlotMask;
            }

            return reservationMask;
        }

        private static byte ExtractParticipatingSlotMask(ulong pairMask)
        {
            byte slotMask = 0;

            for (int sourceSlotIndex = 0; sourceSlotIndex < BundleWidth; sourceSlotIndex++)
            {
                for (int targetSlotIndex = sourceSlotIndex + 1; targetSlotIndex < BundleWidth; targetSlotIndex++)
                {
                    ulong slotPairBit = 1UL << ((sourceSlotIndex * BundleWidth) + targetSlotIndex);
                    if ((pairMask & slotPairBit) == 0)
                        continue;

                    slotMask |= (byte)(1 << sourceSlotIndex);
                    slotMask |= (byte)(1 << targetSlotIndex);
                }
            }

            return slotMask;
        }
    }
}
