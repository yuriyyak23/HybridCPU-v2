using System;
using System.Runtime.CompilerServices;

namespace YAKSys_Hybrid_CPU.Core
{
    /// <summary>
    /// MicroOp Loop Buffer for decode-once-replay-N during strip-mining (req.md §3).
    ///
    /// Purpose: Cache decoded MicroOps from a VLIW bundle so the core can replay
    /// them for thousands of iterations without re-fetching the 2048-bit bundle
    /// from L1-I cache each cycle. This is critical for streaming workloads where
    /// the same operation pattern repeats across data chunks (strip-mining).
    ///
    /// Architecture:
    /// - Fixed-size buffer holding up to 8 decoded MicroOps (one VLIW bundle)
    /// - States: Empty → Loading → Active → Draining
    /// - When Active, the pipeline fetches from the Loop Buffer instead of L1-I
    /// - Replay-stable donor exposure: while replaying, stable donor-safe slots
    ///   are summarized as bundle-donor capacity for the replay duration
    ///
    /// HLS constraints:
    /// - Fixed-size arrays only (no heap allocation)
    /// - Deterministic state machine transitions
    /// - All state fits in register file
    /// </summary>
    public struct LoopBuffer
    {
        /// <summary>
        /// Loop buffer state machine
        /// </summary>
        public enum BufferState : byte
        {
            /// <summary>Buffer is empty, no cached bundle</summary>
            Empty = 0,

            /// <summary>Bundle is being loaded (decode in progress)</summary>
            Loading = 1,

            /// <summary>Buffer is active, replaying cached MicroOps</summary>
            Active = 2,

            /// <summary>Buffer is draining (last iteration, will transition to Empty)</summary>
            Draining = 3
        }

        /// <summary>
        /// Maximum number of MicroOps per VLIW bundle (8 slots × 256 bits)
        /// </summary>
        private const int MAX_SLOTS = 8;

        /// <summary>
        /// HLS-compatible inline buffer for 8 MicroOp slot references.
        /// Maps to an 8-entry register file in hardware — no heap allocation.
        /// </summary>
        [InlineArray(MAX_SLOTS)]
        private struct MicroOpSlotBuffer
        {
            private MicroOp? _element0;
        }

        /// <summary>
        /// HLS-compatible inline buffer for 8 per-slot valid bits.
        /// Maps to an 8-bit register in hardware — no heap allocation.
        /// </summary>
        [InlineArray(MAX_SLOTS)]
        private struct SlotValidBuffer
        {
            private bool _element0;
        }

        /// <summary>
        /// Cached MicroOps from the decoded VLIW bundle.
        /// HLS: maps to an 8-entry register file of MicroOp references (inline, no heap).
        /// </summary>
        private MicroOpSlotBuffer _slots;

        /// <summary>
        /// Per-slot valid bits. A slot may be null/NOP in the original bundle.
        /// HLS: maps to an 8-bit validity register (inline, no heap).
        /// </summary>
        private SlotValidBuffer _slotValid;

        /// <summary>
        /// Number of valid (non-null) slots in the cached bundle
        /// </summary>
        public int ValidSlotCount { get; private set; }

        /// <summary>
        /// Current buffer state
        /// </summary>
        public BufferState State { get; private set; }

        /// <summary>
        /// Program counter of the cached bundle (for PC matching on replay)
        /// </summary>
        public ulong CachedPC { get; private set; }

        /// <summary>
        /// Remaining strip-mining iterations before buffer drains
        /// </summary>
        public ulong RemainingIterations { get; private set; }

        /// <summary>
        /// Total iterations executed from this buffer (performance counter)
        /// </summary>
        public ulong TotalReplays { get; private set; }

        /// <summary>
        /// Total L1-I fetch savings (iterations that did not require re-fetch)
        /// </summary>
        public ulong FetchSavings { get; private set; }

        /// <summary>
        /// Number of replay epochs observed by the loop buffer.
        /// </summary>
        public ulong ReplayEpochCount { get; private set; }

        /// <summary>
        /// Sum of replay-epoch lengths.
        /// </summary>
        public ulong TotalEpochLength { get; private set; }

        /// <summary>
        /// Sum of stable donor-slot samples across replay hits.
        /// </summary>
        public ulong StableDonorSlotSamples { get; private set; }

