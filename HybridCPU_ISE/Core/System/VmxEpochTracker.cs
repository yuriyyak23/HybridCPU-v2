using System.ComponentModel;

namespace YAKSys_Hybrid_CPU.Core
{
    /// <summary>
    /// Tracks VM transition epoch boundaries for deterministic replay.
    /// <para>
    /// Each VM-Entry, VM-Exit, VMXON, or VMXOFF constitutes an epoch boundary.
    /// Any replay template or bundle certificate cached with an older epoch serial
    /// must be treated as invalid at the next bundle boundary.
    /// </para>
    /// <para>
    /// Implements <see cref="IVmxEventSink"/> as a diagnostics-only observer of
    /// retired VMX outcomes. It does not own live VMX semantics.
    /// </para>
    /// </summary>
    [EditorBrowsable(EditorBrowsableState.Never)]
    internal sealed class VmxEpochTracker : IVmxEventSink
    {
        private ulong _epoch;
        private VmxEventKind _lastTransitionKind = VmxEventKind.VmxOff;
        private bool _hasTransitioned;

        /// <summary>Monotonically increasing epoch serial. Starts at 0.</summary>
        public ulong CurrentEpoch => _epoch;

        /// <summary>
        /// The VMX event kind that triggered the most-recent epoch bump,
        /// or <c>null</c> if no VM transition has occurred since construction
        /// or <see cref="Reset"/>.
        /// </summary>
        public VmxEventKind? LastTransitionKind => _hasTransitioned ? _lastTransitionKind : null;

        /// <summary>
        /// Returns <c>true</c> when the given VMX event constitutes an epoch boundary.
        /// </summary>
        public static bool IsEpochBoundary(VmxEventKind kind) =>
            kind is VmxEventKind.VmEntry
                 or VmxEventKind.VmResume
                 or VmxEventKind.VmExit
                 or VmxEventKind.VmxOn
                 or VmxEventKind.VmxOff;

        public void RecordVmxEvent(VmxEventKind kind, ushort vtId, VmExitReason exitReason = VmExitReason.None)
        {
            if (IsEpochBoundary(kind))
            {
                _epoch++;
                _lastTransitionKind = kind;
                _hasTransitioned = true;
            }
        }

        /// <summary>
        /// Returns <c>true</c> when <paramref name="cachedEpoch"/> equals the
        /// current epoch, which means no VM transition has occurred since the
        /// value was cached.
        /// </summary>
        public bool IsCurrentEpoch(ulong cachedEpoch) => cachedEpoch == _epoch;

        /// <summary>
        /// Reset the epoch tracker to its initial state.
        /// </summary>
        public void Reset()
        {
            _epoch = 0;
            _lastTransitionKind = VmxEventKind.VmxOff;
            _hasTransitioned = false;
        }
    }
}
