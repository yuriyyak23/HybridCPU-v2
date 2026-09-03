namespace YAKSys_Hybrid_CPU.Core;

[Flags]
public enum NeutralMemoryAccess : byte
{
    None = 0,
    Read = 1 << 0,
    Write = 1 << 1,
}

public enum NeutralMemoryCoherenceModel : byte
{
    NonCoherent = 0,
}

public enum NeutralMemoryVisibilityRequirement : byte
{
    CoherentAccess = 0,
    PublicationFence,
    CacheMaintenance,
}

public enum NeutralMemoryVisibilityOutcome : byte
{
    Coherent = 0,
    PublicationFenceSatisfied,
    CacheMaintenanceSatisfied,
    Unsupported,
}

public readonly record struct NeutralOwnedRegionMappingHandle(ulong Value);
public readonly record struct NeutralOwnedRegionMappingEpoch(ulong Value);

public readonly record struct NeutralOwnedRegionSlice(
    long Offset,
    long Length,
    NeutralMemoryAccess Access);

public readonly record struct NeutralOwnedRegionMappingLease(
    NeutralOwnedRegionMappingHandle Handle,
    NeutralOwnedRegionMappingEpoch Epoch,
    NeutralDomainBindingLease DomainLease,
    NeutralOwnedRegionSlice Slice,
    NeutralMemoryCoherenceModel Coherence)
{
    public bool IsMaterialized =>
        Handle.Value != 0 &&
        Epoch.Value != 0 &&
        DomainLease.IsMaterialized;
}

public enum NeutralOwnedRegionMapDecision : byte
{
    Mapped = 0,
    InvalidRange,
    InvalidAccess,
    NotFound,
    Stale,
    Revoked,
    Faulted,
}

public readonly record struct NeutralOwnedRegionMapResult(
    NeutralOwnedRegionMapDecision Decision,
    NeutralOwnedRegionMappingLease Lease,
    string Reason)
{
    public bool IsMapped =>
        Decision == NeutralOwnedRegionMapDecision.Mapped && Lease.IsMaterialized;
}

public enum NeutralOwnedRegionVisibilityDecision : byte
{
    Satisfied = 0,
    Unsupported,
    NotFound,
    Stale,
    Revoked,
    Faulted,
}

public readonly record struct NeutralOwnedRegionVisibilityResult(
    NeutralOwnedRegionVisibilityDecision Decision,
    NeutralOwnedRegionMappingLease Lease,
    NeutralMemoryVisibilityRequirement Requirement,
    NeutralMemoryVisibilityOutcome Outcome,
    string Reason)
{
    public bool IsSatisfied =>
        Decision == NeutralOwnedRegionVisibilityDecision.Satisfied &&
        (Requirement, Outcome) switch
        {
            (NeutralMemoryVisibilityRequirement.CoherentAccess,
                NeutralMemoryVisibilityOutcome.Coherent) => true,
            (NeutralMemoryVisibilityRequirement.PublicationFence,
                NeutralMemoryVisibilityOutcome.PublicationFenceSatisfied) => true,
            (NeutralMemoryVisibilityRequirement.CacheMaintenance,
                NeutralMemoryVisibilityOutcome.CacheMaintenanceSatisfied) => true,
            _ => false,
        };
}

public enum NeutralOwnedRegionCloseDecision : byte
{
    Closed = 0,
    NotFound,
    Stale,
    Revoked,
    Faulted,
    ActiveDependents,
}

public readonly record struct NeutralOwnedRegionCloseResult(
    NeutralOwnedRegionCloseDecision Decision,
    NeutralOwnedRegionMappingLease Lease,
    string Reason)
{
    public bool IsClosed => Decision == NeutralOwnedRegionCloseDecision.Closed;
}

public sealed partial class NeutralDomainRuntimeFacade
{
    private sealed class OwnedRegionMappingRecord(NeutralOwnedRegionMappingLease lease)
    {
        public NeutralOwnedRegionMappingLease Lease { get; } = lease;
        public bool Revoked { get; set; }
        public ulong PublicationSequence { get; set; }
    }

    private readonly Dictionary<NeutralOwnedRegionMappingHandle, OwnedRegionMappingRecord>
        _ownedRegionMappings = [];
    private ulong _nextOwnedRegionMappingId = 1;
    private ulong _nextOwnedRegionMappingEpoch = 1;

    public int ActiveOwnedRegionMappingCount =>
        _ownedRegionMappings.Values.Count(static record => !record.Revoked);

