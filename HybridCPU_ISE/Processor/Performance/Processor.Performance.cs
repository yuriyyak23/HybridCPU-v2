using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;

namespace YAKSys_Hybrid_CPU
{
    public partial struct Processor
    {
        // Burst trace collection for detailed profiling
        private static List<PerformanceReport.BurstTrace>? burstTraces = null;

        /// <summary>
        /// Memory wall performance counters (Phase 3)
        /// Tracks pipeline stalls due to memory subsystem limitations
        /// </summary>
        public struct MemoryWallCounters
        {
            // Back-pressure events
            public long TotalMemoryStalls;        // Cycles stalled waiting for memory
            public long MemoryQueueFullEvents;    // Times queue was full
            public long BankSaturationCycles;     // Cycles with all banks busy

            // Memory bandwidth utilization
            public long IdleMemoryCycles;         // Cycles with no memory activity
            public double MemoryUtilization;      // % of cycles with active memory ops

            // Queue statistics
            public int MaxQueueDepth;             // Deepest queue observed
            public double AverageQueueDepth;      // Average across all samples
            public long QueueOverflowEvents;      // Times requests were dropped

            // Latency breakdown
            public long TotalMemoryLatencyCycles; // Sum of all latencies
            public long MemoryRequestCount;       // Total requests
            public double AverageMemoryLatency;   // Average cycles per request

            // By operation type
            public long LoadStalls;               // Stalls on loads
            public long StoreStalls;              // Stalls on stores
            public long BurstStalls;              // Stalls on burst ops

            // Sampling state
            public long TotalSamples;             // Number of samples taken
        }

        public static MemoryWallCounters MemWallStats;

        /// <summary>
        /// Configure profiling options (ref3.md - PerfModel)
        /// </summary>
        public static void ConfigureProfiling(bool enable, ProfilingOptions? opts = null)
        {
            ProfilingEnabled = enable;
            CurrentProfilingOptions = opts ?? ProfilingOptions.Default();

            if (enable)
            {
                // Initialize burst trace collection if requested
                if (CurrentProfilingOptions.CollectBurstTraces)
                {
                    burstTraces = new List<PerformanceReport.BurstTrace>();

                    // Subscribe to memory subsystem events
                    if (Memory != null)
                    {
                        Memory.BurstStarted += OnBurstStarted;
                        Memory.BurstCompleted += OnBurstCompleted;
                    }
                }
            }
            else
            {
                // Unsubscribe from events
                if (Memory != null)
                {
                    Memory.BurstStarted -= OnBurstStarted;
                    Memory.BurstCompleted -= OnBurstCompleted;
                }

                burstTraces = null;
            }
        }

