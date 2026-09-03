namespace YAKSys_Hybrid_CPU.Core;

[Flags]
public enum NeutralDeviceRights : byte
{
    None = 0,
    Read = 1 << 0,
    Write = 1 << 1,
    Configure = 1 << 2,
}

public readonly record struct NeutralDeviceIdentity(string ResourceId)
{
    public bool IsValid =>
        !string.IsNullOrWhiteSpace(ResourceId) && ResourceId.Length <= 256;
}

public readonly record struct NeutralDeviceLeaseHandle(ulong Value);
public readonly record struct NeutralDeviceLeaseEpoch(ulong Value);

public readonly record struct NeutralDeviceLease(
    NeutralDeviceLeaseHandle Handle,
    NeutralDeviceLeaseEpoch Epoch,
    NeutralDomainBindingLease DomainLease,
    NeutralDeviceIdentity Device,
    NeutralDeviceRights Rights)
{
    public bool IsMaterialized =>
        Handle.Value != 0 &&
        Epoch.Value != 0 &&
        DomainLease.IsMaterialized &&
        Device.IsValid &&
        Rights != NeutralDeviceRights.None;
}

public enum NeutralDeviceBindDecision : byte
{
    Bound = 0,
    InvalidDevice,
    InvalidRights,
    AlreadyBound,
    NotFound,
    Stale,
    Revoked,
    Faulted,
}

public readonly record struct NeutralDeviceBindResult(
    NeutralDeviceBindDecision Decision,
    NeutralDeviceLease Lease,
    string Reason)
{
    public bool IsBound =>
        Decision == NeutralDeviceBindDecision.Bound && Lease.IsMaterialized;
}

public enum NeutralDeviceCloseDecision : byte
{
    Closed = 0,
    NotFound,
    Stale,
    Revoked,
    Faulted,
    ActiveDependents,
}

public readonly record struct NeutralDeviceCloseResult(
    NeutralDeviceCloseDecision Decision,
    NeutralDeviceLease Lease,
    string Reason)
{
    public bool IsClosed => Decision == NeutralDeviceCloseDecision.Closed;
}

/// <summary>
/// Provider-facing semantic device authority rooted in an exact live neutral domain.
/// This lease does not expose register addresses, interrupt routing, transfer windows,
/// memory-translation authority, or hardware execution identity.
/// </summary>
public sealed partial class NeutralDomainRuntimeFacade
{
    private sealed class DeviceLeaseRecord(NeutralDeviceLease lease)
    {
        public NeutralDeviceLease Lease { get; } = lease;
        public bool Revoked { get; set; }
    }

    private readonly Dictionary<NeutralDeviceLeaseHandle, DeviceLeaseRecord> _deviceLeases = [];
    private ulong _nextDeviceLeaseId = 1;
    private ulong _nextDeviceLeaseEpoch = 1;

    public int ActiveDeviceLeaseCount =>
        _deviceLeases.Values.Count(record =>
            !record.Revoked && IsDeviceDomainLive(record.Lease.DomainLease));

    public NeutralDeviceBindResult BindDevice(
        NeutralDomainBindingLease domainLease,
        NeutralDeviceIdentity device,
        NeutralDeviceRights rights)
    {
        var domainDecision = ValidateDeviceDomain(domainLease);
        if (domainDecision != NeutralDeviceBindDecision.Bound)
        {
            return new NeutralDeviceBindResult(
                domainDecision,
                default,
                DeviceDomainFailureReason(domainDecision));
        }

        if (!device.IsValid)
        {
            return new NeutralDeviceBindResult(
                NeutralDeviceBindDecision.InvalidDevice,
                default,
                "Neutral device identity must be a non-empty bounded semantic resource identifier.");
        }

        if (!IsValidDeviceRights(rights))
        {
            return new NeutralDeviceBindResult(
                NeutralDeviceBindDecision.InvalidRights,
                default,
                "Neutral device rights must be a non-empty subset of Read, Write, and Configure.");
        }

        if (_deviceLeases.Values.Any(record =>
                !record.Revoked &&
                IsDeviceDomainLive(record.Lease.DomainLease) &&
                record.Lease.DomainLease == domainLease &&
                record.Lease.Device == device))
        {
            return new NeutralDeviceBindResult(
                NeutralDeviceBindDecision.AlreadyBound,
                default,
                "The exact neutral device is already bound in this domain lifetime.");
        }

        try
        {
            var lease = new NeutralDeviceLease(
                new NeutralDeviceLeaseHandle(NextNonZero(ref _nextDeviceLeaseId)),
                new NeutralDeviceLeaseEpoch(NextNonZero(ref _nextDeviceLeaseEpoch)),
                domainLease,
                device,
                rights);
            _deviceLeases.Add(lease.Handle, new DeviceLeaseRecord(lease));
            return new NeutralDeviceBindResult(
                NeutralDeviceBindDecision.Bound,
                lease,
                "Neutral device lease materialized for the exact live domain and semantic device resource.");
        }
        catch (Exception)
        {
            return new NeutralDeviceBindResult(
                NeutralDeviceBindDecision.Faulted,
                default,
                "Neutral device lease materialization faulted.");
        }
    }

