using HybridCPU_ISE.Core;
using System.Text;

namespace YAKSys_Hybrid_CPU
{
    public partial class PerformanceReport
    {
        #region Methods

        /// <summary>
        /// Generate a compact Phase 1 validation summary for replay-aware evidence review.
        /// </summary>
        public string GeneratePhase1ValidationSummary()
        {
            var sb = new StringBuilder();
            sb.AppendLine("Phase 1 Validation Contour:");
            sb.AppendLine($"  Replay Evidence: {ReplayEpochCount:N0} epochs, avg length {AverageReplayEpochLength:F2}, {ReplayAwareCycles:N0} replay-aware cycles");
            sb.AppendLine($"  Dense Timeline: stable donor ratio {StableDonorSlotRatio * 100:F2}%, deterministic transitions {DeterministicReplayTransitions:N0}");
            sb.AppendLine($"  Usefulness: {PhaseCertificateReadyHits:N0} hits / {PhaseCertificateReadyMisses:N0} misses ({PhaseCertificateReuseHitRate * 100:F2}%), est. {EstimatedPhaseCertificateChecksSaved:N0} checks saved");
            sb.AppendLine($"  Invalidations: {PhaseCertificateInvalidations:N0} total ({PhaseCertificateInvalidationRate * 100:F2}% per replay-aware cycle), mutation {PhaseCertificateMutationInvalidations:N0}, phase mismatch {PhaseCertificatePhaseMismatchInvalidations:N0}");
            AppendEligibilityValidationSummary(sb);
            return sb.ToString();
        }

        /// <summary>
        /// Correlate Phase 1 perf-side counters with trace-side evidence and repeated-run determinism.
        /// </summary>
        public Phase1EvidenceCorrelationReport CorrelatePhase1Evidence(ReplayTraceEvidenceSummary traceSummary, ReplayDeterminismReport determinismReport)
        {
            bool replayEpochsAligned = ReplayEpochCount == traceSummary.ReplayEpochCount;
            bool averageEpochLengthAligned = ApproximatelyEqual(AverageReplayEpochLength, traceSummary.AverageEpochLength);
            bool stableDonorRatioAligned = ApproximatelyEqual(StableDonorSlotRatio, traceSummary.StableDonorSlotRatio);
            bool usefulnessAligned =
                PhaseCertificateReadyHits == traceSummary.PhaseCertificateReadyHits &&
                PhaseCertificateReadyMisses == traceSummary.PhaseCertificateReadyMisses &&
                EstimatedPhaseCertificateChecksSaved == traceSummary.EstimatedPhaseCertificateChecksSaved;
            bool invalidationsAligned = PhaseCertificateInvalidations == traceSummary.PhaseCertificateInvalidations;
            bool eligibilityAligned = EligibilityMaskedCycles == traceSummary.EligibilityMaskedCycles &&
                EligibilityMaskedReadyCandidates == traceSummary.EligibilityMaskedReadyCandidates;
            bool determinismAligned = determinismReport.IsDeterministic && DeterministicReplayTransitions == determinismReport.ComparedEpochs;

            int mismatchCount = 0;
            mismatchCount += replayEpochsAligned ? 0 : 1;
            mismatchCount += averageEpochLengthAligned ? 0 : 1;
            mismatchCount += stableDonorRatioAligned ? 0 : 1;
            mismatchCount += usefulnessAligned ? 0 : 1;
            mismatchCount += invalidationsAligned ? 0 : 1;
            mismatchCount += eligibilityAligned ? 0 : 1;
            mismatchCount += determinismAligned ? 0 : 1;

            return new Phase1EvidenceCorrelationReport(
                replayEpochsAligned,
                averageEpochLengthAligned,
                stableDonorRatioAligned,
                usefulnessAligned,
                invalidationsAligned,
                eligibilityAligned,
                determinismAligned,
                mismatchCount);
        }

        /// <summary>
        /// Generate a compact regression-baseline summary that combines perf-side and trace-side Phase 1 evidence.
        /// </summary>
        public string GeneratePhase1RegressionBaselineSummary(ReplayTraceEvidenceSummary traceSummary, ReplayDeterminismReport determinismReport)
        {
            var correlation = CorrelatePhase1Evidence(traceSummary, determinismReport);
            var sb = new StringBuilder();
            sb.Append(GeneratePhase1ValidationSummary());
            sb.AppendLine("Phase 1 Regression Baseline:");
            sb.AppendLine($"  Trace Evidence: {traceSummary.Describe()}");
            sb.AppendLine($"  Repeated Runs: {determinismReport.Describe()}");
            sb.AppendLine($"  Correlation: {correlation.Describe()}");
            return sb.ToString();
        }

