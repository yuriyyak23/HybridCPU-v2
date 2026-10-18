namespace YAKSys_Hybrid_CPU.Core;

public enum NestedEvidencePolicyDecision : byte
{
    Allowed = 0,
    MissingDescriptor = 1,
    RuntimeAuthorityRequired = 2,
    HostEvidenceGateDenied = 3,
    HostOwnedEvidenceDenied = 4,
    EvidencePolicyDenied = 5,
    ObservabilityPolicyDenied = 6,
}

public readonly record struct NestedEvidencePolicyRequest(
    NestedDomainDescriptor? Descriptor,
    EvidencePolicyDescriptor? EvidencePolicy,
    ObservabilityDescriptor? Observability,
    EvidenceVisibilityClass EvidenceClass,
    bool RequiresGuestProjection);

public readonly record struct NestedEvidencePolicyResult(
    NestedEvidencePolicyDecision Decision,
    string Reason)
{
    public bool IsAllowed => Decision == NestedEvidencePolicyDecision.Allowed;

    public static NestedEvidencePolicyResult Allowed { get; } =
        new(NestedEvidencePolicyDecision.Allowed, "Nested evidence policy allowed.");

    public static NestedEvidencePolicyResult Denied(
        NestedEvidencePolicyDecision decision,
        string reason) =>
        new(decision, reason);
}

public sealed partial class NestedEvidencePolicy
{
    public NestedEvidencePolicyResult Validate(NestedEvidencePolicyRequest request)
    {
        if (request.Descriptor is null)
        {
            return NestedEvidencePolicyResult.Denied(
                NestedEvidencePolicyDecision.MissingDescriptor,
                "Nested evidence policy requires a nested domain descriptor.");
        }

        if (!request.Descriptor.IsRuntimeAuthoritative)
        {
            return NestedEvidencePolicyResult.Denied(
                NestedEvidencePolicyDecision.RuntimeAuthorityRequired,
                "Nested evidence policy requires runtime-authoritative descriptors.");
        }

        if (!request.Descriptor.HostEvidenceExcluded)
        {
            return NestedEvidencePolicyResult.Denied(
                NestedEvidencePolicyDecision.HostEvidenceGateDenied,
                "Nested domain composition must exclude host-owned evidence.");
        }

        if (IsHostOwnedEvidence(request.EvidenceClass))
        {
            return NestedEvidencePolicyResult.Denied(
                NestedEvidencePolicyDecision.HostOwnedEvidenceDenied,
                "Nested guest projection cannot expose host-owned evidence classes.");
        }

        if (request.RequiresGuestProjection &&
            (request.EvidencePolicy is null ||
             !request.EvidencePolicy.CanExposeToGuest(request.EvidenceClass)))
        {
            return NestedEvidencePolicyResult.Denied(
                NestedEvidencePolicyDecision.EvidencePolicyDenied,
                "Evidence policy denies nested guest projection for this evidence class.");
        }

        if (request.RequiresGuestProjection &&
            (request.Observability is null ||
             !request.Observability.CanPublishToGuest(request.EvidenceClass)))
        {
            return NestedEvidencePolicyResult.Denied(
                NestedEvidencePolicyDecision.ObservabilityPolicyDenied,
                "Observability policy denies nested guest publication for this evidence class.");
        }

        return NestedEvidencePolicyResult.Allowed;
    }

    public bool CanExpose(NestedEvidencePolicyRequest request) =>
        Validate(request).IsAllowed;

    private static bool IsHostOwnedEvidence(EvidenceVisibilityClass evidenceClass) =>
        evidenceClass is EvidenceVisibilityClass.HostOwnedRuntimeEvidence
            or EvidenceVisibilityClass.SchedulerEvidence
            or EvidenceVisibilityClass.BackendBindingEvidence
            or EvidenceVisibilityClass.NativeTokenEvidence;
}
