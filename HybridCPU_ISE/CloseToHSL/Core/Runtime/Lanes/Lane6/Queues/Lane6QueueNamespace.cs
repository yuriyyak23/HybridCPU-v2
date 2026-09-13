namespace YAKSys_Hybrid_CPU.Core;

public enum Lane6QueueNamespaceAuthority : byte
{
    Runtime = 0,
    Lane6DomainDescriptor = 1,
    CompatibilityProjection = 2,
}

public enum Lane6QueueVisibility : byte
{
    None = 0,
    VirtualQueueIds = 1,
    NativeQueueHandles = 2,
}

public sealed partial class Lane6QueueNamespace
{
    public Lane6QueueNamespace()
        : this(
            authority: Lane6QueueNamespaceAuthority.Runtime,
            namespaceId: 0,
            visibility: Lane6QueueVisibility.VirtualQueueIds,
            allowsCompatibilityProjection: true)
    {
    }

    public Lane6QueueNamespace(
        Lane6QueueNamespaceAuthority authority,
        ulong namespaceId,
        Lane6QueueVisibility visibility,
        bool allowsCompatibilityProjection)
    {
        Authority = authority;
        NamespaceId = namespaceId;
        Visibility = visibility;
        AllowsCompatibilityProjection = allowsCompatibilityProjection;
    }

    public Lane6QueueNamespaceAuthority Authority { get; }

    public ulong NamespaceId { get; }

    public Lane6QueueVisibility Visibility { get; }

    public bool AllowsCompatibilityProjection { get; }

    public bool IsRuntimeAuthoritative =>
        Authority == Lane6QueueNamespaceAuthority.Runtime;

    public bool HasNamespaceBinding =>
        NamespaceId != 0;

    public bool ExposesNativeQueueHandles =>
        Visibility == Lane6QueueVisibility.NativeQueueHandles;

    public bool CanProjectToCompatibilityFrontend =>
        AllowsCompatibilityProjection &&
        IsRuntimeAuthoritative &&
        !ExposesNativeQueueHandles;

    public Lane6QueueNamespace WithNamespaceBinding(ulong namespaceId) =>
        new(Authority, namespaceId, Visibility, AllowsCompatibilityProjection);
}
