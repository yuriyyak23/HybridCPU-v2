using YAKSys_Hybrid_CPU.Core.Nested;

namespace YAKSys_Hybrid_CPU.Core;

public enum NestedCompletionMappingDecision : byte
{
    Allowed = 0,
    MissingDescriptor = 1,
    RuntimeAuthorityRequired = 2,
    CompletionPayloadMissing = 3,
    HostEvidenceDenied = 4,
    LanePassthroughDenied = 5,
    CapabilityDenied = 6,
    CompatibilityProjectionDenied = 7,
}

public readonly record struct NestedCompletionMappingRequest(
    NestedDomainDescriptor? Descriptor,
    CompletionRouteDescriptor? RouteDescriptor,
    CompletionSidebandEnvelope? Completion,
    bool RequiresCompatibilityProjection);

public readonly record struct NestedCompletionMappingResult(
    NestedCompletionMappingDecision Decision,
    CompletionSidebandEnvelope Completion,
    VmExitReason ExitReason,
    ulong Qualification,
    string Reason)
{
    public bool IsAllowed => Decision == NestedCompletionMappingDecision.Allowed;

    public static NestedCompletionMappingResult Allowed(CompletionSidebandEnvelope completion) =>
        new(
            NestedCompletionMappingDecision.Allowed,
            completion,
            VmExitReason.None,
            0,
            "Nested completion mapping allowed.");

    public static NestedCompletionMappingResult Denied(
        NestedCompletionMappingDecision decision,
        VmExitReason exitReason,
        ulong qualification,
        string reason) =>
        new(decision, CompletionSidebandEnvelope.Empty, exitReason, qualification, reason);
}

public sealed partial class NestedCompletionMapper
{
    public NestedCompletionMappingResult Validate(NestedCompletionMappingRequest request)
    {
        if (request.Descriptor is null)
        {
            return NestedCompletionMappingResult.Denied(
                NestedCompletionMappingDecision.MissingDescriptor,
                VmExitReason.SecurityPolicyViolation,
                0,
                "Nested completion mapping requires a nested domain descriptor.");
        }

        if (!request.Descriptor.IsRuntimeAuthoritative)
        {
            return NestedCompletionMappingResult.Denied(
                NestedCompletionMappingDecision.RuntimeAuthorityRequired,
                VmExitReason.SecurityPolicyViolation,
                0,
                "Nested completion mapping requires runtime-authoritative descriptors.");
        }

        if (request.Completion is null || !request.Completion.HasPayload)
        {
            return NestedCompletionMappingResult.Denied(
                NestedCompletionMappingDecision.CompletionPayloadMissing,
                VmExitReason.None,
                0,
                "Nested completion mapping requires a completion sideband payload.");
        }

        if (request.Completion.RequiresHostHandling)
        {
            return NestedCompletionMappingResult.Denied(
                NestedCompletionMappingDecision.HostEvidenceDenied,
                VmExitReason.SecurityPolicyViolation,
                request.Completion.Sequence,
                "Nested completion mapping cannot expose host-owned completion evidence.");
        }

        if (!request.Descriptor.LanePassthroughBlocked)
        {
            return NestedCompletionMappingResult.Denied(
                NestedCompletionMappingDecision.LanePassthroughDenied,
                VmExitReason.SecurityPolicyViolation,
                request.Completion.Sequence,
                "Nested completion mapping requires Lane6/Lane7 passthrough to be blocked.");
        }

        if (!request.Descriptor.HasCapability(NestedCapabilityGrantMask.ExitMapping))
        {
            return NestedCompletionMappingResult.Denied(
                NestedCompletionMappingDecision.CapabilityDenied,
                VmExitReason.SecurityPolicyViolation,
                request.Completion.Sequence,
                "Nested completion mapping requires the nested exit-mapping capability grant.");
        }

        if (request.RequiresCompatibilityProjection &&
            (request.RouteDescriptor is null ||
             !request.RouteDescriptor.CanProjectToCompatibilityFrontend))
        {
            return NestedCompletionMappingResult.Denied(
                NestedCompletionMappingDecision.CompatibilityProjectionDenied,
                VmExitReason.SecurityPolicyViolation,
                request.Completion.RouteId,
                "Nested completion mapping requires descriptor-authorized compatibility projection.");
        }

        return NestedCompletionMappingResult.Allowed(request.Completion);
    }

    public bool CanMap(NestedCompletionMappingRequest request) =>
        Validate(request).IsAllowed;
}
