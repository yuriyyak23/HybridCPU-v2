namespace YAKSys_Hybrid_CPU.Core;

public enum RestoreValidationDecision : byte
{
    Allowed = 0,
    EmptyCheckpoint = 1,
    CompatibilityProjectionDenied = 2,
    CheckpointEpochMismatch = 3,
    MigrationPolicyDenied = 4,
}

public readonly record struct RestoreValidationResult(
    RestoreValidationDecision Decision,
    MigrationValidationResult MigrationResult,
    string Message)
{
    public bool IsAllowed => Decision == RestoreValidationDecision.Allowed;

    public static RestoreValidationResult Allowed { get; } =
        new(
            RestoreValidationDecision.Allowed,
            MigrationValidationResult.Allowed,
            string.Empty);

    public static RestoreValidationResult Denied(
        RestoreValidationDecision decision,
        string message,
        MigrationValidationResult migrationResult = default) =>
        new(decision, migrationResult, message);
}

public sealed partial class RestoreValidationService
{
    public RestoreValidationResult ValidateRestore(
        DomainCheckpointImage checkpoint,
        MigrationValidationPolicy policy,
        EvidenceRestorePolicy restorePolicy,
        ulong expectedCheckpointEpoch = 0)
    {
        if (checkpoint.IsEmpty)
        {
            return RestoreValidationResult.Denied(
                RestoreValidationDecision.EmptyCheckpoint,
                "Restore requires a non-empty domain checkpoint image.");
        }

        if (!checkpoint.IsDomainAuthoritative ||
            checkpoint.ContainsCompatibilityProjectionMetadata)
        {
            return RestoreValidationResult.Denied(
                RestoreValidationDecision.CompatibilityProjectionDenied,
                "Restore cannot accept compatibility projection metadata as authoritative state.");
        }

        if (expectedCheckpointEpoch != 0 &&
            checkpoint.CheckpointEpoch != expectedCheckpointEpoch)
        {
            return RestoreValidationResult.Denied(
                RestoreValidationDecision.CheckpointEpochMismatch,
                "Restore checkpoint epoch does not match the expected domain epoch.");
        }

        MigrationValidationResult migrationResult =
            checkpoint.ValidateRestore(policy, restorePolicy);
        if (!migrationResult.IsAllowed)
        {
            return RestoreValidationResult.Denied(
                RestoreValidationDecision.MigrationPolicyDenied,
                migrationResult.Message,
                migrationResult);
        }

        return RestoreValidationResult.Allowed;
    }
}