        /// <summary>
        /// Generate a compact Phase 2 policy summary for fairness/pressure/budget evidence review.
        /// </summary>
        public string GeneratePhase2PolicySummary()
        {
            var sb = new StringBuilder();
            sb.AppendLine("Phase 2 Policy Contour:");
            if (PerVtInjections != null && PerVtInjections.Length >= 4)
            {
                sb.AppendLine($"  Fairness: VT0={PerVtInjections[0]:N0} VT1={PerVtInjections[1]:N0} VT2={PerVtInjections[2]:N0} VT3={PerVtInjections[3]:N0}, starvation events {FairnessStarvationEvents:N0}");
            }
            else
            {
                sb.AppendLine($"  Fairness: starvation events {FairnessStarvationEvents:N0}");
            }
            sb.AppendLine($"  Bank Pressure: avoidance reorders {BankPressureAvoidanceCount:N0}");
            sb.AppendLine($"  Speculation Budget: exhaustion events {SpeculationBudgetExhaustionEvents:N0}, peak concurrent {PeakConcurrentSpeculativeOps:N0}");
            return sb.ToString();
        }

        /// <summary>
        /// Generate a compact Phase 3 execution summary for replay-aware reuse review.
        /// </summary>
        public string GeneratePhase3ExecutionSummary()
        {
            var sb = new StringBuilder();
            sb.AppendLine("Phase 3 Execution Contour:");
            sb.AppendLine($"  Replay-Aware FSP: {ReplayAwareCycles:N0} replay-aware cycles, {ReplayEpochCount:N0} epochs, stable donor ratio {StableDonorSlotRatio:P2}");
            sb.AppendLine($"  Phase-Certified Packing: {PhaseCertificateReadyHits:N0} hits / {PhaseCertificateReadyMisses:N0} misses ({PhaseCertificateReuseHitRate:P2}), est. {EstimatedPhaseCertificateChecksSaved:N0} checks saved");
            sb.AppendLine($"  Invalidation Surface: {PhaseCertificateInvalidations:N0} total, mutation {PhaseCertificateMutationInvalidations:N0}, phase mismatch {PhaseCertificatePhaseMismatchInvalidations:N0}");
            AppendEligibilityExecutionSummary(sb);
            return sb.ToString();
        }

        /// <summary>
        /// Generate a compact Phase 4 evidence summary for determinism-tax and isolation-stress review.
        /// </summary>
        public string GeneratePhase4EvidenceSummary(ReplayEnvelopeReport? envelopeReport = null)
        {
            var sb = new StringBuilder();
            sb.AppendLine("Phase 4 Evidence Baseline:");

            if (DeterminismReferenceOpportunitySlots > 0 || DeterminismMaskedSlots > 0 || DeterminismEstimatedLostSlots > 0)
            {
                sb.AppendLine($"  Determinism Tax: reference slots {DeterminismReferenceOpportunitySlots:N0}, replay-eligible {DeterminismReplayEligibleSlots:N0}, masked {DeterminismMaskedSlots:N0}, est. lost {DeterminismEstimatedLostSlots:N0} ({DeterminismTaxRate:P2}), constrained cycles {DeterminismConstrainedCycles:N0}");
            }

            if (DomainIsolationProbeAttempts > 0 || DomainIsolationBlockedAttempts > 0)
            {
                sb.AppendLine($"  Domain Isolation Stress: probes {DomainIsolationProbeAttempts:N0}, blocked {DomainIsolationBlockedAttempts:N0} ({DomainIsolationBlockRate:P2}), cross-domain {DomainIsolationCrossDomainBlocks:N0}, kernel-to-user {DomainIsolationKernelToUserBlocks:N0}");
            }

            if (envelopeReport.HasValue)
            {
                sb.AppendLine($"  Determinism Envelope: {envelopeReport.Value.Describe()}");
            }

            return sb.ToString();
        }

        /// <summary>
        /// Generate a richer Phase 4 regression snapshot that combines perf, trace, determinism, and envelope evidence.
        /// </summary>
        public string GeneratePhase4RegressionSnapshot(
            ReplayTraceEvidenceSummary traceSummary,
            ReplayDeterminismReport determinismReport,
            ReplayEnvelopeReport envelopeReport)
        {
            var sb = new StringBuilder();
            sb.Append(GeneratePhase4EvidenceSummary(envelopeReport));
            sb.AppendLine("Phase 4 Regression Snapshot:");
            sb.AppendLine($"  Opportunity Surface: retention {DeterminismOpportunityRetentionRate:P2}, tax rate {DeterminismTaxRate:P2}, masked slots {DeterminismMaskedSlots:N0}, constrained cycles {DeterminismConstrainedCycles:N0}");
            sb.AppendLine($"  Isolation Surface: block rate {DomainIsolationBlockRate:P2}, probes {DomainIsolationProbeAttempts:N0}, blocked {DomainIsolationBlockedAttempts:N0}");
            sb.AppendLine($"  Trace Evidence: {traceSummary.Describe()}");
            sb.AppendLine($"  Repeated Runs: {determinismReport.Describe()}");
            sb.AppendLine($"  Envelope Result: {envelopeReport.Describe()}");
            sb.AppendLine($"  Snapshot Verdict: baseline {(determinismReport.IsDeterministic ? "stable" : "diverged")}, envelope {(envelopeReport.IsWithinEnvelope ? "within bounds" : "out of envelope")}");
            return sb.ToString();
        }

