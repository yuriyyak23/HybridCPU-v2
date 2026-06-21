// ─────────────────────────────────────────────────────────────────────────────
// HybridCPU ISA v4 — Diagnostics / Tracing
// Phase 11: Deterministic Replay and Trace Integration
// ─────────────────────────────────────────────────────────────────────────────

using System.Collections.Generic;

namespace YAKSys_Hybrid_CPU.Core
{
    /// <summary>
    /// Replay snapshot captured at bundle boundaries.
    /// <para>
    /// Contains all state required to deterministically replay execution from this
    /// point. Snapshots are captured at:
    /// <list type="bullet">
    ///   <item>Bundles with <see cref="BundleMetadata.IsReplayAnchor"/> = <c>true</c>.</item>
    ///   <item>Every <see cref="ReplayAnchorEvaluator.PeriodicSnapshotInterval"/> bundles
    ///     as a periodic fallback.</item>
    ///   <item>Pipeline FSM state transitions (VM entry/exit).</item>
    /// </list>
    /// </para>
    /// </summary>
    public sealed record ReplaySnapshot
    {
        /// <summary>Bundle serial number at this snapshot.</summary>
        public required ulong BundleSerial { get; init; }

        /// <summary>Pipeline FSM state at snapshot time (from Phase 05).</summary>
        public required PipelineState FsmState { get; init; }

        /// <summary>VT ID of the executing virtual thread at snapshot time.</summary>
        public required byte VtId { get; init; }

        /// <summary>Program counter (PC) at snapshot time.</summary>
        public required ulong Pc { get; init; }

        /// <summary>
        /// Register file snapshot: all 32 general-purpose registers as signed 64-bit values.
        /// Length must be exactly 32 (indices 0–31; x0 is always 0 but included for index
        /// consistency).
        /// </summary>
        public required long[] Registers { get; init; }

        /// <summary>
        /// CSR snapshot: all v4 CSRs at snapshot time, keyed by CSR address.
        /// Includes counter CSRs (<c>Cycle</c>, <c>BundleRet</c>, <c>InstrRet</c>,
        /// <c>VmExitCnt</c>, <c>BarrierCnt</c>, <c>StealCnt</c>, <c>ReplayCnt</c>).
        /// </summary>
        public required IReadOnlyDictionary<ushort, long> CsrSnapshot { get; init; }

        /// <summary>
        /// LR reservation state at snapshot time.
        /// Required for deterministic replay of LR/SC sequences:
        /// if <see cref="Valid"/> is <c>true</c> and <see cref="Address"/> is within the
        /// reservation window, the first SC following replay will succeed.
        /// </summary>
        public required (ulong Address, bool Valid) LrReservation { get; init; }

        /// <summary>Cycle counter value at snapshot time (from the <c>Cycle</c> CSR).</summary>
        public required ulong CycleCount { get; init; }

        /// <summary>
        /// Indicates how this snapshot was triggered.
        /// </summary>
        public SnapshotTrigger Trigger { get; init; } = SnapshotTrigger.Periodic;
    }

    /// <summary>
    /// Reason a <see cref="ReplaySnapshot"/> was captured.
    /// </summary>
    public enum SnapshotTrigger : byte
    {
        /// <summary>Periodic fallback capture (every N bundles).</summary>
        Periodic = 0,

        /// <summary>Bundle was marked with <see cref="BundleMetadata.IsReplayAnchor"/>.</summary>
        AnchorHint = 1,

        /// <summary>Pipeline FSM state transition (VM entry/exit).</summary>
        FsmTransition = 2,
    }
}
