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
