namespace YAKSys_Hybrid_CPU.Core;

public enum NeutralInterruptTrigger : byte
{
    Edge = 0,
    Level = 1,
}

public readonly record struct NeutralInterruptSourceIdentity(
    string ResourceId,
    NeutralInterruptTrigger Trigger)
{
    public bool IsValid =>
        !string.IsNullOrWhiteSpace(ResourceId) &&
        ResourceId.Length <= 256 &&
        Enum.IsDefined(Trigger);
}

public readonly record struct NeutralInterruptLeaseHandle(ulong Value);
public readonly record struct NeutralInterruptLeaseEpoch(ulong Value);
public readonly record struct NeutralInterruptDeliverySequence(ulong Value);

public readonly record struct NeutralInterruptLease(
    NeutralInterruptLeaseHandle Handle,
    NeutralInterruptLeaseEpoch Epoch,
    NeutralDeviceLease DeviceLease,
    NeutralInterruptSourceIdentity Source)
{
    public bool IsMaterialized =>
        Handle.Value != 0 &&
        Epoch.Value != 0 &&
        DeviceLease.IsMaterialized &&
        Source.IsValid;
}

public enum NeutralInterruptBindDecision : byte
{
    Bound = 0,
    InvalidSource,
    InsufficientDeviceRights,
    AlreadyBound,
    NotFound,
    Stale,
    Revoked,
    Faulted,
}

public readonly record struct NeutralInterruptBindResult(
    NeutralInterruptBindDecision Decision,
    NeutralInterruptLease Lease,
    string Reason)
{
    public bool IsBound =>
        Decision == NeutralInterruptBindDecision.Bound && Lease.IsMaterialized;
}

public enum NeutralInterruptSignalDecision : byte
{
    Signaled = 0,
    AlreadyPending,
    NotFound,
    Stale,
    Revoked,
    Faulted,
}

public readonly record struct NeutralInterruptSignalResult(
    NeutralInterruptSignalDecision Decision,
    NeutralInterruptLease Lease,
    NeutralInterruptDeliverySequence Sequence,
    string Reason)
{
    public bool IsSignaled =>
        Decision == NeutralInterruptSignalDecision.Signaled && Sequence.Value != 0;
}

public enum NeutralInterruptPollDecision : byte
{
    Observed = 0,
    NotFound,
    Stale,
    Revoked,
    Faulted,
}

public readonly record struct NeutralInterruptPollResult(
    NeutralInterruptPollDecision Decision,
    NeutralInterruptLease Lease,
    bool DeliveryAvailable,
    NeutralInterruptDeliverySequence Sequence,
    string Reason)
{
    public bool IsObserved => Decision == NeutralInterruptPollDecision.Observed;
}

public enum NeutralInterruptCompleteDecision : byte
{
    Completed = 0,
    NoPendingDelivery,
    WrongSequence,
    NotFound,
    Stale,
    Revoked,
    Faulted,
}

public readonly record struct NeutralInterruptCompleteResult(
    NeutralInterruptCompleteDecision Decision,
    NeutralInterruptLease Lease,
    NeutralInterruptDeliverySequence Sequence,
    string Reason)
{
    public bool IsCompleted => Decision == NeutralInterruptCompleteDecision.Completed;
}

public enum NeutralInterruptCloseDecision : byte
{
    Closed = 0,
    NotFound,
    Stale,
    Revoked,
    Faulted,
}

public readonly record struct NeutralInterruptCloseResult(
    NeutralInterruptCloseDecision Decision,
    NeutralInterruptLease Lease,
    string Reason)
{
    public bool IsClosed => Decision == NeutralInterruptCloseDecision.Closed;
}

/// <summary>
/// Semantic interrupt-route lifetime rooted in an exact live device lease.
/// The source is a bounded semantic identity plus edge/level behavior only.
/// No interrupt vector, controller route, APIC/GIC/MSI identity, physical address,
/// DMA window, IOMMU token, queue, lane, opcode, or VM execution identity is exposed.
/// </summary>
public sealed partial class NeutralDomainRuntimeFacade
{
    private sealed class InterruptLeaseRecord(NeutralInterruptLease lease)
    {
        public NeutralInterruptLease Lease { get; } = lease;
        public bool Revoked { get; set; }
        public NeutralInterruptDeliverySequence PendingSequence { get; set; }
    }

    private readonly Dictionary<NeutralInterruptLeaseHandle, InterruptLeaseRecord> _interruptLeases = [];
    private ulong _nextInterruptLeaseId = 1;
    private ulong _nextInterruptLeaseEpoch = 1;
    private ulong _nextInterruptDeliverySequence = 1;

