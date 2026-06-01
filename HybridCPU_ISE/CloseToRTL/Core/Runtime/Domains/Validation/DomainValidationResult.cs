namespace YAKSys_Hybrid_CPU.Core;

public enum DomainValidationFailureReason : byte
{
    None = 0,
    MissingExecutionDomain = 1,
    MissingMemoryDomain = 2,
    MissingIoDomain = 3,
    MissingCapabilityGrant = 4,
    ProjectionCannotMutateState = 5,
    UnsupportedCompatibilityAlias = 6,
    MissingBundleLegalityDescriptor = 7,
    MissingSchedulingBudgetDescriptor = 8,
    RuntimeAuthorityMissing = 9,
    CompatibilityProjectionDenied = 10,
    SchedulingLaneRejected = 11,
    SchedulingBudgetExceeded = 12,
    MissingTrapPolicyDescriptor = 13,
    TrapPolicyClassDenied = 14,
    MissingEventQueueDescriptor = 15,
    EventQueueRejected = 16,
    MissingCompletionRouteDescriptor = 17,
    CompletionSourceDenied = 18,
}

public sealed partial class DomainValidationResult
{
    private DomainValidationResult(
        bool isValid,
        DomainValidationFailureReason failureReason,
        string message)
    {
        IsValid = isValid;
        FailureReason = failureReason;
        Message = message;
    }

    public bool IsValid { get; }

    public DomainValidationFailureReason FailureReason { get; }

    public string Message { get; }

    public static DomainValidationResult Passed { get; } =
        new(true, DomainValidationFailureReason.None, string.Empty);

    public static DomainValidationResult Fail(
        DomainValidationFailureReason failureReason,
        string message = "") =>
        new(false, failureReason, message);

    public static DomainValidationResult RequireRuntimeContext(
        DomainRuntimeContext context)
    {
        if (!context.HasExecutionDomain)
        {
            return Fail(DomainValidationFailureReason.MissingExecutionDomain);
        }

        if (!context.HasMemoryDomain)
        {
            return Fail(DomainValidationFailureReason.MissingMemoryDomain);
        }

        if (!context.HasIoDomain)
        {
            return Fail(DomainValidationFailureReason.MissingIoDomain);
        }

        return Passed;
    }

    public static DomainValidationResult RequireOperation(
        DomainRuntimeContext context,
        DomainRuntimeOperation operation,
        CapabilityBoundaryRequirement capabilityRequirement = default)
    {
        if (operation.RequiresCapabilityGrant &&
            (capabilityRequirement.CapabilityMask == 0 ||
             !capabilityRequirement.IsSatisfiedBy(context.Capabilities)))
        {
            return Fail(DomainValidationFailureReason.MissingCapabilityGrant);
        }

        if (!operation.CanMutateAuthoritativeState &&
            !operation.IsProjectionOnly)
        {
            return Fail(DomainValidationFailureReason.ProjectionCannotMutateState);
        }

        return Passed;
    }
}
