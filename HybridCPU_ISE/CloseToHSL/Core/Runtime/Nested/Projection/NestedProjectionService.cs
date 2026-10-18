namespace YAKSys_Hybrid_CPU.Core;

public enum NestedProjectionDecision : byte
{
    Allowed = 0,
    MissingDescriptor = 1,
    RuntimeAdmissionDenied = 2,
    CapabilityFilterDenied = 3,
    EvidencePolicyDenied = 4,
    CompletionMappingDenied = 5,
    CompatibilityProjectionDenied = 6,
}

public readonly record struct NestedProjectionRequest(
    NestedDomainDescriptor? Descriptor,
    NestedDomainRuntimeResult RuntimeAdmission,
    NestedCapabilityFilterResult CapabilityFilter,
    NestedEvidencePolicyResult EvidencePolicy,
    NestedCompletionMappingResult CompletionMapping,
    bool RequiresCompletionMapping,
    bool RequiresCompatibilityProjection);

public readonly record struct NestedProjectionResult(
    NestedProjectionDecision Decision,
    string Reason)
{
    public bool IsAllowed => Decision == NestedProjectionDecision.Allowed;

    public static NestedProjectionResult Allowed { get; } =
        new(NestedProjectionDecision.Allowed, "Nested compatibility projection allowed.");

    public static NestedProjectionResult Denied(
        NestedProjectionDecision decision,
        string reason) =>
        new(decision, reason);
}

public sealed partial class NestedProjectionService
{
    public NestedProjectionResult Validate(NestedProjectionRequest request)
    {
        if (request.Descriptor is null)
        {
            return NestedProjectionResult.Denied(
                NestedProjectionDecision.MissingDescriptor,
                "Nested projection requires a nested domain descriptor.");
        }

        if (!IsExplicitlyAllowed(request.RuntimeAdmission.IsAllowed, request.RuntimeAdmission.Reason))
        {
            return NestedProjectionResult.Denied(
                NestedProjectionDecision.RuntimeAdmissionDenied,
                request.RuntimeAdmission.Reason ?? "Nested projection requires explicit runtime admission.");
        }

        if (!IsExplicitlyAllowed(request.CapabilityFilter.IsAllowed, request.CapabilityFilter.Reason))
        {
            return NestedProjectionResult.Denied(
                NestedProjectionDecision.CapabilityFilterDenied,
                request.CapabilityFilter.Reason ?? "Nested projection requires an explicit capability-filter allow.");
        }

        if (!IsExplicitlyAllowed(request.EvidencePolicy.IsAllowed, request.EvidencePolicy.Reason))
        {
            return NestedProjectionResult.Denied(
                NestedProjectionDecision.EvidencePolicyDenied,
                request.EvidencePolicy.Reason ?? "Nested projection requires an explicit evidence-policy allow.");
        }

        if (request.RequiresCompletionMapping &&
            !IsExplicitlyAllowed(request.CompletionMapping.IsAllowed, request.CompletionMapping.Reason))
        {
            return NestedProjectionResult.Denied(
                NestedProjectionDecision.CompletionMappingDenied,
                request.CompletionMapping.Reason ?? "Nested projection requires explicit completion mapping.");
        }

        if (request.RequiresCompatibilityProjection &&
            !request.Descriptor.CanProjectToCompatibilityFrontend)
        {
            return NestedProjectionResult.Denied(
                NestedProjectionDecision.CompatibilityProjectionDenied,
                "Nested descriptor denies compatibility projection.");
        }

        return NestedProjectionResult.Allowed;
    }

    public bool CanProject(NestedProjectionRequest request) =>
        Validate(request).IsAllowed;

    private static bool IsExplicitlyAllowed(bool allowed, string? reason) =>
        allowed && !string.IsNullOrWhiteSpace(reason);
}
