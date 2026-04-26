using System;
using System.Collections.Generic;
using YAKSys_Hybrid_CPU.Core.Memory;
using YAKSys_Hybrid_CPU.Execution;

namespace YAKSys_Hybrid_CPU.Memory
{
    /// <summary>
    /// Bank arbitration policy for memory requests
    /// </summary>
    public enum BankArbitrationPolicy
    {
        /// <summary>
        /// Round-robin scheduling across banks
        /// </summary>
        RoundRobin,

        /// <summary>
        /// Weighted fair queueing based on priority
        /// </summary>
        WeightedFair,

        /// <summary>
        /// Strict priority-based scheduling
        /// </summary>
        Priority,

        /// <summary>
        /// Fixed Static / Time-Division Multiplexing (TDM) scheduling for strict predictability (Cycle-Determinism)
        /// </summary>
        FixedStatic
    }

    /// <summary>
    /// Unified memory subsystem integrating IOMMU, BurstIO, and DMA.
    /// Manages memory request processing and models bank conflicts.
    ///
    /// Key features:
    /// - Multi-bank memory architecture with conflict detection
    /// - Dynamic bank scheduler with per-bank request queues
    /// - Configurable arbitration policies (round-robin, weighted fair, priority)
    /// - Adaptive stall cycle calculation
    /// - Automatic DMA routing for large transfers
    /// - Configurable parameters (banks, thresholds, boundaries)
    /// - Performance statistics collection
    /// - Event-based profiling support
    /// </summary>
}