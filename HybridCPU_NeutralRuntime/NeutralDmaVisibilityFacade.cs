namespace YAKSys_Hybrid_CPU.Core;

public readonly record struct NeutralDmaVisibilityCycle(ulong Value);

public enum NeutralDmaPrepareDecision : byte
{
    Prepared = 0,
    NotFound,
    Stale,
    Revoked,
    Faulted,
    VisibilityUnsupported,
}

public readonly record struct NeutralDmaPrepareEvidence(
    NeutralDmaGrantHandle GrantHandle,
    NeutralDmaGrantEpoch GrantEpoch,
    NeutralDmaVisibilityCycle Cycle,
    NeutralDmaDirection Direction,
    NeutralMemoryVisibilityRequirement Requirement,
    NeutralMemoryVisibilityOutcome Outcome)
{
    public bool IsSatisfied =>
        GrantHandle.Value != 0 &&
        GrantEpoch.Value != 0 &&
        Cycle.Value != 0 &&
        Requirement == NeutralMemoryVisibilityRequirement.PublicationFence &&
        Outcome == NeutralMemoryVisibilityOutcome.PublicationFenceSatisfied;
}

public readonly record struct NeutralDmaPrepareResult(
    NeutralDmaPrepareDecision Decision,
    NeutralDmaPrepareEvidence Evidence,
    string Reason)
{
    public bool IsPrepared =>
        Decision == NeutralDmaPrepareDecision.Prepared && Evidence.IsSatisfied;
}

public enum NeutralDmaAcquireDecision : byte
{
    Acquired = 0,
    NotRequired,
    NotPrepared,
    AlreadyAcquired,
    NotFound,
    Stale,
    Revoked,
    Faulted,
    VisibilityUnsupported,
}

public readonly record struct NeutralDmaAcquireEvidence(
    NeutralDmaGrantHandle GrantHandle,
    NeutralDmaGrantEpoch GrantEpoch,
    NeutralDmaVisibilityCycle Cycle,
    NeutralDmaDirection Direction,
    NeutralMemoryAcquireRequirement Requirement,
    NeutralMemoryAcquireOutcome Outcome)
{
    public bool IsSatisfied =>
        GrantHandle.Value != 0 &&
        GrantEpoch.Value != 0 &&
        Cycle.Value != 0 &&
        Requirement == NeutralMemoryAcquireRequirement.AcquisitionFence &&
        Outcome == NeutralMemoryAcquireOutcome.AcquisitionFenceSatisfied;
}

public readonly record struct NeutralDmaAcquireResult(
    NeutralDmaAcquireDecision Decision,
    NeutralDmaAcquireEvidence Evidence,
    string Reason)
{
    public bool IsAcquired =>
        Decision == NeutralDmaAcquireDecision.Acquired && Evidence.IsSatisfied;
}

/// <summary>
/// Grant-scoped non-coherent visibility evidence. A prepare cycle is a release/publication
/// boundary before future device access. Acquire is meaningful only for directions in which
/// the device may write memory. Neither operation submits DMA or proves device completion.
/// A prepared cycle that has already been acquired is intentionally consumed and must not be
/// accepted by a future submit operation.
/// </summary>
public sealed partial class NeutralDomainRuntimeFacade
{
    private sealed class DmaVisibilityRecord
    {
        public NeutralDmaVisibilityCycle Cycle { get; set; }
        public bool Acquired { get; set; }
    }

    private readonly Dictionary<NeutralDmaGrantHandle, DmaVisibilityRecord> _dmaVisibility = [];
    private ulong _nextDmaVisibilityCycle = 1;

