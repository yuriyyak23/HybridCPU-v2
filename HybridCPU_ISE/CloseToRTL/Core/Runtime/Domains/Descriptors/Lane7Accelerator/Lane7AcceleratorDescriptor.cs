namespace YAKSys_Hybrid_CPU.Core;

public enum Lane7AcceleratorAuthority : byte
{
    Runtime = 0,
    DomainDescriptor = 1,
    CompatibilityProjection = 2,
}

public sealed partial class Lane7AcceleratorDescriptor
{
    public Lane7AcceleratorDescriptor()
        : this(
            authority: Lane7AcceleratorAuthority.Runtime,
            laneId: VirtualizationLaneBindingPolicy.Lane7Id,
            backendBindingId: 0,
            handleNamespaceId: 0,
            tokenNamespaceId: 0,
            completionRouteId: 0,
            requiresRuntimeBackendBinding: true,
            allowsCompatibilityProjection: true)
    {
    }

    public Lane7AcceleratorDescriptor(
        Lane7AcceleratorAuthority authority,
        byte laneId,
        ulong backendBindingId,
        ulong handleNamespaceId,
        ulong tokenNamespaceId,
        ulong completionRouteId,
        bool requiresRuntimeBackendBinding,
        bool allowsCompatibilityProjection)
    {
        Authority = authority;
        LaneId = laneId;
        BackendBindingId = backendBindingId;
        HandleNamespaceId = handleNamespaceId;
        TokenNamespaceId = tokenNamespaceId;
        CompletionRouteId = completionRouteId;
        RequiresRuntimeBackendBinding = requiresRuntimeBackendBinding;
        AllowsCompatibilityProjection = allowsCompatibilityProjection;
    }

    public Lane7AcceleratorAuthority Authority { get; }

    public byte LaneId { get; }

    public ulong BackendBindingId { get; }

    public ulong HandleNamespaceId { get; }

    public ulong TokenNamespaceId { get; }

    public ulong CompletionRouteId { get; }

    public bool RequiresRuntimeBackendBinding { get; }

    public bool AllowsCompatibilityProjection { get; }

    public bool IsRuntimeAuthoritative =>
        Authority == Lane7AcceleratorAuthority.Runtime;

    public bool IsPinnedToLane7 =>
        LaneId == VirtualizationLaneBindingPolicy.Lane7Id;

    public bool HasBackendBinding =>
        BackendBindingId != 0;

    public bool HasNamespaceBinding =>
        HandleNamespaceId != 0 ||
        TokenNamespaceId != 0 ||
        CompletionRouteId != 0;

    public bool CanProjectToCompatibilityFrontend =>
        AllowsCompatibilityProjection &&
        IsRuntimeAuthoritative;

    public Lane7AcceleratorDescriptor WithBackendBinding(
        ulong backendBindingId,
        ulong handleNamespaceId,
        ulong tokenNamespaceId,
        ulong completionRouteId) =>
        new(
            Authority,
            LaneId,
            backendBindingId,
            handleNamespaceId,
            tokenNamespaceId,
            completionRouteId,
            RequiresRuntimeBackendBinding,
            AllowsCompatibilityProjection);
}
