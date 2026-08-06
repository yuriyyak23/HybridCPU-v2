using HybridCPU_ISE.Core;
using System.Text;

namespace YAKSys_Hybrid_CPU
{
    public partial class PerformanceReport
    {
        #region Memory Subsystem Statistics

        /// <summary>
        /// Total burst operations
        /// </summary>
        public long TotalBursts { get; set; }

        /// <summary>
        /// Total bytes transferred
        /// </summary>
        public long TotalBytesTransferred { get; set; }

        /// <summary>
        /// Bank conflicts detected
        /// </summary>
        public long BankConflicts { get; set; }

        /// <summary>
        /// Stall cycles due to conflicts
        /// </summary>
        public long StallCycles { get; set; }

        /// <summary>
        /// DMA transfers
        /// </summary>
        public long DmaTransfers { get; set; }

        /// <summary>
        /// Average burst length
        /// </summary>
        public double AverageBurstLength { get; set; }

        /// <summary>
        /// Total wait cycles across all memory requests
        /// </summary>
        public long TotalWaitCycles { get; set; }

        /// <summary>
        /// Average wait cycles per request
        /// </summary>
        public double AverageWaitCycles { get; set; }

        /// <summary>
        /// Maximum queue depth observed
        /// </summary>
        public int MaxQueueDepth { get; set; }

        /// <summary>
        /// Current queued requests
        /// </summary>
        public int CurrentQueuedRequests { get; set; }

        // Phase 3: Memory wall counters
        /// <summary>
        /// Total memory stalls (cycles stalled waiting for memory)
        /// </summary>
        public long TotalMemoryStalls { get; set; }

        /// <summary>
        /// Memory queue full events
        /// </summary>
        public long MemoryQueueFullEvents { get; set; }

        /// <summary>
        /// Bank saturation cycles (all banks busy)
        /// </summary>
        public long BankSaturationCycles { get; set; }

        /// <summary>
        /// Idle memory cycles (no activity)
        /// </summary>
        public long IdleMemoryCycles { get; set; }

        /// <summary>
        /// Memory utilization percentage
        /// </summary>
        public double MemoryUtilization { get; set; }

        /// <summary>
        /// Average memory queue depth
        /// </summary>
        public double AverageMemoryQueueDepth { get; set; }

        /// <summary>
        /// Queue overflow events
        /// </summary>
        public long QueueOverflowEvents { get; set; }

        /// <summary>
        /// Average memory latency per request
        /// </summary>
        public double AverageMemoryLatency { get; set; }

        /// <summary>
        /// Additive RF-10 producer telemetry. Availability flags distinguish
        /// a measured zero from a contour that has no truthful producer.
        /// </summary>
        public string MemoryCycleTelemetrySchemaVersion { get; set; } = string.Empty;
        public bool MemoryCycleTelemetryAvailable { get; set; }
        public long MemoryControllerCycles { get; set; }
        public long MemoryReadServiceCycles { get; set; }
        public long MemoryStoreReadinessServiceCycles { get; set; }
        public long MemoryCompletionPublicationCycles { get; set; }
        public long MemoryAcceptedRequests { get; set; }
        public long MemoryCompletedRequests { get; set; }
        public long DataReadAcceptedRequests { get; set; }
        public long DataReadCompletedRequests { get; set; }
        public long DataWriteAcceptedRequests { get; set; }
        public long DataWriteCompletedRequests { get; set; }
        public long DataReadBytes { get; set; }
        public long CommittedDataWriteBytes { get; set; }
        public bool InstructionFetchReadBytesTelemetryAvailable { get; set; }
        public long InstructionFetchReadBytes { get; set; }
        public bool InstructionFetchRequestTelemetryAvailable { get; set; }
        public long MemoryQueueFullRejects { get; set; }
        public bool MemoryBankConflictRejectTelemetryAvailable { get; set; }
        public long MemoryBankConflictRejects { get; set; }
        public long MemoryTelemetryBaselineOutstandingRequests { get; set; }
        public long MemoryCanceledRequests { get; set; }
        public long MemoryConsumedCompletions { get; set; }
        public long MemoryOutstandingRequests { get; set; }

