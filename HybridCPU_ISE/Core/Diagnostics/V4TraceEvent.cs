// ─────────────────────────────────────────────────────────────────────────────
// HybridCPU ISA v4 — Diagnostics / Tracing
// Phase 11: Deterministic Replay and Trace Integration
// ─────────────────────────────────────────────────────────────────────────────

namespace YAKSys_Hybrid_CPU.Core
{
    /// <summary>
    /// Lightweight v4 trace event record.
    /// <para>
    /// Every architecturally observable event is recorded as a <see cref="V4TraceEvent"/>
    /// stamped with the bundle serial, VT ID, and pipeline FSM state at the time of the
    /// event. This record is append-only — no events may be retroactively modified.
    /// </para>
    /// </summary>
    public readonly struct V4TraceEvent
    {
        /// <summary>Monotonically increasing bundle execution counter at event time.</summary>
        public ulong BundleSerial { get; init; }

        /// <summary>Virtual thread ID (0–3) that produced this event.</summary>
        public byte VtId { get; init; }

        /// <summary>Pipeline FSM state at the time of this event.</summary>
        public PipelineState FsmState { get; init; }

        /// <summary>Classification of this event.</summary>
        public TraceEventKind Kind { get; init; }

        /// <summary>
        /// Optional payload for events that carry additional data.
        /// <list type="bullet">
        ///   <item><see cref="TraceEventKind.VmExit"/> — VM-exit reason code.</item>
        ///   <item><see cref="TraceEventKind.FsmTransition"/> — encoded as
        ///     <c>(FromState &lt;&lt; 8) | ToState</c>.</item>
        ///   <item><see cref="TraceEventKind.CsrRead"/> / <see cref="TraceEventKind.CsrWrite"/>
        ///     — CSR address.</item>
        ///   <item>All other events: 0.</item>
        /// </list>
        /// </summary>
        public ulong Payload { get; init; }

        /// <summary>
        /// Create a <see cref="V4TraceEvent"/> with no payload.
        /// </summary>
        public static V4TraceEvent Create(
            ulong bundleSerial,
            byte vtId,
            PipelineState fsmState,
            TraceEventKind kind)
            => new()
            {
                BundleSerial = bundleSerial,
                VtId         = vtId,
                FsmState     = fsmState,
                Kind         = kind,
                Payload      = 0,
            };

        /// <summary>
        /// Create a <see cref="V4TraceEvent"/> with an opaque payload.
        /// </summary>
        public static V4TraceEvent Create(
            ulong bundleSerial,
            byte vtId,
            PipelineState fsmState,
            TraceEventKind kind,
            ulong payload)
            => new()
            {
                BundleSerial = bundleSerial,
                VtId         = vtId,
                FsmState     = fsmState,
                Kind         = kind,
                Payload      = payload,
            };

        /// <inheritdoc/>
        public override string ToString()
            => $"[Bundle={BundleSerial} VT={VtId} FSM={FsmState}] {Kind} payload=0x{Payload:X}";
    }
}
