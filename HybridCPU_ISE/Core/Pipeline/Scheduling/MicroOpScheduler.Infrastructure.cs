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
        //  SMT Nomination Port Management
        // ══════════════════════════════════════════════════════════════

        /// <summary>
        /// Clear all SMT nomination ports.
        /// Called at the start of each cycle to prepare for fresh nominations.
        /// HLS: 4-bit register clear.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void ClearSmtNominationPorts()
        {
            for (int vt = 0; vt < SMT_WAYS; vt++)
            {
                _smtPorts[vt] = null;
                _smtPortValid[vt] = false;
            }
        }

        // ══════════════════════════════════════════════════════════════
        //  Bank Arbitration Integration
        // ══════════════════════════════════════════════════════════════

        /// <summary>
        /// Execution result enum for ScheduleWithArbitration.
        /// </summary>
        public enum ExecutionResult
        {
            /// <summary>Operation scheduled successfully, no conflicts.</summary>
            Success,
            /// <summary>Bank conflict detected, operation must stall.</summary>
            Stall,
            /// <summary>Invalid operation (null or unsupported).</summary>
            Failed
        }

        /// <summary>Number of bank conflicts detected during arbitration.</summary>
        public long BankConflictsCount { get; private set; }

        /// <summary>
        /// Schedule a micro-operation with bank arbitration.
        /// For memory operations, checks bank availability via BankArbitrator.
        /// Non-memory operations always succeed.
        ///
        /// HLS: combinational path — bank CAM lookup + comparator.
        /// </summary>
        /// <param name="op">Micro-operation to schedule</param>
        /// <param name="arbitrator">Bank arbitrator for conflict detection</param>
        /// <returns>Execution result: Success, Stall, or Failed</returns>
        public ExecutionResult ScheduleWithArbitration(MicroOp? op, BankArbitrator arbitrator)
        {
            if (op == null)
                return ExecutionResult.Failed;

            // Non-memory ops always succeed
            if (!op.IsMemoryOp)
                return ExecutionResult.Success;

            // Memory op: check bank availability
            if (op is LoadMicroOp loadOp)
            {
                if (!arbitrator.TryReserveBank(loadOp.Address, out _))
                {
                    BankConflictsCount++;
                    return ExecutionResult.Stall;
                }
            }
            else if (op is StoreMicroOp storeOp)
            {
                if (!arbitrator.TryReserveBank(storeOp.Address, out _))
                {
                    BankConflictsCount++;
                    return ExecutionResult.Stall;
                }
            }

            return ExecutionResult.Success;
        }

        // ══════════════════════════════════════════════════════════════
        //  Memory Wall Suppression (Legacy SuppressLsu compat)
        // ══════════════════════════════════════════════════════════════

        /// <summary>
        /// When true, LSU (Load/Store) operations from background VTs are suppressed
        /// during PackBundleIntraCoreSmt. Replaced in production by GRLB hardware
        /// occupancy mask, but retained for test backward compatibility.
        /// </summary>
        public bool SuppressLsu { get; set; }

        /// <summary>Number of LSU operations suppressed due to SuppressLsu flag.</summary>
        public long MemoryWallSuppressionsCount { get; private set; }

        // ══════════════════════════════════════════════════════════════
        //  Speculative FSP
        // ══════════════════════════════════════════════════════════════

        /// <summary>Number of speculative steals that completed successfully.</summary>
        public long SuccessfulSpeculativeSteals { get; private set; }

        /// <summary>Number of speculative steals that faulted and were squashed.</summary>
        public long FaultedSpeculativeSteals { get; private set; }

        /// <summary>Number of rejected injection attempts.</summary>
        public long RejectedInjectionsCount { get; private set; }

        /// <summary>Number of rollback events (resource mask restoration).</summary>
        public long RollbackEventsCount { get; private set; }

        /// <summary>
        /// Process faulted speculative operations in a bundle.
        /// Clears faulted/speculative flags and increments FaultedSpeculativeSteals counter.
        ///
        /// HLS: 8-slot scan with conditional flag reset.
        /// </summary>
        /// <param name="bundle">VLIW bundle to scan for faulted ops</param>
        public void ProcessFaultedOperations(MicroOp[]? bundle)
        {
            if (bundle == null) return;

            for (int i = 0; i < bundle.Length; i++)
            {
                var op = bundle[i];
                if (op == null) continue;

                if (op.IsSpeculative && op.Faulted)
                {
                    // Restore OriginalResourceMask if it was saved before speculation
                    if (op.OriginalResourceMask != 0)
                    {
                        op.ResourceMask = op.OriginalResourceMask;
                        op.OriginalResourceMask = 0;
                    }

                    op.IsSpeculative = false;
                    op.Faulted = false;
                    FaultedSpeculativeSteals++;

                    // Phase 2C: Release speculation budget on squash
                    ReleaseSpeculationBudget();
                }
            }
        }

        /// <summary>
        /// Phase 2C: Release one unit of speculation budget (on speculative commit or squash).
        /// HLS: 4-bit increment with saturation at SpeculationBudgetMax.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void ReleaseSpeculationBudget()
        {
            if (SpeculationBudgetEnabled && _speculationBudget < SpeculationBudgetMax)
            {
                _speculationBudget++;
            }
        }

        // ══════════════════════════════════════════════════════════════
        //  Barrier Management
        // ══════════════════════════════════════════════════════════════

        /// <summary>
        /// Barrier entries: (vtId, instructionIndex, threadMask).
        /// HLS: fixed-size array of barrier descriptors (max 16).
        /// </summary>
        private (int vtId, int instrIndex, int[] threads)[] _barriers = new (int, int, int[])[16];
        private int _barrierCount;

        /// <summary>Number of active barriers.</summary>
        public int BarrierCount => _barrierCount;

        /// <summary>
        /// Add a manual barrier for a virtual thread at a given instruction index.
        /// </summary>
        public void AddManualBarrier(int vtId, int instructionIndex, int[] threads)
        {
            if (_barrierCount >= _barriers.Length) return;
            _barriers[_barrierCount] = (vtId, instructionIndex, threads);
            _barrierCount++;
        }

        /// <summary>
        /// Optimize barriers by merging adjacent ones (within 5 instructions).
        /// </summary>
        public void OptimizeBarriers()
        {
            if (_barrierCount <= 1) return;

            int writeIdx = 0;
            for (int i = 0; i < _barrierCount; i++)
            {
                bool merged = false;
                if (writeIdx > 0)
                {
                    int prevInstr = _barriers[writeIdx - 1].instrIndex;
                    int curInstr = _barriers[i].instrIndex;
                    // Merge if same VT and within 5 instructions
                    if (_barriers[writeIdx - 1].vtId == _barriers[i].vtId &&
                        Math.Abs(curInstr - prevInstr) <= 5)
                    {
                        merged = true;
                    }
                }

                if (!merged)
                {
                    _barriers[writeIdx] = _barriers[i];
                    writeIdx++;
                }
            }
            _barrierCount = writeIdx;
        }

        /// <summary>
        /// Generate a 16-bit affinity mask from thread IDs.
        /// Each thread ID sets a corresponding bit in the mask.
        /// </summary>
        public ushort GenerateAffinityMask(int[] threads)
        {
            ushort mask = 0;
            if (threads == null) return mask;
            for (int i = 0; i < threads.Length; i++)
            {
                if (threads[i] >= 0 && threads[i] < 16)
                    mask |= (ushort)(1 << threads[i]);
            }
            return mask;
        }
    }
}