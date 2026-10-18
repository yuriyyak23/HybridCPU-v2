namespace YAKSys_Hybrid_CPU.Core;

public enum Lane6TokenNamespaceAuthority : byte
{
    Runtime = 0,
    Lane6DomainDescriptor = 1,
    CompatibilityProjection = 2,
}

public enum Lane6TokenVisibility : byte
{
    None = 0,
    VirtualTokens = 1,
    NativeTokens = 2,
}

public sealed partial class Lane6TokenNamespace
{
    public Lane6TokenNamespace()
        : this(
            authority: Lane6TokenNamespaceAuthority.Runtime,
            namespaceId: 0,
            visibility: Lane6TokenVisibility.VirtualTokens,
            allowsCompatibilityProjection: true)
    {
    }

    public Lane6TokenNamespace(
        Lane6TokenNamespaceAuthority authority,
        ulong namespaceId,
        Lane6TokenVisibility visibility,
        bool allowsCompatibilityProjection)
    {
        Authority = authority;
        NamespaceId = namespaceId;
        Visibility = visibility;
        AllowsCompatibilityProjection = allowsCompatibilityProjection;
    }

    public Lane6TokenNamespaceAuthority Authority { get; }

    public ulong NamespaceId { get; }

    public Lane6TokenVisibility Visibility { get; }

    public bool AllowsCompatibilityProjection { get; }

    public bool IsRuntimeAuthoritative =>
        Authority == Lane6TokenNamespaceAuthority.Runtime;

    public bool HasNamespaceBinding =>
        NamespaceId != 0;

    public bool ExposesNativeTokens =>
        Visibility == Lane6TokenVisibility.NativeTokens;

    public bool CanProjectToCompatibilityFrontend =>
        AllowsCompatibilityProjection &&
        IsRuntimeAuthoritative &&
        !ExposesNativeTokens;

    public Lane6TokenNamespace WithNamespaceBinding(ulong namespaceId) =>
        new(Authority, namespaceId, Visibility, AllowsCompatibilityProjection);
}
