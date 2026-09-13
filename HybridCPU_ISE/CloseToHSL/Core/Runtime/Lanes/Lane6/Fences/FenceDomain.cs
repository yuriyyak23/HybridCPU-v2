namespace YAKSys_Hybrid_CPU.Core;

[System.Flags]
public enum FenceOrderingScope : ushort
{
    None = 0,
    Cpu = 1 << 0,
    Dma = 1 << 1,
    Iommu = 1 << 2,
    Lane6 = 1 << 3,
    Lane7 = 1 << 4,
    MemoryVisibility = 1 << 5,
    CompletionPublication = 1 << 6,
    All = Cpu | Dma | Iommu | Lane6 | Lane7 | MemoryVisibility | CompletionPublication,
}

public enum FenceDomainAuthority : byte
{
    Runtime = 0,
    LaneDescriptor = 1,
    CompatibilityProjection = 2,
}

public sealed partial class FenceDomain
{
    public FenceDomain()
        : this(
            authority: FenceDomainAuthority.Runtime,
            domainId: 0,
            orderingScope: FenceOrderingScope.All,
            allowsCompatibilityProjection: true)
    {
    }

    public FenceDomain(
        FenceDomainAuthority authority,
        ulong domainId,
        FenceOrderingScope orderingScope,
        bool allowsCompatibilityProjection)
    {
        Authority = authority;
        DomainId = domainId;
        OrderingScope = orderingScope;
        AllowsCompatibilityProjection = allowsCompatibilityProjection;
    }

    public FenceDomainAuthority Authority { get; }

    public ulong DomainId { get; }

    public FenceOrderingScope OrderingScope { get; }

    public bool AllowsCompatibilityProjection { get; }

    public bool IsRuntimeAuthoritative =>
        Authority == FenceDomainAuthority.Runtime;

    public bool HasDomainBinding =>
        DomainId != 0;

    public bool CoversRequiredOrdering =>
        (OrderingScope & FenceOrderingScope.All) == FenceOrderingScope.All;

    public bool CanProjectToCompatibilityFrontend =>
        AllowsCompatibilityProjection &&
        IsRuntimeAuthoritative;

    public FenceDomain WithDomainBinding(ulong domainId) =>
        new(Authority, domainId, OrderingScope, AllowsCompatibilityProjection);
}
