namespace YAKSys_Hybrid_CPU.Core;

public enum ShadowVmcsCompatibilityBridgeViolation : byte
{
    None = 0,
    ServiceStoresVmcsDescriptor = 1,
    ServiceCallsShadowVmcsDirectly = 2,
    BridgeNotCompatibilityOnly = 3,
    MissingProjectionServiceBoundary = 4,
    MissingTypedFailure = 5,
}

public readonly record struct ShadowVmcsCompatibilityBridgeRequest(
    bool ServiceStoresVmcsDescriptor,
    bool ServiceCallsShadowVmcsDirectly,
    bool BridgeIsCompatibilityOnly,
    bool HasProjectionServiceBoundary,
    bool ReturnsTypedFailure);

public sealed partial class ShadowVmcsCompatibilityBridgeContract
{
    public ShadowVmcsCompatibilityBridgeViolation Evaluate(
        ShadowVmcsCompatibilityBridgeRequest request)
    {
        if (request.ServiceStoresVmcsDescriptor)
        {
            return ShadowVmcsCompatibilityBridgeViolation.ServiceStoresVmcsDescriptor;
        }

        if (request.ServiceCallsShadowVmcsDirectly)
        {
            return ShadowVmcsCompatibilityBridgeViolation.ServiceCallsShadowVmcsDirectly;
        }

        if (!request.BridgeIsCompatibilityOnly)
        {
            return ShadowVmcsCompatibilityBridgeViolation.BridgeNotCompatibilityOnly;
        }

        if (!request.ReturnsTypedFailure)
        {
            return ShadowVmcsCompatibilityBridgeViolation.MissingTypedFailure;
        }

        return request.HasProjectionServiceBoundary
            ? ShadowVmcsCompatibilityBridgeViolation.None
            : ShadowVmcsCompatibilityBridgeViolation.MissingProjectionServiceBoundary;
    }

    public bool IsSatisfied(ShadowVmcsCompatibilityBridgeRequest request) =>
        Evaluate(request) == ShadowVmcsCompatibilityBridgeViolation.None;
}
