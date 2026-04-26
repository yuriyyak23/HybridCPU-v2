using System;
using System.ComponentModel;

namespace YAKSys_Hybrid_CPU.Core
{
    /// <summary>
    /// An <see cref="IVmxEventSink"/> that forwards every event to two underlying
    /// diagnostics sinks in order: <see cref="Primary"/> first, then <see cref="Secondary"/>.
    /// <para>
    /// Typical use: wire a <see cref="VmxEpochTracker"/> alongside a diagnostic
    /// recording sink without changing <see cref="VmxExecutionUnit"/>'s runtime contract.
    /// </para>
    /// </summary>
    [EditorBrowsable(EditorBrowsableState.Never)]
    internal sealed class CompositeVmxEventSink : IVmxEventSink
    {
        /// <summary>The first sink to receive each event.</summary>
        public IVmxEventSink Primary { get; }

        /// <summary>The second sink to receive each event.</summary>
        public IVmxEventSink Secondary { get; }

        /// <param name="primary">First sink.</param>
        /// <param name="secondary">Second sink.</param>
        public CompositeVmxEventSink(IVmxEventSink primary, IVmxEventSink secondary)
        {
            Primary = primary ?? throw new ArgumentNullException(nameof(primary));
            Secondary = secondary ?? throw new ArgumentNullException(nameof(secondary));
        }

        public void RecordVmxEvent(VmxEventKind kind, ushort vtId, VmExitReason exitReason = VmExitReason.None)
        {
            Primary.RecordVmxEvent(kind, vtId, exitReason);
            Secondary.RecordVmxEvent(kind, vtId, exitReason);
        }
    }
}
