using System;
using YAKSys_Hybrid_CPU.Core;
using YAKSys_Hybrid_CPU.Memory;

namespace YAKSys_Hybrid_CPU
{
    /// <summary>
    /// Pod Controller — orchestrates 16 CPU cores within a single Pod (tech.md §1).
    ///
    /// Responsibilities:
    /// - Manage local FSP arbitration scope (intra-pod only, no inter-pod FSP)
    /// - Route local L2 bank accesses
    /// - Implement pod-level barrier synchronization (tech.md §4)
    /// - Provide XY coordinates for NoC addressing (tech.md §2)
    ///
    /// HLS design constraints:
    /// - Fixed-size arrays only (no dynamic allocation)
    /// - Deterministic iteration order for all scans
    /// - All state fits in register file (no BRAM for control path)
    /// </summary>
    public partial class PodController
    {
        /// <summary>
        /// Number of cores per Pod (hardware constant)
        /// </summary>
        public const int CORES_PER_POD = 16;

        /// <summary>
        /// Pod X coordinate in 2D-Mesh NoC (0–7 for 8×8 grid = 64 pods)
        /// </summary>
        public int PodX { get; }

        /// <summary>
        /// Pod Y coordinate in 2D-Mesh NoC (0–7 for 8×8 grid = 64 pods)
        /// </summary>
        public int PodY { get; }

        /// <summary>
        /// Composite Pod ID: (X &lt;&lt; 8) | Y, written to CSR POD_ID (0xB00)
        /// </summary>
        public ushort PodId => (ushort)((PodX << 8) | PodY);

        /// <summary>
        /// Pod-local FSP scheduler (manages nomination ports for 16 cores)
        /// </summary>
        private readonly MicroOpScheduler _scheduler;

        /// <summary>
        /// Barrier synchronization: bitmask of cores that have reached the barrier.
        /// Bit i = 1 means core i has executed POD.BARRIER instruction.
        /// When (BarrierReached & mask) == mask, all participating cores are synchronized.
        /// HLS: single 16-bit register.
        /// </summary>
        private ushort _barrierReachedMask;

        /// <summary>
        /// Barrier timeout counter — increments each cycle while barrier is active.
        /// If it exceeds BarrierTimeoutThreshold, a deadlock warning is raised.
        /// </summary>
        private uint _barrierTimeoutCounter;

        /// <summary>
        /// Maximum cycles to wait for barrier before triggering deadlock mitigation.
        /// Configurable via CSR, default = 10000 cycles.
        /// </summary>
        public uint BarrierTimeoutThreshold { get; set; } = 10000;

        /// <summary>
        /// Domain certificate for this Pod (Singularity-style isolation, tech.md §3).
        /// Used by TryStealSlot to filter nominations by domain membership.
        /// Written from CSR MEM_DOMAIN_CERT (0xB02).
        /// </summary>
        public ulong DomainCertificate { get; set; }

        /// <summary>
        /// L2 Shared Pod Cache — shared among 16 cores within this Pod (req.md §2).
        /// On L2 miss, the core's slots become FSP donor-eligible.
        /// </summary>
        public SharedPodCache L2Cache { get; }

        /// <summary>
        /// FSP-driven power controller for Dark Silicon optimization (Plan 10).
        /// Monitors donation patterns and gates Fetch/Decode clocks for idle donors.
        /// HLS: 16 × 1-bit clock enable + 16 × 4-bit hysteresis counters.
        /// </summary>
        public FspPowerController PowerController;

        public PodController(int podX, int podY, MicroOpScheduler scheduler)
        {
            ArgumentNullException.ThrowIfNull(scheduler);

            PodX = podX;
            PodY = podY;
            _scheduler = scheduler;
            L2Cache = new SharedPodCache();
        }

        /// <summary>
        /// Check if pod barrier has been reached by all cores in the affinity mask.
        /// Called by CPU_Core.FSM in WAIT_FOR_CLUSTER_SYNC state (tech.md §4).
        /// </summary>
        /// <param name="affinityMask">Bitmask of cores that must reach barrier (from CSR POD_AFFINITY_MASK)</param>
        /// <returns>True if all cores in mask have reached the barrier</returns>
        public bool IsBarrierReached(ushort affinityMask)
        {
            return (_barrierReachedMask & affinityMask) == affinityMask;
        }

