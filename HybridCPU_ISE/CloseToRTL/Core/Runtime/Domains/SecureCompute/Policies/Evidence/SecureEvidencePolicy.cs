namespace YAKSys_Hybrid_CPU.Core;

public enum SecureEvidenceVisibilityClass : byte
{
    Denied = 0,
    GuestVisible = 1,
    MigrationSerializable = 2,
    CompatibilityAlias = 3,
    RecomputedAfterRestore = 4,
    DebugOnly = 5,
    HostOwnedQuarantined = 6,
}

public sealed partial class SecureEvidencePolicy
{
    public SecureEvidencePolicy()
        : this(
            allowGuestVisibleEvidence: false,
            allowMigrationSerializableEvidence: false,
            allowCompatibilityAliasEvidence: false,
            allowDebugEvidence: false)
    {
    }

    public SecureEvidencePolicy(
        bool allowGuestVisibleEvidence,
        bool allowMigrationSerializableEvidence,
        bool allowCompatibilityAliasEvidence,
        bool allowDebugEvidence)
    {
        AllowGuestVisibleEvidence = allowGuestVisibleEvidence;
        AllowMigrationSerializableEvidence = allowMigrationSerializableEvidence;
        AllowCompatibilityAliasEvidence = allowCompatibilityAliasEvidence;
        AllowDebugEvidence = allowDebugEvidence;
    }

    public static SecureEvidencePolicy FailClosed { get; } = new();

    public bool AllowGuestVisibleEvidence { get; }

    public bool AllowMigrationSerializableEvidence { get; }

    public bool AllowCompatibilityAliasEvidence { get; }

    public bool AllowDebugEvidence { get; }

    public bool CanExposeToGuest(
        EvidencePolicyDescriptor basePolicy,
        SecureEvidenceVisibilityClass evidenceClass) =>
        evidenceClass switch
        {
            SecureEvidenceVisibilityClass.GuestVisible =>
                AllowGuestVisibleEvidence &&
                basePolicy.CanExposeToGuest(EvidenceVisibilityClass.GuestArchitecturalState),

            SecureEvidenceVisibilityClass.CompatibilityAlias =>
                AllowCompatibilityAliasEvidence &&
                basePolicy.CanExposeToGuest(EvidenceVisibilityClass.CompatibilityAlias),

            SecureEvidenceVisibilityClass.DebugOnly =>
                AllowDebugEvidence &&
                basePolicy.CanExposeToGuest(EvidenceVisibilityClass.GuestArchitecturalState),

            _ => false,
        };

    public bool CanSerializeAcrossMigration(
        EvidencePolicyDescriptor basePolicy,
        SecureEvidenceVisibilityClass evidenceClass) =>
        evidenceClass == SecureEvidenceVisibilityClass.MigrationSerializable &&
        AllowMigrationSerializableEvidence &&
        basePolicy.CanSerializeAcrossMigration(
            EvidenceVisibilityClass.GuestArchitecturalState,
            EvidenceRestorePolicy.PreserveGuestArchitecturalState);

    public bool MustQuarantine(SecureEvidenceVisibilityClass evidenceClass) =>
        evidenceClass is SecureEvidenceVisibilityClass.HostOwnedQuarantined
            or SecureEvidenceVisibilityClass.Denied;
}
