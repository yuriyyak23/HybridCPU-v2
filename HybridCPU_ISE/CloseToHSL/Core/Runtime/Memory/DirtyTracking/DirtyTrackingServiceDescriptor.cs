namespace YAKSys_Hybrid_CPU.Core;

public enum DirtyTrackingAuthority : byte
{
    RuntimeOwned = 0,
    CompatibilityProjection = 1,
}

public enum DirtyTrackingVisibility : byte
{
    HostOwnedOnly = 0,
    GuestVisibleSummary = 1,
}

public sealed partial class DirtyTrackingServiceDescriptor
{
    public DirtyTrackingServiceDescriptor()
        : this(
            authority: DirtyTrackingAuthority.RuntimeOwned,
            visibility: DirtyTrackingVisibility.HostOwnedOnly,
            enabled: false,
            maxTrackedRanges: 0,
            epoch: 0,
            requireFenceBeforeSnapshot: true)
    {
    }

    public DirtyTrackingServiceDescriptor(
        DirtyTrackingAuthority authority,
        DirtyTrackingVisibility visibility,
        bool enabled,
        uint maxTrackedRanges,
        ulong epoch,
        bool requireFenceBeforeSnapshot)
    {
        Authority = authority;
        Visibility = visibility;
        Enabled = enabled;
        MaxTrackedRanges = maxTrackedRanges;
        Epoch = epoch;
        RequireFenceBeforeSnapshot = requireFenceBeforeSnapshot;
    }

    public DirtyTrackingAuthority Authority { get; }

    public DirtyTrackingVisibility Visibility { get; }

    public bool Enabled { get; }

    public uint MaxTrackedRanges { get; }

    public ulong Epoch { get; }

    public bool RequireFenceBeforeSnapshot { get; }

    public bool IsRuntimeAuthoritative => Authority == DirtyTrackingAuthority.RuntimeOwned;

    public bool CanTrackWrites => IsRuntimeAuthoritative && Enabled && MaxTrackedRanges != 0;

    public bool CanPublishGuestSummary =>
        CanTrackWrites &&
        Visibility == DirtyTrackingVisibility.GuestVisibleSummary;

    public bool CanExposeHostOwnedEvidence => false;

    public bool CanTrackRange(ulong guestPhysicalAddress, ulong sizeBytes)
    {
        if (!CanTrackWrites || sizeBytes == 0)
        {
            return false;
        }

        ulong end = guestPhysicalAddress + sizeBytes - 1;
        return end >= guestPhysicalAddress;
    }

    public DirtyTrackingServiceDescriptor WithEpoch(ulong epoch) =>
        new(Authority, Visibility, Enabled, MaxTrackedRanges, epoch, RequireFenceBeforeSnapshot);
}