    public NeutralOwnedRegionMapResult MapOwnedRegion(
        NeutralDomainBindingLease domainLease,
        NeutralOwnedRegionSlice slice)
    {
        var domain = ValidateLiveDomainForMapping(domainLease);
        if (domain != NeutralOwnedRegionMapDecision.Mapped)
        {
            return new NeutralOwnedRegionMapResult(
                domain,
                default,
                DomainMappingFailureReason(domain));
        }

        if (!IsValidAccess(slice.Access))
        {
            return new NeutralOwnedRegionMapResult(
                NeutralOwnedRegionMapDecision.InvalidAccess,
                default,
                "Neutral owned-region access must be Read, Write, or Read|Write.");
        }

        if (!IsValidRange(slice.Offset, slice.Length))
        {
            return new NeutralOwnedRegionMapResult(
                NeutralOwnedRegionMapDecision.InvalidRange,
                default,
                "Neutral owned-region mapping requires a non-negative, non-overflowing range with positive length.");
        }

        try
        {
            var lease = new NeutralOwnedRegionMappingLease(
                new NeutralOwnedRegionMappingHandle(NextNonZero(ref _nextOwnedRegionMappingId)),
                new NeutralOwnedRegionMappingEpoch(NextNonZero(ref _nextOwnedRegionMappingEpoch)),
                domainLease,
                slice,
                NeutralMemoryCoherenceModel.NonCoherent);
            _ownedRegionMappings.Add(
                lease.Handle,
                new OwnedRegionMappingRecord(lease));
            return new NeutralOwnedRegionMapResult(
                NeutralOwnedRegionMapDecision.Mapped,
                lease,
                "Neutral owned-region mapping materialized with explicit non-coherent semantics.");
        }
        catch (Exception)
        {
            return new NeutralOwnedRegionMapResult(
                NeutralOwnedRegionMapDecision.Faulted,
                default,
                "Neutral owned-region mapping materialization faulted.");
        }
    }

    public NeutralOwnedRegionVisibilityResult PrepareOwnedRegionVisibility(
        NeutralOwnedRegionMappingLease mapping,
        NeutralMemoryVisibilityRequirement requirement)
    {
        var validation = ValidateLiveOwnedRegionMapping(mapping);
        if (validation is { } rejected)
        {
            return new NeutralOwnedRegionVisibilityResult(
                rejected,
                mapping,
                requirement,
                NeutralMemoryVisibilityOutcome.Unsupported,
                MappingFailureReason(rejected));
        }

        if (!Enum.IsDefined(requirement))
        {
            return new NeutralOwnedRegionVisibilityResult(
                NeutralOwnedRegionVisibilityDecision.Faulted,
                mapping,
                requirement,
                NeutralMemoryVisibilityOutcome.Unsupported,
                "The neutral memory-visibility requirement is undefined.");
        }

        if (mapping.Coherence != NeutralMemoryCoherenceModel.NonCoherent)
        {
            return new NeutralOwnedRegionVisibilityResult(
                NeutralOwnedRegionVisibilityDecision.Faulted,
                mapping,
                requirement,
                NeutralMemoryVisibilityOutcome.Unsupported,
                "The neutral mapping coherence model is undefined.");
        }

        if (requirement != NeutralMemoryVisibilityRequirement.PublicationFence)
        {
            return new NeutralOwnedRegionVisibilityResult(
                NeutralOwnedRegionVisibilityDecision.Unsupported,
                mapping,
                requirement,
                NeutralMemoryVisibilityOutcome.Unsupported,
                "This neutral mapping is explicitly non-coherent and only publication-fence preparation is modeled in this slice.");
        }

        _ownedRegionMappings[mapping.Handle].PublicationSequence++;
        return new NeutralOwnedRegionVisibilityResult(
            NeutralOwnedRegionVisibilityDecision.Satisfied,
            mapping,
            requirement,
            NeutralMemoryVisibilityOutcome.PublicationFenceSatisfied,
            "Neutral non-coherent publication fence satisfied for the exact mapping.");
    }

    public NeutralOwnedRegionCloseResult CloseOwnedRegionMapping(
        NeutralOwnedRegionMappingLease mapping)
    {
        if (!mapping.IsMaterialized)
        {
            return new NeutralOwnedRegionCloseResult(
                NeutralOwnedRegionCloseDecision.NotFound,
                mapping,
                "Neutral owned-region mapping lease is not materialized.");
        }

        if (!_ownedRegionMappings.TryGetValue(mapping.Handle, out var record))
        {
            return new NeutralOwnedRegionCloseResult(
                NeutralOwnedRegionCloseDecision.NotFound,
                mapping,
                "Neutral owned-region mapping was not found.");
        }

        if (record.Lease.Epoch != mapping.Epoch)
        {
            return new NeutralOwnedRegionCloseResult(
                NeutralOwnedRegionCloseDecision.Stale,
                mapping,
                "Neutral owned-region mapping epoch is stale.");
        }

        if (record.Lease.DomainLease != mapping.DomainLease ||
            record.Lease.Slice != mapping.Slice ||
            record.Lease.Coherence != mapping.Coherence)
        {
            return new NeutralOwnedRegionCloseResult(
                NeutralOwnedRegionCloseDecision.Faulted,
                mapping,
                "Neutral owned-region mapping identity does not match the materialized mapping.");
        }

        if (record.Revoked)
        {
            return new NeutralOwnedRegionCloseResult(
                NeutralOwnedRegionCloseDecision.Revoked,
                record.Lease,
                "Neutral owned-region mapping has already been closed.");
        }

        if (HasActiveDmaGrantsForMapping(record.Lease))
        {
            return new NeutralOwnedRegionCloseResult(
                NeutralOwnedRegionCloseDecision.ActiveDependents,
                record.Lease,
                "Neutral DMA grants must close before the owning region mapping.");
        }

        record.Revoked = true;
        return new NeutralOwnedRegionCloseResult(
            NeutralOwnedRegionCloseDecision.Closed,
            record.Lease,
            "Neutral owned-region mapping closed.");
    }

