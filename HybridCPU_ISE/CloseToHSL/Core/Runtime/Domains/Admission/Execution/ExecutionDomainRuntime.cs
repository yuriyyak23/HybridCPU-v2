namespace YAKSys_Hybrid_CPU.Core;

public enum ExecutionDomainRuntimeDecision : byte
{
    Allowed = 0,
    MissingDescriptor = 1,
    DescriptorAuthorityDenied = 2,
    MissingBundleLegalityDescriptor = 3,
    MissingSchedulingBudgetDescriptor = 4,
    CompatibilityProjectionDenied = 5,
}

public readonly record struct ExecutionDomainRuntimeRequest(
    ExecutionDomainDescriptor? Descriptor,
    bool RequiresBundleLegality,
    bool RequiresSchedulingBudget,
    bool RequiresCompatibilityProjection);

public readonly record struct ExecutionDomainRuntimeResult(
    ExecutionDomainRuntimeDecision Decision,
    string Reason)
{
    public bool IsAllowed => Decision == ExecutionDomainRuntimeDecision.Allowed;

    public static ExecutionDomainRuntimeResult Allowed { get; } =
        new(ExecutionDomainRuntimeDecision.Allowed, "Execution domain runtime admission allowed.");

    public static ExecutionDomainRuntimeResult Denied(
        ExecutionDomainRuntimeDecision decision,
        string reason) =>
        new(decision, reason);
}

public sealed partial class ExecutionDomainRuntime
{
    public ExecutionDomainRuntimeResult Validate(ExecutionDomainRuntimeRequest request)
    {
        if (request.Descriptor is null)
        {
            return ExecutionDomainRuntimeResult.Denied(
                ExecutionDomainRuntimeDecision.MissingDescriptor,
                "Execution domain runtime requires an execution-domain descriptor.");
        }

        if (!request.Descriptor.IsAuthoritativeExecutionStateOwner)
        {
            return ExecutionDomainRuntimeResult.Denied(
                ExecutionDomainRuntimeDecision.DescriptorAuthorityDenied,
                "Execution state authority must belong to the execution-domain descriptor.");
        }

        if (request.RequiresBundleLegality && !request.Descriptor.HasBundleLegality)
        {
            return ExecutionDomainRuntimeResult.Denied(
                ExecutionDomainRuntimeDecision.MissingBundleLegalityDescriptor,
                "Execution domain runtime requires a bundle-legality descriptor.");
        }

        if (request.RequiresSchedulingBudget && !request.Descriptor.HasSchedulingBudget)
        {
            return ExecutionDomainRuntimeResult.Denied(
                ExecutionDomainRuntimeDecision.MissingSchedulingBudgetDescriptor,
                "Execution domain runtime requires a scheduling-budget descriptor.");
        }

        if (request.RequiresCompatibilityProjection && !request.Descriptor.CompatibilityProjectionEnabled)
        {
            return ExecutionDomainRuntimeResult.Denied(
                ExecutionDomainRuntimeDecision.CompatibilityProjectionDenied,
                "Execution descriptor denies compatibility projection.");
        }

        return ExecutionDomainRuntimeResult.Allowed;
    }

    public bool CanAdmit(ExecutionDomainRuntimeRequest request) =>
        Validate(request).IsAllowed;
}