    public NeutralDeviceCloseResult CloseDevice(NeutralDeviceLease lease)
    {
        if (!lease.IsMaterialized)
        {
            return new NeutralDeviceCloseResult(
                NeutralDeviceCloseDecision.NotFound,
                lease,
                "Neutral device lease is not materialized.");
        }

        if (!_deviceLeases.TryGetValue(lease.Handle, out var record))
        {
            return new NeutralDeviceCloseResult(
                NeutralDeviceCloseDecision.NotFound,
                lease,
                "Neutral device lease was not found.");
        }

        if (record.Lease.Epoch != lease.Epoch)
        {
            return new NeutralDeviceCloseResult(
                NeutralDeviceCloseDecision.Stale,
                lease,
                "Neutral device lease epoch is stale.");
        }

        if (record.Lease.DomainLease != lease.DomainLease ||
            record.Lease.Device != lease.Device ||
            record.Lease.Rights != lease.Rights)
        {
            return new NeutralDeviceCloseResult(
                NeutralDeviceCloseDecision.Faulted,
                lease,
                "Neutral device lease identity does not match the materialized authority record.");
        }

        if (record.Revoked)
        {
            return new NeutralDeviceCloseResult(
                NeutralDeviceCloseDecision.Revoked,
                record.Lease,
                "Neutral device lease has already been closed.");
        }

        if (HasActiveMmioLeasesForDevice(record.Lease))
        {
            return new NeutralDeviceCloseResult(
                NeutralDeviceCloseDecision.ActiveDependents,
                record.Lease,
                "Neutral MMIO leases must close before the owning device lease.");
        }

        if (HasActiveInterruptLeasesForDevice(record.Lease))
        {
            return new NeutralDeviceCloseResult(
                NeutralDeviceCloseDecision.ActiveDependents,
                record.Lease,
                "Neutral interrupt routes must close before the owning device lease.");
        }

        if (HasActiveDmaGrantsForDevice(record.Lease))
        {
            return new NeutralDeviceCloseResult(
                NeutralDeviceCloseDecision.ActiveDependents,
                record.Lease,
                "Neutral DMA grants must close before the owning device lease.");
        }

        var domainDecision = ValidateDeviceDomain(lease.DomainLease);
        if (domainDecision == NeutralDeviceBindDecision.Revoked)
        {
            record.Revoked = true;
            return new NeutralDeviceCloseResult(
                NeutralDeviceCloseDecision.Revoked,
                record.Lease,
                "The owning neutral domain has already been closed; the device lease is no longer live.");
        }

        if (domainDecision == NeutralDeviceBindDecision.Stale)
        {
            return new NeutralDeviceCloseResult(
                NeutralDeviceCloseDecision.Stale,
                lease,
                "The owning neutral domain epoch is stale.");
        }

        if (domainDecision != NeutralDeviceBindDecision.Bound)
        {
            return new NeutralDeviceCloseResult(
                NeutralDeviceCloseDecision.Faulted,
                lease,
                "The owning neutral domain can no longer prove the exact device authority lifetime.");
        }

        record.Revoked = true;
        return new NeutralDeviceCloseResult(
            NeutralDeviceCloseDecision.Closed,
            record.Lease,
            "Neutral device lease closed.");
    }

    private NeutralDeviceBindDecision ValidateDeviceDomain(NeutralDomainBindingLease domainLease)
    {
        if (!domainLease.IsMaterialized)
            return NeutralDeviceBindDecision.NotFound;
        if (!_bindings.TryGetValue(domainLease.Handle, out var record))
            return NeutralDeviceBindDecision.NotFound;
        if (record.Lease.Epoch != domainLease.Epoch)
            return NeutralDeviceBindDecision.Stale;
        if (record.Revoked)
            return NeutralDeviceBindDecision.Revoked;
        if (record.Lease != domainLease)
            return NeutralDeviceBindDecision.Faulted;
        return NeutralDeviceBindDecision.Bound;
    }

    private bool IsDeviceDomainLive(NeutralDomainBindingLease domainLease) =>
        ValidateDeviceDomain(domainLease) == NeutralDeviceBindDecision.Bound;

    private static bool IsValidDeviceRights(NeutralDeviceRights rights) =>
        rights != NeutralDeviceRights.None &&
        (rights & ~(NeutralDeviceRights.Read |
                    NeutralDeviceRights.Write |
                    NeutralDeviceRights.Configure)) == 0;

    private static string DeviceDomainFailureReason(NeutralDeviceBindDecision decision) =>
        decision switch
        {
            NeutralDeviceBindDecision.NotFound => "Neutral runtime domain binding was not found.",
            NeutralDeviceBindDecision.Stale => "Neutral runtime domain binding epoch is stale.",
            NeutralDeviceBindDecision.Revoked => "Neutral runtime domain binding has been revoked.",
            NeutralDeviceBindDecision.Faulted => "Neutral runtime domain binding identity is malformed.",
            _ => "Neutral runtime domain binding is not valid for device authority materialization.",
        };
}
