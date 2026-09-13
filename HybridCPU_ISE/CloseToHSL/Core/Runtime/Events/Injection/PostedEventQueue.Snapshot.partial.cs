using System.Collections.Generic;

namespace YAKSys_Hybrid_CPU.Core
{
    public sealed partial class PostedEventQueue
    {
        public PostedEventQueueSnapshot CreateSnapshot() =>
            new(
                new List<EventInjectionDescriptor>(_queue),
                _nextSequence,
                InvalidDroppedCount,
                CapacityDroppedCount);

        public void RestoreSnapshot(PostedEventQueueSnapshot snapshot)
        {
            _queue.Clear();
            foreach (EventInjectionDescriptor descriptor in snapshot.PendingEvents)
            {
                if (descriptor.IsValid)
                {
                    _queue.Enqueue(descriptor);
                }
            }

            _nextSequence = snapshot.NextSequence;
            InvalidDroppedCount = snapshot.InvalidDroppedCount;
            CapacityDroppedCount = snapshot.CapacityDroppedCount;
        }
    }
}
