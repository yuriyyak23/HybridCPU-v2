namespace YAKSys_Hybrid_CPU.Core;

public enum Lane7CompletionPolicyDecision : byte
{
    Allowed = 0,
    MissingDescriptor = 1,
    RuntimeAuthorityRequired = 2,
    WrongPinnedLane = 3,
    MissingCompletionRouteBinding = 4,
    MissingRouteDescriptor = 5,
    RouteAuthorityDenied = 6,
    InvalidCompletion = 7,
    WrongCompletionSource = 8,
    RouteSourceDenied = 9,
    CompatibilityProjectionDenied = 10,
}

public readonly record struct Lane7CompletionPolicyRequest(
    Lane7AcceleratorDescriptor? Descriptor,
    CompletionRouteDescriptor? RouteDescriptor,
    LaneCompletionDescriptor Completion,
    bool RequiresCompatibilityProjection);

public readonly record struct Lane7CompletionPolicyResult(
    Lane7CompletionPolicyDecision Decision,
    string Reason)
{
    public bool IsAllowed => Decision == Lane7CompletionPolicyDecision.Allowed;

    public static Lane7CompletionPolicyResult Allowed { get; } =
        new(Lane7CompletionPolicyDecision.Allowed, "Lane7 completion policy allowed.");

    public static Lane7CompletionPolicyResult Denied(
        Lane7CompletionPolicyDecision decision,
        string reason) =>
        new(decision, reason);
}

public sealed partial class Lane7CompletionPolicy
{
    public Lane7CompletionPolicyResult Validate(Lane7CompletionPolicyRequest request)
    {
        if (request.Descriptor is null)
        {
            return Lane7CompletionPolicyResult.Denied(
                Lane7CompletionPolicyDecision.MissingDescriptor,
                "Lane7 completion policy requires a Lane7 accelerator descriptor.");
        }

        if (!request.Descriptor.IsRuntimeAuthoritative)
        {
            return Lane7CompletionPolicyResult.Denied(
                Lane7CompletionPolicyDecision.RuntimeAuthorityRequired,
                "Lane7 completion policy requires runtime-authoritative descriptors.");
        }

        if (!request.Descriptor.IsPinnedToLane7)
        {
            return Lane7CompletionPolicyResult.Denied(
                Lane7CompletionPolicyDecision.WrongPinnedLane,
                "Lane7 completion policy requires a descriptor pinned to Lane7.");
        }

        if (request.Descriptor.CompletionRouteId == 0)
        {
            return Lane7CompletionPolicyResult.Denied(
                Lane7CompletionPolicyDecision.MissingCompletionRouteBinding,
                "Lane7 completion policy requires a completion route binding.");
        }

        if (request.RouteDescriptor is null)
        {
            return Lane7CompletionPolicyResult.Denied(
                Lane7CompletionPolicyDecision.MissingRouteDescriptor,
                "Lane7 completion policy requires a completion route descriptor.");
        }

        if (!request.RouteDescriptor.IsRuntimeAuthoritative)
        {
            return Lane7CompletionPolicyResult.Denied(
                Lane7CompletionPolicyDecision.RouteAuthorityDenied,
                "Lane7 completion route must be runtime-authoritative.");
        }

        if (!request.Completion.IsValid)
        {
            return Lane7CompletionPolicyResult.Denied(
                Lane7CompletionPolicyDecision.InvalidCompletion,
                "Lane7 completion policy rejected an invalid completion descriptor.");
        }

        if (request.Completion.SourceKind != LaneCompletionSourceKind.ExternalAcceleratorLane7 ||
            request.Completion.LaneIndex != VirtualizationLaneBindingPolicy.Lane7Id)
        {
            return Lane7CompletionPolicyResult.Denied(
                Lane7CompletionPolicyDecision.WrongCompletionSource,
                "Lane7 completion policy accepts only Lane7 accelerator completions.");
        }

        if (!request.RouteDescriptor.AllowsSource(request.Completion.SourceKind))
        {
            return Lane7CompletionPolicyResult.Denied(
                Lane7CompletionPolicyDecision.RouteSourceDenied,
                "Completion route descriptor denies Lane7 accelerator completions.");
        }

        if (request.RequiresCompatibilityProjection &&
            (!request.Descriptor.CanProjectToCompatibilityFrontend ||
             !request.RouteDescriptor.CanProjectToCompatibilityFrontend))
        {
            return Lane7CompletionPolicyResult.Denied(
                Lane7CompletionPolicyDecision.CompatibilityProjectionDenied,
                "Lane7 completion policy requires descriptor-authorized compatibility projection.");
        }

        return Lane7CompletionPolicyResult.Allowed;
    }

    public bool CanPublish(Lane7CompletionPolicyRequest request) =>
        Validate(request).IsAllowed;
}
