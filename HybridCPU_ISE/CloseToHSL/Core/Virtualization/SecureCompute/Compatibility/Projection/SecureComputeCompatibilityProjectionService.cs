namespace YAKSys_Hybrid_CPU.Core;

public readonly record struct SecureComputeCompatibilityProjectionRequest(
    ulong FieldId,
    ulong AliasBit,
    bool HasNeutralOwner,
    bool HasReadOnlySource,
    bool SecureVisibilityAllowed,
    bool MigrationClassified,
    bool ConformanceProven);

public readonly record struct SecureComputeCompatibilityProjectionResult(
    SecureComputeVmReadVisibilityDecision Decision,
    bool ValueAvailable,
    ulong Value)
{
    public bool IsAllowed =>
        Decision == SecureComputeVmReadVisibilityDecision.AllowedReadOnlyProjection &&
        ValueAvailable;
}

public sealed partial class SecureComputeCompatibilityProjectionService
{
    private readonly SecureComputeVmReadVisibilityPolicy _visibilityPolicy;

    public SecureComputeCompatibilityProjectionService()
        : this(new SecureComputeVmReadVisibilityPolicy())
    {
    }

    public SecureComputeCompatibilityProjectionService(
        SecureComputeVmReadVisibilityPolicy visibilityPolicy)
    {
        _visibilityPolicy = visibilityPolicy;
    }

    public SecureComputeCompatibilityProjectionResult ProjectReadOnlyValue(
        SecureComputeCompatibilityProjectionRequest request,
        ulong value)
    {
        var decision = _visibilityPolicy.Validate(
            request.HasNeutralOwner,
            request.HasReadOnlySource,
            request.SecureVisibilityAllowed,
            request.MigrationClassified,
            request.ConformanceProven);

        return new(
            decision,
            ValueAvailable: decision == SecureComputeVmReadVisibilityDecision.AllowedReadOnlyProjection,
            Value: decision == SecureComputeVmReadVisibilityDecision.AllowedReadOnlyProjection ? value : 0);
    }
}
