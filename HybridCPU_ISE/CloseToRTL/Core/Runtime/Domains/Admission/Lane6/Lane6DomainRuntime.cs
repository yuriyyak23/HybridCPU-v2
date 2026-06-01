namespace YAKSys_Hybrid_CPU.Core;

public enum Lane6DomainRuntimeDecision : byte
{
    Allowed = 0,
    MissingDescriptor = 1,
    RuntimeAuthorityRequired = 2,
    WrongPinnedLane = 3,
    MissingTokenNamespace = 4,
    MissingQueueNamespace = 5,
    MissingFenceDomain = 6,
    CompatibilityProjectionDenied = 7,
}

public readonly record struct Lane6DomainRuntimeRequest(
    Lane6DomainDescriptor? Descriptor,
    bool RequiresTokenNamespace,
    bool RequiresQueueNamespace,
    bool RequiresFenceDomain,
    bool RequiresCompatibilityProjection);

public readonly record struct Lane6DomainRuntimeResult(
    Lane6DomainRuntimeDecision Decision,
    string Reason)
{
    public bool IsAllowed => Decision == Lane6DomainRuntimeDecision.Allowed;

    public static Lane6DomainRuntimeResult Allowed { get; } =
        new(Lane6DomainRuntimeDecision.Allowed, "Lane6 domain runtime admission allowed.");

    public static Lane6DomainRuntimeResult Denied(
        Lane6DomainRuntimeDecision decision,
        string reason) =>
        new(decision, reason);
}

public sealed partial class Lane6DomainRuntime
{
    public Lane6DomainRuntimeResult Validate(Lane6DomainRuntimeRequest request)
    {
        if (request.Descriptor is null)
        {
            return Lane6DomainRuntimeResult.Denied(
                Lane6DomainRuntimeDecision.MissingDescriptor,
                "Lane6 runtime requires a Lane6 domain descriptor.");
        }

        if (!request.Descriptor.IsRuntimeAuthoritative)
        {
            return Lane6DomainRuntimeResult.Denied(
                Lane6DomainRuntimeDecision.RuntimeAuthorityRequired,
                "Lane6 runtime requires a runtime-authoritative descriptor.");
        }

        if (!request.Descriptor.IsPinnedToLane6)
        {
            return Lane6DomainRuntimeResult.Denied(
                Lane6DomainRuntimeDecision.WrongPinnedLane,
                "Lane6 descriptor must be pinned to Lane6.");
        }

        if (request.RequiresTokenNamespace && request.Descriptor.TokenNamespaceId == 0)
        {
            return Lane6DomainRuntimeResult.Denied(
                Lane6DomainRuntimeDecision.MissingTokenNamespace,
                "Lane6 runtime requires a token namespace binding.");
        }

        if (request.RequiresQueueNamespace && request.Descriptor.QueueNamespaceId == 0)
        {
            return Lane6DomainRuntimeResult.Denied(
                Lane6DomainRuntimeDecision.MissingQueueNamespace,
                "Lane6 runtime requires a queue namespace binding.");
        }

        if (request.RequiresFenceDomain && request.Descriptor.FenceDomainId == 0)
        {
            return Lane6DomainRuntimeResult.Denied(
                Lane6DomainRuntimeDecision.MissingFenceDomain,
                "Lane6 runtime requires a fence-domain binding.");
        }

        if (request.RequiresCompatibilityProjection &&
            !request.Descriptor.CanProjectToCompatibilityFrontend)
        {
            return Lane6DomainRuntimeResult.Denied(
                Lane6DomainRuntimeDecision.CompatibilityProjectionDenied,
                "Lane6 descriptor denies compatibility projection.");
        }

        return Lane6DomainRuntimeResult.Allowed;
    }

    public bool CanAdmit(Lane6DomainRuntimeRequest request) =>
        Validate(request).IsAllowed;
}
