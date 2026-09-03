namespace YAKSys_Hybrid_CPU.Core;

public enum NeutralDmaDirection : byte
{
    DeviceReadsMemory = 0,
    DeviceWritesMemory,
    Bidirectional,
}

public readonly record struct NeutralDmaRange(long Offset, long Length)
{
    public bool Fits(long mappingLength) =>
        mappingLength > 0 &&
        Offset >= 0 &&
        Length > 0 &&
        Length <= mappingLength &&
        Offset <= mappingLength - Length;
}

public readonly record struct NeutralDmaGrantHandle(ulong Value);
public readonly record struct NeutralDmaGrantEpoch(ulong Value);

public readonly record struct NeutralDmaGrant(
    NeutralDmaGrantHandle Handle,
    NeutralDmaGrantEpoch Epoch,
    NeutralDeviceLease DeviceLease,
    NeutralOwnedRegionMappingLease MappingLease,
    NeutralDmaRange Range,
    NeutralDmaDirection Direction)
{
    public bool IsMaterialized =>
        Handle.Value != 0 &&
        Epoch.Value != 0 &&
        DeviceLease.IsMaterialized &&
        MappingLease.IsMaterialized &&
        Range.Fits(MappingLease.Slice.Length) &&
        Enum.IsDefined(Direction);
}

public enum NeutralDmaGrantDecision : byte
{
    Granted = 0,
    InvalidRange,
    InvalidDirection,
    InsufficientDeviceRights,
    InsufficientMappingAccess,
    WrongDomain,
    AlreadyGranted,
    NotFound,
    Stale,
    Revoked,
    Faulted,
}

public readonly record struct NeutralDmaGrantResult(
    NeutralDmaGrantDecision Decision,
    NeutralDmaGrant Grant,
    string Reason)
{
    public bool IsGranted =>
        Decision == NeutralDmaGrantDecision.Granted && Grant.IsMaterialized;
}

public enum NeutralDmaGrantCloseDecision : byte
{
    Closed = 0,
    NotFound,
    Stale,
    Revoked,
    Faulted,
}

public readonly record struct NeutralDmaGrantCloseResult(
    NeutralDmaGrantCloseDecision Decision,
    NeutralDmaGrant Grant,
    string Reason)
{
    public bool IsClosed => Decision == NeutralDmaGrantCloseDecision.Closed;
}

/// <summary>
/// Admission-only DMA authority rooted in an exact live semantic device lease and
/// exact live owned-region mapping. The range is relative to the mapping slice.
/// This slice deliberately has no submit/completion operation and exposes no bus,
/// physical-address, IOMMU, descriptor-ring, queue, or hardware execution identity.
/// </summary>
public sealed partial class NeutralDomainRuntimeFacade
{
    private sealed class DmaGrantRecord(NeutralDmaGrant grant)
    {
        public NeutralDmaGrant Grant { get; } = grant;
        public bool Revoked { get; set; }
    }

    private readonly Dictionary<NeutralDmaGrantHandle, DmaGrantRecord> _dmaGrants = [];
    private ulong _nextDmaGrantId = 1;
    private ulong _nextDmaGrantEpoch = 1;

    public int ActiveDmaGrantCount =>
        _dmaGrants.Values.Count(static record => !record.Revoked);

