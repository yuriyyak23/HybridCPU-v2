using System;
using System.Collections.Generic;

namespace YAKSys_Hybrid_CPU.Core
{
    /// <summary>
    /// Immutable discrepancy record from differential trace comparison.
    /// Identifies a specific field mismatch at a specific trace entry index
    /// with slot-level precision.
    /// </summary>
    public readonly struct DifferentialTraceDiscrepancy
    {
        public DifferentialTraceDiscrepancy(
            int entryIndex,
            ulong pc,
            string fieldName,
            string legacyValue,
            string clusterPreparedValue)
        {
            EntryIndex = entryIndex;
            PC = pc;
            FieldName = fieldName;
            LegacyValue = legacyValue;
            ClusterPreparedValue = clusterPreparedValue;
        }

        /// <summary>Index of the trace entry where the discrepancy was found.</summary>
        public int EntryIndex { get; }

        /// <summary>Program counter of the bundle with the discrepancy.</summary>
        public ulong PC { get; }

        /// <summary>Name of the field that differs between the two traces.</summary>
        public string FieldName { get; }

        /// <summary>Value of the field in the reference sequential trace.</summary>
        public string LegacyValue { get; }

        /// <summary>Value of the field in the cluster-prepared trace.</summary>
        public string ClusterPreparedValue { get; }

        public override string ToString() =>
            $"[{EntryIndex}] PC=0x{PC:X}: {FieldName} reference={LegacyValue} cluster={ClusterPreparedValue}";
    }

    /// <summary>
    /// Comparison result from differential trace analysis.
    /// Contains all discrepancies found and summary statistics.
    /// </summary>
    public sealed class DifferentialTraceCompareResult
    {
        public DifferentialTraceCompareResult(
            IReadOnlyList<DifferentialTraceDiscrepancy> discrepancies,
            int comparedEntryCount,
            int legacyEntryCount,
            int clusterPreparedEntryCount)
        {
            Discrepancies = discrepancies;
            ComparedEntryCount = comparedEntryCount;
            LegacyEntryCount = legacyEntryCount;
            ClusterPreparedEntryCount = clusterPreparedEntryCount;
        }

        /// <summary>All field-level discrepancies found during comparison.</summary>
        public IReadOnlyList<DifferentialTraceDiscrepancy> Discrepancies { get; }

        /// <summary>Number of entry pairs actually compared (min of both trace lengths).</summary>
        public int ComparedEntryCount { get; }

        /// <summary>Total entries in the legacy trace.</summary>
        public int LegacyEntryCount { get; }

        /// <summary>Total entries in the cluster-prepared trace.</summary>
        public int ClusterPreparedEntryCount { get; }

        /// <summary>True when the traces are semantically equivalent (no discrepancies, same length).</summary>
        public bool AreEquivalent =>
            Discrepancies.Count == 0 && LegacyEntryCount == ClusterPreparedEntryCount;

        /// <summary>True when the traces have different lengths (structural divergence).</summary>
        public bool HasLengthMismatch => LegacyEntryCount != ClusterPreparedEntryCount;
    }