        /// <summary>
        /// Creates an observation-only interval view of the cumulative RF-10
        /// memory-controller counters. The current report is not modified.
        /// A missing baseline, schema change, or counter regression means that
        /// no truthful interval can be formed and is reported as unavailable.
        /// </summary>
        public PerformanceReport CreateMemoryCycleTelemetryIntervalSince(PerformanceReport baseline)
        {
            ArgumentNullException.ThrowIfNull(baseline);

            var interval = (PerformanceReport)MemberwiseClone();
            long controllerCycles = 0;
            long readServiceCycles = 0;
            long storeReadinessCycles = 0;
            long publicationCycles = 0;
            long acceptedRequests = 0;
            long completedRequests = 0;
            long dataReadAccepted = 0;
            long dataReadCompleted = 0;
            long dataWriteAccepted = 0;
            long dataWriteCompleted = 0;
            long dataReadBytes = 0;
            long committedWriteBytes = 0;
            long queueFullRejects = 0;
            long canceledRequests = 0;
            long consumedCompletions = 0;
            bool compatible = MemoryCycleTelemetryAvailable &&
                              baseline.MemoryCycleTelemetryAvailable &&
                              string.Equals(
                                  MemoryCycleTelemetrySchemaVersion,
                                  baseline.MemoryCycleTelemetrySchemaVersion,
                                  StringComparison.Ordinal) &&
                              MemoryTelemetryBaselineOutstandingRequests ==
                                  baseline.MemoryTelemetryBaselineOutstandingRequests &&
                              TrySubtract(MemoryControllerCycles, baseline.MemoryControllerCycles, out controllerCycles) &&
                              TrySubtract(MemoryReadServiceCycles, baseline.MemoryReadServiceCycles, out readServiceCycles) &&
                              TrySubtract(MemoryStoreReadinessServiceCycles, baseline.MemoryStoreReadinessServiceCycles, out storeReadinessCycles) &&
                              TrySubtract(MemoryCompletionPublicationCycles, baseline.MemoryCompletionPublicationCycles, out publicationCycles) &&
                              TrySubtract(MemoryAcceptedRequests, baseline.MemoryAcceptedRequests, out acceptedRequests) &&
                              TrySubtract(MemoryCompletedRequests, baseline.MemoryCompletedRequests, out completedRequests) &&
                              TrySubtract(DataReadAcceptedRequests, baseline.DataReadAcceptedRequests, out dataReadAccepted) &&
                              TrySubtract(DataReadCompletedRequests, baseline.DataReadCompletedRequests, out dataReadCompleted) &&
                              TrySubtract(DataWriteAcceptedRequests, baseline.DataWriteAcceptedRequests, out dataWriteAccepted) &&
                              TrySubtract(DataWriteCompletedRequests, baseline.DataWriteCompletedRequests, out dataWriteCompleted) &&
                              TrySubtract(DataReadBytes, baseline.DataReadBytes, out dataReadBytes) &&
                              TrySubtract(CommittedDataWriteBytes, baseline.CommittedDataWriteBytes, out committedWriteBytes) &&
                              TrySubtract(MemoryQueueFullRejects, baseline.MemoryQueueFullRejects, out queueFullRejects) &&
                              TrySubtract(MemoryCanceledRequests, baseline.MemoryCanceledRequests, out canceledRequests) &&
                              TrySubtract(MemoryConsumedCompletions, baseline.MemoryConsumedCompletions, out consumedCompletions);

            interval.MemoryCycleTelemetryAvailable = compatible;
            interval.MemoryControllerCycles = compatible ? controllerCycles : 0;
            interval.MemoryReadServiceCycles = compatible ? readServiceCycles : 0;
            interval.MemoryStoreReadinessServiceCycles = compatible ? storeReadinessCycles : 0;
            interval.MemoryCompletionPublicationCycles = compatible ? publicationCycles : 0;
            interval.MemoryAcceptedRequests = compatible ? acceptedRequests : 0;
            interval.MemoryCompletedRequests = compatible ? completedRequests : 0;
            interval.DataReadAcceptedRequests = compatible ? dataReadAccepted : 0;
            interval.DataReadCompletedRequests = compatible ? dataReadCompleted : 0;
            interval.DataWriteAcceptedRequests = compatible ? dataWriteAccepted : 0;
            interval.DataWriteCompletedRequests = compatible ? dataWriteCompleted : 0;
            interval.DataReadBytes = compatible ? dataReadBytes : 0;
            interval.CommittedDataWriteBytes = compatible ? committedWriteBytes : 0;
            interval.MemoryQueueFullRejects = compatible ? queueFullRejects : 0;
            interval.MemoryTelemetryBaselineOutstandingRequests = compatible
                ? MemoryTelemetryBaselineOutstandingRequests
                : 0;
            interval.MemoryCanceledRequests = compatible ? canceledRequests : 0;
            interval.MemoryConsumedCompletions = compatible ? consumedCompletions : 0;
            interval.MemoryOutstandingRequests = compatible ? MemoryOutstandingRequests : 0;

            long instructionFetchReadBytes = 0;
            bool fetchBytesCompatible = compatible &&
                                        InstructionFetchReadBytesTelemetryAvailable &&
                                        baseline.InstructionFetchReadBytesTelemetryAvailable &&
                                        TrySubtract(
                                            InstructionFetchReadBytes,
                                            baseline.InstructionFetchReadBytes,
                                            out instructionFetchReadBytes);
            interval.InstructionFetchReadBytesTelemetryAvailable = fetchBytesCompatible;
            interval.InstructionFetchReadBytes = fetchBytesCompatible ? instructionFetchReadBytes : 0;

            long bankConflictRejects = 0;
            bool bankRejectsCompatible = compatible &&
                                         MemoryBankConflictRejectTelemetryAvailable &&
                                         baseline.MemoryBankConflictRejectTelemetryAvailable &&
                                         TrySubtract(
                                             MemoryBankConflictRejects,
                                             baseline.MemoryBankConflictRejects,
                                             out bankConflictRejects);
            interval.MemoryBankConflictRejectTelemetryAvailable = bankRejectsCompatible;
            interval.MemoryBankConflictRejects = bankRejectsCompatible ? bankConflictRejects : 0;

            return interval;
        }