    public NeutralDmaGrantResult BindDmaGrant(
        NeutralDeviceLease deviceLease,
        NeutralOwnedRegionMappingLease mappingLease,
        NeutralDmaRange range,
        NeutralDmaDirection direction)
    {
        var deviceValidation = ValidateLiveDmaDevice(deviceLease);
        if (deviceValidation is { } deviceRejected)
        {
            return new NeutralDmaGrantResult(
                deviceRejected,
                default,
                DmaValidationFailureReason(deviceRejected));
        }

        var mappingValidation = ValidateLiveDmaMapping(mappingLease);
        if (mappingValidation is { } mappingRejected)
        {
            return new NeutralDmaGrantResult(
                mappingRejected,
                default,
                DmaValidationFailureReason(mappingRejected));
        }

        if (deviceLease.DomainLease != mappingLease.DomainLease)
        {
            return new NeutralDmaGrantResult(
                NeutralDmaGrantDecision.WrongDomain,
                default,
                "DMA device and owned-region mapping must belong to the exact same neutral domain lifetime.");
        }

        if (!range.Fits(mappingLease.Slice.Length))
        {
            return new NeutralDmaGrantResult(
                NeutralDmaGrantDecision.InvalidRange,
                default,
                "DMA range must be positive, non-overflowing, and contained in the exact mapped slice.");
        }

        if (!Enum.IsDefined(direction))
        {
            return new NeutralDmaGrantResult(
                NeutralDmaGrantDecision.InvalidDirection,
                default,
                "DMA direction is undefined.");
        }

        var requiredDeviceRights = NeutralDeviceRights.Configure;
        var requiredMemoryAccess = NeutralMemoryAccess.None;
        switch (direction)
        {
            case NeutralDmaDirection.DeviceReadsMemory:
                requiredDeviceRights |= NeutralDeviceRights.Read;
                requiredMemoryAccess |= NeutralMemoryAccess.Read;
                break;
            case NeutralDmaDirection.DeviceWritesMemory:
                requiredDeviceRights |= NeutralDeviceRights.Write;
                requiredMemoryAccess |= NeutralMemoryAccess.Write;
                break;
            case NeutralDmaDirection.Bidirectional:
                requiredDeviceRights |= NeutralDeviceRights.Read | NeutralDeviceRights.Write;
                requiredMemoryAccess |= NeutralMemoryAccess.Read | NeutralMemoryAccess.Write;
                break;
        }

        if ((deviceLease.Rights & requiredDeviceRights) != requiredDeviceRights)
        {
            return new NeutralDmaGrantResult(
                NeutralDmaGrantDecision.InsufficientDeviceRights,
                default,
                "The neutral device lease lacks Configure plus the rights required by the DMA direction.");
        }

        if ((mappingLease.Slice.Access & requiredMemoryAccess) != requiredMemoryAccess)
        {
            return new NeutralDmaGrantResult(
                NeutralDmaGrantDecision.InsufficientMappingAccess,
                default,
                "The exact owned-region mapping access does not cover the DMA direction.");
        }

        if (_dmaGrants.Values.Any(record =>
                !record.Revoked &&
                record.Grant.MappingLease.Handle == mappingLease.Handle))
        {
            return new NeutralDmaGrantResult(
                NeutralDmaGrantDecision.AlreadyGranted,
                default,
                "The exact owned-region mapping already has a live DMA grant in this admission-only slice.");
        }

        try
        {
            var grant = new NeutralDmaGrant(
                new NeutralDmaGrantHandle(NextNonZero(ref _nextDmaGrantId)),
                new NeutralDmaGrantEpoch(NextNonZero(ref _nextDmaGrantEpoch)),
                deviceLease,
                mappingLease,
                range,
                direction);
            _dmaGrants.Add(grant.Handle, new DmaGrantRecord(grant));
            return new NeutralDmaGrantResult(
                NeutralDmaGrantDecision.Granted,
                grant,
                "Exact admission-only DMA grant materialized; no transfer has been submitted.");
        }
        catch (Exception)
        {
            return new NeutralDmaGrantResult(
                NeutralDmaGrantDecision.Faulted,
                default,
                "Neutral DMA grant materialization faulted.");
        }
    }