    public NeutralDmaPrepareResult PrepareDmaVisibility(NeutralDmaGrant grant)
    {
        var validation = ValidateLiveDmaGrantForVisibility(grant);
        if (validation is { } rejected)
            return PrepareDenied(rejected, grant, DmaVisibilityFailureReason(rejected));

        var mappingVisibility = PrepareOwnedRegionVisibility(
            grant.MappingLease,
            NeutralMemoryVisibilityRequirement.PublicationFence);
        if (!mappingVisibility.IsSatisfied || mappingVisibility.Lease != grant.MappingLease)
        {
            return PrepareDenied(
                mappingVisibility.Decision switch
                {
                    NeutralOwnedRegionVisibilityDecision.NotFound => NeutralDmaPrepareDecision.NotFound,
                    NeutralOwnedRegionVisibilityDecision.Stale => NeutralDmaPrepareDecision.Stale,
                    NeutralOwnedRegionVisibilityDecision.Revoked => NeutralDmaPrepareDecision.Revoked,
                    NeutralOwnedRegionVisibilityDecision.Unsupported => NeutralDmaPrepareDecision.VisibilityUnsupported,
                    _ => NeutralDmaPrepareDecision.Faulted,
                },
                grant,
                mappingVisibility.Reason);
        }

        var cycle = new NeutralDmaVisibilityCycle(NextNonZero(ref _nextDmaVisibilityCycle));
        if (!_dmaVisibility.TryGetValue(grant.Handle, out var state))
        {
            state = new DmaVisibilityRecord();
            _dmaVisibility.Add(grant.Handle, state);
        }

        state.Cycle = cycle;
        state.Acquired = false;
        return new NeutralDmaPrepareResult(
            NeutralDmaPrepareDecision.Prepared,
            new NeutralDmaPrepareEvidence(
                grant.Handle,
                grant.Epoch,
                cycle,
                grant.Direction,
                NeutralMemoryVisibilityRequirement.PublicationFence,
                NeutralMemoryVisibilityOutcome.PublicationFenceSatisfied),
            "Neutral non-coherent DMA release/publication boundary satisfied for the exact grant cycle; no DMA was submitted.");
    }

    public NeutralDmaAcquireResult AcquireDmaVisibility(NeutralDmaGrant grant)
    {
        var validation = ValidateLiveDmaGrantForVisibility(grant);
        if (validation is { } rejected)
            return AcquireDenied(ToAcquireDecision(rejected), grant, DmaVisibilityFailureReason(rejected));

        if (grant.Direction == NeutralDmaDirection.DeviceReadsMemory)
        {
            return AcquireDenied(
                NeutralDmaAcquireDecision.NotRequired,
                grant,
                "Read-only device DMA cannot modify memory, so no post-write CPU acquire is required.");
        }

        if (!_dmaVisibility.TryGetValue(grant.Handle, out var state) || state.Cycle.Value == 0)
        {
            return AcquireDenied(
                NeutralDmaAcquireDecision.NotPrepared,
                grant,
                "The exact DMA grant has no prepared visibility cycle to acquire.");
        }

        if (state.Acquired)
        {
            return AcquireDenied(
                NeutralDmaAcquireDecision.AlreadyAcquired,
                grant,
                "The current DMA visibility cycle has already been acquired for CPU visibility.");
        }

        var mappingAcquire = AcquireOwnedRegionVisibilityWhileMapped(
            grant.MappingLease,
            NeutralMemoryAcquireRequirement.AcquisitionFence);
        if (!mappingAcquire.IsSatisfied || mappingAcquire.Lease != grant.MappingLease)
        {
            return AcquireDenied(
                mappingAcquire.Decision switch
                {
                    NeutralOwnedRegionAcquireDecision.NotFound => NeutralDmaAcquireDecision.NotFound,
                    NeutralOwnedRegionAcquireDecision.Stale => NeutralDmaAcquireDecision.Stale,
                    NeutralOwnedRegionAcquireDecision.RevokedDomain => NeutralDmaAcquireDecision.Revoked,
                    NeutralOwnedRegionAcquireDecision.Unsupported => NeutralDmaAcquireDecision.VisibilityUnsupported,
                    _ => NeutralDmaAcquireDecision.Faulted,
                },
                grant,
                mappingAcquire.Reason);
        }

        state.Acquired = true;
        return new NeutralDmaAcquireResult(
            NeutralDmaAcquireDecision.Acquired,
            new NeutralDmaAcquireEvidence(
                grant.Handle,
                grant.Epoch,
                state.Cycle,
                grant.Direction,
                NeutralMemoryAcquireRequirement.AcquisitionFence,
                NeutralMemoryAcquireOutcome.AcquisitionFenceSatisfied),
            "Neutral non-coherent DMA CPU acquire boundary satisfied for the exact grant cycle; this is visibility evidence, not completion evidence.");
    }