        /// <summary>
        /// Signal that a core has reached the pod barrier.
        /// Called when a core executes POD.BARRIER instruction.
        /// </summary>
        /// <param name="localCoreId">Core index within Pod (0–15)</param>
        public void SignalBarrier(int localCoreId)
        {
            if ((uint)localCoreId < CORES_PER_POD)
            {
                _barrierReachedMask |= (ushort)(1 << localCoreId);
            }
        }

        /// <summary>
        /// Reset barrier state. Called when barrier is released or on interrupt preemption.
        /// </summary>
        public void ResetBarrier()
        {
            _barrierReachedMask = 0;
            _barrierTimeoutCounter = 0;
        }

        /// <summary>
        /// Tick barrier timeout. Returns true if timeout has been reached (deadlock warning).
        /// Called each cycle while any core is in WAIT_FOR_CLUSTER_SYNC.
        /// </summary>
        public bool TickBarrierTimeout()
        {
            if (_barrierReachedMask != 0)
            {
                _barrierTimeoutCounter++;
                return _barrierTimeoutCounter >= BarrierTimeoutThreshold;
            }

            return false;
        }

        /// <summary>
        /// Begin a scheduling cycle: clear nomination ports for fresh nominations.
        /// Called at the top of each pipeline cycle before cores nominate candidates.
        /// Also advances L2 cache cycle counter, propagates miss-pending flags,
        /// and updates FSP power gating state (Plan 10).
        /// </summary>
        public void BeginCycle()
        {
            // Phase 1: Latch nominations from previous cycle into pipeline register
            _scheduler.LatchNominations();

            // Plan 10: Update power gating based on the latched visible inter-core
            // nomination state. Must be called AFTER LatchNominations and BEFORE
            // ClearNominationPorts so the previous cycle's donor visibility is preserved.
            PowerController.UpdateFromFsp(_scheduler.GetLatchedInterCoreNominationReadyMask());

            // Clear live ports for fresh nominations
            _scheduler.ClearNominationPorts();
            _scheduler.ClearInterCoreAssistNominationPorts();

            L2Cache.AdvanceCycle();
        }

        /// <summary>
        /// Pack a VLIW bundle with FSP slot stealing using the Pod's scheduler.
        /// This is the main entry point for FSP from the pipeline.
        /// </summary>
        public MicroOp[] PackBundle(
            System.Collections.Generic.IReadOnlyList<MicroOp?> originalBundle,
            int currentThreadId,
            bool stealEnabled,
            byte stealMask,
            ResourceBitset globalResourceLocks = default,
            byte donorMask = 0,
            int localCoreId = -1,
            int bundleOwnerContextId = -1,
            ulong bundleOwnerDomainTag = 0,
            ulong assistRuntimeEpoch = 0,
            MemorySubsystem? memSub = null,
            PodController?[]? pods = null)
        {
            return _scheduler.PackBundle(
                originalBundle,
                currentThreadId,
                stealEnabled,
                stealMask,
                globalResourceLocks,
                donorMask,
                DomainCertificate,
                localCoreId,
                PodId,
                bundleOwnerContextId,
                bundleOwnerDomainTag,
                assistRuntimeEpoch,
                memSub,
                pods ?? Processor.Pods);
        }

        /// <summary>
        /// Pack a VLIW bundle with intra-core SMT slot stealing using the Pod's scheduler.
        /// This path is used when runtime wants VT-local stealing rather than inter-core donation.
        /// </summary>
        public MicroOp[] PackBundleIntraCoreSmt(
            System.Collections.Generic.IReadOnlyList<MicroOp?> originalBundle,
            int ownerVirtualThreadId,
            int localCoreId,
            byte eligibleVirtualThreadMask,
            MemorySubsystem? memSub = null)
        {
            return _scheduler.PackBundleIntraCoreSmt(
                originalBundle,
                ownerVirtualThreadId,
                localCoreId,
                eligibleVirtualThreadMask,
                memSub);
        }

        /// <summary>
        /// Get the underlying scheduler for direct access (test/debug).
        /// </summary>
        public MicroOpScheduler Scheduler => _scheduler;
    }
}