    public int ActiveInterruptLeaseCount =>
        _interruptLeases.Values.Count(record =>
            !record.Revoked && IsInterruptDeviceLive(record.Lease.DeviceLease));

    public NeutralInterruptBindResult BindInterrupt(
        NeutralDeviceLease deviceLease,
        NeutralInterruptSourceIdentity source)
    {
        var deviceDecision = ValidateInterruptDevice(deviceLease);
        if (deviceDecision != NeutralInterruptBindDecision.Bound)
        {
            return new NeutralInterruptBindResult(
                deviceDecision,
                default,
                InterruptDeviceFailureReason(deviceDecision));
        }

        if (!source.IsValid)
        {
            return new NeutralInterruptBindResult(
                NeutralInterruptBindDecision.InvalidSource,
                default,
                "Neutral interrupt source must be a bounded semantic identity with a defined trigger mode.");
        }

        if ((deviceLease.Rights & NeutralDeviceRights.Configure) == 0)
        {
            return new NeutralInterruptBindResult(
                NeutralInterruptBindDecision.InsufficientDeviceRights,
                default,
                "Neutral interrupt binding requires Configure authority on the exact device lease.");
        }

        if (_interruptLeases.Values.Any(record =>
                !record.Revoked &&
                IsInterruptDeviceLive(record.Lease.DeviceLease) &&
                record.Lease.DeviceLease.Handle == deviceLease.Handle &&
                record.Lease.Source == source))
        {
            return new NeutralInterruptBindResult(
                NeutralInterruptBindDecision.AlreadyBound,
                default,
                "The exact semantic interrupt source already has a live route for this device lifetime.");
        }

        try
        {
            var lease = new NeutralInterruptLease(
                new NeutralInterruptLeaseHandle(NextNonZero(ref _nextInterruptLeaseId)),
                new NeutralInterruptLeaseEpoch(NextNonZero(ref _nextInterruptLeaseEpoch)),
                deviceLease,
                source);
            _interruptLeases.Add(lease.Handle, new InterruptLeaseRecord(lease));
            return new NeutralInterruptBindResult(
                NeutralInterruptBindDecision.Bound,
                lease,
                "Neutral interrupt route materialized for the exact semantic device source.");
        }
        catch (Exception)
        {
            return new NeutralInterruptBindResult(
                NeutralInterruptBindDecision.Faulted,
                default,
                "Neutral interrupt route materialization faulted.");
        }
    }

    public NeutralInterruptSignalResult SignalInterrupt(NeutralInterruptLease lease)
    {
        var validation = ValidateInterruptLease(lease, out var record);
        if (validation != NeutralInterruptBindDecision.Bound)
        {
            return new NeutralInterruptSignalResult(
                ToSignalDecision(validation),
                lease,
                default,
                InterruptLeaseFailureReason(validation));
        }

        if (record!.PendingSequence.Value != 0)
        {
            return new NeutralInterruptSignalResult(
                NeutralInterruptSignalDecision.AlreadyPending,
                record.Lease,
                record.PendingSequence,
                "The exact interrupt route already has one pending semantic delivery.");
        }

        try
        {
            var sequence = new NeutralInterruptDeliverySequence(
                NextNonZero(ref _nextInterruptDeliverySequence));
            record.PendingSequence = sequence;
            return new NeutralInterruptSignalResult(
                NeutralInterruptSignalDecision.Signaled,
                record.Lease,
                sequence,
                "Semantic interrupt delivery is pending for the exact route.");
        }
        catch (Exception)
        {
            return new NeutralInterruptSignalResult(
                NeutralInterruptSignalDecision.Faulted,
                record.Lease,
                default,
                "Neutral interrupt delivery sequencing faulted.");
        }
    }

    public NeutralInterruptPollResult PollInterrupt(NeutralInterruptLease lease)
    {
        var validation = ValidateInterruptLease(lease, out var record);
        if (validation != NeutralInterruptBindDecision.Bound)
        {
            return new NeutralInterruptPollResult(
                ToPollDecision(validation),
                lease,
                false,
                default,
                InterruptLeaseFailureReason(validation));
        }

        var sequence = record!.PendingSequence;
        return new NeutralInterruptPollResult(
            NeutralInterruptPollDecision.Observed,
            record.Lease,
            sequence.Value != 0,
            sequence,
            sequence.Value == 0
                ? "No semantic interrupt delivery is pending for the exact route."
                : "A semantic interrupt delivery is pending for the exact route.");
    }

