namespace YAKSys_Hybrid_CPU.Core;

public enum CapabilityGrantAuthorityViolation : byte
{
    None = 0,
    MissingGrant = 1,
    EmptyGrantedMask = 2,
    MissingTypedAuthority = 3,
    MissingOwnerDomain = 4,
    DelegationPolicyMissing = 5,
    RevocationPolicyMissing = 6,
    MigrationClassMissing = 7,
    EvidenceVisibilityMissing = 8,
    FrontendProjectionPolicyMissing = 9,
    CompatibilityProjectionPolicyDenied = 10,
}

public sealed partial class CapabilityGrantAuthorityContract
{
    public CapabilityGrantAuthorityViolation Evaluate(CapabilityGrant? grant)
    {
        if (grant is null)
        {
            return CapabilityGrantAuthorityViolation.MissingGrant;
        }

        if (!grant.IsGranted)
        {
            return CapabilityGrantAuthorityViolation.None;
        }

        if (grant.CapabilityMask == 0)
        {
            return CapabilityGrantAuthorityViolation.EmptyGrantedMask;
        }

        if (!grant.HasTypedAuthority)
        {
            return CapabilityGrantAuthorityViolation.MissingTypedAuthority;
        }

        if (grant.OwnerDomainId == 0 && grant.Scope == CapabilityGrantScope.DomainGranted)
        {
            return CapabilityGrantAuthorityViolation.MissingOwnerDomain;
        }

        if (grant.DelegationPolicy == CapabilityDelegationPolicy.None)
        {
            return CapabilityGrantAuthorityViolation.DelegationPolicyMissing;
        }

        if (grant.RevocationPolicy == CapabilityRevocationPolicy.None)
        {
            return CapabilityGrantAuthorityViolation.RevocationPolicyMissing;
        }

        if (grant.MigrationClass == CapabilityMigrationClass.None)
        {
            return CapabilityGrantAuthorityViolation.MigrationClassMissing;
        }

        if (grant.EvidenceVisibility == CapabilityEvidenceVisibility.None)
        {
            return CapabilityGrantAuthorityViolation.EvidenceVisibilityMissing;
        }

        if (grant.FrontendProjectionPolicy == CapabilityFrontendProjectionPolicy.None)
        {
            return CapabilityGrantAuthorityViolation.FrontendProjectionPolicyMissing;
        }

        if (grant.Scope == CapabilityGrantScope.CompatibilityProjection &&
            !grant.IsPublishableCompatibilityGrant)
        {
            return CapabilityGrantAuthorityViolation.CompatibilityProjectionPolicyDenied;
        }

        return CapabilityGrantAuthorityViolation.None;
    }

    public bool IsSatisfied(CapabilityGrant? grant) =>
        Evaluate(grant) == CapabilityGrantAuthorityViolation.None;
}
