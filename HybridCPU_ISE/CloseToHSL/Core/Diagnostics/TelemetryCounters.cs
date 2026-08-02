// ─────────────────────────────────────────────────────────────────────────────
// HybridCPU ISA v4 — Diagnostics / Tracing
// Phase 11: Deterministic Replay and Trace Integration
// ─────────────────────────────────────────────────────────────────────────────

using System;
using System.Collections.Generic;
using YAKSys_Hybrid_CPU.Arch;

namespace YAKSys_Hybrid_CPU.Core
{
    /// <summary>
    /// Per-VT and global diagnostic telemetry counters for HybridCPU ISA v4.
    /// <para>
    /// Tracks architecturally significant event counts for telemetry export and
    /// counter CSR population (Cycle, BundleRet, InstrRet, VmExitCnt, BarrierCnt,
    /// StealCnt, ReplayCnt).
    /// </para>
    /// <para>
    /// This class is NOT thread-safe. Callers must serialize access when updating
    /// counters from multiple VTs in the same simulation step.
    /// </para>
    /// </summary>
    public sealed class TelemetryCounters
    {
        /// <summary>Number of supported VTs (matches HybridCPU SMT width).</summary>
        public const int VtCount = 4;

        // ─── Global counters ─────────────────────────────────────────────────

        /// <summary>Total cycles elapsed since reset.</summary>
        public ulong CycleCount { get; private set; }

        /// <summary>Total bundles retired across all VTs.</summary>
        public ulong BundleRetiredCount { get; private set; }

        /// <summary>Total instructions retired across all VTs.</summary>
        public ulong InstrRetiredCount { get; private set; }

        /// <summary>Total VM exits (VMLAUNCH/VMRESUME → VMEXIT transitions).</summary>
        public ulong VmExitCount { get; private set; }

        /// <summary>Total barrier operations (POD_BARRIER + VT_BARRIER) across all VTs.</summary>
        public ulong BarrierCount { get; private set; }

        /// <summary>Total FSP slot pilfering operations across all VTs.</summary>
        public ulong StealCount { get; private set; }

        /// <summary>Total replay operations executed from snapshots.</summary>
        public ulong ReplayCount { get; private set; }

        // ─── Per-VT counters ─────────────────────────────────────────────────

        private readonly ulong[] _amoDwordCountPerVt  = new ulong[VtCount];
        private readonly ulong[] _amoWordCountPerVt   = new ulong[VtCount];
        private readonly ulong[] _lrCountPerVt        = new ulong[VtCount];
        private readonly ulong[] _scSuccessCountPerVt = new ulong[VtCount];
        private readonly ulong[] _scFailCountPerVt    = new ulong[VtCount];
        private readonly ulong[] _instrCountPerVt     = new ulong[VtCount];

        // ─── Increment APIs ──────────────────────────────────────────────────

        /// <summary>Advance the cycle counter by one.</summary>
        public void IncrementCycle() => CycleCount++;

        /// <summary>Advance the cycle counter by <paramref name="delta"/> cycles.</summary>
        public void IncrementCycle(ulong delta) => CycleCount += delta;

        /// <summary>Increment retired bundle count.</summary>
        public void IncrementBundleRetired() => BundleRetiredCount++;

        /// <summary>Increment retired instruction count.</summary>
        public void IncrementInstrRetired() => InstrRetiredCount++;

        /// <summary>
        /// Increment retired instruction count for a specific VT.
        /// </summary>
        /// <param name="vtId">VT ID (0–3).</param>
        public void IncrementInstrRetiredForVt(byte vtId)
        {
            InstrRetiredCount++;
            if (vtId < VtCount) _instrCountPerVt[vtId]++;
        }

        /// <summary>Increment VM exit count.</summary>
        public void IncrementVmExit() => VmExitCount++;

        /// <summary>
        /// Increment barrier count (for POD_BARRIER or VT_BARRIER).
        /// </summary>
        public void IncrementBarrier() => BarrierCount++;

        /// <summary>Increment FSP steal/pilfer count.</summary>
        public void IncrementSteal() => StealCount++;

        /// <summary>Increment replay operation count.</summary>
        public void IncrementReplay() => ReplayCount++;

