namespace YAKSys_Hybrid_CPU.Core;

public enum Lane7DomainRuntimeDecision : byte
{
    Allowed = 0,
    MissingDescriptor = 1,
    RuntimeAuthorityRequired = 2,
    WrongPinnedLane = 3,
    MissingBackendBinding = 4,
    MissingHandleNamespace = 5,
    MissingTokenNamespace = 6,
    MissingCompletionRoute = 7,
    CompatibilityProjectionDenied = 8,
}

public readonly record struct Lane7DomainRuntimeRequest(
    Lane7AcceleratorDescriptor? Descriptor,
    bool RequiresBackendBinding,
    bool RequiresHandleNamespace,
    bool RequiresTokenNamespace,
    bool RequiresCompletionRoute,
    bool RequiresCompatibilityProjection);

public readonly record struct Lane7DomainRuntimeResult(
    Lane7DomainRuntimeDecision Decision,
    string Reason)
{
    public bool IsAllowed => Decision == Lane7DomainRuntimeDecision.Allowed;

    public static Lane7DomainRuntimeResult Allowed { get; } =
        new(Lane7DomainRuntimeDecision.Allowed, "Lane7 domain runtime admission allowed.");

    public static Lane7DomainRuntimeResult Denied(
        Lane7DomainRuntimeDecision decision,
        string reason) =>
        new(decision, reason);
}

public sealed partial class Lane7DomainRuntime
{
    public Lane7DomainRuntimeResult Validate(Lane7DomainRuntimeRequest request)
    {
        if (request.Descriptor is null)
        {
            return Lane7DomainRuntimeResult.Denied(
                Lane7DomainRuntimeDecision.MissingDescriptor,
                "Lane7 runtime requires a Lane7 accelerator descriptor.");
        }

        if (!request.Descriptor.IsRuntimeAuthoritative)
        {
            return Lane7DomainRuntimeResult.Denied(
                Lane7DomainRuntimeDecision.RuntimeAuthorityRequired,
                "Lane7 runtime requires a runtime-authoritative descriptor.");
        }

        if (!request.Descriptor.IsPinnedToLane7)
        {
            return Lane7DomainRuntimeResult.Denied(
                Lane7DomainRuntimeDecision.WrongPinnedLane,
                "Lane7 descriptor must be pinned to Lane7.");
        }

        if ((request.RequiresBackendBinding || request.Descriptor.RequiresRuntimeBackendBinding) &&
            !request.Descriptor.HasBackendBinding)
        {
            return Lane7DomainRuntimeResult.Denied(
                Lane7DomainRuntimeDecision.MissingBackendBinding,
                "Lane7 runtime requires a backend binding.");
        }

        if (request.RequiresHandleNamespace && request.Descriptor.HandleNamespaceId == 0)
        {
            return Lane7DomainRuntimeResult.Denied(
                Lane7DomainRuntimeDecision.MissingHandleNamespace,
                "Lane7 runtime requires a handle namespace binding.");
        }

        if (request.RequiresTokenNamespace && request.Descriptor.TokenNamespaceId == 0)
        {
            return Lane7DomainRuntimeResult.Denied(
                Lane7DomainRuntimeDecision.MissingTokenNamespace,
                "Lane7 runtime requires a token namespace binding.");
        }

        if (request.RequiresCompletionRoute && request.Descriptor.CompletionRouteId == 0)
        {
            return Lane7DomainRuntimeResult.Denied(
                Lane7DomainRuntimeDecision.MissingCompletionRoute,
                "Lane7 runtime requires a completion route binding.");
        }

        if (request.RequiresCompatibilityProjection &&
            !request.Descriptor.CanProjectToCompatibilityFrontend)
        {
            return Lane7DomainRuntimeResult.Denied(
                Lane7DomainRuntimeDecision.CompatibilityProjectionDenied,
                "Lane7 descriptor denies compatibility projection.");
        }

        return Lane7DomainRuntimeResult.Allowed;
    }

    public bool CanAdmit(Lane7DomainRuntimeRequest request) =>
        Validate(request).IsAllowed;
}