        /// <summary>
        /// Generate a compact Phase 08 typed-slot telemetry summary.
        /// </summary>
        public string GeneratePhase8TypedSlotSummary()
        {
            var sb = new StringBuilder();
            sb.AppendLine("Typed-Slot Statistics (Phase 08):");

            long totalInjects = ClassFlexibleInjects + HardPinnedInjects;
            if (totalInjects > 0)
            {
                sb.AppendLine($"  Class-Flexible Injects: {ClassFlexibleInjects:N0} ({ClassFlexibleRatio:P1})");
                sb.AppendLine($"  Hard-Pinned Injects: {HardPinnedInjects:N0} ({1.0 - ClassFlexibleRatio:P1})");
            }

            long totalRejects = StaticClassOvercommitRejects + DynamicClassExhaustionRejects
                              + PinnedLaneConflicts + LateBindingConflicts
                              + NopDueToResourceConflict + TypedSlotDomainRejects;
            if (totalRejects > 0)
            {
                sb.AppendLine("  Reject Breakdown:");
                if (StaticClassOvercommitRejects > 0)
                    sb.AppendLine($"    StaticClassOvercommit: {StaticClassOvercommitRejects:N0}");
                if (DynamicClassExhaustionRejects > 0)
                    sb.AppendLine($"    DynamicClassExhaustion: {DynamicClassExhaustionRejects:N0}");
                if (PinnedLaneConflicts > 0)
                    sb.AppendLine($"    PinnedLaneConflict: {PinnedLaneConflicts:N0}");
                if (LateBindingConflicts > 0)
                    sb.AppendLine($"    LateBindingConflict: {LateBindingConflicts:N0}");
                if (NopDueToResourceConflict > 0)
                    sb.AppendLine($"    ResourceConflict: {NopDueToResourceConflict:N0}");
                if (TypedSlotDomainRejects > 0)
                    sb.AppendLine($"    DomainReject: {TypedSlotDomainRejects:N0}");
            }

            if (TotalLaneBindings > 0)
                sb.AppendLine($"  Lane Reuse Rate: {LaneReuseRate:P1}");
            if (ClassTemplateReuseHits > 0 || ClassTemplateInvalidations > 0)
                sb.AppendLine($"  Class Template: {ClassTemplateReuseHits:N0} hits, {ClassTemplateInvalidations:N0} invalidations");
            if (NopAvoided > 0 || NopDueToPinnedConstraint > 0 || NopDueToNoClassCapacity > 0)
                sb.AppendLine($"  NOP Reduction: {NopReductionRate:P1} avoided via class-flexible placement");

            return sb.ToString();
        }

