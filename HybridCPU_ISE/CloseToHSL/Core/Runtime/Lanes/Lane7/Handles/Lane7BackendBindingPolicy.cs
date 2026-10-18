namespace YAKSys_Hybrid_CPU.Core;

public enum Lane7BackendBindingDecision : byte
{
    Allowed = 0,
    MissingDescriptor = 1,
    RuntimeAuthorityRequired = 2,
    WrongPinnedLane = 3,
    BackendBindingMissing = 4,
    HandleNamespaceAuthorityDenied = 5,
    NativeBackendHandleExposureDenied = 6,
    CompatibilityProjectionDenied = 7,
}

public readonly record struct Lane7BackendBindingRequest(
    Lane7AcceleratorDescriptor? Descriptor,
    Lane7HandleNamespace? HandleNamespace,
    bool RequiresBackendBinding,
    bool RequiresCompatibilityProjection);

public readonly record struct Lane7BackendBindingResult(
    Lane7BackendBindingDecision Decision,
    string Reason)
{
    public bool IsAllowed => Decision == Lane7BackendBindingDecision.Allowed;

    public static Lane7BackendBindingResult Allowed { get; } =
        new(Lane7BackendBindingDecision.Allowed, "Lane7 backend binding policy allowed.");

    public static Lane7BackendBindingResult Denied(
        Lane7BackendBindingDecision decision,
        string reason) =>
        new(decision, reason);
}

public sealed partial class Lane7BackendBindingPolicy
{
    public Lane7BackendBindingResult Validate(Lane7BackendBindingRequest request)
    {
        if (request.Descriptor is null)
        {
            return Lane7BackendBindingResult.Denied(
                Lane7BackendBindingDecision.MissingDescriptor,
                "Lane7 backend binding policy requires a Lane7 accelerator descriptor.");
        }

        if (!request.Descriptor.IsRuntimeAuthoritative)
        {
            return Lane7BackendBindingResult.Denied(
                Lane7BackendBindingDecision.RuntimeAuthorityRequired,
                "Lane7 backend binding policy requires runtime-authoritative descriptors.");
        }

        if (!request.Descriptor.IsPinnedToLane7)
        {
            return Lane7BackendBindingResult.Denied(
                Lane7BackendBindingDecision.WrongPinnedLane,
                "Lane7 backend binding policy requires a descriptor pinned to Lane7.");
        }

        if ((request.RequiresBackendBinding || request.Descriptor.RequiresRuntimeBackendBinding) &&
            !request.Descriptor.HasBackendBinding)
        {
            return Lane7BackendBindingResult.Denied(
                Lane7BackendBindingDecision.BackendBindingMissing,
                "Lane7 backend binding policy requires a runtime backend binding.");
        }

        if (request.HandleNamespace is not null &&
            !request.HandleNamespace.IsRuntimeAuthoritative)
        {
            return Lane7BackendBindingResult.Denied(
                Lane7BackendBindingDecision.HandleNamespaceAuthorityDenied,
                "Lane7 backend binding policy requires a runtime-owned handle namespace.");
        }

        if (request.HandleNamespace is not null &&
            request.HandleNamespace.ExposesNativeBackendHandles)
        {
            return Lane7BackendBindingResult.Denied(
                Lane7BackendBindingDecision.NativeBackendHandleExposureDenied,
                "Lane7 backend binding policy cannot expose native backend handles.");
        }

        if (request.RequiresCompatibilityProjection &&
            !request.Descriptor.CanProjectToCompatibilityFrontend)
        {
            return Lane7BackendBindingResult.Denied(
                Lane7BackendBindingDecision.CompatibilityProjectionDenied,
                "Lane7 backend binding descriptor denies compatibility projection.");
        }

        return Lane7BackendBindingResult.Allowed;
    }

    public bool CanBind(Lane7BackendBindingRequest request) =>
        Validate(request).IsAllowed;
}