        /// <summary>
        /// Total slot samples observed during replay hits.
        /// </summary>
        public ulong TotalReplaySlotSamples { get; private set; }

        /// <summary>
        /// Deterministic epoch transition count.
        /// </summary>
        public ulong DeterministicTransitionCount { get; private set; }

        private ulong _currentEpochId;
        private ulong _currentEpochReplayCount;
        private ulong _currentEpochLength;
        private byte _stableDonorMask;
        private ReplayPhaseInvalidationReason _lastInvalidationReason;

        // Phase 07: Per-class donor capacity for class-level replay stability

        /// <summary>
        /// Number of ALU-class donor slots available in the cached replay bundle.
        /// More stable than exact slot positions for replay template matching.
        /// HLS: 3-bit counter.
        /// </summary>
        private byte _aluDonorCapacity;

        /// <summary>
        /// Number of LSU-class donor slots available in the cached replay bundle.
        /// HLS: 3-bit counter.
        /// </summary>
        private byte _lsuDonorCapacity;

        /// <summary>
        /// Number of DmaStream-class donor slots available in the cached replay bundle.
        /// HLS: 3-bit counter.
        /// </summary>
        private byte _dmaStreamDonorCapacity;

        /// <summary>
        /// Number of BranchControl-class donor slots available in the cached replay bundle.
        /// HLS: 3-bit counter.
        /// </summary>
        private byte _branchControlDonorCapacity;

        /// <summary>
        /// Number of SystemSingleton-class donor slots available in the cached replay bundle.
        /// HLS: 3-bit counter.
        /// </summary>
        private byte _systemSingletonDonorCapacity;

        /// <summary>
        /// Initialize the loop buffer. Must be called once before use.
        /// HLS-compatible: no heap allocation, resets inline register state.
        /// </summary>
        public void Initialize()
        {
            _slots = default;
            _slotValid = default;
            State = BufferState.Empty;
            CachedPC = 0;
            RemainingIterations = 0;
            ValidSlotCount = 0;
            TotalReplays = 0;
            FetchSavings = 0;
            ReplayEpochCount = 0;
            TotalEpochLength = 0;
            StableDonorSlotSamples = 0;
            TotalReplaySlotSamples = 0;
            DeterministicTransitionCount = 0;
            _currentEpochId = 0;
            _currentEpochReplayCount = 0;
            _currentEpochLength = 0;
            _stableDonorMask = 0;
            _lastInvalidationReason = ReplayPhaseInvalidationReason.None;
            _aluDonorCapacity = 0;
            _lsuDonorCapacity = 0;
            _dmaStreamDonorCapacity = 0;
            _branchControlDonorCapacity = 0;
            _systemSingletonDonorCapacity = 0;
        }

        /// <summary>
        /// Begin loading a decoded VLIW bundle into the loop buffer.
        /// Called by the Decode stage when a strip-mining loop is detected.
        /// </summary>
        /// <param name="pc">Program counter of the bundle being cached</param>
        /// <param name="totalIterations">Total strip-mining iterations expected</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void BeginLoad(ulong pc, ulong totalIterations)
        {
            if (totalIterations <= 1)
                return; // No point caching a single iteration

            CachedPC = pc;
            RemainingIterations = totalIterations;
            ValidSlotCount = 0;
            State = BufferState.Loading;
            _currentEpochReplayCount = 0;
            _currentEpochLength = 0;
            _stableDonorMask = 0;
            _lastInvalidationReason = ReplayPhaseInvalidationReason.None;

            for (int i = 0; i < MAX_SLOTS; i++)
            {
                _slots[i] = null;
                _slotValid[i] = false;
            }
        }

        /// <summary>
        /// Store a decoded MicroOp into the buffer during the Loading phase.
        /// Called once per slot during the first decode pass.
        /// </summary>
        /// <param name="slotIndex">VLIW slot index (0–7)</param>
        /// <param name="op">Decoded MicroOp (null for empty/NOP slots)</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void StoreSlot(int slotIndex, MicroOp? op)
        {
            if (State != BufferState.Loading)
                return;

            if ((uint)slotIndex >= MAX_SLOTS)
                return;

            _slots[slotIndex] = op;
            _slotValid[slotIndex] = op != null;
            if (op != null)
                ValidSlotCount++;
        }