        /// <summary>
        /// Generate human-readable summary
        /// </summary>
        public string GenerateSummary()
        {
            var sb = new StringBuilder();
            sb.AppendLine("=== HybridCPU Performance Report ===");
            sb.AppendLine();

            sb.AppendLine("Memory Subsystem:");
            sb.AppendLine($"  Total Bursts: {TotalBursts:N0}");
            sb.AppendLine($"  Total Bytes: {TotalBytesTransferred:N0} ({FormatBytes(TotalBytesTransferred)})");
            sb.AppendLine($"  Bank Conflicts: {BankConflicts:N0}");
            sb.AppendLine($"  Stall Cycles: {StallCycles:N0}");
            sb.AppendLine($"  DMA Transfers: {DmaTransfers:N0}");
            sb.AppendLine($"  Avg Burst Length: {AverageBurstLength:F2} bytes");
            sb.AppendLine($"  Total Wait Cycles: {TotalWaitCycles:N0}");
            sb.AppendLine($"  Avg Wait Cycles: {AverageWaitCycles:F2}");
            sb.AppendLine($"  Max Queue Depth: {MaxQueueDepth}");
            sb.AppendLine($"  Current Queued: {CurrentQueuedRequests}");
            sb.AppendLine();

            // Phase 3: Memory Wall metrics
            sb.AppendLine("Memory Wall (Phase 3):");
            sb.AppendLine($"  Total Memory Stalls: {TotalMemoryStalls:N0}");
            sb.AppendLine($"  Queue Full Events: {MemoryQueueFullEvents:N0}");
            sb.AppendLine($"  Bank Saturation Cycles: {BankSaturationCycles:N0}");
            sb.AppendLine($"  Idle Cycles: {IdleMemoryCycles:N0}");
            sb.AppendLine($"  Memory Utilization: {MemoryUtilization * 100:F2}%");
            sb.AppendLine($"  Avg Queue Depth: {AverageMemoryQueueDepth:F2}");
            sb.AppendLine($"  Avg Memory Latency: {AverageMemoryLatency:F2} cycles");
            sb.AppendLine($"  Load Stalls: {LoadStalls:N0}");
            sb.AppendLine($"  Store Stalls: {StoreStalls:N0}");
            sb.AppendLine($"  Burst Stalls: {BurstStalls:N0}");
            sb.AppendLine();

            // Phase 3: Burst timing metrics
            if (UnalignedAccessCount > 0 || BurstEfficiency > 0)
            {
                sb.AppendLine("Burst Timing & Efficiency (Phase 3):");
                sb.AppendLine($"  Unaligned Accesses: {UnalignedAccessCount:N0}");
                sb.AppendLine($"  Total Alignment Penalty: {TotalAlignmentPenalty:N0} cycles");
                sb.AppendLine($"  Avg Alignment Penalty: {AverageAlignmentPenalty:F2} cycles");
                sb.AppendLine($"  Burst Efficiency: {BurstEfficiency * 100:F2}%");
                sb.AppendLine();
            }

            sb.AppendLine("DMA Controller:");
            sb.AppendLine($"  Active Transfers: {ActiveDmaTransfers}");
            sb.AppendLine($"  Completed Transfers: {CompletedDmaTransfers:N0}");
            sb.AppendLine($"  Total Latency: {TotalDmaLatency:N0} cycles");
            sb.AppendLine($"  Errors: {DmaErrors:N0}");
            sb.AppendLine($"  Bytes Transferred: {DmaBytesTransferred:N0} ({FormatBytes(DmaBytesTransferred)})");
            if (CompletedDmaTransfers > 0)
            {
                sb.AppendLine($"  Avg Latency: {TotalDmaLatency / CompletedDmaTransfers:N0} cycles/transfer");
            }
            sb.AppendLine();

            sb.AppendLine("Pipeline:");
            sb.AppendLine($"  Total Instructions: {TotalInstructions:N0}");
            sb.AppendLine($"  Total Cycles: {TotalCycles:N0}");
            sb.AppendLine($"  IPC: {IPC:F3}");
            sb.AppendLine($"  Pipeline Stalls: {PipelineStalls:N0}");
            sb.AppendLine($"  Branch Mispredictions: {BranchMispredictions:N0}");
            sb.AppendLine();

            sb.AppendLine("Vector Engine:");
            sb.AppendLine($"  Vector Operations: {VectorOperations:N0}");
            sb.AppendLine($"  Elements Processed: {VectorElementsProcessed:N0}");
            sb.AppendLine($"  Vector Exceptions: {VectorExceptions:N0}");
            sb.AppendLine();

            // Phase 3: Scheduler metrics
            if (SuccessfulInjections > 0 || RejectedInjections > 0)
            {
                sb.AppendLine("MicroOp Scheduler (Phase 3):");
                sb.AppendLine($"  Successful Injections: {SuccessfulInjections:N0}");
                sb.AppendLine($"  Rejected Injections: {RejectedInjections:N0}");
                long totalAttempts = SuccessfulInjections + RejectedInjections;
                if (totalAttempts > 0)
                {
                    sb.AppendLine($"  Rejection Rate: {RejectedInjections * 100.0 / totalAttempts:F2}%");
                }
                sb.AppendLine($"  FSP Contribution: {FSPContribution * 100:F2}%");
                sb.AppendLine($"  Scheduler Utilization: {SchedulerUtilization * 100:F2}%");
                sb.AppendLine($"  Avg Ready Queue Depth: {AverageReadyQueueDepth:F2}");
                sb.AppendLine($"  Bank Contention Stalls: {BankContentionStalls:N0}");
                sb.AppendLine($"  FSP Pipeline Latency Cycles: {FspPipelineLatencyCycles:N0}");
                sb.AppendLine();
            }

            if (HasPhase1ValidationTelemetry)
            {
                sb.Append(GeneratePhase1ValidationSummary());
                sb.AppendLine();
            }

            if (HasPhase3ReuseTelemetry)
            {
                sb.Append(GeneratePhase3ExecutionSummary());
                sb.AppendLine();
            }

            if (HasPhase4EvidenceTelemetry)
            {
                sb.Append(GeneratePhase4EvidenceSummary());
                sb.AppendLine();
            }

            if (HasPhase5OracleGapTelemetry)
            {
                sb.Append(GeneratePhase5OracleGapSummary());
                sb.AppendLine();
            }

            if (HasPhase2PolicyTelemetry)
            {
                sb.Append(GeneratePhase2PolicySummary());
                sb.AppendLine();
            }

            if (HasPhase8TypedSlotTelemetry)
            {
                sb.Append(GeneratePhase8TypedSlotSummary());
                sb.AppendLine();
            }

            // Q1 Review §5: Stream Register File ingress/bypass metrics
            if (HasStreamIngressWarmTelemetry)
            {
                sb.AppendLine("Stream Register File (Q1 §5):");
                sb.AppendLine($"  L1 Bypass Hits: {L1BypassHits:N0}");
                if (ForegroundWarmAttempts > 0 || ForegroundWarmSuccesses > 0 || ForegroundWarmReuseHits > 0 || ForegroundBypassHits > 0)
                {
                    sb.AppendLine($"  Foreground Warm: {ForegroundWarmSuccesses:N0}/{ForegroundWarmAttempts:N0} success, {ForegroundWarmReuseHits:N0} reuse, {ForegroundBypassHits:N0} bypass");
                }

                if (AssistWarmAttempts > 0 || AssistWarmSuccesses > 0 || AssistWarmReuseHits > 0 || AssistBypassHits > 0)
                {
                    sb.AppendLine($"  Assist Warm: {AssistWarmSuccesses:N0}/{AssistWarmAttempts:N0} success, {AssistWarmReuseHits:N0} reuse, {AssistBypassHits:N0} bypass");
                }

                if (StreamWarmTranslationRejects > 0 || StreamWarmBackendRejects > 0 || AssistWarmResidentBudgetRejects > 0 || AssistWarmLoadingBudgetRejects > 0 || AssistWarmNoVictimRejects > 0)
                {
                    sb.AppendLine($"  Warm Rejects: translation {StreamWarmTranslationRejects:N0}, backend {StreamWarmBackendRejects:N0}, resident-budget {AssistWarmResidentBudgetRejects:N0}, loading-budget {AssistWarmLoadingBudgetRejects:N0}, no-victim {AssistWarmNoVictimRejects:N0}");
                }

                sb.AppendLine();
            }

            AppendBurstTraceSummary(sb);

            return sb.ToString();
        }

