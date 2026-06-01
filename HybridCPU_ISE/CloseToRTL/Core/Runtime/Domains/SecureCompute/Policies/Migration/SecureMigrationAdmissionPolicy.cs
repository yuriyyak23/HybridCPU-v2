namespace YAKSys_Hybrid_CPU.Core;

public enum SecureMigrationAdmissionDecision : byte
{
    Allowed = 0,
    DeniedMissingMigrationPolicy = 1,
    DeniedMigrationDisabled = 2,
    DeniedPolicyEpochRollback = 3,
    DeniedStaleMeasurementEpoch = 4,
    DeniedMeasurementRevalidationRequired = 5,
    DeniedMeasurementReattestationRequired = 6,
    DeniedStaleGrantEpoch = 7,
    DeniedHostOwnedEvidence = 8,
    DeniedVmcsProjectionAuthority = 9,
    DeniedCompatibilityMetadataAuthority = 10,
    DeniedPrivateMemoryWithoutSealedEncryptedContract = 11,
    DeniedDebugTraceAsGuestState = 12,
    DeniedRawSecret = 13,
    DeniedActiveHostPointer = 14,
    DeniedPayloadClass = 15,
    DeniedRestorePolicy = 16,
    DeniedGrantProvenanceMissing = 17,
    DeniedGrantRestoreProvenanceRevalidation = 18,
}

public readonly record struct SecureMigrationAdmissionResult(
    SecureMigrationAdmissionDecision Decision,
    string Reason)
{
    public bool IsAllowed => Decision == SecureMigrationAdmissionDecision.Allowed;

    public static SecureMigrationAdmissionResult Allowed { get; } =
        new(SecureMigrationAdmissionDecision.Allowed, string.Empty);

    public static SecureMigrationAdmissionResult Denied(
        SecureMigrationAdmissionDecision decision,
        string reason) =>
        new(decision, reason);
}

public readonly record struct SecurePrivateMemorySealedPayloadContract(
    bool HasSealedPayload,
    bool HasEncryptedPayload,
    bool HasNeutralKeyOwner,
    bool HasEvidencePolicy,
    bool HasRestoreValidationProof,
    bool ContainsRawSealingKey = false)
{
    public bool IsComplete =>
        HasSealedPayload &&
        HasEncryptedPayload &&
        HasNeutralKeyOwner &&
        HasEvidencePolicy &&
        HasRestoreValidationProof &&
        !ContainsRawSealingKey;
}

public sealed partial class SecureMigrationAdmissionPolicy
{
    public static SecureMigrationAdmissionPolicy Default { get; } = new();

    public SecureMigrationAdmissionResult AdmitCheckpointPayload(
        SecureMigrationDescriptor? migration,
        SecureCheckpointPayloadClass payloadClass,
        SecurePrivateMemorySealedPayloadContract? privateMemoryContract = null)
    {
        SecureMigrationAdmissionResult migrationState = ValidateMigrationEnabled(migration);
        if (!migrationState.IsAllowed)
        {
            return migrationState;
        }

        if (!migration!.PolicyEpoch.IsMaterialized)
        {
            return Deny(
                SecureMigrationAdmissionDecision.DeniedPolicyEpochRollback,
                "Secure migration policy requires a materialized policy epoch.");
        }

        return payloadClass switch
        {
            SecureCheckpointPayloadClass.HostOwnedEvidence
                or SecureCheckpointPayloadClass.SchedulerEvidence
                or SecureCheckpointPayloadClass.BackendBindingEvidence
                or SecureCheckpointPayloadClass.NativeTokenEvidence =>
                    Deny(
                        SecureMigrationAdmissionDecision.DeniedHostOwnedEvidence,
                        "Host-owned evidence classes are recomputed after restore and cannot be checkpoint authority."),

            SecureCheckpointPayloadClass.VmcsProjectionMetadata =>
                Deny(
                    SecureMigrationAdmissionDecision.DeniedVmcsProjectionAuthority,
                    "VMCS projection metadata cannot be secure migration authority."),

            SecureCheckpointPayloadClass.CompatibilityProjectionMetadata =>
                Deny(
                    SecureMigrationAdmissionDecision.DeniedCompatibilityMetadataAuthority,
                    "Compatibility projection metadata cannot be secure migration authority."),

            SecureCheckpointPayloadClass.DebugTrace =>
                Deny(
                    SecureMigrationAdmissionDecision.DeniedDebugTraceAsGuestState,
                    "Debug traces cannot be restored as guest-visible secure state."),

            SecureCheckpointPayloadClass.RawMeasurementSecret
                or SecureCheckpointPayloadClass.RawSealingKey =>
                    Deny(
                        SecureMigrationAdmissionDecision.DeniedRawSecret,
                        "Raw measurement secrets and raw sealing keys are not checkpoint payloads."),

            SecureCheckpointPayloadClass.ActiveHostPointer =>
                Deny(
                    SecureMigrationAdmissionDecision.DeniedActiveHostPointer,
                    "Active host pointers are not portable secure migration payloads."),

            SecureCheckpointPayloadClass.SecurePrivateMemory =>
                AdmitPrivateMemoryPayload(migration, privateMemoryContract),

            _ => SecureMigrationAdmissionResult.Allowed,
        };
    }