        /// <summary>
        /// Commit the load and transition to Active state.
        /// Called after all 8 slots have been stored.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void CommitLoad()
        {
            if (State != BufferState.Loading)
                return;

            _currentEpochId++;
            _currentEpochReplayCount = 0;
            _currentEpochLength = RemainingIterations;
            _stableDonorMask = ComputeStableDonorMask();
            ComputeClassDonorCapacity();
            ReplayEpochCount++;
            TotalEpochLength += _currentEpochLength;
            DeterministicTransitionCount++;
            State = BufferState.Active;
        }

        /// <summary>
        /// Attempt to replay the cached bundle for the current iteration.
        /// Copies cached MicroOps into the caller-provided target buffer on hit.
        /// HLS-compatible: reads from inline register file, no heap exposure.
        /// </summary>
        /// <param name="requestedPC">PC being fetched by the pipeline</param>
        /// <param name="target">Caller-provided buffer (min 8 elements) to receive cached MicroOps</param>
        /// <returns>True if loop buffer hit (target filled), false if miss (fetch from L1-I)</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool TryReplay(ulong requestedPC, MicroOp?[] target)
        {
            if (State != BufferState.Active && State != BufferState.Draining)
                return false;

            if (requestedPC != CachedPC)
            {
                // PC mismatch — invalidate buffer (branch out of loop)
                Invalidate(ReplayPhaseInvalidationReason.PcMismatch);
                return false;
            }

            // Loop buffer hit — copy cached MicroOps to caller buffer
            for (int i = 0; i < MAX_SLOTS; i++)
                target[i] = _slots[i];

            // Consume one iteration
            TotalReplays++;
            FetchSavings++;
            _currentEpochReplayCount++;
            TotalReplaySlotSamples += MAX_SLOTS;
            StableDonorSlotSamples += (ulong)CountBits(_stableDonorMask);

            if (RemainingIterations > 0)
                RemainingIterations--;

            if (RemainingIterations == 0)
                State = BufferState.Draining;

            return true;
        }

        /// <summary>
        /// Complete the current replay cycle. If draining, transition to Empty.
        /// Called at the end of the pipeline cycle.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void EndCycle()
        {
            if (State == BufferState.Draining)
            {
                Invalidate(ReplayPhaseInvalidationReason.Completed);
            }
        }

        /// <summary>
        /// Invalidate the loop buffer (branch, interrupt, or strip-mining complete).
        /// HLS-compatible: resets inline register state without heap interaction.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Invalidate(ReplayPhaseInvalidationReason reason = ReplayPhaseInvalidationReason.Manual)
        {
            if (State != BufferState.Empty)
            {
                _lastInvalidationReason = reason;
            }

            State = BufferState.Empty;
            CachedPC = 0;
            RemainingIterations = 0;
            ValidSlotCount = 0;
            _currentEpochLength = 0;
            _currentEpochReplayCount = 0;
            _stableDonorMask = 0;
            _aluDonorCapacity = 0;
            _lsuDonorCapacity = 0;
            _dmaStreamDonorCapacity = 0;
            _branchControlDonorCapacity = 0;
            _systemSingletonDonorCapacity = 0;

            _slots = default;
            _slotValid = default;
        }

        /// <summary>
        /// Current replay phase context for phase-aware scheduling.
        /// </summary>
        public ReplayPhaseContext CurrentReplayPhase => new ReplayPhaseContext(
            State == BufferState.Active || State == BufferState.Draining,
            _currentEpochId,
            CachedPC,
            _currentEpochLength,
            _currentEpochReplayCount,
            ValidSlotCount,
            _stableDonorMask,
            _lastInvalidationReason);

        /// <summary>
        /// Aggregate replay-phase metrics for Phase 1 observability.
        /// </summary>
        public ReplayPhaseMetrics GetReplayPhaseMetrics()
        {
            return new ReplayPhaseMetrics
            {
                ReplayEpochCount = ReplayEpochCount,
                TotalEpochLength = TotalEpochLength,
                StableDonorSlotSamples = StableDonorSlotSamples,
                TotalReplaySlotSamples = TotalReplaySlotSamples,
                DeterministicTransitionCount = DeterministicTransitionCount
            };
        }

