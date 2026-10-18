using System.Collections.Generic;

namespace YAKSys_Hybrid_CPU.Core
{
    public enum LaneCompletionSourceKind : byte
    {
        None = 0,
        DmaStreamComputeLane6 = 1,
        ExternalAcceleratorLane7 = 2,
    }

    public enum LaneCompletionRouteStatus : byte
    {
        None = 0,
        Routed = 1,
        IgnoredRouteDisabled = 2,
        DroppedInvalid = 3,
        DroppedQueueFull = 4,
        DroppedEventQueue = 5,
    }

    public readonly record struct LaneCompletionDescriptor(
        LaneCompletionSourceKind SourceKind,
        byte LaneIndex,
        ushort SourceOpcode,
        ushort OwnerVirtualThreadId,
        ushort ExecutionDomainTag,
        ushort AddressSpaceTag,
        ulong TokenId,
        ulong TokenGeneration,
        ulong DescriptorIdentityHash,
        ulong NormalizedFootprintHash,
        ulong OwnerDomainTag,
        ulong RuntimeQueueSequence,
        ulong CompletionStatus,
        bool Succeeded,
        ulong Sequence)
    {
        public bool IsValid =>
            SourceKind != LaneCompletionSourceKind.None &&
            (LaneIndex == 6 || LaneIndex == 7) &&
            SourceOpcode != 0 &&
            TokenId != 0;

        public ulong EncodePayload()
        {
            ulong payload = TokenId;
            payload ^= TokenGeneration << 7;
            payload ^= DescriptorIdentityHash << 13;
            payload ^= NormalizedFootprintHash >> 11;
            payload ^= OwnerDomainTag << 3;
            payload ^= RuntimeQueueSequence << 17;
            payload ^= CompletionStatus << 41;
            payload ^= (ulong)LaneIndex << 56;
            payload ^= (ulong)SourceOpcode << 32;
            if (Succeeded)
            {
                payload ^= 1UL << 63;
            }

            return payload == 0 ? 1UL : payload;
        }

        public LaneCompletionDescriptor WithSequence(ulong sequence) =>
            this with { Sequence = sequence };
    }

    public readonly record struct LaneCompletionRoute(
        bool Enabled,
        byte LaneIndex,
        byte TargetVtId,
        ushort ExecutionDomainTag,
        ushort AddressSpaceTag,
        byte Vector,
        byte Priority,
        bool CoalescePosted)
    {
        public bool Matches(LaneCompletionDescriptor descriptor) =>
            Enabled &&
            descriptor.IsValid &&
            LaneIndex == descriptor.LaneIndex &&
            (descriptor.ExecutionDomainTag == 0 || ExecutionDomainTag == descriptor.ExecutionDomainTag) &&
            (descriptor.AddressSpaceTag == 0 || AddressSpaceTag == descriptor.AddressSpaceTag) &&
            (TargetVtId == descriptor.OwnerVirtualThreadId ||
             TargetVtId == 0xFF);

        public static LaneCompletionRoute Disabled(byte laneIndex) =>
            new(
                Enabled: false,
                LaneIndex: laneIndex,
                TargetVtId: 0,
                ExecutionDomainTag: 0,
                AddressSpaceTag: 0,
                Vector: 0,
                Priority: 0,
                CoalescePosted: true);

        public static LaneCompletionRoute EnabledForOwner(
            byte laneIndex,
            byte targetVtId,
            ushort executionDomainTag,
            ushort addressSpaceTag,
            byte vector,
            byte priority = 0,
            bool coalescePosted = true) =>
            new(
                Enabled: true,
                LaneIndex: laneIndex,
                TargetVtId: targetVtId,
                ExecutionDomainTag: executionDomainTag,
                AddressSpaceTag: addressSpaceTag,
                Vector: vector,
                Priority: priority,
                CoalescePosted: coalescePosted);
    }

    public readonly record struct LaneCompletionRoutingResult(
        LaneCompletionRouteStatus Status,
        LaneCompletionDescriptor Completion,
        EventInjectionDescriptor Event,
        InterruptPostDisposition EventDisposition,
        ulong RoutingEpoch,
        string Message)
    {
        public bool Routed => Status == LaneCompletionRouteStatus.Routed;

        public static LaneCompletionRoutingResult Create(
            LaneCompletionRouteStatus status,
            LaneCompletionDescriptor completion,
            EventInjectionDescriptor evt,
            InterruptPostDisposition eventDisposition,
            ulong routingEpoch,
            string message) =>
            new(status, completion, evt, eventDisposition, routingEpoch, message);
    }

