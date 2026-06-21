namespace YAKSys_Hybrid_CPU.Core;

public enum Lane7TokenNamespaceAuthority : byte
{
    Runtime = 0,
    Lane7AcceleratorDescriptor = 1,
    CompatibilityProjection = 2,
}

public enum Lane7TokenVisibility : byte
{
    None = 0,
    VirtualTokens = 1,
    NativeTokens = 2,
}

public sealed partial class Lane7TokenNamespace
{
    public Lane7TokenNamespace()
        : this(
            authority: Lane7TokenNamespaceAuthority.Runtime,
            namespaceId: 0,
            visibility: Lane7TokenVisibility.VirtualTokens,
            allowsCompatibilityProjection: true)
    {
    }

    public Lane7TokenNamespace(
        Lane7TokenNamespaceAuthority authority,
        ulong namespaceId,
        Lane7TokenVisibility visibility,
        bool allowsCompatibilityProjection)
    {
        Authority = authority;
        NamespaceId = namespaceId;
        Visibility = visibility;
        AllowsCompatibilityProjection = allowsCompatibilityProjection;
    }

    public Lane7TokenNamespaceAuthority Authority { get; }

    public ulong NamespaceId { get; }

    public Lane7TokenVisibility Visibility { get; }

    public bool AllowsCompatibilityProjection { get; }

    public bool IsRuntimeAuthoritative =>
        Authority == Lane7TokenNamespaceAuthority.Runtime;

    public bool HasNamespaceBinding =>
        NamespaceId != 0;

    public bool ExposesNativeTokens =>
        Visibility == Lane7TokenVisibility.NativeTokens;

    public bool CanProjectToCompatibilityFrontend =>
        AllowsCompatibilityProjection &&
        IsRuntimeAuthoritative &&
        !ExposesNativeTokens;

    public Lane7TokenNamespace WithNamespaceBinding(ulong namespaceId) =>
        new(Authority, namespaceId, Visibility, AllowsCompatibilityProjection);
}
