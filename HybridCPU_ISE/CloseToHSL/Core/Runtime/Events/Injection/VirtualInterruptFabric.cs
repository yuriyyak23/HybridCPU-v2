namespace YAKSys_Hybrid_CPU.Core
{
    public sealed partial class VirtualInterruptFabric
    {
        private readonly PostedEventQueue _pending;
        private readonly InterruptRemapTable _remap = new();
        private ulong _inServiceBits;

        public VirtualInterruptFabric(int capacity = 64)
        {
            _pending = new PostedEventQueue(capacity);
        }

        public int PendingCount => _pending.Count;

        public ulong DroppedCount => _pending.DroppedCount + RemapDroppedCount;

        public ulong InvalidDroppedCount => _pending.InvalidDroppedCount;

        public ulong CapacityDroppedCount => _pending.CapacityDroppedCount;

        public ulong RemapDroppedCount { get; private set; }

        public ulong RemappedCount { get; private set; }

        public ulong CoalescedCount { get; private set; }

        public ulong RoutingEpoch { get; private set; }

        public ulong RemapPolicyEpoch => _remap.PolicyEpoch;

        public ulong DeliveredCount { get; private set; }

        public ulong EoiCount { get; private set; }

        public bool TryPost(
            EventInjectionDescriptor descriptor,
            out EventInjectionDescriptor queued)
        {
            return TryPost(descriptor, out queued, out _);
        }

        public bool TryPost(
            EventInjectionDescriptor descriptor,
            out EventInjectionDescriptor queued,
            out InterruptPostDisposition disposition)
        {
            queued = default;

            if (!_remap.TryRoute(
                    descriptor,
                    out EventInjectionDescriptor routed,
                    out bool remapped,
                    out bool coalescePosted,
                    out disposition))
            {
                if (disposition == InterruptPostDisposition.DroppedByRemap)
                {
                    RemapDroppedCount++;
                    AdvanceRoutingEpoch();
                    return false;
                }

                _pending.TryEnqueue(descriptor, out _, out disposition);
                AdvanceRoutingEpoch();
                return false;
            }

            if (remapped)
            {
                RemappedCount++;
            }

            EventInjectionDescriptor posted = routed with { Posted = true };
            if (coalescePosted &&
                _pending.TryFindCoalescingMatch(posted, out EventInjectionDescriptor existing))
            {
                queued = existing;
                CoalescedCount++;
                disposition = remapped
                    ? InterruptPostDisposition.RemappedCoalesced
                    : InterruptPostDisposition.Coalesced;
                AdvanceRoutingEpoch();
                return true;
            }

            bool accepted = _pending.TryEnqueue(posted, out queued, out disposition);
            if (accepted)
            {
                disposition = remapped
                    ? InterruptPostDisposition.RemappedQueued
                    : InterruptPostDisposition.Queued;
            }

            AdvanceRoutingEpoch();
            return accepted;
        }

        public bool TryDeliver(
            byte targetVtId,
            ushort executionDomainTag,
            bool blocked,
            out EventInjectionDescriptor descriptor)
        {
            if (blocked ||
                !_pending.TryDequeueMatching(targetVtId, executionDomainTag, preferPriority: true, out descriptor))
            {
                descriptor = default;
                return false;
            }

            if (descriptor.Vector < 64)
            {
                _inServiceBits |= 1UL << descriptor.Vector;
            }

            DeliveredCount++;
            AdvanceRoutingEpoch();
            return true;
        }

        public void CompleteEoi(byte vector)
        {
            if (vector < 64)
            {
                _inServiceBits &= ~(1UL << vector);
            }

            EoiCount++;
            AdvanceRoutingEpoch();
        }

        public bool IsInService(byte vector) =>
            vector < 64 && (_inServiceBits & (1UL << vector)) != 0;

        private void AdvanceRoutingEpoch()
        {
            unchecked
            {
                RoutingEpoch++;
            }
        }
    }
}
