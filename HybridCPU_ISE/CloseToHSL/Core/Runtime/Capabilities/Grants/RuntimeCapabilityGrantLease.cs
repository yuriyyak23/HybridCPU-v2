using System.Runtime.CompilerServices;

namespace YAKSys_Hybrid_CPU.Core;

/// <summary>
/// Non-serializable identity for one live typed grant. The contained grant remains
/// policy data; liveness belongs exclusively to its neutral runtime owner.
/// </summary>
internal sealed class RuntimeCapabilityGrantLease
{
    private readonly object _ownerSeal;

    internal RuntimeCapabilityGrantLease(
        object ownerSeal,
        ulong grantIdentity,
        ulong generation,
        CapabilityGrant grant)
    {
        _ownerSeal = ownerSeal;
        GrantIdentity = grantIdentity;
        Generation = generation;
        Grant = grant;
    }

    internal ulong GrantIdentity { get; }
    internal ulong Generation { get; }
    internal CapabilityGrant Grant { get; }

    internal bool WasIssuedBy(object ownerSeal) => ReferenceEquals(_ownerSeal, ownerSeal);
}

/// <summary>
/// Neutral lifecycle owner for generation-bearing typed capability grants.
/// Revocation advances the generation and invalidates every prior lease.
/// </summary>
internal sealed class RuntimeCapabilityGrantOwner
{
    private sealed record LiveGrant(ulong Identity, ulong Generation);

    private readonly object _ownerSeal = new();
    private readonly ConditionalWeakTable<RuntimeCapabilityGrantLease, LiveGrant> _live = new();
    private ulong _generation = 1;
    private ulong _nextIdentity = 1;

    internal ulong CurrentGeneration => _generation;

    internal RuntimeCapabilityGrantLease Issue(CapabilityGrant grant)
    {
        ArgumentNullException.ThrowIfNull(grant);
        ulong identity = AllocateIdentity();
        var lease = new RuntimeCapabilityGrantLease(_ownerSeal, identity, _generation, grant);
        _live.Add(lease, new LiveGrant(identity, _generation));
        return lease;
    }

    internal bool IsLive(RuntimeCapabilityGrantLease? lease)
    {
        return lease is not null &&
            lease.WasIssuedBy(_ownerSeal) &&
            lease.GrantIdentity != 0 &&
            lease.Generation == _generation &&
            _live.TryGetValue(lease, out LiveGrant? live) &&
            live.Identity == lease.GrantIdentity &&
            live.Generation == _generation;
    }

    internal void RevokeAll()
    {
        unchecked
        {
            _generation++;
            if (_generation == 0)
                _generation = 1;
        }
    }

    private ulong AllocateIdentity()
    {
        ulong identity = _nextIdentity++;
        if (identity == 0)
            identity = _nextIdentity++;
        return identity;
    }
}
