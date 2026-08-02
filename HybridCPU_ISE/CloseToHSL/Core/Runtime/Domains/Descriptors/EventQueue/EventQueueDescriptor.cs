namespace YAKSys_Hybrid_CPU.Core;

public enum EventQueueAuthority : byte
{
    Runtime = 0,
    CompatibilityProjection = 1,
}

public sealed partial class EventQueueDescriptor
{
    public const int DefaultMaxPendingEvents = 64;

    public EventQueueDescriptor()
        : this(
            authority: EventQueueAuthority.Runtime,
            maxPendingEvents: DefaultMaxPendingEvents,
            prefersPriorityDelivery: true,
            allowsEventCoalescing: true,
            allowsCompatibilityProjection: true)
    {
    }

    public EventQueueDescriptor(
        EventQueueAuthority authority,
        int maxPendingEvents,
        bool prefersPriorityDelivery,
        bool allowsEventCoalescing,
        bool allowsCompatibilityProjection)
    {
        Authority = authority;
        MaxPendingEvents = maxPendingEvents <= 0 ? 1 : maxPendingEvents;
        PrefersPriorityDelivery = prefersPriorityDelivery;
        AllowsEventCoalescing = allowsEventCoalescing;
        AllowsCompatibilityProjection = allowsCompatibilityProjection;
    }

    public EventQueueAuthority Authority { get; }

    public int MaxPendingEvents { get; }

    public bool PrefersPriorityDelivery { get; }

    public bool AllowsEventCoalescing { get; }

    public bool AllowsCompatibilityProjection { get; }

    public bool IsRuntimeAuthoritative =>
        Authority == EventQueueAuthority.Runtime;

    public bool CanProjectToCompatibilityFrontend =>
        AllowsCompatibilityProjection &&
        IsRuntimeAuthoritative;

    public bool AcceptsPendingCount(int pendingCount) =>
        pendingCount >= 0 &&
        pendingCount < MaxPendingEvents;

    public EventQueueDescriptor WithMaxPendingEvents(int maxPendingEvents) =>
        new(
            Authority,
            maxPendingEvents,
            PrefersPriorityDelivery,
            AllowsEventCoalescing,
            AllowsCompatibilityProjection);
}
