namespace YAKSys_Hybrid_CPU.Core;

public enum SecureComputeVmReadVisibilityDecision : byte
{
    AllowedReadOnlyProjection = 0,
    DeniedNoNeutralOwner = 1,
    DeniedNoReadOnlySource = 2,
    DeniedNoSecureVisibility = 3,
    DeniedNoMigrationClass = 4,
    DeniedNoConformanceProof = 5,
}

public sealed partial class SecureComputeVmReadVisibilityPolicy
{
    public SecureComputeVmReadVisibilityDecision Validate(
        bool hasNeutralOwner,
        bool hasReadOnlySource,
        bool hasSecureVisibility,
        bool hasMigrationClassification,
        bool hasConformanceProof)
    {
        if (!hasNeutralOwner)
        {
            return SecureComputeVmReadVisibilityDecision.DeniedNoNeutralOwner;
        }

        if (!hasReadOnlySource)
        {
            return SecureComputeVmReadVisibilityDecision.DeniedNoReadOnlySource;
        }

        if (!hasSecureVisibility)
        {
            return SecureComputeVmReadVisibilityDecision.DeniedNoSecureVisibility;
        }

        if (!hasMigrationClassification)
        {
            return SecureComputeVmReadVisibilityDecision.DeniedNoMigrationClass;
        }

        if (!hasConformanceProof)
        {
            return SecureComputeVmReadVisibilityDecision.DeniedNoConformanceProof;
        }

        return SecureComputeVmReadVisibilityDecision.AllowedReadOnlyProjection;
    }
}
