using HybridCPU_ISE.Core;
using System.Text;

namespace YAKSys_Hybrid_CPU
{
    /// <summary>
    /// Correlates perf-side and trace-side Phase 1 evidence for regression-baseline validation.
    /// </summary>
    public readonly struct Phase1EvidenceCorrelationReport
    {
        public Phase1EvidenceCorrelationReport(
            bool replayEpochsAligned,
            bool averageEpochLengthAligned,
            bool stableDonorRatioAligned,
            bool usefulnessAligned,
            bool invalidationsAligned,
            bool eligibilityAligned,
            bool determinismAligned,
            int mismatchCount)
        {
            ReplayEpochsAligned = replayEpochsAligned;
            AverageEpochLengthAligned = averageEpochLengthAligned;
            StableDonorRatioAligned = stableDonorRatioAligned;
            UsefulnessAligned = usefulnessAligned;
            InvalidationsAligned = invalidationsAligned;
            EligibilityAligned = eligibilityAligned;
            DeterminismAligned = determinismAligned;
            MismatchCount = mismatchCount;
        }

        public bool ReplayEpochsAligned { get; }

        public bool AverageEpochLengthAligned { get; }

        public bool StableDonorRatioAligned { get; }

        public bool UsefulnessAligned { get; }

        public bool InvalidationsAligned { get; }

        public bool EligibilityAligned { get; }

        public bool DeterminismAligned { get; }

        public int MismatchCount { get; }

        public bool IsAligned => MismatchCount == 0;

        public string Describe()
        {
            if (IsAligned)
            {
                return "Aligned: perf-side counters, trace-side evidence, and repeated-run determinism agree.";
            }

            return $"Mismatch detected across {MismatchCount} validation dimensions.";
        }
    }
}