    public SecureMigrationAdmissionResult AdmitRestore(
        SecureMigrationDescriptor? migration,
        DomainMeasurementDescriptor? measurement,
        SecureMemoryDomainDescriptor? memory,
        SecureRevocationEpoch expectedPolicyEpoch,
        SecureGrantHandle restoredGrant,
        bool measurementRevalidated,
        bool reattestationCompleted,
        bool grantProvenanceValidated = false)
    {
        SecureMigrationAdmissionResult migrationState = ValidateMigrationEnabled(migration);
        if (!migrationState.IsAllowed)
        {
            return migrationState;
        }

        if (!expectedPolicyEpoch.IsMaterialized ||
            !expectedPolicyEpoch.IsCurrent(migration!.PolicyEpoch.Current))
        {
            return Deny(
                SecureMigrationAdmissionDecision.DeniedPolicyEpochRollback,
                "Secure restore policy epoch is stale or rolled back.");
        }

        if (migration.MeasurementRestorePolicy == SecureMeasurementRestorePolicy.Denied)
        {
            return Deny(
                SecureMigrationAdmissionDecision.DeniedRestorePolicy,
                "Secure restore requires an explicit measurement restore policy.");
        }

        if (restoredGrant.Kind != SecureGrantHandleKind.None &&
            migration.GrantRestorePolicy == SecureGrantRestorePolicy.Denied)
        {
            return Deny(
                SecureMigrationAdmissionDecision.DeniedRestorePolicy,
                "Secure restore requires an explicit grant restore policy.");
        }

        if (restoredGrant.Kind != SecureGrantHandleKind.None &&
            (!restoredGrant.HasScalarShape || !restoredGrant.HasProvenance))
        {
            return Deny(
                SecureMigrationAdmissionDecision.DeniedGrantProvenanceMissing,
                "Secure restore rejected a grant handle without runtime provenance.");
        }

        if (restoredGrant.Kind != SecureGrantHandleKind.None &&
            !restoredGrant.MatchesEpoch(migration.PolicyEpoch))
        {
            return Deny(
                SecureMigrationAdmissionDecision.DeniedStaleGrantEpoch,
                "Secure restore rejected a stale grant epoch.");
        }

        if (restoredGrant.Kind != SecureGrantHandleKind.None &&
            !grantProvenanceValidated)
        {
            return Deny(
                SecureMigrationAdmissionDecision.DeniedGrantRestoreProvenanceRevalidation,
                "Secure restore must rederive or revalidate restored grant provenance.");
        }

        if (migration.RequiresReattestation && !reattestationCompleted)
        {
            return Deny(
                SecureMigrationAdmissionDecision.DeniedMeasurementReattestationRequired,
                "Secure restore requires re-attestation before secure-domain entry.");
        }

        if (migration.RequiresMeasurementRevalidation && !measurementRevalidated)
        {
            return Deny(
                SecureMigrationAdmissionDecision.DeniedMeasurementRevalidationRequired,
                "Secure restore requires measurement revalidation before secure-domain entry.");
        }

        if (migration.RequiresMeasurementRevalidation || migration.RequiresReattestation)
        {
            if (measurement is null ||
                !measurement.IsMaterialized ||
                !measurement.IsCurrentFor(migration.PolicyEpoch))
            {
                return Deny(
                    SecureMigrationAdmissionDecision.DeniedStaleMeasurementEpoch,
                    "Secure restore requires a materialized measurement at the current migration epoch.");
            }
        }

        if (memory is { IsMaterialized: true, HasPrivateMemory: true } &&
            migration.PrivateMemoryPolicy == SecurePrivateMemoryMigrationPolicy.Denied)
        {
            return Deny(
                SecureMigrationAdmissionDecision.DeniedPrivateMemoryWithoutSealedEncryptedContract,
                "Secure restore cannot import private memory without an explicit private-memory migration policy.");
        }

        return SecureMigrationAdmissionResult.Allowed;
    }

    private static SecureMigrationAdmissionResult AdmitPrivateMemoryPayload(
        SecureMigrationDescriptor migration,
        SecurePrivateMemorySealedPayloadContract? privateMemoryContract)
    {
        if (!migration.AllowsPrivateMemoryPayload ||
            privateMemoryContract?.IsComplete != true)
        {
            return Deny(
                SecureMigrationAdmissionDecision.DeniedPrivateMemoryWithoutSealedEncryptedContract,
                "Secure private memory migration requires explicit sealed and encrypted payload contract proof.");
        }

        return SecureMigrationAdmissionResult.Allowed;
    }

    private static SecureMigrationAdmissionResult ValidateMigrationEnabled(
        SecureMigrationDescriptor? migration)
    {
        if (migration is null)
        {
            return Deny(
                SecureMigrationAdmissionDecision.DeniedMissingMigrationPolicy,
                "Secure migration requires a migration policy descriptor.");
        }

        if (!migration.AllowsMigration)
        {
            return Deny(
                SecureMigrationAdmissionDecision.DeniedMigrationDisabled,
                "Secure migration is disabled by policy.");
        }

        return SecureMigrationAdmissionResult.Allowed;
    }

    private static SecureMigrationAdmissionResult Deny(
        SecureMigrationAdmissionDecision decision,
        string reason) =>
        SecureMigrationAdmissionResult.Denied(decision, reason);
}
