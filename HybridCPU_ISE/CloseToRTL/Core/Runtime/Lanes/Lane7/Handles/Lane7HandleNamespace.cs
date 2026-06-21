namespace YAKSys_Hybrid_CPU.Core;

public enum Lane7HandleNamespaceAuthority : byte
{
    Runtime = 0,
    Lane7AcceleratorDescriptor = 1,
    CompatibilityProjection = 2,
}

public enum Lane7HandleVisibility : byte
{
    None = 0,
    VirtualHandles = 1,
    NativeBackendHandles = 2,
}

public sealed partial class Lane7HandleNamespace
{
    public Lane7HandleNamespace()
        : this(
            authority: Lane7HandleNamespaceAuthority.Runtime,
            namespaceId: 0,
            visibility: Lane7HandleVisibility.VirtualHandles,
            allowsCompatibilityProjection: true)
    {
    }

    public Lane7HandleNamespace(
        Lane7HandleNamespaceAuthority authority,
        ulong namespaceId,
        Lane7HandleVisibility visibility,
        bool allowsCompatibilityProjection)
    {
        Authority = authority;
        NamespaceId = namespaceId;
        Visibility = visibility;
        AllowsCompatibilityProjection = allowsCompatibilityProjection;
    }

    public Lane7HandleNamespaceAuthority Authority { get; }

    public ulong NamespaceId { get; }

    public Lane7HandleVisibility Visibility { get; }

    public bool AllowsCompatibilityProjection { get; }

    public bool IsRuntimeAuthoritative =>
        Authority == Lane7HandleNamespaceAuthority.Runtime;

    public bool HasNamespaceBinding =>
        NamespaceId != 0;

    public bool ExposesNativeBackendHandles =>
        Visibility == Lane7HandleVisibility.NativeBackendHandles;

    public bool CanProjectToCompatibilityFrontend =>
        AllowsCompatibilityProjection &&
        IsRuntimeAuthoritative &&
        !ExposesNativeBackendHandles;

    public Lane7HandleNamespace WithNamespaceBinding(ulong namespaceId) =>
        new(Authority, namespaceId, Visibility, AllowsCompatibilityProjection);
}