        private static bool TrySubtract(long current, long baseline, out long difference)
        {
            if (current < baseline)
            {
                difference = 0;
                return false;
            }

            difference = current - baseline;
            return true;
        }

        /// <summary>
        /// Stalls on load operations
        /// </summary>
        public long LoadStalls { get; set; }

        /// <summary>
        /// Stalls on store operations
        /// </summary>
        public long StoreStalls { get; set; }

        /// <summary>
        /// Stalls on burst operations
        /// </summary>
        public long BurstStalls { get; set; }

        // Phase 3: Burst timing and efficiency
        /// <summary>
        /// Number of unaligned memory accesses (Phase 3)
        /// </summary>
        public long UnalignedAccessCount { get; set; }

        /// <summary>
        /// Total alignment penalty cycles (Phase 3)
        /// </summary>
        public long TotalAlignmentPenalty { get; set; }

        /// <summary>
        /// Burst efficiency (data cycles / total cycles) (Phase 3)
        /// </summary>
        public double BurstEfficiency { get; set; }

        /// <summary>
        /// Average alignment penalty per unaligned access (Phase 3)
        /// </summary>
        public double AverageAlignmentPenalty
        {
            get
            {
                if (UnalignedAccessCount == 0) return 0.0;
                return (double)TotalAlignmentPenalty / UnalignedAccessCount;
            }
        }

        #endregion
    }
}
