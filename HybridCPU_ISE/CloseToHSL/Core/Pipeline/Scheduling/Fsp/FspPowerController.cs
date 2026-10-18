using System.Runtime.CompilerServices;

namespace YAKSys_Hybrid_CPU.Core
{
    /// <summary>
    /// Retained compatibility power controller for donor-idleness gating.
    ///
    /// <para>Monitors inter-core donor nomination patterns and gates clocks for idle donor threads.
    /// The type keeps its legacy FSP name for retained compatibility, but the live
    /// semantics are donor-idleness gating after work donation.</para>
    ///
    /// <para><b>Microarchitectural contract:</b></para>
    /// <list type="bullet">
    ///   <item>Gating decision uses hysteresis: thread must be idle for
    ///         <see cref="IDLE_HYSTERESIS_CYCLES"/> consecutive cycles before gating.</item>
    ///   <item>Wakeup latency: 1 cycle (just re-enable clock AND-gate).</item>
    ///   <item>No architectural state is lost — pipeline stages are gated, not flushed.</item>
    ///   <item>Execute/MEM/WB remain active for commit of previously stolen ops.</item>
    /// </list>
    ///
    /// <para><b>HLS characteristics:</b></para>
    /// <list type="bullet">
    ///   <item>16 × 1-bit clock enable → 16 AND-gates on clock tree input
    ///         (standard ICG cell pattern).</item>
    ///   <item>Hysteresis counters: 16 × 4-bit → 64 bits total → 1 LUTRAM.</item>
    ///   <item>Wakeup: single-cycle — deassert AND-gate on clock, no pipeline flush.</item>
    ///   <item>Estimated power savings: ~30% dynamic power per gated thread
    ///         (Fetch + Decode + I-cache access disabled).</item>
    ///   <item>Zero heap allocation — <see cref="InlineArray"/>-backed fixed storage.</item>
    ///   <item>Combinatorial depth: 1 level (comparator + mux per thread).</item>
    ///   <item>Estimated area: ~80 LUT (counters + comparators + gate logic).</item>
    /// </list>
    /// </summary>
    public struct FspPowerController
    {
        /// <summary>Number of hardware threads per Pod.</summary>
        private const int MAX_THREADS = 16;

        /// <summary>
        /// Hysteresis threshold: thread must be idle for N consecutive cycles
        /// before gating. Prevents thrashing on short idle periods.
        /// HLS: 4-bit comparator per thread (threshold fits in 4 bits).
        /// </summary>
        public const int IDLE_HYSTERESIS_CYCLES = 8;

        /// <summary>
        /// Fraction of per-thread dynamic power saved by Fetch/Decode gating.
        /// Based on typical VLIW pipeline power breakdown: Fetch ~15%, Decode ~15%.
        /// </summary>
        private const double FETCH_DECODE_POWER_FRACTION = 0.30;

        // ── HLS-compatible storage (InlineArray, zero heap) ─────────

        /// <summary>Per-thread idle cycle counter (4-bit range sufficient for hysteresis ≤ 15).</summary>
        [InlineArray(MAX_THREADS)]
        private struct IdleCounterArray
        {
            private int _element0;
        }

        /// <summary>Per-thread clock gate state (true = gated/Fetch+Decode OFF).</summary>
        [InlineArray(MAX_THREADS)]
        private struct GateStateArray
        {
            private bool _element0;
        }

        private IdleCounterArray _idleCounters;
        private GateStateArray _gateStates;

        // ── Statistics ──────────────────────────────────────────────

        /// <summary>Total thread-cycles spent in gated state (across all threads).</summary>
        public ulong TotalGatedCycles { get; private set; }

        /// <summary>Number of gate/ungate transitions (for thrashing detection).</summary>
        public ulong GateTransitions { get; private set; }

        /// <summary>
        /// Phase 06: Number of cycles where class-capacity exhaustion triggered
        /// early power-gating because the compatibility-era densification path has no remaining class-flexible admission opportunity.
        /// <para>HLS: 64-bit diagnostic counter — not on critical path.</para>
        /// </summary>
        public ulong ClassCapacityEarlyGateCycles { get; private set; }

        /// <summary>Number of threads currently gated (snapshot).</summary>
        public readonly int CurrentGatedCount
        {
            get
            {
                int count = 0;
                for (int t = 0; t < MAX_THREADS; t++)
                {
                    if (_gateStates[t]) count++;
                }
                return count;
            }
        }

        // ── Core API ────────────────────────────────────────────────

        /// <summary>
        /// Update idle counters based on the visible inter-core donor-ready mask.
        /// Called every cycle from <see cref="PodController.BeginCycle"/>.
        ///
        /// <para><b>Logic per thread:</b></para>
        /// <list type="bullet">
        ///   <item>If port is valid (thread has work) → reset idle counter, ungate immediately.</item>
        ///   <item>If port is invalid (thread donated everything / idle) → increment counter.</item>
        ///   <item>If counter ≥ <see cref="IDLE_HYSTERESIS_CYCLES"/> → gate the thread.</item>
        /// </list>
        ///
        /// HLS: 16 parallel comparators + AND-gate updates, single cycle.
        /// </summary>
        /// <param name="readyMask">16-bit visible nomination ready mask from <see cref="MicroOpScheduler"/>.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void UpdateFromFsp(ushort readyMask)
        {
            for (int t = 0; t < MAX_THREADS; t++)
            {
                bool hasVisibleNomination = ((readyMask >> t) & 1) != 0;
                if (hasVisibleNomination)
                {
                    // Thread has work → ungate immediately (1-cycle wakeup)
                    if (_gateStates[t])
                    {
                        _gateStates[t] = false;
                        GateTransitions++;
                    }
                    _idleCounters[t] = 0;
                }
                else
                {
                    // Thread idle → increment hysteresis counter
                    _idleCounters[t]++;

                    if (_idleCounters[t] >= IDLE_HYSTERESIS_CYCLES && !_gateStates[t])
                    {
                        _gateStates[t] = true;
                        GateTransitions++;
                    }
                }

                // Accumulate gated-cycle statistics
                if (_gateStates[t])
                    TotalGatedCycles++;
            }
        }