        /// <summary>
        /// Get comprehensive performance statistics (ref3.md - PerfModel)
        /// </summary>
        public static PerformanceReport GetPerformanceStats()
        {
            var report = new PerformanceReport();

            // Collect Memory Subsystem statistics
            if (Memory != null && (CurrentProfilingOptions?.CollectMemoryStats ?? false))
            {
                report.TotalBursts = Memory.TotalBursts;
                report.TotalBytesTransferred = Memory.TotalBytesTransferred;
                report.BankConflicts = Memory.BankConflicts;
                report.StallCycles = Memory.StallCycles;
                report.DmaTransfers = Memory.DmaTransfers;
                report.AverageBurstLength = Memory.AverageBurstLength;
                report.TotalWaitCycles = Memory.TotalWaitCycles;
                report.AverageWaitCycles = Memory.AverageWaitCycles;
                report.MaxQueueDepth = Memory.MaxQueueDepth;
                report.CurrentQueuedRequests = Memory.CurrentQueuedRequests;

                // Phase 3: Memory wall counters
                report.TotalMemoryStalls = MemWallStats.TotalMemoryStalls;
                report.MemoryQueueFullEvents = MemWallStats.MemoryQueueFullEvents;
                report.BankSaturationCycles = MemWallStats.BankSaturationCycles;
                report.IdleMemoryCycles = MemWallStats.IdleMemoryCycles;
                report.MemoryUtilization = MemWallStats.MemoryUtilization;
                report.AverageMemoryQueueDepth = MemWallStats.AverageQueueDepth;
                report.QueueOverflowEvents = MemWallStats.QueueOverflowEvents;
                report.AverageMemoryLatency = MemWallStats.AverageMemoryLatency;
                report.LoadStalls = MemWallStats.LoadStalls;
                report.StoreStalls = MemWallStats.StoreStalls;
                report.BurstStalls = MemWallStats.BurstStalls;

                // Phase 3: Burst timing metrics
                report.UnalignedAccessCount = Memory.UnalignedAccessCount;
                report.TotalAlignmentPenalty = Memory.TotalAlignmentPenalty;
                report.BurstEfficiency = Memory.BurstEfficiency;
            }

            // Collect DMA Controller statistics
            if (DMAController != null && (CurrentProfilingOptions?.CollectDMATransfers ?? false))
            {
                var dmaStats = DMAController.GetStatistics();
                report.DmaBytesTransferred = (long)dmaStats.totalBytes;
                report.CompletedDmaTransfers = (long)dmaStats.totalTransfers;
                report.DmaErrors = (long)dmaStats.totalErrors;

                // Note: ActiveTransfers and TotalLatency would require extending DMAController
                report.ActiveDmaTransfers = 0; // Placeholder
                report.TotalDmaLatency = 0;     // Placeholder
            }

            // Collect Pipeline statistics from CPU_Core[0]
            if (CurrentProfilingOptions?.CollectPipelineStats ?? false)
            {
                if (CPU_Cores != null && CPU_Cores.Length > 0)
                {
                    var pc = CPU_Cores[0].GetPipelineControl();
                    report.TotalInstructions = (long)pc.InstructionsRetired;
                    report.TotalCycles = (long)pc.CycleCount;
                    report.PipelineStalls = (long)pc.StallCycles;
                    report.BranchMispredictions = (long)pc.BranchMispredicts;

                    var replayMetrics = CPU_Cores[0].GetReplayPhaseMetrics();
                    var schedulerPhaseMetrics = CPU_Cores[0].GetSchedulerPhaseMetrics();
                    report.ReplayEpochCount = (long)replayMetrics.ReplayEpochCount;
                    report.AverageReplayEpochLength = replayMetrics.AverageEpochLength;
                    report.StableDonorSlotRatio = replayMetrics.StableDonorSlotRatio;
                    report.DeterministicReplayTransitions = (long)replayMetrics.DeterministicTransitionCount;
                    report.ReplayAwareCycles = schedulerPhaseMetrics.ReplayAwareCycles;
                    report.PhaseCertificateReadyHits = schedulerPhaseMetrics.PhaseCertificateReadyHits;
                    report.PhaseCertificateReadyMisses = schedulerPhaseMetrics.PhaseCertificateReadyMisses;
                    report.EstimatedPhaseCertificateChecksSaved = schedulerPhaseMetrics.EstimatedChecksSaved;
                    report.PhaseCertificateInvalidations = schedulerPhaseMetrics.PhaseCertificateInvalidations;
                    report.PhaseCertificateMutationInvalidations = schedulerPhaseMetrics.PhaseCertificateMutationInvalidations;
                    report.PhaseCertificatePhaseMismatchInvalidations = schedulerPhaseMetrics.PhaseCertificatePhaseMismatchInvalidations;
                    report.DeterminismReferenceOpportunitySlots = schedulerPhaseMetrics.DeterminismReferenceOpportunitySlots;
                    report.DeterminismReplayEligibleSlots = schedulerPhaseMetrics.DeterminismReplayEligibleSlots;
                    report.DeterminismMaskedSlots = schedulerPhaseMetrics.DeterminismMaskedSlots;
                    report.DeterminismEstimatedLostSlots = schedulerPhaseMetrics.DeterminismEstimatedLostSlots;
                    report.DeterminismConstrainedCycles = schedulerPhaseMetrics.DeterminismConstrainedCycles;
                    report.DomainIsolationProbeAttempts = schedulerPhaseMetrics.DomainIsolationProbeAttempts;
                    report.DomainIsolationBlockedAttempts = schedulerPhaseMetrics.DomainIsolationBlockedAttempts;
                    report.DomainIsolationCrossDomainBlocks = schedulerPhaseMetrics.DomainIsolationCrossDomainBlocks;
                    report.DomainIsolationKernelToUserBlocks = schedulerPhaseMetrics.DomainIsolationKernelToUserBlocks;
                    report.EligibilityMaskedCycles = schedulerPhaseMetrics.EligibilityMaskedCycles;
                    report.EligibilityMaskedReadyCandidates = schedulerPhaseMetrics.EligibilityMaskedReadyCandidates;
                    report.LastEligibilityRequestedMask = schedulerPhaseMetrics.LastEligibilityRequestedMask;
                    report.LastEligibilityNormalizedMask = schedulerPhaseMetrics.LastEligibilityNormalizedMask;
                    report.LastEligibilityReadyPortMask = schedulerPhaseMetrics.LastEligibilityReadyPortMask;
                    report.LastEligibilityVisibleReadyMask = schedulerPhaseMetrics.LastEligibilityVisibleReadyMask;
                    report.LastEligibilityMaskedReadyMask = schedulerPhaseMetrics.LastEligibilityMaskedReadyMask;

                    // Phase 08: typed-slot telemetry
                    report.ClassTemplateReuseHits = schedulerPhaseMetrics.ClassTemplateReuseHits;
                    report.ClassTemplateInvalidations = schedulerPhaseMetrics.ClassTemplateInvalidations;
                    report.TypedSlotFastPathAccepts = schedulerPhaseMetrics.TypedSlotFastPathAccepts;
                    report.TypedSlotStandardPathAccepts = schedulerPhaseMetrics.TypedSlotStandardPathAccepts;
                    report.TotalLaneBindings = schedulerPhaseMetrics.TotalLaneBindings;
                    report.LaneReuseHits = schedulerPhaseMetrics.LaneReuseHits;
                    report.LaneReuseMisses = schedulerPhaseMetrics.LaneReuseMisses;
                    report.AluClassInjects = schedulerPhaseMetrics.AluClassInjects;
                    report.LsuClassInjects = schedulerPhaseMetrics.LsuClassInjects;
                    report.DmaStreamClassInjects = schedulerPhaseMetrics.DmaStreamClassInjects;
                    report.BranchControlInjects = schedulerPhaseMetrics.BranchControlInjects;
                    report.HardPinnedInjects = schedulerPhaseMetrics.HardPinnedInjects;
                    report.ClassFlexibleInjects = schedulerPhaseMetrics.ClassFlexibleInjects;
                    report.NopAvoided = schedulerPhaseMetrics.NopAvoided;
                    report.NopDueToPinnedConstraint = schedulerPhaseMetrics.NopDueToPinnedConstraint;
                    report.NopDueToNoClassCapacity = schedulerPhaseMetrics.NopDueToNoClassCapacity;
                    report.NopDueToResourceConflict = schedulerPhaseMetrics.NopDueToResourceConflict;
                    report.NopDueToDynamicState = schedulerPhaseMetrics.NopDueToDynamicState;
                    report.StaticClassOvercommitRejects = schedulerPhaseMetrics.StaticClassOvercommitRejects;
                    report.DynamicClassExhaustionRejects = schedulerPhaseMetrics.DynamicClassExhaustionRejects;
                    report.PinnedLaneConflicts = schedulerPhaseMetrics.PinnedLaneConflicts;
                    report.LateBindingConflicts = schedulerPhaseMetrics.LateBindingConflicts;
                    report.TypedSlotDomainRejects = schedulerPhaseMetrics.TypedSlotDomainRejects;
                    report.SmtOwnerContextGuardRejects = schedulerPhaseMetrics.SmtOwnerContextGuardRejects;
                    report.SmtDomainGuardRejects = schedulerPhaseMetrics.SmtDomainGuardRejects;
                    report.SmtBoundaryGuardRejects = schedulerPhaseMetrics.SmtBoundaryGuardRejects;
                    report.SmtSharedResourceCertificateRejects = schedulerPhaseMetrics.SmtSharedResourceCertificateRejects;
                    report.SmtRegisterGroupCertificateRejects = schedulerPhaseMetrics.SmtRegisterGroupCertificateRejects;
                    report.LastSmtLegalityRejectKind = schedulerPhaseMetrics.LastSmtLegalityRejectKind;
                    report.LastSmtLegalityAuthoritySource = schedulerPhaseMetrics.LastSmtLegalityAuthoritySource;
                    report.SmtLegalityRejectByAluClass = schedulerPhaseMetrics.SmtLegalityRejectByAluClass;
                    report.SmtLegalityRejectByLsuClass = schedulerPhaseMetrics.SmtLegalityRejectByLsuClass;
                    report.SmtLegalityRejectByDmaStreamClass = schedulerPhaseMetrics.SmtLegalityRejectByDmaStreamClass;
                    report.SmtLegalityRejectByBranchControl = schedulerPhaseMetrics.SmtLegalityRejectByBranchControl;
                    report.SmtLegalityRejectBySystemSingleton = schedulerPhaseMetrics.SmtLegalityRejectBySystemSingleton;
                    report.ClassTemplateDomainInvalidations = schedulerPhaseMetrics.ClassTemplateDomainInvalidations;
                    report.ClassTemplateCapacityMismatchInvalidations = schedulerPhaseMetrics.ClassTemplateCapacityMismatchInvalidations;
                }
            }

            // Collect Vector Engine statistics
            if (CurrentProfilingOptions?.CollectVectorStats ?? false)
            {
                // Note: These would come from StreamEngine/VectorALU tracking
                // Placeholder values for now
                report.VectorOperations = 0;
                report.VectorElementsProcessed = 0;
                report.VectorExceptions = 0;
            }

            // Q1 Review §5: Stream Register File bypass metrics
            if (Memory?.StreamRegisters != null)
            {
                var srfStats = Memory.StreamRegisters.GetStatistics();
                var ingressTelemetry = Memory.StreamRegisters.GetIngressWarmTelemetry();
                report.L1BypassHits = (long)srfStats.l1BypassHits;
                report.ForegroundWarmAttempts = (long)ingressTelemetry.ForegroundWarmAttempts;
                report.ForegroundWarmSuccesses = (long)ingressTelemetry.ForegroundWarmSuccesses;
                report.ForegroundWarmReuseHits = (long)ingressTelemetry.ForegroundWarmReuseHits;
                report.ForegroundBypassHits = (long)ingressTelemetry.ForegroundBypassHits;
                report.AssistWarmAttempts = (long)ingressTelemetry.AssistWarmAttempts;
                report.AssistWarmSuccesses = (long)ingressTelemetry.AssistWarmSuccesses;
                report.AssistWarmReuseHits = (long)ingressTelemetry.AssistWarmReuseHits;
                report.AssistBypassHits = (long)ingressTelemetry.AssistBypassHits;
                report.StreamWarmTranslationRejects = (long)ingressTelemetry.TranslationRejects;
                report.StreamWarmBackendRejects = (long)ingressTelemetry.BackendRejects;
                report.AssistWarmResidentBudgetRejects = (long)ingressTelemetry.AssistResidentBudgetRejects;
                report.AssistWarmLoadingBudgetRejects = (long)ingressTelemetry.AssistLoadingBudgetRejects;
                report.AssistWarmNoVictimRejects = (long)ingressTelemetry.AssistNoVictimRejects;
            }

            // Attach burst traces if collected
            if (CurrentProfilingOptions?.CollectBurstTraces ?? false)
            {
                report.BurstTraces = burstTraces;
            }

            return report;
        }

