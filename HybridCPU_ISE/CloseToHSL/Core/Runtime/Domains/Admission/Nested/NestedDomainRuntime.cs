namespace YAKSys_Hybrid_CPU.Core;

public enum NestedDomainRuntimeDecision : byte
{
    Allowed = 0,
    MissingDescriptor = 1,
    RuntimeAuthorityRequired = 2,
    DomainCompositionDenied = 3,
    CapabilityFilterDenied = 4,
    CompatibilityProjectionDenied = 5,
}

public readonly record struct NestedDomainRuntimeRequest(
    NestedDomainDescriptor? Descriptor,
    NestedCapabilityFilterResult CapabilityFilter,
    bool RequiresCompatibilityProjection);

public readonly record struct NestedDomainRuntimeResult(
    NestedDomainRuntimeDecision Decision,
    string Reason)
{
    public bool IsAllowed => Decision == NestedDomainRuntimeDecision.Allowed;

    public static NestedDomainRuntimeResult Allowed { get; } =
        new(NestedDomainRuntimeDecision.Allowed, "Nested domain runtime admission allowed.");

    public static NestedDomainRuntimeResult Denied(
        NestedDomainRuntimeDecision decision,
        string reason) =>
        new(decision, reason);
}

public sealed partial class NestedDomainRuntime
{
    public NestedDomainRuntimeResult Validate(NestedDomainRuntimeRequest request)
    {
        if (request.Descriptor is null)
        {
            return NestedDomainRuntimeResult.Denied(
                NestedDomainRuntimeDecision.MissingDescriptor,
                "Nested domain runtime admission requires a descriptor.");
        }

        if (!request.Descriptor.IsRuntimeAuthoritative)
        {
            return NestedDomainRuntimeResult.Denied(
                NestedDomainRuntimeDecision.RuntimeAuthorityRequired,
                "Nested domain runtime admission requires runtime-authoritative descriptors.");
        }

        if (!request.Descriptor.CanComposeDomain)
        {
            return NestedDomainRuntimeResult.Denied(
                NestedDomainRuntimeDecision.DomainCompositionDenied,
                "Nested domain runtime admission requires validated domain composition.");
        }

        if (!request.CapabilityFilter.IsAllowed)
        {
            return NestedDomainRuntimeResult.Denied(
                NestedDomainRuntimeDecision.CapabilityFilterDenied,
                request.CapabilityFilter.Reason);
        }

        if (request.RequiresCompatibilityProjection &&
            !request.Descriptor.CanProjectToCompatibilityFrontend)
        {
            return NestedDomainRuntimeResult.Denied(
                NestedDomainRuntimeDecision.CompatibilityProjectionDenied,
                "Nested compatibility projection requires descriptor-authorized projection.");
        }

        return NestedDomainRuntimeResult.Allowed;
    }

    public bool CanAdmit(NestedDomainRuntimeRequest request) =>
        Validate(request).IsAllowed;
}
