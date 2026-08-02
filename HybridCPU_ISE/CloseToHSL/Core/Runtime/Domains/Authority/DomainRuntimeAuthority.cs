namespace YAKSys_Hybrid_CPU.Core;

public enum DomainRuntimeAuthorityDecision : byte
{
    Allowed = 0,
    MissingRuntimeContext = 1,
    MissingRootAuthority = 2,
    FrontendActivationDenied = 3,
    CapabilityDenied = 4,
    MutationDenied = 5,
    ProjectionDenied = 6,
}

public readonly record struct DomainRuntimeAuthorityResult(
    DomainRuntimeAuthorityDecision Decision,
    string Message)
{
    public bool IsAllowed => Decision == DomainRuntimeAuthorityDecision.Allowed;

    public static DomainRuntimeAuthorityResult Allowed { get; } =
        new(DomainRuntimeAuthorityDecision.Allowed, string.Empty);

    public static DomainRuntimeAuthorityResult Denied(
        DomainRuntimeAuthorityDecision decision,
        string message) =>
        new(decision, message);
}

public sealed partial class DomainRuntimeAuthority
{
    public DomainRuntimeAuthorityResult Validate(
        RootAuthorityDescriptor? root,
        DomainRuntimeContext context,
        DomainRuntimeOperation operation,
        ulong requiredCapabilityMask = 0)
    {
        CapabilityBoundaryRequirement capabilityRequirement =
            requiredCapabilityMask == 0
                ? CapabilityBoundaryRequirement.None
                : CapabilityBoundaryRequirement.TypedGrant(
                    requiredCapabilityMask,
                    CapabilityGrantScope.CompatibilityProjection);

        return Validate(root, context, operation, capabilityRequirement);
    }

    public DomainRuntimeAuthorityResult Validate(
        RootAuthorityDescriptor? root,
        DomainRuntimeContext context,
        DomainRuntimeOperation operation,
        CapabilityBoundaryRequirement capabilityRequirement)
    {
        if (!context.HasRequiredDomains)
        {
            return Deny(
                DomainRuntimeAuthorityDecision.MissingRuntimeContext,
                "Domain runtime authority requires execution, memory and I/O descriptors.");
        }

        if (root is null)
        {
            return Deny(
                DomainRuntimeAuthorityDecision.MissingRootAuthority,
                "Domain runtime authority requires a root authority descriptor.");
        }

        if (operation.Kind == DomainRuntimeOperationKind.ActivateCompatibilityFrontend &&
            !root.CanActivateCompatibilityFrontend)
        {
            return Deny(
                DomainRuntimeAuthorityDecision.FrontendActivationDenied,
                "Root authority denies compatibility frontend activation.");
        }

        if (operation.RequiresCapabilityGrant)
        {
            if (capabilityRequirement.CapabilityMask == 0 ||
                !capabilityRequirement.IsSatisfiedBy(context.Capabilities) ||
                !root.HasCapability(capabilityRequirement.CapabilityMask))
            {
                return Deny(
                    DomainRuntimeAuthorityDecision.CapabilityDenied,
                    "Required capability is not backed by typed domain authority.");
            }
        }

        if (operation.IsProjectionOnly)
        {
            return context.Execution?.CompatibilityProjectionEnabled == true
                ? DomainRuntimeAuthorityResult.Allowed
                : Deny(
                    DomainRuntimeAuthorityDecision.ProjectionDenied,
                    "Compatibility projection is disabled by the execution descriptor.");
        }

        if (!root.CanMutateAuthoritativeState(operation))
        {
            return Deny(
                DomainRuntimeAuthorityDecision.MutationDenied,
                "Root authority denies authoritative state mutation.");
        }

        return DomainRuntimeAuthorityResult.Allowed;
    }

    private static DomainRuntimeAuthorityResult Deny(
        DomainRuntimeAuthorityDecision decision,
        string message) =>
        DomainRuntimeAuthorityResult.Denied(decision, message);
}