        /// <summary>
        /// Check if a specific slot is replay-stable donor eligible.
        /// During Active replay, empty/NOP slots and donor-safe non-vector slots
        /// are exposed as stable donor capacity for the replay epoch.
        /// </summary>
        /// <param name="slotIndex">Slot to check (0–7)</param>
        /// <returns>True if the slot is part of the replay-stable donor mask.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool IsSlotDonorEligible(int slotIndex)
        {
            if (State != BufferState.Active)
                return false;

            if ((uint)slotIndex >= MAX_SLOTS)
                return false;

            return IsReplayStableDonorSlot(slotIndex);
        }

        /// <summary>
        /// Get the number of replay-stable donor slots in the cached bundle.
        /// This is the bounded donor-capacity surface exposed to replay-aware
        /// bundle densification during the active replay epoch.
        /// </summary>
        public int DonorEligibleSlotCount
        {
            get
            {
                if (State != BufferState.Active)
                    return 0;

                return CountBits(_stableDonorMask);
            }
        }

        /// <summary>
        /// Check if the loop buffer is currently active and replaying.
        /// </summary>
        public bool IsReplaying => State == BufferState.Active;

        /// <summary>
        /// Phase 07: Get per-class donor capacity for the cached replay bundle.
        /// Returns the number of donor-eligible (empty/NOP) slots that belong to each class.
        /// More stable across replay iterations than exact slot positions.
        /// </summary>
        /// <param name="slotClass">The slot class to query.</param>
        /// <returns>Number of donor slots available for the given class.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public readonly int GetClassDonorCapacity(SlotClass slotClass)
        {
            if (State != BufferState.Active)
                return 0;

            return slotClass switch
            {
                SlotClass.AluClass => _aluDonorCapacity,
                SlotClass.LsuClass => _lsuDonorCapacity,
                SlotClass.DmaStreamClass => _dmaStreamDonorCapacity,
                SlotClass.BranchControl => _branchControlDonorCapacity,
                SlotClass.SystemSingleton => _systemSingletonDonorCapacity,
                _ => 0,
            };
        }

        private byte ComputeStableDonorMask()
        {
            byte mask = 0;
            for (int i = 0; i < MAX_SLOTS; i++)
            {
                if (IsReplayStableDonorSlot(i))
                {
                    mask |= (byte)(1 << i);
                }
            }

            return mask;
        }

        private bool IsReplayStableDonorSlot(int slotIndex)
        {
            if (!_slotValid[slotIndex])
                return true;

            var op = _slots[slotIndex];
            return op != null && op is not VectorMicroOp;
        }

        /// <summary>
        /// Phase 07: Compute per-class donor capacity from stable donor mask and lane map.
        /// A donor slot at lane N belongs to the class that lane N is mapped to.
        /// </summary>
        private void ComputeClassDonorCapacity()
        {
            _aluDonorCapacity = 0;
            _lsuDonorCapacity = 0;
            _dmaStreamDonorCapacity = 0;
            _branchControlDonorCapacity = 0;
            _systemSingletonDonorCapacity = 0;

            for (int i = 0; i < MAX_SLOTS; i++)
            {
                if ((_stableDonorMask & (1 << i)) == 0)
                    continue;

                // Map lane index to primary class using default W=8 topology:
                // lanes 0-3 → AluClass, lanes 4-5 → LsuClass,
                // lane 6 → DmaStreamClass, lane 7 → BranchControl (aliased with SystemSingleton)
                switch (i)
                {
                    case 0: case 1: case 2: case 3:
                        _aluDonorCapacity++; break;
                    case 4: case 5:
                        _lsuDonorCapacity++; break;
                    case 6:
                        _dmaStreamDonorCapacity++; break;
                    case 7:
                        // Lane 7 is aliased: count for both BranchControl and SystemSingleton
                        _branchControlDonorCapacity++;
                        _systemSingletonDonorCapacity++;
                        break;
                }
            }
        }

        private static int CountBits(byte value)
        {
            int count = 0;
            byte remaining = value;
            while (remaining != 0)
            {
                count += remaining & 1;
                remaining >>= 1;
            }

            return count;
        }
    }
}
