namespace YAKSys_Hybrid_CPU.Core;

public enum NeutralMemoryAcquireRequirement : byte
{
    AcquisitionFence = 0,
}

public enum NeutralMemoryAcquireOutcome : byte
{
    AcquisitionFenceSatisfied = 0,
    Unsupported,
}

public enum NeutralOwnedRegionAcquireDecision : byte
{
    Satisfied = 0,
    Unsupported,
    NotClosed,
    NotFound,
    Stale,
    RevokedDomain,
    Faulted,
}

public readonly record struct NeutralOwnedRegionAcquireResult(
    NeutralOwnedRegionAcquireDecision Decision,
    NeutralOwnedRegionMappingLease Lease,
    NeutralMemoryAcquireRequirement Requirement,
    NeutralMemoryAcquireOutcome Outcome,
    string Reason)
{
    public bool IsSatisfied =>
        Decision == NeutralOwnedRegionAcquireDecision.Satisfied &&
        Requirement == NeutralMemoryAcquireRequirement.AcquisitionFence &&
        Outcome == NeutralMemoryAcquireOutcome.AcquisitionFenceSatisfied;
}

public sealed partial class NeutralDomainRuntimeFacade
{
    private readonly Dictionary<NeutralOwnedRegionMappingHandle, ulong>
        _ownedRegionAcquireSequences = [];

    public NeutralOwnedRegionAcquireResult AcquireOwnedRegionVisibility(
        NeutralOwnedRegionMappingLease mapping,
        NeutralMemoryAcquireRequirement requirement)
    {
        if (!mapping.IsMaterialized ||
            !_ownedRegionMappings.TryGetValue(mapping.Handle, out var record))
        {
            return AcquireDenied(
                NeutralOwnedRegionAcquireDecision.NotFound,
                mapping,
                requirement,
                "Neutral owned-region mapping was not found.");
        }

        if (record.Lease.Epoch != mapping.Epoch)
        {
            return AcquireDenied(
                NeutralOwnedRegionAcquireDecision.Stale,
                mapping,
                requirement,
                "Neutral owned-region mapping epoch is stale.");
        }

        if (record.Lease.DomainLease != mapping.DomainLease ||
            record.Lease.Slice != mapping.Slice ||
            record.Lease.Coherence != mapping.Coherence)
        {
            return AcquireDenied(
                NeutralOwnedRegionAcquireDecision.Faulted,
                mapping,
                requirement,
                "Neutral owned-region mapping identity does not match the materialized mapping.");
        }

        if (!Enum.IsDefined(requirement))
        {
            return AcquireDenied(
                NeutralOwnedRegionAcquireDecision.Faulted,
                mapping,
                requirement,
                "The neutral memory-acquire requirement is undefined.");
        }

        if (!record.Revoked)
        {
            return AcquireDenied(
                NeutralOwnedRegionAcquireDecision.NotClosed,
                mapping,
                requirement,
                "External mapping authority must be closed before CPU acquire evidence can be produced.");
        }

        var domainDecision = ValidateLiveDomainForMapping(mapping.DomainLease);
        if (domainDecision != NeutralOwnedRegionMapDecision.Mapped)
        {
            return AcquireDenied(
                domainDecision switch
                {
                    NeutralOwnedRegionMapDecision.NotFound => NeutralOwnedRegionAcquireDecision.NotFound,
                    NeutralOwnedRegionMapDecision.Stale => NeutralOwnedRegionAcquireDecision.Stale,
                    NeutralOwnedRegionMapDecision.Revoked => NeutralOwnedRegionAcquireDecision.RevokedDomain,
                    _ => NeutralOwnedRegionAcquireDecision.Faulted,
                },
                mapping,
                requirement,
                "The neutral runtime domain is not live for post-close acquire.");
        }

        if (mapping.Coherence != NeutralMemoryCoherenceModel.NonCoherent)
        {
            return AcquireDenied(
                NeutralOwnedRegionAcquireDecision.Faulted,
                mapping,
                requirement,
                "The neutral mapping coherence model is undefined.");
        }

        if (requirement != NeutralMemoryAcquireRequirement.AcquisitionFence)
        {
            return AcquireDenied(
                NeutralOwnedRegionAcquireDecision.Unsupported,
                mapping,
                requirement,
                "The requested neutral memory-acquire mode is not supported.");
        }

        _ownedRegionAcquireSequences[mapping.Handle] = 1;
        return new NeutralOwnedRegionAcquireResult(
            NeutralOwnedRegionAcquireDecision.Satisfied,
            record.Lease,
            requirement,
            NeutralMemoryAcquireOutcome.AcquisitionFenceSatisfied,
            "Neutral non-coherent acquisition fence satisfied after exact mapping closure.");
    }

