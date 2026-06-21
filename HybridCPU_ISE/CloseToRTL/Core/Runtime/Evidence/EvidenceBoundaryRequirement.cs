// Description: Neutral evidence-boundary requirement for guest visibility and migration-safe serialization policy.
namespace YAKSys_Hybrid_CPU.Core;

public readonly record struct EvidenceBoundaryRequirement(
    EvidenceVisibilityClass EvidenceClass,
    EvidenceRestorePolicy RestorePolicy,
    bool RequiresGuestVisibility,
    bool RequiresMigrationSerialization)
{
    public static EvidenceBoundaryRequirement None { get; } =
        new(
            EvidenceVisibilityClass.GuestArchitecturalState,
            EvidenceRestorePolicy.RecomputeAfterRestore,
            RequiresGuestVisibility: false,
            RequiresMigrationSerialization: false);

    public static EvidenceBoundaryRequirement GuestVisible(
        EvidenceVisibilityClass evidenceClass) =>
        new(
            evidenceClass,
            EvidenceRestorePolicy.RecomputeAfterRestore,
            RequiresGuestVisibility: true,
            RequiresMigrationSerialization: false);

    public static EvidenceBoundaryRequirement MigrationSerializableGuestState { get; } =
        new(
            EvidenceVisibilityClass.GuestArchitecturalState,
            EvidenceRestorePolicy.PreserveGuestArchitecturalState,
            RequiresGuestVisibility: true,
            RequiresMigrationSerialization: true);

    public bool IsSatisfiedBy(EvidencePolicyDescriptor? evidencePolicy)
    {
        if (!RequiresGuestVisibility && !RequiresMigrationSerialization)
        {
            return true;
        }

        if (evidencePolicy is null)
        {
            return false;
        }

        if (RequiresGuestVisibility &&
            !evidencePolicy.CanExposeToGuest(EvidenceClass))
        {
            return false;
        }

        if (RequiresMigrationSerialization &&
            !evidencePolicy.CanSerializeAcrossMigration(EvidenceClass, RestorePolicy))
        {
            return false;
        }

        return !evidencePolicy.MustRecomputeAfterRestore(EvidenceClass);
    }
}