    public sealed class LaneCompletionRouterSnapshot
    {
        public LaneCompletionRouterSnapshot(
            IReadOnlyList<LaneCompletionRoute> routes,
            ulong routingEpoch,
            ulong routedCount,
            ulong ignoredDisabledCount,
            ulong invalidDroppedCount,
            ulong capacityDroppedCount,
            ulong eventQueueDroppedCount)
        {
            Routes = routes;
            RoutingEpoch = routingEpoch;
            RoutedCount = routedCount;
            IgnoredDisabledCount = ignoredDisabledCount;
            InvalidDroppedCount = invalidDroppedCount;
            CapacityDroppedCount = capacityDroppedCount;
            EventQueueDroppedCount = eventQueueDroppedCount;
        }

        public IReadOnlyList<LaneCompletionRoute> Routes { get; }

        public ulong RoutingEpoch { get; }

        public ulong RoutedCount { get; }

        public ulong IgnoredDisabledCount { get; }

        public ulong InvalidDroppedCount { get; }

        public ulong CapacityDroppedCount { get; }

        public ulong EventQueueDroppedCount { get; }
    }

    public sealed class LaneCompletionQueue
    {
        private readonly Queue<LaneCompletionDescriptor> _queue = new();
        private readonly int _capacity;
        private ulong _nextSequence;

        public LaneCompletionQueue(int capacity = 64)
        {
            _capacity = capacity <= 0 ? 1 : capacity;
        }

        public int Count => _queue.Count;

        public ulong InvalidDroppedCount { get; private set; }

        public ulong CapacityDroppedCount { get; private set; }

        public bool TryEnqueue(
            LaneCompletionDescriptor descriptor,
            out LaneCompletionDescriptor queued,
            out LaneCompletionRouteStatus status)
        {
            queued = default;
            if (!descriptor.IsValid)
            {
                InvalidDroppedCount++;
                status = LaneCompletionRouteStatus.DroppedInvalid;
                return false;
            }

            if (_queue.Count >= _capacity)
            {
                CapacityDroppedCount++;
                status = LaneCompletionRouteStatus.DroppedQueueFull;
                return false;
            }

            unchecked
            {
                _nextSequence++;
                if (_nextSequence == 0)
                {
                    _nextSequence = 1;
                }
            }

            queued = descriptor.WithSequence(_nextSequence);
            _queue.Enqueue(queued);
            status = LaneCompletionRouteStatus.Routed;
            return true;
        }

        public bool TryDequeue(out LaneCompletionDescriptor descriptor)
        {
            if (_queue.Count == 0)
            {
                descriptor = default;
                return false;
            }

            descriptor = _queue.Dequeue();
            return true;
        }

        public void RestoreCounters(ulong invalidDroppedCount, ulong capacityDroppedCount)
        {
            _queue.Clear();
            InvalidDroppedCount = invalidDroppedCount;
            CapacityDroppedCount = capacityDroppedCount;
            _nextSequence = 0;
        }
    }

    public sealed class LaneCompletionRouter
    {
        private readonly LaneCompletionQueue _queue;
        private readonly LaneCompletionRoute[] _routes =
        [
            LaneCompletionRoute.Disabled(0),
            LaneCompletionRoute.Disabled(1),
            LaneCompletionRoute.Disabled(2),
            LaneCompletionRoute.Disabled(3),
            LaneCompletionRoute.Disabled(4),
            LaneCompletionRoute.Disabled(5),
            LaneCompletionRoute.Disabled(6),
            LaneCompletionRoute.Disabled(7),
        ];

        public LaneCompletionRouter(int capacity = 64)
        {
            _queue = new LaneCompletionQueue(capacity);
        }

        public int PendingCount => _queue.Count;

        public ulong RoutingEpoch { get; private set; }

        public ulong RoutedCount { get; private set; }

        public ulong IgnoredDisabledCount { get; private set; }

        public ulong InvalidDroppedCount => _queue.InvalidDroppedCount;

        public ulong CapacityDroppedCount => _queue.CapacityDroppedCount;

        public ulong EventQueueDroppedCount { get; private set; }

        public void ConfigureRoute(LaneCompletionRoute route)
        {
            if (route.LaneIndex >= _routes.Length)
            {
                return;
            }

            _routes[route.LaneIndex] = route;
            AdvanceRoutingEpoch();
        }

