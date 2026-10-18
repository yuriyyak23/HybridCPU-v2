using System;

namespace YAKSys_Hybrid_CPU.Core;

public enum CapabilityGrantScope : byte
{
    None = 0,
    HardwareAvailable = 1,
    RuntimeEnabled = 2,
    DomainGranted = 3,
    CompatibilityProjection = 4,
}

public enum CapabilityDelegationPolicy : byte
{
    None = 0,
    NonDelegable = 1,
    DelegableWithinOwnerDomain = 2,
}

public enum CapabilityRevocationPolicy : byte
{
    None = 0,
    RuntimeRevocable = 1,
    RevokedOnMigration = 2,
}

public enum CapabilityMigrationClass : byte
{
    None = 0,
    DomainLocal = 1,
    MigratableDescriptor = 2,
}

public enum CapabilityEvidenceVisibility : byte
{
    None = 0,
    HostOnly = 1,
    GuestVisibleProjection = 2,
}

public enum CapabilityFrontendProjectionPolicy : byte
{
    None = 0,
    NeverProject = 1,
    ProjectIfCompatible = 2,
}

public sealed partial class CapabilityGrant
{
    public const ulong NoOwnerDomainId = 0;
    public const ulong DefaultRuntimeOwnerDomainId = 1;

    public CapabilityGrant()
        : this(
            0,
            CapabilityGrantScope.None,
            isGranted: false,
            ownerDomainId: NoOwnerDomainId,
            CapabilityDelegationPolicy.None,
            CapabilityRevocationPolicy.None,
            CapabilityMigrationClass.None,
            CapabilityEvidenceVisibility.None,
            CapabilityFrontendProjectionPolicy.NeverProject)
    {
    }

    public CapabilityGrant(
        ulong capabilityMask,
        CapabilityGrantScope scope,
        bool isGranted)
        : this(
            capabilityMask,
            scope,
            isGranted,
            ownerDomainId: isGranted && scope == CapabilityGrantScope.DomainGranted
                ? DefaultRuntimeOwnerDomainId
                : NoOwnerDomainId,
            CapabilityDelegationPolicy.NonDelegable,
            CapabilityRevocationPolicy.RuntimeRevocable,
            CapabilityMigrationClass.DomainLocal,
            CapabilityEvidenceVisibility.HostOnly,
            CapabilityFrontendProjectionPolicy.ProjectIfCompatible)
    {
    }

    public CapabilityGrant(
        ulong capabilityMask,
        CapabilityGrantScope scope,
        bool isGranted,
        ulong ownerDomainId,
        CapabilityDelegationPolicy delegationPolicy,
        CapabilityRevocationPolicy revocationPolicy,
        CapabilityMigrationClass migrationClass,
        CapabilityEvidenceVisibility evidenceVisibility,
        CapabilityFrontendProjectionPolicy frontendProjectionPolicy)
    {
        CapabilityMask = capabilityMask;
        Scope = scope;
        IsGranted = isGranted;
        OwnerDomainId = ownerDomainId;
        DelegationPolicy = delegationPolicy;
        RevocationPolicy = revocationPolicy;
        MigrationClass = migrationClass;
        EvidenceVisibility = evidenceVisibility;
        FrontendProjectionPolicy = frontendProjectionPolicy;
    }

    public static CapabilityGrant Denied { get; } = new();

    public ulong CapabilityMask { get; }

    public CapabilityGrantScope Scope { get; }

    public bool IsGranted { get; }

    public ulong OwnerDomainId { get; }

    public CapabilityDelegationPolicy DelegationPolicy { get; }

    public CapabilityRevocationPolicy RevocationPolicy { get; }

    public CapabilityMigrationClass MigrationClass { get; }

    public CapabilityEvidenceVisibility EvidenceVisibility { get; }

    public CapabilityFrontendProjectionPolicy FrontendProjectionPolicy { get; }

    public bool HasTypedAuthority =>
        IsGranted &&
        CapabilityMask != 0 &&
        Scope != CapabilityGrantScope.None &&
        DelegationPolicy != CapabilityDelegationPolicy.None &&
        RevocationPolicy != CapabilityRevocationPolicy.None &&
        MigrationClass != CapabilityMigrationClass.None &&
        EvidenceVisibility != CapabilityEvidenceVisibility.None &&
        FrontendProjectionPolicy != CapabilityFrontendProjectionPolicy.None;

    public bool Grants(ulong capabilityMask) =>
        IsGranted &&
        capabilityMask != 0 &&
        (CapabilityMask & capabilityMask) == capabilityMask;

    public bool IsPublishableCompatibilityGrant =>
        IsGranted &&
        Scope == CapabilityGrantScope.CompatibilityProjection &&
        FrontendProjectionPolicy == CapabilityFrontendProjectionPolicy.ProjectIfCompatible;
}

public sealed partial class CapabilityGrantCollection
{
    private readonly CapabilityGrant[] _grants;

    public CapabilityGrantCollection()
        : this(Array.Empty<CapabilityGrant>())
    {
    }

    public CapabilityGrantCollection(CapabilityGrant[] grants)
    {
        _grants = grants ?? Array.Empty<CapabilityGrant>();
    }

    public static CapabilityGrantCollection Empty { get; } = new();

    public ReadOnlySpan<CapabilityGrant> Grants => _grants;

    public int Count => _grants.Length;

    public ulong ProjectMask(CapabilityGrantScope scope)
    {
        ulong mask = 0;

        foreach (CapabilityGrant grant in _grants)
        {
            if (grant.IsGranted &&
                grant.Scope == scope)
            {
                mask |= grant.CapabilityMask;
            }
        }

        return mask;
    }

    public ulong EffectiveCompatibilityMask
    {
        get
        {
            return ProjectMask(CapabilityGrantScope.CompatibilityProjection);
        }
    }

    public bool HasAuthoritativeTypedGrants
    {
        get
        {
            foreach (CapabilityGrant grant in _grants)
            {
                if (!grant.HasTypedAuthority)
                {
                    return false;
                }
            }

            return _grants.Length > 0;
        }
    }

    public bool TryGetGrant(
        ulong capabilityMask,
        CapabilityGrantScope scope,
        out CapabilityGrant grant)
    {
        foreach (CapabilityGrant candidate in _grants)
        {
            if (candidate.Scope == scope &&
                candidate.Grants(capabilityMask))
            {
                grant = candidate;
                return true;
            }
        }

        grant = CapabilityGrant.Denied;
        return false;
    }

    private static CapabilityGrant CreateInternalGrant(
        ulong capabilityMask,
        CapabilityGrantScope scope,
        ulong ownerDomainId) =>
        new(
            capabilityMask,
            scope,
            isGranted: true,
            ownerDomainId,
            CapabilityDelegationPolicy.NonDelegable,
            CapabilityRevocationPolicy.RuntimeRevocable,
            CapabilityMigrationClass.DomainLocal,
            CapabilityEvidenceVisibility.HostOnly,
            CapabilityFrontendProjectionPolicy.NeverProject);
}