        /// <summary>
        /// Export to CSV format
        /// </summary>
        public string ExportToCSV()
        {
            var sb = new StringBuilder();
            sb.AppendLine("Category,Metric,Value");

            // Memory
            sb.AppendLine($"Memory,TotalBursts,{TotalBursts}");
            sb.AppendLine($"Memory,TotalBytesTransferred,{TotalBytesTransferred}");
            sb.AppendLine($"Memory,BankConflicts,{BankConflicts}");
            sb.AppendLine($"Memory,StallCycles,{StallCycles}");
            sb.AppendLine($"Memory,DmaTransfers,{DmaTransfers}");
            sb.AppendLine($"Memory,AverageBurstLength,{AverageBurstLength}");
            sb.AppendLine($"Memory,TotalWaitCycles,{TotalWaitCycles}");
            sb.AppendLine($"Memory,AverageWaitCycles,{AverageWaitCycles}");
            sb.AppendLine($"Memory,MaxQueueDepth,{MaxQueueDepth}");
            sb.AppendLine($"Memory,CurrentQueuedRequests,{CurrentQueuedRequests}");

            // Phase 3: Memory wall
            sb.AppendLine($"MemoryWall,TotalMemoryStalls,{TotalMemoryStalls}");
            sb.AppendLine($"MemoryWall,MemoryQueueFullEvents,{MemoryQueueFullEvents}");
            sb.AppendLine($"MemoryWall,BankSaturationCycles,{BankSaturationCycles}");
            sb.AppendLine($"MemoryWall,IdleMemoryCycles,{IdleMemoryCycles}");
            sb.AppendLine($"MemoryWall,MemoryUtilization,{MemoryUtilization}");
            sb.AppendLine($"MemoryWall,AverageMemoryQueueDepth,{AverageMemoryQueueDepth}");
            sb.AppendLine($"MemoryWall,AverageMemoryLatency,{AverageMemoryLatency}");
            sb.AppendLine($"MemoryWall,LoadStalls,{LoadStalls}");
            sb.AppendLine($"MemoryWall,StoreStalls,{StoreStalls}");
            sb.AppendLine($"MemoryWall,BurstStalls,{BurstStalls}");

            // Phase 3: Burst timing
            sb.AppendLine($"BurstTiming,UnalignedAccessCount,{UnalignedAccessCount}");
            sb.AppendLine($"BurstTiming,TotalAlignmentPenalty,{TotalAlignmentPenalty}");
            sb.AppendLine($"BurstTiming,AverageAlignmentPenalty,{AverageAlignmentPenalty}");
            sb.AppendLine($"BurstTiming,BurstEfficiency,{BurstEfficiency}");

            // DMA
            sb.AppendLine($"DMA,ActiveTransfers,{ActiveDmaTransfers}");
            sb.AppendLine($"DMA,CompletedTransfers,{CompletedDmaTransfers}");
            sb.AppendLine($"DMA,TotalLatency,{TotalDmaLatency}");
            sb.AppendLine($"DMA,Errors,{DmaErrors}");
            sb.AppendLine($"DMA,BytesTransferred,{DmaBytesTransferred}");

            // Pipeline
            sb.AppendLine($"Pipeline,TotalInstructions,{TotalInstructions}");
            sb.AppendLine($"Pipeline,TotalCycles,{TotalCycles}");
            sb.AppendLine($"Pipeline,IPC,{IPC}");
            sb.AppendLine($"Pipeline,PipelineStalls,{PipelineStalls}");
            sb.AppendLine($"Pipeline,BranchMispredictions,{BranchMispredictions}");

            // Vector
            sb.AppendLine($"Vector,VectorOperations,{VectorOperations}");
            sb.AppendLine($"Vector,ElementsProcessed,{VectorElementsProcessed}");
            sb.AppendLine($"Vector,VectorExceptions,{VectorExceptions}");

            // Phase 3: Scheduler
            sb.AppendLine($"Scheduler,TotalSchedulerCycles,{TotalSchedulerCycles}");
            sb.AppendLine($"Scheduler,SuccessfulInjections,{SuccessfulInjections}");
            sb.AppendLine($"Scheduler,RejectedInjections,{RejectedInjections}");
            sb.AppendLine($"Scheduler,RejectedDueToHazards,{RejectedDueToHazards}");
            sb.AppendLine($"Scheduler,RejectedDueToMemConflict,{RejectedDueToMemConflict}");
            sb.AppendLine($"Scheduler,RollbackEvents,{RollbackEvents}");
            sb.AppendLine($"Scheduler,AverageReadyQueueDepth,{AverageReadyQueueDepth}");
            sb.AppendLine($"Scheduler,MaxReadyQueueDepth,{MaxReadyQueueDepth}");
            sb.AppendLine($"Scheduler,ThreadStarvationEvents,{ThreadStarvationEvents}");
            sb.AppendLine($"Scheduler,SchedulerUtilization,{SchedulerUtilization}");
            sb.AppendLine($"Scheduler,FSPContribution,{FSPContribution}");
            sb.AppendLine($"Scheduler,TotalSchedulerLatency,{TotalSchedulerLatency}");
            sb.AppendLine($"Scheduler,AverageSchedulerLatency,{AverageSchedulerLatency}");
            sb.AppendLine($"Scheduler,BankContentionStalls,{BankContentionStalls}");
            sb.AppendLine($"Scheduler,FspPipelineLatencyCycles,{FspPipelineLatencyCycles}");
            sb.AppendLine($"Scheduler,ReplayEpochCount,{ReplayEpochCount}");
            sb.AppendLine($"Scheduler,AverageReplayEpochLength,{AverageReplayEpochLength}");
            sb.AppendLine($"Scheduler,StableDonorSlotRatio,{StableDonorSlotRatio}");
            sb.AppendLine($"Scheduler,ReplayAwareCycles,{ReplayAwareCycles}");
            sb.AppendLine($"Scheduler,PhaseCertificateReadyHits,{PhaseCertificateReadyHits}");
            sb.AppendLine($"Scheduler,PhaseCertificateReadyMisses,{PhaseCertificateReadyMisses}");
            sb.AppendLine($"Scheduler,PhaseCertificateReuseHitRate,{PhaseCertificateReuseHitRate}");
            sb.AppendLine($"Scheduler,EstimatedPhaseCertificateChecksSaved,{EstimatedPhaseCertificateChecksSaved}");
            sb.AppendLine($"Scheduler,PhaseCertificateInvalidations,{PhaseCertificateInvalidations}");
            sb.AppendLine($"Scheduler,PhaseCertificateInvalidationRate,{PhaseCertificateInvalidationRate}");
            sb.AppendLine($"Scheduler,PhaseCertificateMutationInvalidations,{PhaseCertificateMutationInvalidations}");
            sb.AppendLine($"Scheduler,PhaseCertificatePhaseMismatchInvalidations,{PhaseCertificatePhaseMismatchInvalidations}");
            sb.AppendLine($"Scheduler,DeterministicReplayTransitions,{DeterministicReplayTransitions}");
            sb.AppendLine($"Scheduler,DeterminismReferenceOpportunitySlots,{DeterminismReferenceOpportunitySlots}");
            sb.AppendLine($"Scheduler,DeterminismReplayEligibleSlots,{DeterminismReplayEligibleSlots}");
            sb.AppendLine($"Scheduler,DeterminismMaskedSlots,{DeterminismMaskedSlots}");
            sb.AppendLine($"Scheduler,DeterminismEstimatedLostSlots,{DeterminismEstimatedLostSlots}");
            sb.AppendLine($"Scheduler,DeterminismConstrainedCycles,{DeterminismConstrainedCycles}");
            sb.AppendLine($"Scheduler,DeterminismTaxRate,{DeterminismTaxRate}");
            sb.AppendLine($"Scheduler,DomainIsolationProbeAttempts,{DomainIsolationProbeAttempts}");
            sb.AppendLine($"Scheduler,DomainIsolationBlockedAttempts,{DomainIsolationBlockedAttempts}");
            sb.AppendLine($"Scheduler,DomainIsolationCrossDomainBlocks,{DomainIsolationCrossDomainBlocks}");
            sb.AppendLine($"Scheduler,DomainIsolationKernelToUserBlocks,{DomainIsolationKernelToUserBlocks}");
            sb.AppendLine($"Scheduler,DomainIsolationBlockRate,{DomainIsolationBlockRate}");

            // Phase 5: Oracle Gap
            sb.AppendLine($"OracleGap,TotalOracleSlots,{OracleGapTotalOracleSlots}");
            sb.AppendLine($"OracleGap,TotalRealSlots,{OracleGapTotalRealSlots}");
            sb.AppendLine($"OracleGap,DonorRestriction,{OracleGapDonorRestriction}");
            sb.AppendLine($"OracleGap,FairnessOrdering,{OracleGapFairnessOrdering}");
            sb.AppendLine($"OracleGap,LegalityConservatism,{OracleGapLegalityConservatism}");
            sb.AppendLine($"OracleGap,DomainIsolation,{OracleGapDomainIsolation}");
            sb.AppendLine($"OracleGap,SpeculationBudget,{OracleGapSpeculationBudget}");
            sb.AppendLine($"OracleGap,CyclesWithGap,{OracleGapCyclesWithGap}");
            sb.AppendLine($"OracleGap,TotalCyclesAnalyzed,{OracleGapTotalCyclesAnalyzed}");
            sb.AppendLine($"OracleGap,OracleEfficiency,{OracleEfficiency}");
            sb.AppendLine($"OracleGap,GapRate,{OracleGapRate}");
            sb.AppendLine($"OracleGap,CounterexampleCount,{OracleCounterexampleCount}");
            sb.AppendLine($"OracleGap,CounterexampleTotalMissedSlots,{OracleCounterexampleTotalMissedSlots}");

            // Phase 2: Fairness / Pressure / Speculation Budget
            sb.AppendLine($"Scheduler,FairnessStarvationEvents,{FairnessStarvationEvents}");
            sb.AppendLine($"Scheduler,BankPressureAvoidanceCount,{BankPressureAvoidanceCount}");
            sb.AppendLine($"Scheduler,SpeculationBudgetExhaustionEvents,{SpeculationBudgetExhaustionEvents}");
            sb.AppendLine($"Scheduler,PeakConcurrentSpeculativeOps,{PeakConcurrentSpeculativeOps}");
            if (PerVtInjections != null)
            {
                for (int vt = 0; vt < PerVtInjections.Length && vt < 4; vt++)
                    sb.AppendLine($"Scheduler,PerVtInjections_VT{vt},{PerVtInjections[vt]}");
            }

            // Q1 Review §5: Stream Register File
            sb.AppendLine($"StreamRegFile,L1BypassHits,{L1BypassHits}");
            sb.AppendLine($"StreamRegFile,ForegroundWarmAttempts,{ForegroundWarmAttempts}");
            sb.AppendLine($"StreamRegFile,ForegroundWarmSuccesses,{ForegroundWarmSuccesses}");
            sb.AppendLine($"StreamRegFile,ForegroundWarmReuseHits,{ForegroundWarmReuseHits}");
            sb.AppendLine($"StreamRegFile,ForegroundBypassHits,{ForegroundBypassHits}");
            sb.AppendLine($"StreamRegFile,AssistWarmAttempts,{AssistWarmAttempts}");
            sb.AppendLine($"StreamRegFile,AssistWarmSuccesses,{AssistWarmSuccesses}");
            sb.AppendLine($"StreamRegFile,AssistWarmReuseHits,{AssistWarmReuseHits}");
            sb.AppendLine($"StreamRegFile,AssistBypassHits,{AssistBypassHits}");
            sb.AppendLine($"StreamRegFile,StreamWarmTranslationRejects,{StreamWarmTranslationRejects}");
            sb.AppendLine($"StreamRegFile,StreamWarmBackendRejects,{StreamWarmBackendRejects}");
            sb.AppendLine($"StreamRegFile,AssistWarmResidentBudgetRejects,{AssistWarmResidentBudgetRejects}");
            sb.AppendLine($"StreamRegFile,AssistWarmLoadingBudgetRejects,{AssistWarmLoadingBudgetRejects}");
            sb.AppendLine($"StreamRegFile,AssistWarmNoVictimRejects,{AssistWarmNoVictimRejects}");

            return sb.ToString();
        }

