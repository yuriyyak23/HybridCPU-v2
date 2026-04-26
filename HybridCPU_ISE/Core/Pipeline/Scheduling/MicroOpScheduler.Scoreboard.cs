using System;
using System.Collections.Generic;
using System.Numerics;
using System.Runtime.CompilerServices;
using YAKSys_Hybrid_CPU.Core.Diagnostics;
using YAKSys_Hybrid_CPU.Core.Memory;

namespace YAKSys_Hybrid_CPU.Core
{
    public partial class MicroOpScheduler
    {
        // ══════════════════════════════════════════════════════════════
        //  Global Scoreboard (DMA / Outstanding Loads)
        // ══════════════════════════════════════════════════════════════

        /// <summary>
        /// Mark a target (register ID, DMA channel, etc.) as pending in the scoreboard.
        /// Returns the slot index used, or -1 if scoreboard is full.
        ///
        /// HLS: 8-entry CAM scan + first-free allocation.
        /// </summary>
        /// <param name="targetId">Resource target to mark pending</param>
        /// <param name="ownerThreadId">Thread that owns this pending entry</param>
        /// <param name="currentCycle">Cycle when entry was created</param>
        /// <returns>Allocated slot index (0–7), or -1 if full</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int SetScoreboardPending(int targetId, int ownerThreadId, long currentCycle)
        {
            for (int i = 0; i < SCOREBOARD_SLOTS; i++)
            {
                if (_scoreboard[i] == -1)
                {
                    _scoreboard[i] = targetId;
                    return i;
                }
            }
            return -1; // Full
        }

        /// <summary>
        /// Check if a target is pending in the global scoreboard.
        /// HLS: 8-entry CAM compare.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool IsScoreboardPending(int targetId)
        {
            for (int i = 0; i < SCOREBOARD_SLOTS; i++)
            {
                if (_scoreboard[i] == targetId)
                    return true;
            }
            return false;
        }

        /// <summary>
        /// Clear a specific scoreboard entry by slot index.
        /// HLS: single register write.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void ClearScoreboardEntry(int slotIndex)
        {
            if ((uint)slotIndex < SCOREBOARD_SLOTS)
                _scoreboard[slotIndex] = -1;
        }

        /// <summary>
        /// Clear all scoreboard entries (flush on pipeline reset / context switch).
        /// HLS: 8-entry register clear.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void ClearScoreboard()
        {
            for (int i = 0; i < SCOREBOARD_SLOTS; i++)
                _scoreboard[i] = -1;
        }

        // ══════════════════════════════════════════════════════════════
        //  Per-VT Scoreboard (4-Way SMT Isolation)
        // ══════════════════════════════════════════════════════════════

        /// <summary>
        /// Mark a target as pending in the per-VT scoreboard (legacy DMA-typed entry).
        /// Returns the slot index used, or -1 if scoreboard is full.
        ///
        /// HLS: per-VT 8-entry CAM.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int SetSmtScoreboardPending(int targetId, int virtualThreadId, long currentCycle)
        {
            return SetSmtScoreboardPendingTyped(targetId, virtualThreadId, currentCycle, ScoreboardEntryType.Dma, -1);
        }

        /// <summary>
        /// Mark a target as pending with explicit entry type and optional bank ID (Refactoring Pt. 3).
        /// Returns the slot index used, or -1 if scoreboard is full.
        ///
        /// HLS: per-VT 8-entry CAM scan + first-free allocation + 3-field write.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int SetSmtScoreboardPendingTyped(int targetId, int virtualThreadId, long currentCycle,
            ScoreboardEntryType entryType, int bankId)
        {
            if ((uint)virtualThreadId >= SMT_WAYS) return -1;

            for (int i = 0; i < SCOREBOARD_SLOTS; i++)
            {
                if (_smtScoreboard[virtualThreadId, i] == -1)
                {
                    _smtScoreboard[virtualThreadId, i] = targetId;
                    _smtScoreboardType[virtualThreadId, i] = entryType;
                    _smtScoreboardBankId[virtualThreadId, i] = bankId;
                    return i;
                }
            }
            return -1; // Full
        }

        /// <summary>
        /// Check if a target is pending in a specific VT's scoreboard.
        /// HLS: 8-entry CAM per VT.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool IsSmtScoreboardPending(int targetId, int virtualThreadId)
        {
            if ((uint)virtualThreadId >= SMT_WAYS) return false;

            for (int i = 0; i < SCOREBOARD_SLOTS; i++)
            {
                if (_smtScoreboard[virtualThreadId, i] == targetId)
                    return true;
            }
            return false;
        }

