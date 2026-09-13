using YAKSys_Hybrid_CPU.Memory;

namespace YAKSys_Hybrid_CPU.Core;

public enum AddressSpaceAuthority : byte
{
    RuntimeOwned = 0,
    CompatibilityProjection = 1,
}

public sealed partial class AddressSpaceDescriptor
{
    public AddressSpaceDescriptor()
        : this(
            addressSpaceId: default,
            authority: AddressSpaceAuthority.RuntimeOwned,
            guestPhysicalBase: 0,
            sizeBytes: 0,
            generation: 0,
            compatibilityProjectionEnabled: false)
    {
    }

    public AddressSpaceDescriptor(
        AddressSpaceId addressSpaceId,
        AddressSpaceAuthority authority,
        ulong guestPhysicalBase,
        ulong sizeBytes,
        ulong generation,
        bool compatibilityProjectionEnabled)
    {
        AddressSpaceId = addressSpaceId;
        Authority = authority;
        GuestPhysicalBase = guestPhysicalBase;
        SizeBytes = sizeBytes;
        Generation = generation;
        CompatibilityProjectionEnabled = compatibilityProjectionEnabled;
    }

    public AddressSpaceId AddressSpaceId { get; }

    public AddressSpaceAuthority Authority { get; }

    public ulong GuestPhysicalBase { get; }

    public ulong SizeBytes { get; }

    public ulong Generation { get; }

    public bool CompatibilityProjectionEnabled { get; }

    public bool IsRuntimeAuthoritative => Authority == AddressSpaceAuthority.RuntimeOwned;

    public bool HasBoundedRange => SizeBytes != 0;

    public bool AllowsRange(ulong guestPhysicalAddress, ulong sizeBytes)
    {
        if (!IsRuntimeAuthoritative ||
            !HasBoundedRange ||
            sizeBytes == 0)
        {
            return false;
        }

        ulong end = guestPhysicalAddress + sizeBytes - 1;
        ulong domainEnd = GuestPhysicalBase + SizeBytes - 1;
        if (end < guestPhysicalAddress || domainEnd < GuestPhysicalBase)
        {
            return false;
        }

        return guestPhysicalAddress >= GuestPhysicalBase &&
               end <= domainEnd;
    }

    public AddressSpaceDescriptor WithCompatibilityProjection(bool enabled) =>
        new(AddressSpaceId, Authority, GuestPhysicalBase, SizeBytes, Generation, enabled);
}