        /// <summary>
        /// Export scheduler metrics (Phase 3)
        /// </summary>
        public string ExportSchedulerMetrics()
        {
            var sb = new StringBuilder();
            sb.AppendLine("Scheduler Metrics:");
            sb.AppendLine($"Total Cycles: {TotalSchedulerCycles:N0}");
            sb.AppendLine($"Successful Injections: {SuccessfulInjections:N0}");

            long totalAttempts = SuccessfulInjections + RejectedInjections;
            if (totalAttempts > 0)
            {
                sb.AppendLine($"Rejection Rate: {RejectedInjections * 100.0 / totalAttempts:F2}%");
            }

            sb.AppendLine($"Hazard Rejections: {RejectedDueToHazards:N0}");
            sb.AppendLine($"Memory Conflict Rejections: {RejectedDueToMemConflict:N0}");
            sb.AppendLine($"FSP Contribution: {FSPContribution * 100:F2}%");
            sb.AppendLine($"Rollback Events: {RollbackEvents:N0}");
            sb.AppendLine($"Scheduler Utilization: {SchedulerUtilization * 100:F2}%");
            sb.AppendLine($"Avg Ready Queue Depth: {AverageReadyQueueDepth:F2}");
            sb.AppendLine($"Max Ready Queue Depth: {MaxReadyQueueDepth}");
            sb.AppendLine($"Thread Starvation Events: {ThreadStarvationEvents:N0}");

            if (HasPhase4EvidenceTelemetry)
            {
                sb.AppendLine($"Determinism Tax Rate: {DeterminismTaxRate:P2}");
                sb.AppendLine($"Determinism Masked Slots: {DeterminismMaskedSlots:N0}");
                sb.AppendLine($"Domain Isolation Block Rate: {DomainIsolationBlockRate:P2}");
            }

            if (TotalSchedulerCycles > 0)
            {
                sb.AppendLine($"Avg Scheduler Latency: {AverageSchedulerLatency:F2} cycles/bundle");
            }

            return sb.ToString();
        }

