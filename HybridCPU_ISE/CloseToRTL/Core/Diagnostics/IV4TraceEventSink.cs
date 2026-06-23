// ─────────────────────────────────────────────────────────────────────────────
// HybridCPU ISA v4 — Diagnostics / Tracing
// Phase 11: Deterministic Replay and Trace Integration
// ─────────────────────────────────────────────────────────────────────────────

namespace YAKSys_Hybrid_CPU.Core
{
    /// <summary>
    /// Sink for v4 trace events.
    /// <para>
    /// All v4 instruction classes that produce architecturally observable events
    /// (ALU, memory, atomic, CSR, SMT/VT, VMX, FSP) route events through this
    /// interface for deterministic replay and diagnostic tracing.
    /// </para>
    /// <para>
    /// The trace is append-only during execution — no events may be retroactively
    /// modified after recording.
    /// </para>
    /// </summary>
    public interface IV4TraceEventSink
    {
        /// <summary>
        /// Record a v4 trace event.
        /// </summary>
        /// <param name="evt">The event to record.</param>
        void RecordV4Event(V4TraceEvent evt);
    }

    /// <summary>
    /// No-op v4 trace event sink that discards all events.
    /// Used when no diagnostic trace is configured.
    /// </summary>
    public sealed class NullV4TraceEventSink : IV4TraceEventSink
    {
        /// <summary>Singleton instance.</summary>
        public static readonly NullV4TraceEventSink Instance = new();

        private NullV4TraceEventSink() { }

        /// <inheritdoc/>
        public void RecordV4Event(V4TraceEvent evt)
        {
            // No-op — events are discarded
        }
    }
}
