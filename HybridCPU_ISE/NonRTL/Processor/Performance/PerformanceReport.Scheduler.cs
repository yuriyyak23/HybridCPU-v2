using HybridCPU_ISE.Core;
using System.Text;

namespace YAKSys_Hybrid_CPU
{
    public partial class PerformanceReport
    {
        #region MicroOp Scheduler Statistics (Phase 3)

        /// <summary>
        /// Total scheduler cycles (cycles scheduler was active)
        /// </summary>
        public long TotalSchedulerCycles { get; set; }

        /// <summary>
        /// Successful FSP injections
        /// </summary>
        public long SuccessfulInjections { get; set; }

        /// <summary>
        /// Rejected FSP injections
        /// </summary>
        public long RejectedInjections { get; set; }

        /// <summary>
        /// Rejections due to hazards (RAW/WAW/WAR)
        /// </summary>
        public long RejectedDueToHazards { get; set; }

        /// <summary>
        /// Rejections due to memory conflicts
        /// </summary>
        public long RejectedDueToMemConflict { get; set; }

        /// <summary>
        /// Number of rollback events
        /// </summary>
        public long RollbackEvents { get; set; }

        /// <summary>
        /// Average ready queue depth across all threads
        /// </summary>
        public double AverageReadyQueueDepth { get; set; }

        /// <summary>
        /// Maximum ready queue depth observed
        /// </summary>
        public int MaxReadyQueueDepth { get; set; }

        /// <summary>
        /// Thread starvation events (thread had no ready ops)
        /// </summary>
        public long ThreadStarvationEvents { get; set; }

        /// <summary>
        /// Scheduler utilization (% of VLIW slots filled)
        /// </summary>
        public double SchedulerUtilization { get; set; }

        /// <summary>
        /// FSP contribution (% of slots filled by FSP)
        /// </summary>
        public double FSPContribution { get; set; }

        /// <summary>
        /// Utilization per thread (16 threads)
        /// </summary>
        public double[]? PerThreadUtilization { get; set; }

        /// <summary>
        /// Total scheduler latency (cycles spent in scheduler)
        /// </summary>
        public long TotalSchedulerLatency { get; set; }

        /// <summary>
        /// Average scheduler latency per bundle
        /// </summary>
        public double AverageSchedulerLatency { get; set; }

        /// <summary>
        /// Bank contention stalls detected by MSHR/bank-level scoreboard (Q1 Review §2).
        /// Proves the Universal Scoreboard successfully predicts bank-level hazards.
        /// </summary>
        public long BankContentionStalls { get; set; }

        /// <summary>
        /// FSP pipeline latency cycles from 2-stage pipelined arbitration (Q1 Review §1).
        /// Non-zero when PipelinedFspEnabled is true, indicates stolen ops delayed by 1 cycle.
        /// </summary>
        public long FspPipelineLatencyCycles { get; set; }

        /// <summary>
        /// Replay epochs observed by the phase-aware runtime substrate.
        /// </summary>
        public long ReplayEpochCount { get; set; }

        /// <summary>
        /// Average replay epoch length.
        /// </summary>
        public double AverageReplayEpochLength { get; set; }

        /// <summary>
        /// Ratio of slots that remained stable donors across replay hits.
        /// </summary>
        public double StableDonorSlotRatio { get; set; }

        /// <summary>
        /// Number of scheduler cycles that observed an active replay phase.
        /// </summary>
        public long ReplayAwareCycles { get; set; }

        /// <summary>
        /// Number of legality checks that could reuse a phase-certificate template.
        /// </summary>
        public long PhaseCertificateReadyHits { get; set; }

        /// <summary>
        /// Number of legality checks that required a fresh phase-certificate path.
        /// </summary>
        public long PhaseCertificateReadyMisses { get; set; }

        /// <summary>
        /// Estimated number of repeated checks avoided by phase-certificate reuse.
        /// </summary>
        public long EstimatedPhaseCertificateChecksSaved { get; set; }

        /// <summary>
        /// Number of explicit phase-certificate invalidations.
        /// </summary>
        public long PhaseCertificateInvalidations { get; set; }

        /// <summary>
        /// Invalidation count caused by bundle mutation after a successful injection.
        /// </summary>
        public long PhaseCertificateMutationInvalidations { get; set; }

        /// <summary>
        /// Invalidation count caused by replay-phase mismatch or inactive phases.
        /// </summary>
        public long PhaseCertificatePhaseMismatchInvalidations { get; set; }

        /// <summary>
        /// Deterministic replay transitions recorded by the loop buffer.
        /// </summary>
        public long DeterministicReplayTransitions { get; set; }