    internal ulong PublicationSequenceForTesting(
        NeutralOwnedRegionMappingLease mapping) =>
        _ownedRegionMappings.TryGetValue(mapping.Handle, out var record) &&
        record.Lease == mapping
            ? record.PublicationSequence
            : 0;

    private NeutralOwnedRegionMapDecision ValidateLiveDomainForMapping(
        NeutralDomainBindingLease domainLease)
    {
        if (!domainLease.IsMaterialized ||
            !_bindings.TryGetValue(domainLease.Handle, out var record))
        {
            return NeutralOwnedRegionMapDecision.NotFound;
        }

        if (record.Lease.Epoch != domainLease.Epoch)
            return NeutralOwnedRegionMapDecision.Stale;

        return record.Revoked
            ? NeutralOwnedRegionMapDecision.Revoked
            : NeutralOwnedRegionMapDecision.Mapped;
    }

    private NeutralOwnedRegionVisibilityDecision? ValidateLiveOwnedRegionMapping(
        NeutralOwnedRegionMappingLease mapping)
    {
        if (!mapping.IsMaterialized ||
            !_ownedRegionMappings.TryGetValue(mapping.Handle, out var record))
        {
            return NeutralOwnedRegionVisibilityDecision.NotFound;
        }

        if (record.Lease.Epoch != mapping.Epoch)
            return NeutralOwnedRegionVisibilityDecision.Stale;

        if (record.Lease.DomainLease != mapping.DomainLease ||
            record.Lease.Slice != mapping.Slice ||
            record.Lease.Coherence != mapping.Coherence)
        {
            return NeutralOwnedRegionVisibilityDecision.Faulted;
        }

        if (record.Revoked)
            return NeutralOwnedRegionVisibilityDecision.Revoked;

        var domainDecision = ValidateLiveDomainForMapping(mapping.DomainLease);
        return domainDecision switch
        {
            NeutralOwnedRegionMapDecision.Mapped => null,
            NeutralOwnedRegionMapDecision.NotFound => NeutralOwnedRegionVisibilityDecision.NotFound,
            NeutralOwnedRegionMapDecision.Stale => NeutralOwnedRegionVisibilityDecision.Stale,
            NeutralOwnedRegionMapDecision.Revoked => NeutralOwnedRegionVisibilityDecision.Revoked,
            _ => NeutralOwnedRegionVisibilityDecision.Faulted,
        };
    }

    private static bool IsValidRange(long offset, long length) =>
        offset >= 0 &&
        length > 0 &&
        offset <= long.MaxValue - length;

    private static bool IsValidAccess(NeutralMemoryAccess access) =>
        access != NeutralMemoryAccess.None &&
        (access & ~(NeutralMemoryAccess.Read | NeutralMemoryAccess.Write)) == 0;

    private static string DomainMappingFailureReason(NeutralOwnedRegionMapDecision decision) =>
        decision switch
        {
            NeutralOwnedRegionMapDecision.NotFound => "Neutral runtime domain binding was not found.",
            NeutralOwnedRegionMapDecision.Stale => "Neutral runtime domain lease epoch is stale.",
            NeutralOwnedRegionMapDecision.Revoked => "Neutral runtime domain binding has already been closed.",
            _ => "Neutral runtime domain cannot materialize owned-region mapping authority.",
        };

    private static string MappingFailureReason(NeutralOwnedRegionVisibilityDecision decision) =>
        decision switch
        {
            NeutralOwnedRegionVisibilityDecision.NotFound => "Neutral owned-region mapping was not found.",
            NeutralOwnedRegionVisibilityDecision.Stale => "Neutral owned-region mapping epoch is stale.",
            NeutralOwnedRegionVisibilityDecision.Revoked => "Neutral owned-region mapping has already been closed.",
            _ => "Neutral owned-region mapping identity is malformed.",
        };
}
