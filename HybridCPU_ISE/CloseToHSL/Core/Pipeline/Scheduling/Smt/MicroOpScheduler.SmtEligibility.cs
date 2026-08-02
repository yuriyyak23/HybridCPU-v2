using System;
using System.Numerics;
using System.Runtime.CompilerServices;

namespace YAKSys_Hybrid_CPU.Core
{
    /// <summary>
    /// Phase 06 SMT eligibility helpers.
    /// Scheduler consumes an explicit runnable VT mask from the FSM-facing caller
    /// rather than deriving its own competing wait/wake truth model.
    /// </summary>
    public partial class MicroOpScheduler
    {
        /// <summary>
        /// Eligibility snapshot for the most recent SMT nomination pass.
        /// Diagnostics-only contour: exposes how the FSM-provided runnable mask
        /// intersected with ready SMT donor ports.
        /// </summary>
        public readonly record struct SmtEligibilitySnapshot(
            byte RequestedMask,
            byte NormalizedMask,
            byte ReadyPortMask,
            byte VisibleReadyMask,
            byte MaskedReadyMask)
        {
            public int EligibleCount => BitOperations.PopCount((uint)NormalizedMask);

            public int ReadyPortCount => BitOperations.PopCount((uint)ReadyPortMask);

            public int VisibleReadyCount => BitOperations.PopCount((uint)VisibleReadyMask);

            public int MaskedReadyCount => BitOperations.PopCount((uint)MaskedReadyMask);

            public bool HasMaskedReadyCandidates => MaskedReadyMask != 0;
        }

        public static byte AllEligibleVirtualThreadMask => (byte)((1 << SMT_WAYS) - 1);

        private SmtEligibilitySnapshot _lastSmtEligibilitySnapshot;

        public long EligibilityMaskedCycles { get; private set; }

        public long EligibilityMaskedReadyCandidates { get; private set; }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static byte NormalizeEligibleVirtualThreadMask(byte eligibleVirtualThreadMask)
        {
            return (byte)(eligibleVirtualThreadMask & AllEligibleVirtualThreadMask);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private SmtNominationState CreateEligibleSmtNominationState(byte eligibleVirtualThreadMask)
        {
            byte normalizedEligibleMask = NormalizeEligibleVirtualThreadMask(eligibleVirtualThreadMask);
            byte readyPortMask = 0;
            byte visibleReadyMask = 0;

            for (int vt = 0; vt < SMT_WAYS; vt++)
            {
                if (_smtPortValid[vt] && _smtPorts[vt] is not null)
                {
                    byte vtMask = (byte)(1 << vt);
                    readyPortMask |= vtMask;

                    if ((normalizedEligibleMask & vtMask) != 0)
                    {
                        visibleReadyMask |= vtMask;
                    }
                }
            }

            RecordSmtEligibilitySnapshot(new SmtEligibilitySnapshot(
                eligibleVirtualThreadMask,
                normalizedEligibleMask,
                readyPortMask,
                visibleReadyMask,
                (byte)(readyPortMask & ~normalizedEligibleMask)));

            return new SmtNominationState(visibleReadyMask);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void RecordSmtEligibilitySnapshot(in SmtEligibilitySnapshot snapshot)
        {
            _lastSmtEligibilitySnapshot = snapshot;
            if (snapshot.HasMaskedReadyCandidates)
            {
                EligibilityMaskedCycles++;
                EligibilityMaskedReadyCandidates += snapshot.MaskedReadyCount;
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private SmtNominationSnapshot CaptureSmtNominationSnapshot(byte eligibleVirtualThreadMask)
        {
            return CaptureSmtNominationSnapshot(
                CreateEligibleSmtNominationState(eligibleVirtualThreadMask));
        }
    }
}