        // ── Phase 2A: Credit-Based Fairness Telemetry ─────────────────

        /// <summary>
        /// Per-VT successful injection counts for fairness distribution analysis.
        /// Index 0–3 corresponds to VT 0–3.
        /// </summary>
        public long[]? PerVtInjections { get; set; }

        /// <summary>
        /// Number of starvation events (VT credit saturated at cap).
        /// </summary>
        public long FairnessStarvationEvents { get; set; }

        // ── Phase 2B: Bank-Pressure Telemetry ─────────────────────────

        /// <summary>
        /// Number of injection decisions where bank-pressure tie-break reordered candidates.
        /// </summary>
        public long BankPressureAvoidanceCount { get; set; }

        // ── Phase 2C: Speculation Budget Telemetry ────────────────────

        /// <summary>
        /// Number of speculative injections blocked due to budget exhaustion.
        /// </summary>
        public long SpeculationBudgetExhaustionEvents { get; set; }

        /// <summary>
        /// Peak concurrent speculative operations observed during execution.
        /// </summary>
        public long PeakConcurrentSpeculativeOps { get; set; }

        /// <summary>
        /// Reference empty-slot opportunities before replay-stable masking is applied.
        /// </summary>
        public long DeterminismReferenceOpportunitySlots { get; set; }

        /// <summary>
        /// Empty-slot opportunities that remained eligible after replay-stable masking.
        /// </summary>
        public long DeterminismReplayEligibleSlots { get; set; }

        /// <summary>
        /// Empty-slot opportunities hidden by deterministic replay constraints.
        /// </summary>
        public long DeterminismMaskedSlots { get; set; }

        /// <summary>
        /// Bounded estimate of injection opportunities lost to deterministic replay constraints.
        /// </summary>
        public long DeterminismEstimatedLostSlots { get; set; }

        /// <summary>
        /// Scheduling cycles where deterministic replay constraints removed at least one otherwise empty slot.
        /// </summary>
        public long DeterminismConstrainedCycles { get; set; }

        /// <summary>
        /// Domain-isolation probes evaluated while screening injected candidates.
        /// </summary>
        public long DomainIsolationProbeAttempts { get; set; }

        /// <summary>
        /// Domain-isolation probes rejected by policy.
        /// </summary>
        public long DomainIsolationBlockedAttempts { get; set; }

        /// <summary>
        /// Domain-isolation blocks caused by disjoint non-kernel domains.
        /// </summary>
        public long DomainIsolationCrossDomainBlocks { get; set; }

        /// <summary>
        /// Domain-isolation blocks caused by kernel-to-user enforcement.
        /// </summary>
        public long DomainIsolationKernelToUserBlocks { get; set; }

        // ── Phase 5: Oracle Gap Telemetry ──────────────────────────

        /// <summary>
        /// Total slots the shadow oracle packed across all analyzed cycles.
        /// </summary>
        public long OracleGapTotalOracleSlots { get; set; }

        /// <summary>
        /// Total slots the real scheduler packed across all analyzed cycles.
        /// </summary>
        public long OracleGapTotalRealSlots { get; set; }

        /// <summary>
        /// Gap slots attributed to donor/replay mask restrictions.
        /// </summary>
        public long OracleGapDonorRestriction { get; set; }

        /// <summary>
        /// Gap slots attributed to fairness/TDM ordering.
        /// </summary>
        public long OracleGapFairnessOrdering { get; set; }

        /// <summary>
        /// Gap slots attributed to legality-layer conservatism.
        /// </summary>
        public long OracleGapLegalityConservatism { get; set; }

        /// <summary>
        /// Gap slots attributed to domain-isolation enforcement.
        /// </summary>
        public long OracleGapDomainIsolation { get; set; }

        /// <summary>
        /// Gap slots attributed to speculation budget exhaustion.
        /// </summary>
        public long OracleGapSpeculationBudget { get; set; }

        /// <summary>
        /// Scheduling cycles where oracle packed more than real.
        /// </summary>
        public long OracleGapCyclesWithGap { get; set; }

        /// <summary>
        /// Total scheduling cycles analyzed by the shadow oracle.
        /// </summary>
        public long OracleGapTotalCyclesAnalyzed { get; set; }

        /// <summary>
        /// Number of counterexample records captured.
        /// </summary>
        public int OracleCounterexampleCount { get; set; }

        /// <summary>
        /// Total missed slots across all captured counterexamples.
        /// </summary>
        public long OracleCounterexampleTotalMissedSlots { get; set; }

        /// <summary>
        /// Dominant counterexample category name.
        /// </summary>
        public string? OracleCounterexampleDominantCategory { get; set; }

        #endregion
    }
}
