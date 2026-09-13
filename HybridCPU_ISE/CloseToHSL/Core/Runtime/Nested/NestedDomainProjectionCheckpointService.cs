namespace YAKSys_Hybrid_CPU.Core;

public enum NestedDomainProjectionCheckpointDecision : byte
{
    Allowed = 0,
    ProjectionDenied = 1,
    RestoreDenied = 2,
}

public readonly record struct NestedDomainProjectionCheckpointRequest(
    NestedProjectionRequest ProjectionRequest,
    DomainCheckpointImage? Checkpoint,
    MigrationValidationPolicy? MigrationPolicy,
    EvidenceRestorePolicy RestorePolicy,
    ulong ExpectedCheckpointEpoch);

public readonly record struct NestedDomainProjectionCheckpointResult(
    NestedDomainProjectionCheckpointDecision Decision,
    NestedProjectionResult ProjectionResult,
    RestoreValidationResult RestoreResult,
    string Reason)
{
    public bool IsAllowed => Decision == NestedDomainProjectionCheckpointDecision.Allowed;

    public static NestedDomainProjectionCheckpointResult Allowed(
        NestedProjectionResult projection,
        RestoreValidationResult restore) =>
        new(NestedDomainProjectionCheckpointDecision.Allowed, projection, restore, string.Empty);

    public static NestedDomainProjectionCheckpointResult DeniedProjection(
        NestedProjectionResult projection) =>
        new(
            NestedDomainProjectionCheckpointDecision.ProjectionDenied,
            projection,
            default,
            projection.Reason);

    public static NestedDomainProjectionCheckpointResult DeniedRestore(
        NestedProjectionResult projection,
        RestoreValidationResult restore) =>
        new(
            NestedDomainProjectionCheckpointDecision.RestoreDenied,
            projection,
            restore,
            restore.Message);
}

public sealed class NestedDomainProjectionCheckpointService
{
    private readonly NestedProjectionService _projectionService = new();
    private readonly RestoreValidationService _restoreService = new();

    public NestedDomainProjectionCheckpointResult Validate(
        NestedDomainProjectionCheckpointRequest request)
    {
        NestedProjectionResult projectionResult =
            _projectionService.Validate(request.ProjectionRequest);
        if (!projectionResult.IsAllowed)
        {
            return NestedDomainProjectionCheckpointResult.DeniedProjection(projectionResult);
        }

        if (request.Checkpoint is null || request.MigrationPolicy is null)
        {
            return NestedDomainProjectionCheckpointResult.DeniedRestore(
                projectionResult,
                RestoreValidationResult.Denied(
                    RestoreValidationDecision.EmptyCheckpoint,
                    "Nested domain restore requires a checkpoint image and migration policy."));
        }

        RestoreValidationResult restoreResult = _restoreService.ValidateRestore(
            request.Checkpoint,
            request.MigrationPolicy,
            request.RestorePolicy,
            request.ExpectedCheckpointEpoch);
        if (!restoreResult.IsAllowed)
        {
            return NestedDomainProjectionCheckpointResult.DeniedRestore(
                projectionResult,
                restoreResult);
        }

        return NestedDomainProjectionCheckpointResult.Allowed(
            projectionResult,
            restoreResult);
    }
}