    /// <summary>
    /// A/B comparison engine for differential trace verification.
    /// Compares two <see cref="DifferentialTraceCapture"/> instances (reference vs cluster-prepared)
    /// and reports discrepancies with slot-level precision.
    /// This is a diagnostic facility that does not modify pipeline state.
    /// </summary>
    public static class DifferentialTraceCompare
    {
        /// <summary>
        /// Compare two trace captures and return all discrepancies.
        /// Fields compared: PC, PreparedScalarMask, RefinedPreparedScalarMask,
        /// AdvisoryScalarIssueWidth, RefinedAdvisoryScalarIssueWidth,
        /// ShouldProbeClusterPath, SuggestsFallbackDiagnostics, RetainsReferenceSequentialPath,
        /// ScalarCandidateMask, BlockedScalarCandidateMask, RegisterHazardMask, OrderingHazardMask.
        /// </summary>
        /// <param name="legacyTrace">Trace captured in reference sequential mode.</param>
        /// <param name="clusterPreparedTrace">Trace captured in cluster-prepared mode.</param>
        public static DifferentialTraceCompareResult Compare(
            DifferentialTraceCapture legacyTrace,
            DifferentialTraceCapture clusterPreparedTrace)
        {
            ArgumentNullException.ThrowIfNull(legacyTrace);
            ArgumentNullException.ThrowIfNull(clusterPreparedTrace);

            IReadOnlyList<DifferentialTraceEntry> legacyEntries = legacyTrace.GetEntries();
            IReadOnlyList<DifferentialTraceEntry> clusterEntries = clusterPreparedTrace.GetEntries();

            int compareCount = Math.Min(legacyEntries.Count, clusterEntries.Count);
            List<DifferentialTraceDiscrepancy> discrepancies = [];

            for (int i = 0; i < compareCount; i++)
            {
                CompareEntry(i, legacyEntries[i], clusterEntries[i], discrepancies);
            }

            return new DifferentialTraceCompareResult(
                discrepancies,
                compareCount,
                legacyEntries.Count,
                clusterEntries.Count);
        }

        private static void CompareEntry(
            int index,
            DifferentialTraceEntry legacy,
            DifferentialTraceEntry cluster,
            List<DifferentialTraceDiscrepancy> discrepancies)
        {
            ulong pc = legacy.PC;

            CheckField(index, pc, "PC", legacy.PC, cluster.PC, discrepancies);
            CheckField(index, pc, "PreparedScalarMask", legacy.PreparedScalarMask, cluster.PreparedScalarMask, discrepancies);
            CheckField(index, pc, "RefinedPreparedScalarMask", legacy.RefinedPreparedScalarMask, cluster.RefinedPreparedScalarMask, discrepancies);
            CheckField(index, pc, "AdvisoryScalarIssueWidth", legacy.AdvisoryScalarIssueWidth, cluster.AdvisoryScalarIssueWidth, discrepancies);
            CheckField(index, pc, "RefinedAdvisoryScalarIssueWidth", legacy.RefinedAdvisoryScalarIssueWidth, cluster.RefinedAdvisoryScalarIssueWidth, discrepancies);
            CheckField(index, pc, "ShouldProbeClusterPath", legacy.ShouldProbeClusterPath, cluster.ShouldProbeClusterPath, discrepancies);
            CheckField(index, pc, "SuggestsFallbackDiagnostics", legacy.SuggestsFallbackDiagnostics, cluster.SuggestsFallbackDiagnostics, discrepancies);
            CheckField(index, pc, "RetainsReferenceSequentialPath", legacy.RetainsReferenceSequentialPath, cluster.RetainsReferenceSequentialPath, discrepancies);
            CheckField(index, pc, "ScalarCandidateMask", legacy.ScalarCandidateMask, cluster.ScalarCandidateMask, discrepancies);
            CheckField(index, pc, "BlockedScalarCandidateMask", legacy.BlockedScalarCandidateMask, cluster.BlockedScalarCandidateMask, discrepancies);
            CheckField(index, pc, "RegisterHazardMask", legacy.RegisterHazardMask, cluster.RegisterHazardMask, discrepancies);
            CheckField(index, pc, "OrderingHazardMask", legacy.OrderingHazardMask, cluster.OrderingHazardMask, discrepancies);
        }

        private static void CheckField<T>(
            int index,
            ulong pc,
            string fieldName,
            T legacyValue,
            T clusterValue,
            List<DifferentialTraceDiscrepancy> discrepancies) where T : IEquatable<T>
        {
            if (!legacyValue.Equals(clusterValue))
            {
                discrepancies.Add(new DifferentialTraceDiscrepancy(
                    index,
                    pc,
                    fieldName,
                    legacyValue.ToString()!,
                    clusterValue.ToString()!));
            }
        }
    }
}