    public NeutralInterruptCompleteResult CompleteInterruptDelivery(
        NeutralInterruptLease lease,
        NeutralInterruptDeliverySequence sequence)
    {
        var validation = ValidateInterruptLease(lease, out var record);
        if (validation != NeutralInterruptBindDecision.Bound)
        {
            return new NeutralInterruptCompleteResult(
                ToCompleteDecision(validation),
                lease,
                sequence,
                InterruptLeaseFailureReason(validation));
        }

        if (record!.PendingSequence.Value == 0)
        {
            return new NeutralInterruptCompleteResult(
                NeutralInterruptCompleteDecision.NoPendingDelivery,
                record.Lease,
                sequence,
                "No semantic interrupt delivery is pending for the exact route.");
        }

        if (sequence.Value == 0 || record.PendingSequence != sequence)
        {
            return new NeutralInterruptCompleteResult(
                NeutralInterruptCompleteDecision.WrongSequence,
                record.Lease,
                sequence,
                "The interrupt delivery sequence does not match the exact pending delivery.");
        }

        record.PendingSequence = default;
        return new NeutralInterruptCompleteResult(
            NeutralInterruptCompleteDecision.Completed,
            record.Lease,
            sequence,
            "The exact semantic interrupt delivery was completed.");
    }

    public NeutralInterruptCloseResult CloseInterrupt(NeutralInterruptLease lease)
    {
        if (!lease.IsMaterialized)
        {
            return new NeutralInterruptCloseResult(
                NeutralInterruptCloseDecision.NotFound,
                lease,
                "Neutral interrupt lease is not materialized.");
        }

        if (!_interruptLeases.TryGetValue(lease.Handle, out var record))
        {
            return new NeutralInterruptCloseResult(
                NeutralInterruptCloseDecision.NotFound,
                lease,
                "Neutral interrupt lease was not found.");
        }

        if (record.Lease.Epoch != lease.Epoch)
        {
            return new NeutralInterruptCloseResult(
                NeutralInterruptCloseDecision.Stale,
                lease,
                "Neutral interrupt lease epoch is stale.");
        }

        if (record.Lease != lease)
        {
            return new NeutralInterruptCloseResult(
                NeutralInterruptCloseDecision.Faulted,
                lease,
                "Neutral interrupt lease identity does not match the materialized route.");
        }

        if (record.Revoked)
        {
            return new NeutralInterruptCloseResult(
                NeutralInterruptCloseDecision.Revoked,
                record.Lease,
                "Neutral interrupt lease has already been closed.");
        }

        var deviceDecision = ValidateInterruptDevice(lease.DeviceLease);
        if (deviceDecision == NeutralInterruptBindDecision.Revoked)
        {
            record.Revoked = true;
            record.PendingSequence = default;
            return new NeutralInterruptCloseResult(
                NeutralInterruptCloseDecision.Revoked,
                record.Lease,
                "The owning neutral device lease has already been closed; interrupt authority is no longer live.");
        }

        if (deviceDecision == NeutralInterruptBindDecision.Stale)
        {
            return new NeutralInterruptCloseResult(
                NeutralInterruptCloseDecision.Stale,
                lease,
                "The owning neutral device lease epoch is stale.");
        }

        if (deviceDecision != NeutralInterruptBindDecision.Bound)
        {
            return new NeutralInterruptCloseResult(
                NeutralInterruptCloseDecision.Faulted,
                lease,
                "The owning neutral device lease can no longer prove this interrupt-route lifetime.");
        }

        record.Revoked = true;
        record.PendingSequence = default;
        return new NeutralInterruptCloseResult(
            NeutralInterruptCloseDecision.Closed,
            record.Lease,
            "Neutral interrupt route closed; any pending semantic delivery was discarded.");
    }

    internal bool HasActiveInterruptLeasesForDevice(NeutralDeviceLease deviceLease) =>
        _interruptLeases.Values.Any(record =>
            !record.Revoked &&
            record.Lease.DeviceLease.Handle == deviceLease.Handle &&
            IsInterruptDeviceLive(record.Lease.DeviceLease));