        /// <summary>
        /// Dump profiling data to file (ref3.md - PerfModel)
        /// </summary>
        public static void DumpProfilingData(string filePath, string format = "csv")
        {
            if (!ProfilingEnabled)
            {
                Console.WriteLine("Warning: Profiling is not enabled. No data to dump.");
                return;
            }

            var report = GetPerformanceStats();

            try
            {
                switch (format.ToLower())
                {
                    case "csv":
                        File.WriteAllText(filePath, report.ExportToCSV());
                        Console.WriteLine($"Performance data exported to {filePath}");
                        break;

                    case "json":
                        string json = JsonSerializer.Serialize(report, new JsonSerializerOptions
                        {
                            WriteIndented = true
                        });
                        File.WriteAllText(filePath, json);
                        Console.WriteLine($"Performance data exported to {filePath}");
                        break;

                    case "summary":
                        File.WriteAllText(filePath, report.GenerateSummary());
                        Console.WriteLine($"Performance summary exported to {filePath}");
                        break;

                    case "traces":
                        if (report.BurstTraces != null)
                        {
                            File.WriteAllText(filePath, report.ExportBurstTracesToCSV());
                            Console.WriteLine($"Burst traces exported to {filePath}");
                        }
                        else
                        {
                            Console.WriteLine("Warning: No burst traces collected. Enable CollectBurstTraces in ProfilingOptions.");
                        }
                        break;

                    default:
                        Console.WriteLine($"Unknown format: {format}. Supported formats: csv, json, summary, traces");
                        break;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error exporting profiling data: {ex.Message}");
            }
        }

        /// <summary>
        /// Sample memory subsystem state (Phase 3)
        /// Call once per N cycles to collect memory wall metrics
        /// </summary>
        public static void SampleMemoryState()
        {
            if (Memory == null) return;

            MemWallStats.TotalSamples++;

            // Update queue statistics
            int queueDepth = Memory.CurrentQueuedRequests;
            MemWallStats.MaxQueueDepth = Math.Max(
                MemWallStats.MaxQueueDepth, queueDepth);

            // Update average queue depth (running average with decay)
            MemWallStats.AverageQueueDepth =
                (MemWallStats.AverageQueueDepth * 0.95) + (queueDepth * 0.05);

            // Check saturation - use a reasonable queue capacity estimate
            int estimatedCapacity = Memory.NumBanks * 16; // 16 requests per bank
            if (queueDepth >= estimatedCapacity)
            {
                MemWallStats.MemoryQueueFullEvents++;
            }

            // Track busy banks - count banks with active transfers
            // We'll track this via DMA/burst activity
            bool hasActivity = (queueDepth > 0) || (Memory.TotalBursts > 0);

            // Check idle
            if (queueDepth == 0 && !hasActivity)
            {
                MemWallStats.IdleMemoryCycles++;
            }

            // Update memory utilization
            if (MemWallStats.TotalSamples > 0)
            {
                long activeCycles = MemWallStats.TotalSamples - MemWallStats.IdleMemoryCycles;
                MemWallStats.MemoryUtilization = (double)activeCycles / MemWallStats.TotalSamples;
            }

            // Update average memory latency
            if (MemWallStats.MemoryRequestCount > 0)
            {
                MemWallStats.AverageMemoryLatency =
                    (double)MemWallStats.TotalMemoryLatencyCycles / MemWallStats.MemoryRequestCount;
            }
        }

        /// <summary>
        /// Reset all performance counters
        /// </summary>
        public static void ResetPerformanceCounters()
        {
            Memory?.ResetStatistics();
            burstTraces?.Clear();
            MemWallStats = new MemoryWallCounters(); // Reset memory wall counters
        }

        #region Event Handlers

        private static Dictionary<ulong, long> burstStartTimes = new Dictionary<ulong, long>();

        private static void OnBurstStarted(object? sender, YAKSys_Hybrid_CPU.Memory.MemorySubsystem.BurstEventArgs e)
        {
            if (burstTraces != null && (CurrentProfilingOptions?.CollectBurstTraces ?? false))
            {
                burstStartTimes[e.Address] = e.Timestamp;
            }
        }

        private static void OnBurstCompleted(object? sender, YAKSys_Hybrid_CPU.Memory.MemorySubsystem.BurstEventArgs e)
        {
            if (burstTraces != null && (CurrentProfilingOptions?.CollectBurstTraces ?? false))
            {
                long startTime = 0;
                if (burstStartTimes.TryGetValue(e.Address, out startTime))
                {
                    burstTraces.Add(new PerformanceReport.BurstTrace
                    {
                        Timestamp = startTime,
                        Address = e.Address,
                        Length = e.Length,
                        IsRead = e.IsRead,
                        BankId = e.BankId,
                        Duration = e.Timestamp - startTime
                    });

                    burstStartTimes.Remove(e.Address);
                }
            }
        }

        #endregion
    }
}
