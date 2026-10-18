namespace YAKSys_Hybrid_CPU.Core;

public sealed partial class IoDomainDescriptor
{
    public IoDomainDescriptor()
        : this(
            virtualizationBlock: null,
            dmaWindow: null,
            ownsDmaAuthority: true,
            ownsIommuAuthority: true,
            compatibilityProjectionEnabled: true)
    {
    }

    public IoDomainDescriptor(
        IoVirtualizationBlock? virtualizationBlock,
        DmaWindowDescriptor? dmaWindow,
        bool ownsDmaAuthority,
        bool ownsIommuAuthority,
        bool compatibilityProjectionEnabled)
    {
        VirtualizationBlock = virtualizationBlock;
        DmaWindow = dmaWindow;
        OwnsDmaAuthority = ownsDmaAuthority;
        OwnsIommuAuthority = ownsIommuAuthority;
        CompatibilityProjectionEnabled = compatibilityProjectionEnabled;
    }

    public IoVirtualizationBlock? VirtualizationBlock { get; }

    public DmaWindowDescriptor? DmaWindow { get; }

    public bool OwnsDmaAuthority { get; }

    public bool OwnsIommuAuthority { get; }

    public bool CompatibilityProjectionEnabled { get; }

    public bool IsAuthoritativeIoStateOwner => true;

    public bool HasVirtualizationBlock => VirtualizationBlock is not null;

    public bool HasDmaWindow => DmaWindow is not null;

    public bool HasRequiredIoAuthority =>
        OwnsDmaAuthority &&
        OwnsIommuAuthority;

    public IoDomainDescriptor WithCompatibilityProjection(bool enabled) =>
        new(VirtualizationBlock, DmaWindow, OwnsDmaAuthority, OwnsIommuAuthority, enabled);
}
