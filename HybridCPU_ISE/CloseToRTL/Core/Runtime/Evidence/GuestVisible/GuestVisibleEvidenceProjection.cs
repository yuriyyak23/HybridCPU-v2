namespace YAKSys_Hybrid_CPU.Core;

public enum GuestVisibleEvidenceProjectionDecision : byte
{
    Allowed = 0,
    DeniedByEvidencePolicy = 1,
    DeniedByObservabilityPolicy = 2,
    DeniedHostOwnedEvidence = 3,
    DeniedEmptyPayload = 4,
}

public readonly record struct GuestVisibleEvidenceRecord(
    ulong SubjectId,
    ulong Sequence,
    ulong PayloadHash,
    EvidenceVisibilityClass VisibilityClass);

public sealed partial class GuestVisibleEvidenceProjection
{
    public GuestVisibleEvidenceProjectionDecision Evaluate(
        EvidenceSidebandEnvelope envelope,
        EvidencePolicyDescriptor evidencePolicy,
        ObservabilityDescriptor observability)
    {
        if (envelope is null || !envelope.HasPayload)
        {
            return GuestVisibleEvidenceProjectionDecision.DeniedEmptyPayload;
        }

        if (envelope.IsHostOwned || envelope.RequiresHostHandling)
        {
            return GuestVisibleEvidenceProjectionDecision.DeniedHostOwnedEvidence;
        }

        if (evidencePolicy is null || !evidencePolicy.CanExposeToGuest(envelope.VisibilityClass))
        {
            return GuestVisibleEvidenceProjectionDecision.DeniedByEvidencePolicy;
        }

        if (observability is null || !observability.CanPublishToGuest(envelope.VisibilityClass))
        {
            return GuestVisibleEvidenceProjectionDecision.DeniedByObservabilityPolicy;
        }

        return GuestVisibleEvidenceProjectionDecision.Allowed;
    }

    public bool TryProject(
        EvidenceSidebandEnvelope envelope,
        EvidencePolicyDescriptor evidencePolicy,
        ObservabilityDescriptor observability,
        out GuestVisibleEvidenceRecord record,
        out GuestVisibleEvidenceProjectionDecision decision)
    {
        decision = Evaluate(envelope, evidencePolicy, observability);
        if (decision != GuestVisibleEvidenceProjectionDecision.Allowed)
        {
            record = default;
            return false;
        }

        record = new GuestVisibleEvidenceRecord(
            envelope.SubjectId,
            envelope.Sequence,
            envelope.PayloadHash,
            envelope.VisibilityClass);
        return true;
    }
}