    private NeutralInterruptBindDecision ValidateInterruptDevice(NeutralDeviceLease deviceLease)
    {
        if (!deviceLease.IsMaterialized)
            return NeutralInterruptBindDecision.NotFound;
        if (!_deviceLeases.TryGetValue(deviceLease.Handle, out var record))
            return NeutralInterruptBindDecision.NotFound;
        if (record.Lease.Epoch != deviceLease.Epoch)
            return NeutralInterruptBindDecision.Stale;
        if (record.Lease != deviceLease)
            return NeutralInterruptBindDecision.Faulted;
        if (record.Revoked)
            return NeutralInterruptBindDecision.Revoked;

        var domainDecision = ValidateDeviceDomain(deviceLease.DomainLease);
        return domainDecision switch
        {
            NeutralDeviceBindDecision.Bound => NeutralInterruptBindDecision.Bound,
            NeutralDeviceBindDecision.Stale => NeutralInterruptBindDecision.Stale,
            NeutralDeviceBindDecision.Revoked => NeutralInterruptBindDecision.Revoked,
            NeutralDeviceBindDecision.NotFound => NeutralInterruptBindDecision.NotFound,
            _ => NeutralInterruptBindDecision.Faulted,
        };
    }

    private NeutralInterruptBindDecision ValidateInterruptLease(
        NeutralInterruptLease lease,
        out InterruptLeaseRecord? record)
    {
        record = null;
        if (!lease.IsMaterialized)
            return NeutralInterruptBindDecision.NotFound;
        if (!_interruptLeases.TryGetValue(lease.Handle, out record))
            return NeutralInterruptBindDecision.NotFound;
        if (record.Lease.Epoch != lease.Epoch)
            return NeutralInterruptBindDecision.Stale;
        if (record.Lease != lease)
            return NeutralInterruptBindDecision.Faulted;
        if (record.Revoked)
            return NeutralInterruptBindDecision.Revoked;
        return ValidateInterruptDevice(lease.DeviceLease);
    }

    private bool IsInterruptDeviceLive(NeutralDeviceLease deviceLease) =>
        ValidateInterruptDevice(deviceLease) == NeutralInterruptBindDecision.Bound;

    private static string InterruptDeviceFailureReason(NeutralInterruptBindDecision decision) =>
        decision switch
        {
            NeutralInterruptBindDecision.NotFound => "Neutral device lease was not found.",
            NeutralInterruptBindDecision.Stale => "Neutral device lease epoch is stale.",
            NeutralInterruptBindDecision.Revoked => "Neutral device lease has been revoked.",
            NeutralInterruptBindDecision.Faulted => "Neutral device lease identity is malformed.",
            _ => "Neutral device lease is not valid for interrupt-route materialization.",
        };

    private static string InterruptLeaseFailureReason(NeutralInterruptBindDecision decision) =>
        decision switch
        {
            NeutralInterruptBindDecision.NotFound => "Neutral interrupt lease was not found.",
            NeutralInterruptBindDecision.Stale => "Neutral interrupt lease epoch is stale.",
            NeutralInterruptBindDecision.Revoked => "Neutral interrupt lease has been revoked.",
            NeutralInterruptBindDecision.Faulted => "Neutral interrupt lease identity is malformed.",
            _ => "Neutral interrupt lease is not live.",
        };

    private static NeutralInterruptSignalDecision ToSignalDecision(NeutralInterruptBindDecision decision) =>
        decision switch
        {
            NeutralInterruptBindDecision.NotFound => NeutralInterruptSignalDecision.NotFound,
            NeutralInterruptBindDecision.Stale => NeutralInterruptSignalDecision.Stale,
            NeutralInterruptBindDecision.Revoked => NeutralInterruptSignalDecision.Revoked,
            _ => NeutralInterruptSignalDecision.Faulted,
        };

    private static NeutralInterruptPollDecision ToPollDecision(NeutralInterruptBindDecision decision) =>
        decision switch
        {
            NeutralInterruptBindDecision.NotFound => NeutralInterruptPollDecision.NotFound,
            NeutralInterruptBindDecision.Stale => NeutralInterruptPollDecision.Stale,
            NeutralInterruptBindDecision.Revoked => NeutralInterruptPollDecision.Revoked,
            _ => NeutralInterruptPollDecision.Faulted,
        };

    private static NeutralInterruptCompleteDecision ToCompleteDecision(NeutralInterruptBindDecision decision) =>
        decision switch
        {
            NeutralInterruptBindDecision.NotFound => NeutralInterruptCompleteDecision.NotFound,
            NeutralInterruptBindDecision.Stale => NeutralInterruptCompleteDecision.Stale,
            NeutralInterruptBindDecision.Revoked => NeutralInterruptCompleteDecision.Revoked,
            _ => NeutralInterruptCompleteDecision.Faulted,
        };
}
