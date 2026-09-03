namespace YAKSys_Hybrid_CPU.Core;

[Flags]
public enum NeutralMmioAccess : byte
{
    None = 0,
    Read = 1 << 0,
    Write = 1 << 1,
}

public readonly record struct NeutralMmioRegionIdentity(
    string ResourceId,
    long ByteLength)
{
    public bool IsValid =>
        !string.IsNullOrWhiteSpace(ResourceId) &&
        ResourceId.Length <= 256 &&
        ByteLength > 0;
}

public readonly record struct NeutralMmioRange(long Offset, long Length)
{
    public bool Fits(long byteLength) =>
        byteLength > 0 &&
        Offset >= 0 &&
        Length > 0 &&
        Length <= byteLength &&
        Offset <= byteLength - Length;
}

public readonly record struct NeutralMmioLeaseHandle(ulong Value);
public readonly record struct NeutralMmioLeaseEpoch(ulong Value);

public readonly record struct NeutralMmioLease(
    NeutralMmioLeaseHandle Handle,
    NeutralMmioLeaseEpoch Epoch,
    NeutralDeviceLease DeviceLease,
    NeutralMmioRegionIdentity Region,
    NeutralMmioRange Range,
    NeutralMmioAccess Access)
{
    public bool IsMaterialized =>
        Handle.Value != 0 &&
        Epoch.Value != 0 &&
        DeviceLease.IsMaterialized &&
        Region.IsValid &&
        Range.Fits(Region.ByteLength) &&
        Access != NeutralMmioAccess.None;
}

public enum NeutralMmioMapDecision : byte
{
    Mapped = 0,
    InvalidRegion,
    InvalidRange,
    InvalidAccess,
    InsufficientDeviceRights,
    AlreadyMapped,
    NotFound,
    Stale,
    Revoked,
    Faulted,
}

public readonly record struct NeutralMmioMapResult(
    NeutralMmioMapDecision Decision,
    NeutralMmioLease Lease,
    string Reason)
{
    public bool IsMapped =>
        Decision == NeutralMmioMapDecision.Mapped && Lease.IsMaterialized;
}

public enum NeutralMmioCloseDecision : byte
{
    Closed = 0,
    NotFound,
    Stale,
    Revoked,
    Faulted,
}

public readonly record struct NeutralMmioCloseResult(
    NeutralMmioCloseDecision Decision,
    NeutralMmioLease Lease,
    string Reason)
{
    public bool IsClosed => Decision == NeutralMmioCloseDecision.Closed;
}

/// <summary>
/// Exact semantic MMIO-window lifetime rooted in an exact live device lease.
/// Region identity and ranges are relative semantic authority only: no physical
/// address, BAR number, page-table identity, interrupt route, or DMA/IOMMU token
/// is exposed by this facade.
/// </summary>
public sealed partial class NeutralDomainRuntimeFacade
{
    private sealed class MmioLeaseRecord(NeutralMmioLease lease)
    {
        public NeutralMmioLease Lease { get; } = lease;
        public bool Revoked { get; set; }
    }

    private readonly Dictionary<NeutralMmioLeaseHandle, MmioLeaseRecord> _mmioLeases = [];
    private ulong _nextMmioLeaseId = 1;
    private ulong _nextMmioLeaseEpoch = 1;

    public int ActiveMmioLeaseCount =>
        _mmioLeases.Values.Count(record =>
            !record.Revoked && IsMmioDeviceLive(record.Lease.DeviceLease));

    public NeutralMmioMapResult MapMmio(
        NeutralDeviceLease deviceLease,
        NeutralMmioRegionIdentity region,
        NeutralMmioRange range,
        NeutralMmioAccess access)
    {
        var deviceDecision = ValidateMmioDevice(deviceLease);
        if (deviceDecision != NeutralMmioMapDecision.Mapped)
        {
            return new NeutralMmioMapResult(
                deviceDecision,
                default,
                MmioDeviceFailureReason(deviceDecision));
        }

        if (!region.IsValid)
        {
            return new NeutralMmioMapResult(
                NeutralMmioMapDecision.InvalidRegion,
                default,
                "Neutral MMIO region identity requires a bounded semantic id and positive byte length.");
        }

        if (!range.Fits(region.ByteLength))
        {
            return new NeutralMmioMapResult(
                NeutralMmioMapDecision.InvalidRange,
                default,
                "Neutral MMIO range must be positive, non-overflowing, and contained in the exact region extent.");
        }

        if (!IsValidMmioAccess(access))
        {
            return new NeutralMmioMapResult(
                NeutralMmioMapDecision.InvalidAccess,
                default,
                "Neutral MMIO access must be Read, Write, or Read|Write.");
        }

        var requiredRights = NeutralDeviceRights.Configure;
        if ((access & NeutralMmioAccess.Read) != 0)
            requiredRights |= NeutralDeviceRights.Read;
        if ((access & NeutralMmioAccess.Write) != 0)
            requiredRights |= NeutralDeviceRights.Write;
        if ((deviceLease.Rights & requiredRights) != requiredRights)
        {
            return new NeutralMmioMapResult(
                NeutralMmioMapDecision.InsufficientDeviceRights,
                default,
                "The neutral device lease does not carry the rights required for this MMIO mapping.");
        }

        if (_mmioLeases.Values.Any(record =>
                !record.Revoked &&
                IsMmioDeviceLive(record.Lease.DeviceLease) &&
                record.Lease.DeviceLease.Handle == deviceLease.Handle &&
                string.Equals(record.Lease.Region.ResourceId, region.ResourceId, StringComparison.Ordinal)))
        {
            return new NeutralMmioMapResult(
                NeutralMmioMapDecision.AlreadyMapped,
                default,
                "The exact semantic MMIO region already has a live lease for this device lifetime.");
        }

        try
        {
            var lease = new NeutralMmioLease(
                new NeutralMmioLeaseHandle(NextNonZero(ref _nextMmioLeaseId)),
                new NeutralMmioLeaseEpoch(NextNonZero(ref _nextMmioLeaseEpoch)),
                deviceLease,
                region,
                range,
                access);
            _mmioLeases.Add(lease.Handle, new MmioLeaseRecord(lease));
            return new NeutralMmioMapResult(
                NeutralMmioMapDecision.Mapped,
                lease,
                "Neutral MMIO lease materialized for the exact semantic region and bounded range.");
        }
        catch (Exception)
        {
            return new NeutralMmioMapResult(
                NeutralMmioMapDecision.Faulted,
                default,
                "Neutral MMIO lease materialization faulted.");
        }
    }

