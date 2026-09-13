namespace YAKSys_Hybrid_CPU.Core;

public readonly record struct CompatibilityControlPolicyConstructionProfile
{
    public CompatibilityControlPolicyConstructionProfile(
        ulong domainIdentity,
        CompatibilityEventRoutingPolicy eventRoutingPolicy)
    {
        if (domainIdentity == 0)
            throw new ArgumentOutOfRangeException(nameof(domainIdentity));
        if (eventRoutingPolicy == CompatibilityEventRoutingPolicy.None ||
            (eventRoutingPolicy & ~CompatibilityControlPolicyOwner.KnownEventRoutingPolicyMask) != 0)
        {
            throw new ArgumentOutOfRangeException(nameof(eventRoutingPolicy));
        }

        DomainIdentity = domainIdentity;
        EventRoutingPolicy = eventRoutingPolicy;
        IsConfigured = true;
    }

    public ulong DomainIdentity { get; }
    public CompatibilityEventRoutingPolicy EventRoutingPolicy { get; }
    public bool IsConfigured { get; }

    public static CompatibilityControlPolicyConstructionProfile FailClosed(
        ulong domainIdentity) =>
        new(
            domainIdentity,
            CompatibilityEventRoutingPolicy.RuntimeTrapPolicyRequired |
            CompatibilityEventRoutingPolicy.NeutralTrapResultRequired |
            CompatibilityEventRoutingPolicy.PublicationFenceRequired);
}

public readonly record struct CompatibilityControlPolicyIdentity(
    ulong OwnerIdentity,
    ulong OwnerEpoch,
    ulong DomainIdentity,
    ulong PolicyGeneration)
{
    public bool IsMaterialized =>
        OwnerIdentity != 0 && OwnerEpoch != 0 &&
        DomainIdentity != 0 && PolicyGeneration != 0;
}

public sealed class CompatibilityControlPolicySnapshot
{
    private readonly object _issuerSeal;

    internal CompatibilityControlPolicySnapshot(
        object issuerSeal,
        CompatibilityControlPolicyIdentity identity,
        CompatibilityControlDescriptor descriptor)
    {
        _issuerSeal = issuerSeal;
        Identity = identity;
        Descriptor = descriptor;
    }

    public CompatibilityControlPolicyIdentity Identity { get; }
    public CompatibilityControlDescriptor Descriptor { get; }
    public CompatibilityEventRoutingPolicy EventRoutingPolicy =>
        Descriptor.ReadOnlyView.EventRoutingPolicy;
    public bool IsMaterialized =>
        Identity.IsMaterialized && Descriptor.HasMaterializedReadOnlyProjection;
    public bool RuntimeAuthorityGranted => false;
    public bool IsCompatibilityValue => false;

    internal bool WasIssuedBy(object issuerSeal) =>
        ReferenceEquals(_issuerSeal, issuerSeal);
}

public sealed class CompatibilityControlPolicyOwner
{
    public const ulong CanonicalOwnerIdentity = 0x4343_504F_4C4F_574EUL;
    public const CompatibilityEventRoutingPolicy KnownEventRoutingPolicyMask =
        CompatibilityEventRoutingPolicy.RuntimeTrapPolicyRequired |
        CompatibilityEventRoutingPolicy.NeutralTrapResultRequired |
        CompatibilityEventRoutingPolicy.PublicationFenceRequired;

    private readonly object _sync = new();
    private readonly object _issuerSeal = new();
    private ulong _ownerEpoch = 1;
    private ulong _domainIdentity;
    private ulong _policyGeneration;
    private CompatibilityControlPolicySnapshot _current;

    public CompatibilityControlPolicyOwner(
        CompatibilityControlPolicyConstructionProfile profile)
    {
        if (!profile.IsConfigured)
            throw new ArgumentException("Compatibility-control policy construction requires an explicit profile.", nameof(profile));

        _domainIdentity = profile.DomainIdentity;
        _policyGeneration = 1;
        _current = IssueSnapshot(profile.EventRoutingPolicy);
    }

    public CompatibilityControlPolicySnapshot CaptureCurrent()
    {
        lock (_sync)
            return _current;
    }

    public ulong CurrentDomainIdentity
    {
        get
        {
            lock (_sync)
                return _domainIdentity;
        }
    }

    public bool IsCurrent(CompatibilityControlPolicySnapshot? snapshot)
    {
        lock (_sync)
            return IsCurrentUnderLock(snapshot);
    }

    public CompatibilityControlPolicySnapshot ReplacePolicy(
        CompatibilityEventRoutingPolicy eventRoutingPolicy)
    {
        ValidatePolicy(eventRoutingPolicy);
        lock (_sync)
        {
            AdvanceNonZero(ref _ownerEpoch);
            AdvanceNonZero(ref _policyGeneration);
            _current = IssueSnapshot(eventRoutingPolicy);
            return _current;
        }
    }

    public CompatibilityControlPolicySnapshot RebindDomain(ulong domainIdentity)
    {
        if (domainIdentity == 0)
            throw new ArgumentOutOfRangeException(nameof(domainIdentity));
        lock (_sync)
        {
            CompatibilityEventRoutingPolicy policy = _current.EventRoutingPolicy;
            _domainIdentity = domainIdentity;
            AdvanceNonZero(ref _ownerEpoch);
            AdvanceNonZero(ref _policyGeneration);
            _current = IssueSnapshot(policy);
            return _current;
        }
    }