    internal NeutralOwnedRegionAcquireResult AcquireOwnedRegionVisibilityWhileMapped(
        NeutralOwnedRegionMappingLease mapping,
        NeutralMemoryAcquireRequirement requirement)
    {
        if (!mapping.IsMaterialized ||
            !_ownedRegionMappings.TryGetValue(mapping.Handle, out var record))
        {
            return AcquireDenied(
                NeutralOwnedRegionAcquireDecision.NotFound,
                mapping,
                requirement,
                "Neutral owned-region mapping was not found for DMA-scoped acquire.");
        }

        if (record.Lease.Epoch != mapping.Epoch)
        {
            return AcquireDenied(
                NeutralOwnedRegionAcquireDecision.Stale,
                mapping,
                requirement,
                "Neutral owned-region mapping epoch is stale for DMA-scoped acquire.");
        }

        if (record.Lease != mapping)
        {
            return AcquireDenied(
                NeutralOwnedRegionAcquireDecision.Faulted,
                mapping,
                requirement,
                "Neutral owned-region mapping identity is malformed for DMA-scoped acquire.");
        }

        if (record.Revoked)
        {
            return AcquireDenied(
                NeutralOwnedRegionAcquireDecision.Faulted,
                mapping,
                requirement,
                "DMA-scoped acquire requires the exact mapped-region authority to remain live.");
        }

        if (!Enum.IsDefined(requirement) ||
            requirement != NeutralMemoryAcquireRequirement.AcquisitionFence)
        {
            return AcquireDenied(
                Enum.IsDefined(requirement)
                    ? NeutralOwnedRegionAcquireDecision.Unsupported
                    : NeutralOwnedRegionAcquireDecision.Faulted,
                mapping,
                requirement,
                "DMA-scoped acquire supports only the explicit acquisition-fence requirement.");
        }

        var domainDecision = ValidateLiveDomainForMapping(mapping.DomainLease);
        if (domainDecision != NeutralOwnedRegionMapDecision.Mapped)
        {
            return AcquireDenied(
                domainDecision switch
                {
                    NeutralOwnedRegionMapDecision.NotFound => NeutralOwnedRegionAcquireDecision.NotFound,
                    NeutralOwnedRegionMapDecision.Stale => NeutralOwnedRegionAcquireDecision.Stale,
                    NeutralOwnedRegionMapDecision.Revoked => NeutralOwnedRegionAcquireDecision.RevokedDomain,
                    _ => NeutralOwnedRegionAcquireDecision.Faulted,
                },
                mapping,
                requirement,
                "The neutral runtime domain is not live for DMA-scoped acquire.");
        }

        if (mapping.Coherence != NeutralMemoryCoherenceModel.NonCoherent)
        {
            return AcquireDenied(
                NeutralOwnedRegionAcquireDecision.Faulted,
                mapping,
                requirement,
                "The neutral mapping coherence model is undefined for DMA-scoped acquire.");
        }

        _ownedRegionAcquireSequences.TryGetValue(mapping.Handle, out var sequence);
        _ownedRegionAcquireSequences[mapping.Handle] = sequence + 1;
        return new NeutralOwnedRegionAcquireResult(
            NeutralOwnedRegionAcquireDecision.Satisfied,
            record.Lease,
            requirement,
            NeutralMemoryAcquireOutcome.AcquisitionFenceSatisfied,
            "Neutral non-coherent DMA-scoped acquisition fence satisfied while the exact mapping remains live.");
    }

    internal ulong AcquisitionSequenceForTesting(
        NeutralOwnedRegionMappingLease mapping) =>
        _ownedRegionAcquireSequences.TryGetValue(mapping.Handle, out var sequence) &&
        _ownedRegionMappings.TryGetValue(mapping.Handle, out var record) &&
        record.Lease == mapping
            ? sequence
            : 0;

    private static NeutralOwnedRegionAcquireResult AcquireDenied(
        NeutralOwnedRegionAcquireDecision decision,
        NeutralOwnedRegionMappingLease mapping,
        NeutralMemoryAcquireRequirement requirement,
        string reason) =>
        new(
            decision,
            mapping,
            requirement,
            NeutralMemoryAcquireOutcome.Unsupported,
            reason);
}