    internal bool HasPreparedUnacquiredDmaVisibilityCycle(NeutralDmaGrant grant) =>
        ValidateLiveDmaGrantForVisibility(grant) is null &&
        _dmaVisibility.TryGetValue(grant.Handle, out var state) &&
        state.Cycle.Value != 0 &&
        !state.Acquired;

    internal NeutralDmaVisibilityCycle CurrentDmaVisibilityCycleForTesting(NeutralDmaGrant grant) =>
        _dmaVisibility.TryGetValue(grant.Handle, out var state)
            ? state.Cycle
            : default;

    private NeutralDmaPrepareDecision? ValidateLiveDmaGrantForVisibility(NeutralDmaGrant grant)
    {
        if (!grant.IsMaterialized || !_dmaGrants.TryGetValue(grant.Handle, out var record))
            return NeutralDmaPrepareDecision.NotFound;
        if (record.Grant.Epoch != grant.Epoch)
            return NeutralDmaPrepareDecision.Stale;
        if (record.Grant != grant)
            return NeutralDmaPrepareDecision.Faulted;
        if (record.Revoked)
            return NeutralDmaPrepareDecision.Revoked;

        var device = ValidateLiveDmaDevice(grant.DeviceLease);
        if (device is not null)
        {
            return device switch
            {
                NeutralDmaGrantDecision.NotFound => NeutralDmaPrepareDecision.NotFound,
                NeutralDmaGrantDecision.Stale => NeutralDmaPrepareDecision.Stale,
                NeutralDmaGrantDecision.Revoked => NeutralDmaPrepareDecision.Revoked,
                _ => NeutralDmaPrepareDecision.Faulted,
            };
        }

        var mapping = ValidateLiveDmaMapping(grant.MappingLease);
        return mapping switch
        {
            null => null,
            NeutralDmaGrantDecision.NotFound => NeutralDmaPrepareDecision.NotFound,
            NeutralDmaGrantDecision.Stale => NeutralDmaPrepareDecision.Stale,
            NeutralDmaGrantDecision.Revoked => NeutralDmaPrepareDecision.Revoked,
            _ => NeutralDmaPrepareDecision.Faulted,
        };
    }

    private static NeutralDmaAcquireDecision ToAcquireDecision(NeutralDmaPrepareDecision decision) =>
        decision switch
        {
            NeutralDmaPrepareDecision.NotFound => NeutralDmaAcquireDecision.NotFound,
            NeutralDmaPrepareDecision.Stale => NeutralDmaAcquireDecision.Stale,
            NeutralDmaPrepareDecision.Revoked => NeutralDmaAcquireDecision.Revoked,
            NeutralDmaPrepareDecision.VisibilityUnsupported => NeutralDmaAcquireDecision.VisibilityUnsupported,
            _ => NeutralDmaAcquireDecision.Faulted,
        };

    private static NeutralDmaPrepareResult PrepareDenied(
        NeutralDmaPrepareDecision decision,
        NeutralDmaGrant grant,
        string reason) =>
        new(decision, default, reason);

    private static NeutralDmaAcquireResult AcquireDenied(
        NeutralDmaAcquireDecision decision,
        NeutralDmaGrant grant,
        string reason) =>
        new(decision, default, reason);

    private static string DmaVisibilityFailureReason(NeutralDmaPrepareDecision decision) =>
        decision switch
        {
            NeutralDmaPrepareDecision.NotFound => "The exact neutral DMA grant was not found.",
            NeutralDmaPrepareDecision.Stale => "The exact neutral DMA grant generation is stale.",
            NeutralDmaPrepareDecision.Revoked => "The exact neutral DMA grant has been revoked.",
            NeutralDmaPrepareDecision.VisibilityUnsupported => "The required non-coherent DMA visibility operation is unsupported.",
            _ => "The exact neutral DMA grant identity or dependency is malformed.",
        };
}