        /// <summary>
        /// Increment AMO*_D (64-bit atomic) count for the given VT.
        /// </summary>
        /// <param name="vtId">VT ID (0–3).</param>
        public void IncrementAmoDwordCount(byte vtId)
        {
            if (vtId < VtCount) _amoDwordCountPerVt[vtId]++;
        }

        /// <summary>
        /// Increment AMO*_W (32-bit atomic) count for the given VT.
        /// </summary>
        /// <param name="vtId">VT ID (0–3).</param>
        public void IncrementAmoWordCount(byte vtId)
        {
            if (vtId < VtCount) _amoWordCountPerVt[vtId]++;
        }

        /// <summary>
        /// Increment LR (load-reserved) count for the given VT.
        /// </summary>
        /// <param name="vtId">VT ID (0–3).</param>
        public void IncrementLrCount(byte vtId)
        {
            if (vtId < VtCount) _lrCountPerVt[vtId]++;
        }

        /// <summary>
        /// Increment SC success count for the given VT.
        /// </summary>
        /// <param name="vtId">VT ID (0–3).</param>
        public void IncrementScSuccess(byte vtId)
        {
            if (vtId < VtCount) _scSuccessCountPerVt[vtId]++;
        }

        /// <summary>
        /// Increment SC failure count for the given VT.
        /// </summary>
        /// <param name="vtId">VT ID (0–3).</param>
        public void IncrementScFail(byte vtId)
        {
            if (vtId < VtCount) _scFailCountPerVt[vtId]++;
        }

        // ─── Per-VT read APIs ────────────────────────────────────────────────

        /// <summary>Total AMO*_D operations for VT <paramref name="vtId"/>.</summary>
        public ulong GetAmoDwordCount(byte vtId)  => vtId < VtCount ? _amoDwordCountPerVt[vtId]  : 0;

        /// <summary>Total AMO*_W operations for VT <paramref name="vtId"/>.</summary>
        public ulong GetAmoWordCount(byte vtId)   => vtId < VtCount ? _amoWordCountPerVt[vtId]   : 0;

        /// <summary>Total LR operations for VT <paramref name="vtId"/>.</summary>
        public ulong GetLrCount(byte vtId)        => vtId < VtCount ? _lrCountPerVt[vtId]        : 0;

        /// <summary>Total SC successes for VT <paramref name="vtId"/>.</summary>
        public ulong GetScSuccessCount(byte vtId) => vtId < VtCount ? _scSuccessCountPerVt[vtId] : 0;

        /// <summary>Total SC failures for VT <paramref name="vtId"/>.</summary>
        public ulong GetScFailCount(byte vtId)    => vtId < VtCount ? _scFailCountPerVt[vtId]    : 0;

        /// <summary>Total instructions retired by VT <paramref name="vtId"/>.</summary>
        public ulong GetInstrCountForVt(byte vtId) => vtId < VtCount ? _instrCountPerVt[vtId]   : 0;

        /// <summary>
        /// Total AMO*_D operations across all VTs.
        /// </summary>
        public ulong GetTotalAmoDwordCount()
        {
            ulong sum = 0;
            for (int i = 0; i < VtCount; i++) sum += _amoDwordCountPerVt[i];
            return sum;
        }

        // ─── Counter dispatch from v4 trace events ───────────────────────────

