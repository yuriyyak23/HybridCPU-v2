namespace YAKSys_Hybrid_CPU.Core;

public enum MigrationValidationDecision : byte
{
    Allowed = 0,
    PayloadClassDenied = 1,
    EvidencePolicyDenied = 2,
    HostOwnedEvidenceRejected = 3,
    RestorePolicyRejected = 4,
}

public readonly record struct MigrationValidationResult(
    MigrationValidationDecision Decision,
    string Message)
{
    public bool IsAllowed => Decision == MigrationValidationDecision.Allowed;

    public static MigrationValidationResult Allowed { get; } =
        new(MigrationValidationDecision.Allowed, string.Empty);

    public static MigrationValidationResult Denied(
        MigrationValidationDecision decision,
        string message) =>
        new(decision, message);
}

public sealed partial class MigrationValidationPolicy
{
    public MigrationValidationPolicy()
        : this(
            MigrationDescriptor.FailClosed,
            EvidencePolicyDescriptor.FailClosed,
            rejectCompatibilityProjectionMetadata: true,
            requireGuestStatePreservePolicy: true)
    {
    }

    public MigrationValidationPolicy(
        MigrationDescriptor migration,
        EvidencePolicyDescriptor evidence,
        bool rejectCompatibilityProjectionMetadata,
        bool requireGuestStatePreservePolicy)
    {
        Migration = migration;
        Evidence = evidence;
        RejectCompatibilityProjectionMetadata = rejectCompatibilityProjectionMetadata;
        RequireGuestStatePreservePolicy = requireGuestStatePreservePolicy;
    }

    public static MigrationValidationPolicy FailClosed { get; } = new();

    public MigrationDescriptor Migration { get; }

    public EvidencePolicyDescriptor Evidence { get; }

    public bool RejectCompatibilityProjectionMetadata { get; }

    public bool RequireGuestStatePreservePolicy { get; }

    public MigrationValidationResult ValidateImport(
        MigrationPayloadClass payloadClass,
        EvidenceVisibilityClass evidenceClass,
        EvidenceRestorePolicy restorePolicy)
    {
        if (Migration.MustRejectImportedPayload(payloadClass))
        {
            return MigrationValidationResult.Denied(
                MigrationValidationDecision.PayloadClassDenied,
                "Migration descriptor rejects this payload class.");
        }

        if (RejectCompatibilityProjectionMetadata &&
            payloadClass == MigrationPayloadClass.CompatibilityProjectionMetadata)
        {
            return MigrationValidationResult.Denied(
                MigrationValidationDecision.PayloadClassDenied,
                "Compatibility projection metadata is not authoritative restore state.");
        }

        if (Migration.MustRecomputeAfterRestore(payloadClass) ||
            Evidence.MustRecomputeAfterRestore(evidenceClass))
        {
            return MigrationValidationResult.Denied(
                MigrationValidationDecision.HostOwnedEvidenceRejected,
                "Host-owned runtime evidence must be recomputed after restore.");
        }

        if (RequireGuestStatePreservePolicy &&
            evidenceClass == EvidenceVisibilityClass.GuestArchitecturalState &&
            restorePolicy != EvidenceRestorePolicy.PreserveGuestArchitecturalState)
        {
            return MigrationValidationResult.Denied(
                MigrationValidationDecision.RestorePolicyRejected,
                "Guest architectural state import requires preserve restore policy.");
        }

        if (!Evidence.CanSerializeAcrossMigration(evidenceClass, restorePolicy))
        {
            return MigrationValidationResult.Denied(
                MigrationValidationDecision.EvidencePolicyDenied,
                "Evidence policy denies migration serialization for this evidence class.");
        }

        return MigrationValidationResult.Allowed;
    }
}
