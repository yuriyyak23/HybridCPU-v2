using System.Collections.Generic;

namespace YAKSys_Hybrid_CPU.Core;

public enum SecureMemoryRegionClass : byte
{
    Private = 0,
    Shared = 1,
    Measured = 2,
    RuntimeMutable = 3,
}

public enum SecureMemoryHostVisibility : byte
{
    Denied = 0,
    MetadataOnly = 1,
    ExplicitShared = 2,
}

public enum SecureMemoryDmaPolicy : byte
{
    Denied = 0,
    ExplicitSharedBuffersWithTypedGrant = 1,
}

public enum SecureRuntimeMutableDirtyPolicy : byte
{
    None = 0,
    TrackDirtyPages = 1,
    ReinitializeOnRestore = 2,
}

public enum SecureRuntimeMutableMigrationClass : byte
{
    Unclassified = 0,
    DomainLocal = 1,
    RecomputeAfterRestore = 2,
    SealedPayloadRequired = 3,
}

public readonly record struct SecureMemoryRegionDescriptor(
    SecureMemoryRegionClass RegionClass,
    ulong Start,
    ulong Length,
    SecureMemoryHostVisibility HostVisibility,
    ulong PolicyEpoch,
    SecureRuntimeMutableDirtyPolicy RuntimeDirtyPolicy = SecureRuntimeMutableDirtyPolicy.None,
    SecureRuntimeMutableMigrationClass RuntimeMigrationClass = SecureRuntimeMutableMigrationClass.Unclassified)
{
    public bool IsEmpty => Length == 0;

    public bool IsPrivate =>
        RegionClass == SecureMemoryRegionClass.Private;

    public bool IsShared =>
        RegionClass == SecureMemoryRegionClass.Shared;

    public bool IsMeasured =>
        RegionClass == SecureMemoryRegionClass.Measured;

    public bool IsRuntimeMutable =>
        RegionClass == SecureMemoryRegionClass.RuntimeMutable;

    public bool IsHostReadable =>
        HostVisibility == SecureMemoryHostVisibility.ExplicitShared;

    public bool HasRuntimeMutableClassification =>
        !IsRuntimeMutable ||
        (RuntimeDirtyPolicy != SecureRuntimeMutableDirtyPolicy.None &&
         RuntimeMigrationClass != SecureRuntimeMutableMigrationClass.Unclassified);

    public bool Contains(ulong address, ulong length)
    {
        if (IsEmpty || length == 0 || address < Start)
        {
            return false;
        }

        ulong offset = address - Start;
        return offset < Length && length <= Length - offset;
    }

    public bool IsCurrentFor(SecureRevocationEpoch descriptorEpoch) =>
        descriptorEpoch.IsCurrent(PolicyEpoch);
}

public sealed partial class SecureMemoryDomainDescriptor
{
    public SecureMemoryDomainDescriptor()
        : this(
            domainTag: 0,
            addressSpaceTag: 0,
            policyEpoch: SecureRevocationEpoch.Unmaterialized,
            regions: System.Array.Empty<SecureMemoryRegionDescriptor>(),
            dmaPolicy: SecureMemoryDmaPolicy.Denied)
    {
    }

    public SecureMemoryDomainDescriptor(
        ulong domainTag,
        ulong addressSpaceTag,
        SecureRevocationEpoch policyEpoch,
        IReadOnlyList<SecureMemoryRegionDescriptor> regions)
        : this(
            domainTag,
            addressSpaceTag,
            policyEpoch,
            regions,
            SecureMemoryDmaPolicy.Denied)
    {
    }

    public SecureMemoryDomainDescriptor(
        ulong domainTag,
        ulong addressSpaceTag,
        SecureRevocationEpoch policyEpoch,
        IReadOnlyList<SecureMemoryRegionDescriptor> regions,
        SecureMemoryDmaPolicy dmaPolicy)
    {
        DomainTag = domainTag;
        AddressSpaceTag = addressSpaceTag;
        PolicyEpoch = policyEpoch;
        Regions = regions;
        DmaPolicy = dmaPolicy;
    }

    public static SecureMemoryDomainDescriptor Disabled { get; } = new();

    public ulong DomainTag { get; }

    public ulong AddressSpaceTag { get; }

    public SecureRevocationEpoch PolicyEpoch { get; }

    public IReadOnlyList<SecureMemoryRegionDescriptor> Regions { get; }

    public SecureMemoryDmaPolicy DmaPolicy { get; }

    public bool IsMaterialized =>
        DomainTag != 0 &&
        AddressSpaceTag != 0 &&
        PolicyEpoch.IsMaterialized;

    public bool RequiresStageBPolicyRoute =>
        IsMaterialized && Regions.Count != 0;

    public bool HasPrivateMemory
    {
        get
        {
            foreach (var region in Regions)
            {
                if (region.IsPrivate && !region.IsEmpty)
                {
                    return true;
                }
            }

            return false;
        }
    }

    public bool HasSharedMemory => HasRegionClass(SecureMemoryRegionClass.Shared);

    public bool HasMeasuredMemory => HasRegionClass(SecureMemoryRegionClass.Measured);

    public bool HasRuntimeMutableMemory => HasRegionClass(SecureMemoryRegionClass.RuntimeMutable);

    public bool AllowsExplicitSharedDma =>
        IsMaterialized &&
        DmaPolicy == SecureMemoryDmaPolicy.ExplicitSharedBuffersWithTypedGrant &&
        HasSharedMemory;

    public bool IsBoundTo(ulong domainTag, ulong addressSpaceTag) =>
        DomainTag != 0 &&
        AddressSpaceTag != 0 &&
        DomainTag == domainTag &&
        AddressSpaceTag == addressSpaceTag;

    public bool TryFindRegion(
        ulong address,
        ulong length,
        out SecureMemoryRegionDescriptor region)
    {
        foreach (var candidate in Regions)
        {
            if (candidate.Contains(address, length))
            {
                region = candidate;
                return true;
            }
        }

        region = default;
        return false;
    }

    private bool HasRegionClass(SecureMemoryRegionClass regionClass)
    {
        foreach (var region in Regions)
        {
            if (region.RegionClass == regionClass && !region.IsEmpty)
            {
                return true;
            }
        }

        return false;
    }
}
