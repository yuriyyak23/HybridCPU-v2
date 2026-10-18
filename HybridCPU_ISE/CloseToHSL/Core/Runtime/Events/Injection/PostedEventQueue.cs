using System.Collections.Generic;

namespace YAKSys_Hybrid_CPU.Core
{
    public sealed class PostedEventQueueSnapshot
    {
        public PostedEventQueueSnapshot(
            IReadOnlyList<EventInjectionDescriptor> pendingEvents,
            ulong nextSequence,
            ulong invalidDroppedCount,
            ulong capacityDroppedCount)
        {
            PendingEvents = pendingEvents;
            NextSequence = nextSequence;
            InvalidDroppedCount = invalidDroppedCount;
            CapacityDroppedCount = capacityDroppedCount;
        }

        public IReadOnlyList<EventInjectionDescriptor> PendingEvents { get; }

        public ulong NextSequence { get; }

        public ulong InvalidDroppedCount { get; }

        public ulong CapacityDroppedCount { get; }
    }

    public sealed partial class PostedEventQueue
    {
        private readonly int _capacity;
        private readonly Queue<EventInjectionDescriptor> _queue = new();
        private ulong _nextSequence;

        public PostedEventQueue(int capacity = 64)
        {
            _capacity = capacity <= 0 ? 1 : capacity;
        }

        public int Count => _queue.Count;

        public ulong DroppedCount => InvalidDroppedCount + CapacityDroppedCount;

        public ulong InvalidDroppedCount { get; private set; }

        public ulong CapacityDroppedCount { get; private set; }

        public bool TryEnqueue(
            EventInjectionDescriptor descriptor,
            out EventInjectionDescriptor queued)
        {
            return TryEnqueue(descriptor, out queued, out _);
        }

        public bool TryEnqueue(
            EventInjectionDescriptor descriptor,
            out EventInjectionDescriptor queued,
            out InterruptPostDisposition disposition)
        {
            if (!descriptor.IsValid)
            {
                queued = default;
                InvalidDroppedCount++;
                disposition = InterruptPostDisposition.DroppedInvalid;
                return false;
            }

            if (_queue.Count >= _capacity)
            {
                queued = default;
                CapacityDroppedCount++;
                disposition = InterruptPostDisposition.DroppedQueueFull;
                return false;
            }

            unchecked
            {
                _nextSequence++;
            }

            queued = descriptor.WithSequence(_nextSequence == 0 ? 1 : _nextSequence);
            _queue.Enqueue(queued);
            disposition = InterruptPostDisposition.Queued;
            return true;
        }

        public bool TryPeek(out EventInjectionDescriptor descriptor) =>
            _queue.TryPeek(out descriptor);

        public bool TryFindCoalescingMatch(
            EventInjectionDescriptor descriptor,
            out EventInjectionDescriptor existing)
        {
            foreach (EventInjectionDescriptor candidate in _queue)
            {
                if (candidate.CoalescesWith(descriptor))
                {
                    existing = candidate;
                    return true;
                }
            }

            existing = default;
            return false;
        }

        public bool TryDequeue(out EventInjectionDescriptor descriptor)
        {
            if (_queue.Count == 0)
            {
                descriptor = default;
                return false;
            }

            descriptor = _queue.Dequeue();
            return true;
        }

        public bool TryDequeueMatching(
            byte targetVtId,
            ushort executionDomainTag,
            bool preferPriority,
            out EventInjectionDescriptor descriptor)
        {
            descriptor = default;
            if (_queue.Count == 0)
            {
                return false;
            }

            int count = _queue.Count;
            bool found = false;
            EventInjectionDescriptor selected = default;
            Queue<EventInjectionDescriptor> retained = new(count);

            for (int i = 0; i < count; i++)
            {
                EventInjectionDescriptor candidate = _queue.Dequeue();
                if (!candidate.Matches(targetVtId, executionDomainTag))
                {
                    retained.Enqueue(candidate);
                    continue;
                }

                if (!found ||
                    (preferPriority && candidate.Priority > selected.Priority) ||
                    (preferPriority &&
                     candidate.Priority == selected.Priority &&
                     candidate.Sequence < selected.Sequence))
                {
                    if (found)
                    {
                        retained.Enqueue(selected);
                    }

                    selected = candidate;
                    found = true;
                    continue;
                }

                retained.Enqueue(candidate);
            }

            while (retained.Count != 0)
            {
                _queue.Enqueue(retained.Dequeue());
            }

            if (!found)
            {
                return false;
            }

            descriptor = selected;
            return true;
        }

    }
}
