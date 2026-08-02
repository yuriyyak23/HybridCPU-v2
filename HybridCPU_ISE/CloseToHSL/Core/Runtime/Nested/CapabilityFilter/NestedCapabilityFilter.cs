using YAKSys_Hybrid_CPU.Core.Nested;

namespace YAKSys_Hybrid_CPU.Core;

public enum NestedCapabilityFilterDecision : byte
{
    Allowed = 0,
    MissingDescriptor = 1,
    RuntimeAuthorityRequired = 2,
    CapabilityDenied = 3,
    HostEvidenceGateDenied = 4,
    LanePassthroughGateDenied = 5,
    DomainCompositionDenied = 6,
}

public readonly record struct NestedCapabilityFilterRequest(
    NestedDomainDescriptor? Descriptor,
    NestedCapabilityGrantMask RequiredCapabilities,
    bool RequiresHostEvidenceExclusion,
    bool RequiresLanePassthroughBlocked,
    bool RequiresDomainComposition);

public readonly record struct NestedCapabilityFilterResult(
    NestedCapabilityFilterDecision Decision,
    string Reason)
{
    public bool IsAllowed => Decision == NestedCapabilityFilterDecision.Allowed;

    public static NestedCapabilityFilterResult Allowed { get; } =
        new(NestedCapabilityFilterDecision.Allowed, "Nested capability filter allowed.");

    public static NestedCapabilityFilterResult Denied(
        NestedCapabilityFilterDecision decision,
        string reason) =>
        new(decision, reason);
}

public sealed partial class NestedCapabilityFilter
{
    public NestedCapabilityFilterResult Validate(NestedCapabilityFilterRequest request)
    {
        if (request.Descriptor is null)
        {
            return NestedCapabilityFilterResult.Denied(
                NestedCapabilityFilterDecision.MissingDescriptor,
                "Nested capability filtering requires a nested-domain descriptor.");
        }

        if (!request.Descriptor.IsRuntimeAuthoritative)
        {
            return NestedCapabilityFilterResult.Denied(
                NestedCapabilityFilterDecision.RuntimeAuthorityRequired,
                "Nested capability filtering requires a runtime-authoritative descriptor.");
        }

        if (!request.Descriptor.HasCapability(request.RequiredCapabilities))
        {
            return NestedCapabilityFilterResult.Denied(
                NestedCapabilityFilterDecision.CapabilityDenied,
                "Required nested capability grants are not present.");
        }

        if (request.RequiresHostEvidenceExclusion && !request.Descriptor.HostEvidenceExcluded)
        {
            return NestedCapabilityFilterResult.Denied(
                NestedCapabilityFilterDecision.HostEvidenceGateDenied,
                "Nested composition requires host-owned evidence exclusion.");
        }

        if (request.RequiresLanePassthroughBlocked && !request.Descriptor.LanePassthroughBlocked)
        {
            return NestedCapabilityFilterResult.Denied(
                NestedCapabilityFilterDecision.LanePassthroughGateDenied,
                "Nested composition requires Lane6/Lane7 passthrough to be blocked.");
        }

        if (request.RequiresDomainComposition && !request.Descriptor.CanComposeDomain)
        {
            return NestedCapabilityFilterResult.Denied(
                NestedCapabilityFilterDecision.DomainCompositionDenied,
                "Nested domain composition is not enabled by descriptor gates.");
        }

        return NestedCapabilityFilterResult.Allowed;
    }

    public bool CanPass(NestedCapabilityFilterRequest request) =>
        Validate(request).IsAllowed;
}