        /// <summary>
        /// Clear a specific entry in the per-VT scoreboard.
        /// HLS: 3-register write (targetId + type + bankId).
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void ClearSmtScoreboardEntry(int virtualThreadId, int slotIndex)
        {
            if ((uint)virtualThreadId >= SMT_WAYS) return;
            if ((uint)slotIndex >= SCOREBOARD_SLOTS) return;
            _smtScoreboard[virtualThreadId, slotIndex] = -1;
            _smtScoreboardType[virtualThreadId, slotIndex] = ScoreboardEntryType.Free;
            _smtScoreboardBankId[virtualThreadId, slotIndex] = -1;
        }

        /// <summary>
        /// Clear all entries in all VT scoreboards.
        /// HLS: 32-entry register clear (4 VTs × 8 slots × 3 fields).
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void ClearSmtScoreboard()
        {
            for (int vt = 0; vt < SMT_WAYS; vt++)
                for (int s = 0; s < SCOREBOARD_SLOTS; s++)
                {
                    _smtScoreboard[vt, s] = -1;
                    _smtScoreboardType[vt, s] = ScoreboardEntryType.Free;
                    _smtScoreboardBankId[vt, s] = -1;
                }
        }

        // ══════════════════════════════════════════════════════════════
        //  Universal Scoreboard Queries (Refactoring Pt. 3)
        // ══════════════════════════════════════════════════════════════

        /// <summary>
        /// Check if any outstanding load (MSHR entry) targets the specified memory bank
        /// for a given virtual thread. Used by FSP and pipeline ID-stage to prevent
        /// Load/Store injection when the bank has in-flight MSHR entries.
        ///
        /// HLS: 8-entry CAM scan with type + bankId comparator = O(8) fixed.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool IsBankPendingForVT(int bankId, int virtualThreadId)
        {
            if ((uint)virtualThreadId >= SMT_WAYS) return false;

            for (int i = 0; i < SCOREBOARD_SLOTS; i++)
            {
                if (_smtScoreboard[virtualThreadId, i] != -1 &&
                    _smtScoreboardBankId[virtualThreadId, i] == bankId &&
                    (_smtScoreboardType[virtualThreadId, i] == ScoreboardEntryType.OutstandingLoad ||
                     _smtScoreboardType[virtualThreadId, i] == ScoreboardEntryType.OutstandingStore))
                {
                    return true;
                }
            }
            return false;
        }

        /// <summary>
        /// Check if any outstanding load/store targets the specified memory bank
        /// across ALL virtual threads. Used for inter-VT bank conflict detection
        /// during Pod-level FSP injection.
        ///
        /// HLS: 4 × 8-entry CAM scan = 32 comparisons, single-cycle with parallel reduction.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool IsBankPendingGlobal(int bankId)
        {
            for (int vt = 0; vt < SMT_WAYS; vt++)
            {
                if (IsBankPendingForVT(bankId, vt))
                    return true;
            }
            return false;
        }

        /// <summary>
        /// Count outstanding memory entries (Load + Store) for a given virtual thread.
        /// Used for MSHR saturation detection and back-pressure signaling.
        ///
        /// HLS: 8-entry type-match scan.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int GetOutstandingMemoryCount(int virtualThreadId)
        {
            if ((uint)virtualThreadId >= SMT_WAYS) return 0;

            int count = 0;
            for (int i = 0; i < SCOREBOARD_SLOTS; i++)
            {
                var entryType = _smtScoreboardType[virtualThreadId, i];
                if (entryType == ScoreboardEntryType.OutstandingLoad ||
                    entryType == ScoreboardEntryType.OutstandingStore)
                {
                    count++;
                }
            }
            return count;
        }

        /// <summary>
        /// Count free scoreboard slots for a given virtual thread using the same
        /// slot allocator truth as <see cref="SetSmtScoreboardPendingTyped"/>.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private int GetFreeSmtScoreboardSlotCount(int virtualThreadId)
        {
            if ((uint)virtualThreadId >= SMT_WAYS) return 0;

            int count = 0;
            for (int i = 0; i < SCOREBOARD_SLOTS; i++)
            {
                if (_smtScoreboard[virtualThreadId, i] == -1)
                {
                    count++;
                }
            }

            return count;
        }

        /// <summary>Number of MSHR/bank scoreboard stalls detected during scheduling.</summary>
        public long MshrScoreboardStalls { get; private set; }

    }
}