    public NeutralDmaGrantCloseResult CloseDmaGrant(NeutralDmaGrant grant)
    {
        if (!grant.IsMaterialized)
        {
            return new NeutralDmaGrantCloseResult(
                NeutralDmaGrantCloseDecision.NotFound,
                grant,
                "Neutral DMA grant is not materialized.");
        }

        if (!_dmaGrants.TryGetValue(grant.Handle, out var record))
        {
            return new NeutralDmaGrantCloseResult(
                NeutralDmaGrantCloseDecision.NotFound,
                grant,
                "Neutral DMA grant was not found.");
        }

        if (record.Grant.Epoch != grant.Epoch)
        {
            return new NeutralDmaGrantCloseResult(
                NeutralDmaGrantCloseDecision.Stale,
                grant,
                "Neutral DMA grant epoch is stale.");
        }

        if (record.Grant != grant)
        {
            return new NeutralDmaGrantCloseResult(
                NeutralDmaGrantCloseDecision.Faulted,
                grant,
                "Neutral DMA grant identity does not match the materialized authority record.");
        }

        if (record.Revoked)
        {
            return new NeutralDmaGrantCloseResult(
                NeutralDmaGrantCloseDecision.Revoked,
                record.Grant,
                "Neutral DMA grant has already been closed.");
        }

        record.Revoked = true;
        return new NeutralDmaGrantCloseResult(
            NeutralDmaGrantCloseDecision.Closed,
            record.Grant,
            "Neutral DMA grant closed.");
    }

    internal bool HasActiveDmaGrantsForDevice(NeutralDeviceLease deviceLease) =>
        _dmaGrants.Values.Any(record =>
            !record.Revoked && record.Grant.DeviceLease.Handle == deviceLease.Handle);

    internal bool HasActiveDmaGrantsForMapping(NeutralOwnedRegionMappingLease mappingLease) =>
        _dmaGrants.Values.Any(record =>
            !record.Revoked && record.Grant.MappingLease.Handle == mappingLease.Handle);

    private NeutralDmaGrantDecision? ValidateLiveDmaDevice(NeutralDeviceLease deviceLease)
    {
        if (!deviceLease.IsMaterialized ||
            !_deviceLeases.TryGetValue(deviceLease.Handle, out var record))
        {
            return NeutralDmaGrantDecision.NotFound;
        }

        if (record.Lease.Epoch != deviceLease.Epoch)
            return NeutralDmaGrantDecision.Stale;
        if (record.Lease != deviceLease)
            return NeutralDmaGrantDecision.Faulted;
        if (record.Revoked)
            return NeutralDmaGrantDecision.Revoked;

        return ValidateDeviceDomain(deviceLease.DomainLease) switch
        {
            NeutralDeviceBindDecision.Bound => null,
            NeutralDeviceBindDecision.NotFound => NeutralDmaGrantDecision.NotFound,
            NeutralDeviceBindDecision.Stale => NeutralDmaGrantDecision.Stale,
            NeutralDeviceBindDecision.Revoked => NeutralDmaGrantDecision.Revoked,
            _ => NeutralDmaGrantDecision.Faulted,
        };
    }

    private NeutralDmaGrantDecision? ValidateLiveDmaMapping(
        NeutralOwnedRegionMappingLease mappingLease)
    {
        var validation = ValidateLiveOwnedRegionMapping(mappingLease);
        return validation switch
        {
            null => null,
            NeutralOwnedRegionVisibilityDecision.NotFound => NeutralDmaGrantDecision.NotFound,
            NeutralOwnedRegionVisibilityDecision.Stale => NeutralDmaGrantDecision.Stale,
            NeutralOwnedRegionVisibilityDecision.Revoked => NeutralDmaGrantDecision.Revoked,
            _ => NeutralDmaGrantDecision.Faulted,
        };
    }

    private static string DmaValidationFailureReason(NeutralDmaGrantDecision decision) =>
        decision switch
        {
            NeutralDmaGrantDecision.NotFound => "A required neutral DMA authority dependency was not found.",
            NeutralDmaGrantDecision.Stale => "A required neutral DMA authority dependency is stale.",
            NeutralDmaGrantDecision.Revoked => "A required neutral DMA authority dependency has been revoked.",
            _ => "A required neutral DMA authority dependency is malformed.",
        };
}
