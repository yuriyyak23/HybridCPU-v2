using System;
using System.Collections.Generic;
using System.Numerics;

namespace YAKSys_Hybrid_CPU.Core
{
    /// <summary>
    /// Internal runtime-readable preparation snapshot derived from the decoder-side cluster seam.
    /// This object is advisory only and exists to prepare future runtime admission logic without
    /// changing the reference narrow execution mode.
    /// </summary>
    internal readonly struct RuntimeClusterAdmissionPreparation
    {
        public RuntimeClusterAdmissionPreparation(
            DecodeMode decodeMode,
            byte preparedScalarMask,
            byte fallbackDiagnosticsMask,
            int preparedScalarCount,
            int auxiliaryReservationCount,
            bool shouldConsiderClusterAdmission,
            bool suggestsFallbackDiagnostics)
        {
            DecodeMode = decodeMode;
            PreparedScalarMask = preparedScalarMask;
            FallbackDiagnosticsMask = fallbackDiagnosticsMask;
            PreparedScalarCount = preparedScalarCount;
            AuxiliaryReservationCount = auxiliaryReservationCount;
            ShouldConsiderClusterAdmission = shouldConsiderClusterAdmission;
            SuggestsFallbackDiagnostics = suggestsFallbackDiagnostics;
        }

        public DecodeMode DecodeMode { get; }
        public byte PreparedScalarMask { get; }
        public byte FallbackDiagnosticsMask { get; }
        public int PreparedScalarCount { get; }
        public int AuxiliaryReservationCount { get; }
        public bool ShouldConsiderClusterAdmission { get; }
        public bool SuggestsFallbackDiagnostics { get; }

        public static RuntimeClusterAdmissionPreparation Create(ClusterIssuePreparation clusterPreparation)
        {
            return new RuntimeClusterAdmissionPreparation(
                clusterPreparation.DecodeMode,
                clusterPreparation.ScalarClusterGroup.PreparedMask,
                clusterPreparation.FallbackDiagnosticsMask,
                clusterPreparation.ScalarClusterGroup.Count,
                clusterPreparation.AuxiliaryReservations.Length,
                shouldConsiderClusterAdmission: clusterPreparation.HasScalarClusterCandidate,
                suggestsFallbackDiagnostics: clusterPreparation.SuggestFallbackDiagnostics);
        }

        public static RuntimeClusterAdmissionPreparation CreateEmpty()
        {
            return new RuntimeClusterAdmissionPreparation(
                DecodeMode.ReferenceSequentialMode,
                preparedScalarMask: 0,
                fallbackDiagnosticsMask: 0,
                preparedScalarCount: 0,
                auxiliaryReservationCount: 0,
                shouldConsiderClusterAdmission: false,
                suggestsFallbackDiagnostics: false);
        }
    }
}
