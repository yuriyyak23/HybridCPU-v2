namespace YAKSys_Hybrid_CPU.Core;

public enum Lane6DomainAuthority : byte
{
    Runtime = 0,
    DomainDescriptor = 1,
    CompatibilityProjection = 2,
}

public sealed partial class Lane6DomainDescriptor
{
    public Lane6DomainDescriptor()
        : this(
            authority: Lane6DomainAuthority.Runtime,
            laneId: VirtualizationLaneBindingPolicy.Lane6Id,
            tokenNamespaceId: 0,
            queueNamespaceId: 0,
            fenceDomainId: 0,
            allowsCompatibilityProjection: true)
    {
    }

    public Lane6DomainDescriptor(
        Lane6DomainAuthority authority,
        byte laneId,
        ulong tokenNamespaceId,
        ulong queueNamespaceId,
        ulong fenceDomainId,
        bool allowsCompatibilityProjection)
    {
        Authority = authority;
        LaneId = laneId;
        TokenNamespaceId = tokenNamespaceId;
        QueueNamespaceId = queueNamespaceId;
        FenceDomainId = fenceDomainId;
        AllowsCompatibilityProjection = allowsCompatibilityProjection;
    }

    public Lane6DomainAuthority Authority { get; }

    public byte LaneId { get; }

    public ulong TokenNamespaceId { get; }

    public ulong QueueNamespaceId { get; }

    public ulong FenceDomainId { get; }

    public bool AllowsCompatibilityProjection { get; }

    public bool IsRuntimeAuthoritative =>
        Authority == Lane6DomainAuthority.Runtime;

    public bool IsPinnedToLane6 =>
        LaneId == VirtualizationLaneBindingPolicy.Lane6Id;

    public bool HasNamespaceBinding =>
        TokenNamespaceId != 0 ||
        QueueNamespaceId != 0 ||
        FenceDomainId != 0;

    public bool CanProjectToCompatibilityFrontend =>
        AllowsCompatibilityProjection &&
        IsRuntimeAuthoritative;

    public Lane6DomainDescriptor WithNamespaceBinding(
        ulong tokenNamespaceId,
        ulong queueNamespaceId,
        ulong fenceDomainId) =>
        new(
            Authority,
            LaneId,
            tokenNamespaceId,
            queueNamespaceId,
            fenceDomainId,
            AllowsCompatibilityProjection);
}