        /// <summary>
        /// Phase 06: Update idle counters with class-capacity-aware early power-gating.
        ///
        /// <para>Extends <see cref="UpdateFromFsp"/> with an additional gating signal:
        /// when <paramref name="classCapacity"/> shows no free capacity in any
        /// class-flexible class (ALU, LSU), the compatibility-era densification path has no useful admission opportunity left.
        /// In this state all threads with valid ports are still treated as idle
        /// for power-gating purposes.</para>
        ///
        /// <para><b>Logic:</b></para>
        /// <list type="bullet">
        ///   <item>If class-capacity exhausted → override port validity to false for all threads
        ///         (no class-flexible lanes remain for densification, so threads are effectively idle).</item>
        ///   <item>Otherwise → delegate to <see cref="UpdateFromFsp"/>.</item>
        /// </list>
        ///
        /// HLS: 2 additional comparators (ALU free, LSU free) → 1 AND-gate → override mux.
        /// </summary>
        /// <param name="readyMask">16-bit visible nomination ready mask from <see cref="MicroOpScheduler"/>.</param>
        /// <param name="classCapacity">Current class-capacity snapshot from MicroOpScheduler.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void UpdateFromFspWithClassCapacity(
            ushort readyMask,
            SlotClassCapacityState classCapacity)
        {
            if (IsClassCapacityExhausted(classCapacity))
            {
                // No class-flexible capacity means the compatibility-era densification path is idle.
                // Run the standard update with all ports forced invalid.
                ClassCapacityEarlyGateCycles++;
                UpdateFromFsp(0);
                return;
            }

            UpdateFromFsp(readyMask);
        }

        /// <summary>
        /// Phase 06: Check if all class-flexible slot classes have exhausted capacity.
        /// When true, no class-flexible densification opportunity remains.
        /// <para>
        /// Checks ALU and LSU classes (the two multi-lane class-flexible classes).
        /// Single-lane classes (DmaStream, Branch, System) are not checked because
        /// they are typically HardPinned and do not benefit from class-flexible densification.
        /// </para>
        /// <para>HLS: 2 comparators (ALU free, LSU free) → 1 NOR gate.</para>
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool IsClassCapacityExhausted(SlotClassCapacityState classCapacity)
        {
            return !classCapacity.HasFreeCapacity(SlotClass.AluClass) &&
                   !classCapacity.HasFreeCapacity(SlotClass.LsuClass);
        }

        /// <summary>
        /// Check if a thread's Fetch/Decode should be clock-gated.
        /// True = skip Fetch/Decode for this thread (power saving mode).
        /// Execute/MEM/WB remain active for commit of stolen ops.
        /// HLS: single AND-gate read, zero combinatorial depth.
        /// </summary>
        /// <param name="threadId">Hardware thread index (0–15).</param>
        /// <returns>True if thread is clock-gated.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public readonly bool IsThreadGated(int threadId)
        {
            return (uint)threadId < MAX_THREADS && _gateStates[threadId];
        }

        /// <summary>
        /// Force-wake a thread (interrupt, new work arrival, barrier release).
        /// Immediate ungate with 1-cycle wakeup latency.
        /// HLS: single register write + counter clear.
        /// </summary>
        /// <param name="threadId">Hardware thread index (0–15).</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void WakeThread(int threadId)
        {
            if ((uint)threadId < MAX_THREADS && _gateStates[threadId])
            {
                _gateStates[threadId] = false;
                _idleCounters[threadId] = 0;
                GateTransitions++;
            }
        }

        /// <summary>
        /// Force-wake all threads (global event, e.g., full barrier release or reset).
        /// </summary>
        public void WakeAll()
        {
            for (int t = 0; t < MAX_THREADS; t++)
            {
                if (_gateStates[t])
                {
                    _gateStates[t] = false;
                    _idleCounters[t] = 0;
                    GateTransitions++;
                }
            }
        }

        /// <summary>
        /// Get estimated power savings ratio (0.0–1.0) over the observation window.
        /// Based on the fraction of thread-cycles saved by Fetch/Decode gating.
        /// </summary>
        /// <param name="totalCycles">Total simulation cycles elapsed.</param>
        /// <returns>Estimated fraction of total power saved (0.0 = no savings).</returns>
        public readonly double GetPowerSavingsRatio(ulong totalCycles)
        {
            if (totalCycles == 0) return 0.0;
            // Each gated thread-cycle saves FETCH_DECODE_POWER_FRACTION of per-thread power
            return (double)TotalGatedCycles / (totalCycles * MAX_THREADS) * FETCH_DECODE_POWER_FRACTION;
        }

        /// <summary>
        /// Reset all state (init or full pipeline flush).
        /// </summary>
        public void Reset()
        {
            _idleCounters = default;
            _gateStates = default;
            TotalGatedCycles = 0;
            GateTransitions = 0;
            ClassCapacityEarlyGateCycles = 0;
        }
    }
}
