using HybridCPU_ISE.Core;
using System.Linq;
using System.Text;

namespace YAKSys_Hybrid_CPU
{
    public partial class PerformanceReport
    {
        public double PhaseCertificateReuseHitRate
        {
            get
            {
                long totalReadinessChecks = PhaseCertificateReadyHits + PhaseCertificateReadyMisses;
                if (totalReadinessChecks == 0) return 0.0;
                return (double)PhaseCertificateReadyHits / totalReadinessChecks;
            }
        }

        public double PhaseCertificateInvalidationRate
        {
            get
            {
                if (ReplayAwareCycles == 0) return 0.0;
                return (double)PhaseCertificateInvalidations / ReplayAwareCycles;
            }
        }

        public bool HasPhase1ValidationTelemetry =>
            ReplayEpochCount > 0 ||
            ReplayAwareCycles > 0 ||
            PhaseCertificateReadyHits > 0 ||
            PhaseCertificateReadyMisses > 0 ||
            EstimatedPhaseCertificateChecksSaved > 0 ||
            PhaseCertificateInvalidations > 0 ||
            DeterministicReplayTransitions > 0;

        public bool HasPhase2PolicyTelemetry =>
            FairnessStarvationEvents > 0 ||
            BankPressureAvoidanceCount > 0 ||
            SpeculationBudgetExhaustionEvents > 0 ||
            PeakConcurrentSpeculativeOps > 0 ||
            (PerVtInjections != null && PerVtInjections.Any(v => v > 0));

        public bool HasPhase3ReuseTelemetry =>
            ReplayAwareCycles > 0 ||
            PhaseCertificateReadyHits > 0 ||
            PhaseCertificateReadyMisses > 0 ||
            EstimatedPhaseCertificateChecksSaved > 0 ||
            PhaseCertificateInvalidations > 0 ||
            HasEligibilityTelemetry;

        public bool HasPhase4EvidenceTelemetry =>
            DeterminismReferenceOpportunitySlots > 0 ||
            DeterminismReplayEligibleSlots > 0 ||
            DeterminismMaskedSlots > 0 ||
            DeterminismEstimatedLostSlots > 0 ||
            DeterminismConstrainedCycles > 0 ||
            DomainIsolationProbeAttempts > 0 ||
            DomainIsolationBlockedAttempts > 0;

        public double DeterminismOpportunityRetentionRate =>
            DeterminismReferenceOpportunitySlots == 0
                ? 0.0
                : (double)DeterminismReplayEligibleSlots / DeterminismReferenceOpportunitySlots;

        public double DeterminismTaxRate =>
            DeterminismReferenceOpportunitySlots == 0
                ? 0.0
                : (double)DeterminismEstimatedLostSlots / DeterminismReferenceOpportunitySlots;

        public double DomainIsolationBlockRate =>
            DomainIsolationProbeAttempts == 0
                ? 0.0
                : (double)DomainIsolationBlockedAttempts / DomainIsolationProbeAttempts;

        public bool HasPhase5OracleGapTelemetry =>
            OracleGapTotalCyclesAnalyzed > 0;

        public bool HasStreamIngressWarmTelemetry =>
            L1BypassHits > 0 ||
            ForegroundWarmAttempts > 0 ||
            ForegroundWarmSuccesses > 0 ||
            ForegroundWarmReuseHits > 0 ||
            ForegroundBypassHits > 0 ||
            AssistWarmAttempts > 0 ||
            AssistWarmSuccesses > 0 ||
            AssistWarmReuseHits > 0 ||
            AssistBypassHits > 0 ||
            StreamWarmTranslationRejects > 0 ||
            StreamWarmBackendRejects > 0 ||
            AssistWarmResidentBudgetRejects > 0 ||
            AssistWarmLoadingBudgetRejects > 0 ||
            AssistWarmNoVictimRejects > 0;

        public double OracleEfficiency =>
            OracleGapTotalOracleSlots == 0
                ? 1.0
                : (double)OracleGapTotalRealSlots / OracleGapTotalOracleSlots;

        public double OracleGapRate =>
            OracleGapTotalOracleSlots == 0
                ? 0.0
                : (double)(OracleGapTotalOracleSlots - OracleGapTotalRealSlots) / OracleGapTotalOracleSlots;
    }
}
