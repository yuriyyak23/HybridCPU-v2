using System.ComponentModel;

namespace YAKSys_Hybrid_CPU.Core
{
    /// <summary>
    /// Sink for VMX diagnostic events.
    /// <para>
    /// This is an observability mirror of retired VMX outcomes. It is not a
    /// production semantic owner for VMX execution, VMCS lifecycle, or VM-entry /
    /// VM-exit control-flow.
    /// </para>
    /// </summary>
    [EditorBrowsable(EditorBrowsableState.Never)]
    internal interface IVmxEventSink
    {
        /// <summary>
        /// Record a VMX event for diagnostic trace and replay observability.
        /// </summary>
        /// <param name="kind">The kind of VMX event.</param>
        /// <param name="vtId">Virtual thread ID that triggered the event.</param>
        /// <param name="exitReason">VM-Exit reason when <paramref name="kind"/> is <see cref="VmxEventKind.VmExit"/>.</param>
        void RecordVmxEvent(VmxEventKind kind, ushort vtId, VmExitReason exitReason = VmExitReason.None);
    }

    /// <summary>
    /// No-op VMX event sink that discards all events.
    /// Used when no diagnostic trace projection is configured.
    /// </summary>
    [EditorBrowsable(EditorBrowsableState.Never)]
    internal sealed class NullVmxEventSink : IVmxEventSink
    {
        public static readonly NullVmxEventSink Instance = new();

        public void RecordVmxEvent(VmxEventKind kind, ushort vtId, VmExitReason exitReason = VmExitReason.None)
        {
            // Intentionally empty.
        }
    }
}