    public CompatibilityControlPolicySnapshot InvalidateAfterRestore()
    {
        lock (_sync)
        {
            CompatibilityEventRoutingPolicy policy = _current.EventRoutingPolicy;
            AdvanceNonZero(ref _policyGeneration);
            _current = IssueSnapshot(policy);
            return _current;
        }
    }

    public CompatibilityControlPolicySnapshot ReplaceAfterArchitecturalStateReplacement()
    {
        lock (_sync)
        {
            CompatibilityEventRoutingPolicy policy = _current.EventRoutingPolicy;
            AdvanceNonZero(ref _ownerEpoch);
            AdvanceNonZero(ref _policyGeneration);
            _current = IssueSnapshot(policy);
            return _current;
        }
    }

    private CompatibilityControlPolicySnapshot IssueSnapshot(
        CompatibilityEventRoutingPolicy eventRoutingPolicy)
    {
        CompatibilityControlReadOnlyView baseline =
            CompatibilityControlReadOnlyView.FailClosedProjectionOnly;
        CompatibilityControlDescriptor descriptor =
            CompatibilityControlDescriptor.FromNeutralSemantics(
                baseline with { EventRoutingPolicy = eventRoutingPolicy });
        return new CompatibilityControlPolicySnapshot(
            _issuerSeal,
            new CompatibilityControlPolicyIdentity(
                CanonicalOwnerIdentity,
                _ownerEpoch,
                _domainIdentity,
                _policyGeneration),
            descriptor);
    }

    private bool IsCurrentUnderLock(CompatibilityControlPolicySnapshot? snapshot) =>
        snapshot is not null && snapshot.IsMaterialized &&
        snapshot.WasIssuedBy(_issuerSeal) && ReferenceEquals(snapshot, _current) &&
        snapshot.Identity.OwnerIdentity == CanonicalOwnerIdentity &&
        snapshot.Identity.OwnerEpoch == _ownerEpoch &&
        snapshot.Identity.DomainIdentity == _domainIdentity &&
        snapshot.Identity.PolicyGeneration == _policyGeneration;

    private static void ValidatePolicy(CompatibilityEventRoutingPolicy policy)
    {
        if (policy == CompatibilityEventRoutingPolicy.None ||
            (policy & ~KnownEventRoutingPolicyMask) != 0)
            throw new ArgumentOutOfRangeException(nameof(policy));
    }

    private static void AdvanceNonZero(ref ulong value)
    {
        checked { value++; }
        if (value == 0)
            throw new InvalidOperationException("Compatibility-control policy freshness exhausted.");
    }
}

internal enum NeutralInterruptRoutingPolicyDecision : byte
{
    Allowed = 0,
    DeniedStaleOrForeignSnapshot = 1,
    DeniedDomainMismatch = 2,
    DeniedRuntimeTrapPolicy = 3,
    DeniedNeutralTrapResult = 4,
    DeniedPublicationFence = 5,
}

internal readonly record struct NeutralInterruptRoutingPolicyResult(
    NeutralInterruptRoutingPolicyDecision Decision,
    string Reason)
{
    internal bool IsAllowed => Decision == NeutralInterruptRoutingPolicyDecision.Allowed;
}

internal sealed class NeutralInterruptRoutingPolicyConsumer
{
    internal NeutralInterruptRoutingPolicyResult Validate(
        CompatibilityControlPolicyOwner owner,
        CompatibilityControlPolicySnapshot? snapshot,
        ulong expectedDomainIdentity)
    {
        if (!owner.IsCurrent(snapshot))
            return Denied(NeutralInterruptRoutingPolicyDecision.DeniedStaleOrForeignSnapshot,
                "Neutral interrupt routing requires the current owner-issued compatibility-control policy snapshot.");
        if (snapshot!.Identity.DomainIdentity != expectedDomainIdentity || expectedDomainIdentity == 0)
            return Denied(NeutralInterruptRoutingPolicyDecision.DeniedDomainMismatch,
                "Neutral interrupt routing policy must match the non-zero runtime domain identity.");

        CompatibilityEventRoutingPolicy policy = snapshot.EventRoutingPolicy;
        if ((policy & CompatibilityEventRoutingPolicy.RuntimeTrapPolicyRequired) == 0)
            return Denied(NeutralInterruptRoutingPolicyDecision.DeniedRuntimeTrapPolicy,
                "Neutral interrupt routing requires the runtime trap policy predicate.");
        if ((policy & CompatibilityEventRoutingPolicy.NeutralTrapResultRequired) == 0)
            return Denied(NeutralInterruptRoutingPolicyDecision.DeniedNeutralTrapResult,
                "Neutral interrupt routing requires neutral trap-result semantics.");
        if ((policy & CompatibilityEventRoutingPolicy.PublicationFenceRequired) == 0)
            return Denied(NeutralInterruptRoutingPolicyDecision.DeniedPublicationFence,
                "Neutral interrupt routing requires the publication fence predicate.");
        return new(NeutralInterruptRoutingPolicyDecision.Allowed,
            "Current owner-issued neutral event-routing policy admitted interrupt routing.");
    }

    private static NeutralInterruptRoutingPolicyResult Denied(
        NeutralInterruptRoutingPolicyDecision decision,
        string reason) => new(decision, reason);
}
