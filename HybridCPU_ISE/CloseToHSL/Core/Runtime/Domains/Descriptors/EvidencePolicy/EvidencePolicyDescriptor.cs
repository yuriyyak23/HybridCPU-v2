namespace YAKSys_Hybrid_CPU.Core;

public enum EvidenceVisibilityClass : byte
{
    GuestArchitecturalState = 0,
    CompatibilityAlias = 1,
    HostOwnedRuntimeEvidence = 2,
    SchedulerEvidence = 3,
    BackendBindingEvidence = 4,
    NativeTokenEvidence = 5,
}

public enum EvidenceRestorePolicy : byte
{
    RecomputeAfterRestore = 0,
    ZeroizeAfterRestore = 1,
    PreserveGuestArchitecturalState = 2,
}

public sealed partial class EvidencePolicyDescriptor
{
    public EvidencePolicyDescriptor()
        : this(
            allowCompatibilityAliases: false,
            allowGuestArchitecturalState: false,
            allowMigrationSerializableState: false)
    {
    }

    public EvidencePolicyDescriptor(
        bool allowCompatibilityAliases,
        bool allowGuestArchitecturalState,
        bool allowMigrationSerializableState)
    {
        AllowCompatibilityAliases = allowCompatibilityAliases;
        AllowGuestArchitecturalState = allowGuestArchitecturalState;
        AllowMigrationSerializableState = allowMigrationSerializableState;
    }

    public static EvidencePolicyDescriptor FailClosed { get; } = new();

    public bool AllowCompatibilityAliases { get; }

    public bool AllowGuestArchitecturalState { get; }

    public bool AllowMigrationSerializableState { get; }

    public bool CanExposeToGuest(EvidenceVisibilityClass evidenceClass) =>
        evidenceClass switch
        {
            EvidenceVisibilityClass.GuestArchitecturalState => AllowGuestArchitecturalState,
            EvidenceVisibilityClass.CompatibilityAlias => AllowCompatibilityAliases,
            _ => false,
        };

    public bool CanSerializeAcrossMigration(
        EvidenceVisibilityClass evidenceClass,
        EvidenceRestorePolicy restorePolicy) =>
        AllowMigrationSerializableState &&
        evidenceClass == EvidenceVisibilityClass.GuestArchitecturalState &&
        restorePolicy == EvidenceRestorePolicy.PreserveGuestArchitecturalState;

    public bool MustRecomputeAfterRestore(EvidenceVisibilityClass evidenceClass) =>
        evidenceClass is EvidenceVisibilityClass.HostOwnedRuntimeEvidence
            or EvidenceVisibilityClass.SchedulerEvidence
            or EvidenceVisibilityClass.BackendBindingEvidence
            or EvidenceVisibilityClass.NativeTokenEvidence;
}