        /// <summary>
        /// Update telemetry counters from a <see cref="V4TraceEvent"/>.
        /// Called by the v4 trace event recording path to keep counters in sync.
        /// </summary>
        /// <param name="evt">The trace event to account for.</param>
        public void ApplyTraceEvent(V4TraceEvent evt)
        {
            switch (evt.Kind)
            {
                case TraceEventKind.BundleRetired:
                    IncrementBundleRetired();
                    break;

                case TraceEventKind.AluExecuted:
                case TraceEventKind.LoadExecuted:
                case TraceEventKind.StoreExecuted:
                case TraceEventKind.FenceExecuted:
                case TraceEventKind.BranchTaken:
                case TraceEventKind.BranchNotTaken:
                case TraceEventKind.JumpExecuted:
                case TraceEventKind.TrapTaken:
                case TraceEventKind.PrivilegeReturn:
                case TraceEventKind.WfiEntered:
                case TraceEventKind.CsrRead:
                case TraceEventKind.CsrWrite:
                    IncrementInstrRetiredForVt(evt.VtId);
                    break;

                case TraceEventKind.LrExecuted:
                    IncrementInstrRetiredForVt(evt.VtId);
                    IncrementLrCount(evt.VtId);
                    break;

                case TraceEventKind.ScSucceeded:
                    IncrementInstrRetiredForVt(evt.VtId);
                    IncrementScSuccess(evt.VtId);
                    break;

                case TraceEventKind.ScFailed:
                    IncrementInstrRetiredForVt(evt.VtId);
                    IncrementScFail(evt.VtId);
                    break;

                case TraceEventKind.AmoWordExecuted:
                    IncrementInstrRetiredForVt(evt.VtId);
                    IncrementAmoWordCount(evt.VtId);
                    break;

                case TraceEventKind.AmoDwordExecuted:
                    IncrementInstrRetiredForVt(evt.VtId);
                    IncrementAmoDwordCount(evt.VtId);
                    break;

                case TraceEventKind.VtYield:
                case TraceEventKind.VtWfe:
                case TraceEventKind.VtSev:
                    IncrementInstrRetiredForVt(evt.VtId);
                    break;

                case TraceEventKind.PodBarrierEntered:
                case TraceEventKind.VtBarrierEntered:
                    IncrementInstrRetiredForVt(evt.VtId);
                    IncrementBarrier();
                    break;

                // Exited events do not count a second instruction retirement
                case TraceEventKind.PodBarrierExited:
                case TraceEventKind.VtBarrierExited:
                    break;

                case TraceEventKind.VmxOn:
                case TraceEventKind.VmxOff:
                case TraceEventKind.VmEntry:
                case TraceEventKind.VmEntryFailed:
                case TraceEventKind.VmcsRead:
                case TraceEventKind.VmcsWrite:
                    IncrementInstrRetiredForVt(evt.VtId);
                    break;

                case TraceEventKind.VmExit:
                    IncrementVmExit();
                    break;

                case TraceEventKind.FspPilfer:
                    IncrementSteal();
                    break;

                case TraceEventKind.BundleReplayed:
                    IncrementReplay();
                    break;

                // Other events (BundleDispatched, FspBoundary, FsmTransition) do not
                // map to simple counters.
                default:
                    break;
            }
        }

        /// <summary>Reset all counters to zero.</summary>
        public void Reset()
        {
            CycleCount          = 0;
            BundleRetiredCount  = 0;
            InstrRetiredCount   = 0;
            VmExitCount         = 0;
            BarrierCount        = 0;
            StealCount          = 0;
            ReplayCount         = 0;
            Array.Clear(_amoDwordCountPerVt,  0, VtCount);
            Array.Clear(_amoWordCountPerVt,   0, VtCount);
            Array.Clear(_lrCountPerVt,        0, VtCount);
            Array.Clear(_scSuccessCountPerVt, 0, VtCount);
            Array.Clear(_scFailCountPerVt,    0, VtCount);
            Array.Clear(_instrCountPerVt,     0, VtCount);
        }

        /// <summary>
        /// Export all counters as a CSR snapshot compatible with
        /// <see cref="ReplaySnapshot.CsrSnapshot"/>.
        /// </summary>
        public IReadOnlyDictionary<ushort, long> ExportAsCsrSnapshot()
            => new Dictionary<ushort, long>
            {
                [CsrAddresses.Cycle]      = checked((long)CycleCount),
                [CsrAddresses.BundleRet]  = checked((long)BundleRetiredCount),
                [CsrAddresses.InstrRet]   = checked((long)InstrRetiredCount),
                [CsrAddresses.VmExitCnt]  = checked((long)VmExitCount),
                [CsrAddresses.BarrierCnt] = checked((long)BarrierCount),
                [CsrAddresses.StealCnt]   = checked((long)StealCount),
                [CsrAddresses.ReplayCnt]  = checked((long)ReplayCount),
            };
    }
}