    public NeutralMmioCloseResult CloseMmio(NeutralMmioLease lease)
    {
        if (!lease.IsMaterialized)
        {
            return new NeutralMmioCloseResult(
                NeutralMmioCloseDecision.NotFound,
                lease,
                "Neutral MMIO lease is not materialized.");
        }

        if (!_mmioLeases.TryGetValue(lease.Handle, out var record))
        {
            return new NeutralMmioCloseResult(
                NeutralMmioCloseDecision.NotFound,
                lease,
                "Neutral MMIO lease was not found.");
        }

        if (record.Lease.Epoch != lease.Epoch)
        {
            return new NeutralMmioCloseResult(
                NeutralMmioCloseDecision.Stale,
                lease,
                "Neutral MMIO lease epoch is stale.");
        }

        if (record.Lease != lease)
        {
            return new NeutralMmioCloseResult(
                NeutralMmioCloseDecision.Faulted,
                lease,
                "Neutral MMIO lease identity does not match the materialized authority record.");
        }

        if (record.Revoked)
        {
            return new NeutralMmioCloseResult(
                NeutralMmioCloseDecision.Revoked,
                record.Lease,
                "Neutral MMIO lease has already been closed.");
        }

        var deviceDecision = ValidateMmioDevice(lease.DeviceLease);
        if (deviceDecision == NeutralMmioMapDecision.Revoked)
        {
            record.Revoked = true;
            return new NeutralMmioCloseResult(
                NeutralMmioCloseDecision.Revoked,
                record.Lease,
                "The owning neutral device lease has already been closed; MMIO authority is no longer live.");
        }

        if (deviceDecision == NeutralMmioMapDecision.Stale)
        {
            return new NeutralMmioCloseResult(
                NeutralMmioCloseDecision.Stale,
                lease,
                "The owning neutral device lease epoch is stale.");
        }

        if (deviceDecision != NeutralMmioMapDecision.Mapped)
        {
            return new NeutralMmioCloseResult(
                NeutralMmioCloseDecision.Faulted,
                lease,
                "The owning neutral device lease can no longer prove this MMIO authority lifetime.");
        }

        record.Revoked = true;
        return new NeutralMmioCloseResult(
            NeutralMmioCloseDecision.Closed,
            record.Lease,
            "Neutral MMIO lease closed.");
    }

    internal bool HasActiveMmioLeasesForDevice(NeutralDeviceLease deviceLease) =>
        _mmioLeases.Values.Any(record =>
            !record.Revoked &&
            record.Lease.DeviceLease.Handle == deviceLease.Handle &&
            IsMmioDeviceLive(record.Lease.DeviceLease));

    private NeutralMmioMapDecision ValidateMmioDevice(NeutralDeviceLease deviceLease)
    {
        if (!deviceLease.IsMaterialized)
            return NeutralMmioMapDecision.NotFound;
        if (!_deviceLeases.TryGetValue(deviceLease.Handle, out var record))
            return NeutralMmioMapDecision.NotFound;
        if (record.Lease.Epoch != deviceLease.Epoch)
            return NeutralMmioMapDecision.Stale;
        if (record.Lease != deviceLease)
            return NeutralMmioMapDecision.Faulted;
        if (record.Revoked)
            return NeutralMmioMapDecision.Revoked;

        var domainDecision = ValidateDeviceDomain(deviceLease.DomainLease);
        return domainDecision switch
        {
            NeutralDeviceBindDecision.Bound => NeutralMmioMapDecision.Mapped,
            NeutralDeviceBindDecision.Stale => NeutralMmioMapDecision.Stale,
            NeutralDeviceBindDecision.Revoked => NeutralMmioMapDecision.Revoked,
            NeutralDeviceBindDecision.NotFound => NeutralMmioMapDecision.NotFound,
            _ => NeutralMmioMapDecision.Faulted,
        };
    }

    private bool IsMmioDeviceLive(NeutralDeviceLease deviceLease) =>
        ValidateMmioDevice(deviceLease) == NeutralMmioMapDecision.Mapped;

    private static bool IsValidMmioAccess(NeutralMmioAccess access) =>
        access != NeutralMmioAccess.None &&
        (access & ~(NeutralMmioAccess.Read | NeutralMmioAccess.Write)) == 0;

    private static string MmioDeviceFailureReason(NeutralMmioMapDecision decision) =>
        decision switch
        {
            NeutralMmioMapDecision.NotFound => "Neutral device lease was not found.",
            NeutralMmioMapDecision.Stale => "Neutral device lease epoch is stale.",
            NeutralMmioMapDecision.Revoked => "Neutral device lease has been revoked.",
            NeutralMmioMapDecision.Faulted => "Neutral device lease identity is malformed.",
            _ => "Neutral device lease is not valid for MMIO authority materialization.",
        };
}