        public void DisableRoute(byte laneIndex)
        {
            if (laneIndex >= _routes.Length)
            {
                return;
            }

            _routes[laneIndex] = LaneCompletionRoute.Disabled(laneIndex);
            AdvanceRoutingEpoch();
        }

        public bool TryBuildPostedCompletion(
            LaneCompletionDescriptor descriptor,
            out EventInjectionDescriptor evt,
            out LaneCompletionRoutingResult result)
        {
            evt = default;
            if (!descriptor.IsValid)
            {
                _queue.TryEnqueue(descriptor, out LaneCompletionDescriptor invalid, out _);
                AdvanceRoutingEpoch();
                result = LaneCompletionRoutingResult.Create(
                    LaneCompletionRouteStatus.DroppedInvalid,
                    invalid,
                    evt,
                    InterruptPostDisposition.DroppedInvalid,
                    RoutingEpoch,
                    "Lane completion descriptor was invalid before compatibility completion routing.");
                return false;
            }

            LaneCompletionRoute route = _routes[descriptor.LaneIndex];
            if (!route.Matches(descriptor))
            {
                IgnoredDisabledCount++;
                AdvanceRoutingEpoch();
                result = LaneCompletionRoutingResult.Create(
                    LaneCompletionRouteStatus.IgnoredRouteDisabled,
                    descriptor,
                    evt,
                    InterruptPostDisposition.None,
                    RoutingEpoch,
                    "Lane completion routing is disabled or does not match the completion owner.");
                return false;
            }

            if (!_queue.TryEnqueue(
                    descriptor,
                    out LaneCompletionDescriptor queued,
                    out LaneCompletionRouteStatus queueStatus))
            {
                AdvanceRoutingEpoch();
                result = LaneCompletionRoutingResult.Create(
                    queueStatus,
                    queued,
                    evt,
                    queueStatus == LaneCompletionRouteStatus.DroppedQueueFull
                        ? InterruptPostDisposition.DroppedQueueFull
                        : InterruptPostDisposition.DroppedInvalid,
                    RoutingEpoch,
                    "Lane completion queue rejected the descriptor before posted-event materialization.");
                return false;
            }

            _queue.TryDequeue(out queued);
            ulong payload = queued.EncodePayload();
            if (!route.CoalescePosted)
            {
                payload ^= queued.Sequence << 1;
            }

            evt = EventInjectionDescriptor.Create(
                EventInjectionKind.PostedCompletion,
                route.Vector,
                route.TargetVtId,
                route.ExecutionDomainTag,
                route.AddressSpaceTag,
                route.Priority,
                payload,
                posted: true);

            AdvanceRoutingEpoch();
            result = LaneCompletionRoutingResult.Create(
                LaneCompletionRouteStatus.Routed,
                queued,
                evt,
                InterruptPostDisposition.None,
                RoutingEpoch,
                "Lane completion descriptor was queued and materialized as a posted compatibility completion event.");
            return true;
        }

        public void RecordEventQueueDrop()
        {
            EventQueueDroppedCount++;
            AdvanceRoutingEpoch();
        }

        public void RecordRouted()
        {
            RoutedCount++;
            AdvanceRoutingEpoch();
        }

        public LaneCompletionRouterSnapshot CreateSnapshot() =>
            new(
                new List<LaneCompletionRoute>(_routes),
                RoutingEpoch,
                RoutedCount,
                IgnoredDisabledCount,
                InvalidDroppedCount,
                CapacityDroppedCount,
                EventQueueDroppedCount);

        public void RestoreSnapshot(LaneCompletionRouterSnapshot snapshot)
        {
            int count = snapshot.Routes.Count < _routes.Length
                ? snapshot.Routes.Count
                : _routes.Length;
            for (int index = 0; index < _routes.Length; index++)
            {
                _routes[index] = index < count
                    ? snapshot.Routes[index]
                    : LaneCompletionRoute.Disabled((byte)index);
            }

            _queue.RestoreCounters(snapshot.InvalidDroppedCount, snapshot.CapacityDroppedCount);
            RoutingEpoch = snapshot.RoutingEpoch;
            RoutedCount = snapshot.RoutedCount;
            IgnoredDisabledCount = snapshot.IgnoredDisabledCount;
            EventQueueDroppedCount = snapshot.EventQueueDroppedCount;
        }

        private void AdvanceRoutingEpoch()
        {
            unchecked
            {
                RoutingEpoch++;
                if (RoutingEpoch == 0)
                {
                    RoutingEpoch = 1;
                }
            }
        }
    }
}