        /// <summary>
        /// Generate a compact Phase 5 oracle-gap summary for research-grade evidence review.
        /// </summary>
        public string GeneratePhase5OracleGapSummary()
        {
            var sb = new StringBuilder();
            sb.AppendLine("Phase 5 Oracle Gap Analysis:");

            if (OracleGapTotalCyclesAnalyzed > 0)
            {
                long totalGap = OracleGapTotalOracleSlots - OracleGapTotalRealSlots;
                sb.AppendLine($"  Oracle Contour: {OracleGapTotalOracleSlots:N0} oracle slots, {OracleGapTotalRealSlots:N0} real slots, gap {totalGap:N0} ({OracleGapRate:P2})");
                sb.AppendLine($"  Gap Cycles: {OracleGapCyclesWithGap:N0} / {OracleGapTotalCyclesAnalyzed:N0} cycles");
                sb.AppendLine($"  Gap Decomposition: donor restriction {OracleGapDonorRestriction:N0}, fairness ordering {OracleGapFairnessOrdering:N0}, legality conservatism {OracleGapLegalityConservatism:N0}, domain isolation {OracleGapDomainIsolation:N0}, speculation budget {OracleGapSpeculationBudget:N0}");
            }

            if (OracleCounterexampleCount > 0)
            {
                sb.AppendLine($"  Counterexamples: {OracleCounterexampleCount} captured, {OracleCounterexampleTotalMissedSlots:N0} total missed slots, dominant category: {OracleCounterexampleDominantCategory ?? "none"}");
            }

            return sb.ToString();
        }

        private static string FormatBytes(long bytes)
        {
            string[] sizes = { "B", "KB", "MB", "GB", "TB" };
            double len = bytes;
            int order = 0;
            while (len >= 1024 && order < sizes.Length - 1)
            {
                order++;
                len = len / 1024;
            }
            return $"{len:F2} {sizes[order]}";
        }

        private static bool ApproximatelyEqual(double left, double right, double tolerance = 0.0001)
        {
            return Math.Abs(left - right) <= tolerance;
        }

        #endregion
    }
}
