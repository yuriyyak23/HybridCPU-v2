namespace YAKSys_Hybrid_CPU.Core;

public enum RetireEvidenceDecision : byte
{
    Allowed = 0,
    InvalidRetireEffect = 1,
    GuestPublicationDenied = 2,
    HostCaptureDenied = 3,
    HostOwnedEvidenceDenied = 4,
}

public readonly record struct RetireEvidenceResult(
    RetireEvidenceDecision Decision,
    string Reason)
{
    public bool IsAllowed => Decision == RetireEvidenceDecision.Allowed;

    public static RetireEvidenceResult Allowed { get; } =
        new(RetireEvidenceDecision.Allowed, string.Empty);

    public static RetireEvidenceResult Denied(
        RetireEvidenceDecision decision,
        string reason) =>
        new(decision, reason);
}

public readonly record struct RetireEvidencePublicationRequest(
    VmxRetireEffect RetireEffect,
    EvidenceVisibilityClass EvidenceClass,
    bool GuestVisiblePublication);

public sealed partial class RetireEvidenceBoundary
{
    public RetireEvidenceResult ValidatePublication(
        RetireEvidencePublicationRequest request,
        EvidencePolicyDescriptor evidencePolicy,
        ObservabilityDescriptor observability)
    {
        if (!request.RetireEffect.IsValid)
        {
            return RetireEvidenceResult.Denied(
                RetireEvidenceDecision.InvalidRetireEffect,
                "Retire evidence publication requires a valid retire effect.");
        }

        return request.GuestVisiblePublication
            ? ValidateGuestPublication(request.EvidenceClass, evidencePolicy, observability)
            : ValidateHostLocalCapture(request.EvidenceClass, observability);
    }

    private static RetireEvidenceResult ValidateGuestPublication(
        EvidenceVisibilityClass evidenceClass,
        EvidencePolicyDescriptor evidencePolicy,
        ObservabilityDescriptor observability)
    {
        if (evidencePolicy.MustRecomputeAfterRestore(evidenceClass))
        {
            return RetireEvidenceResult.Denied(
                RetireEvidenceDecision.HostOwnedEvidenceDenied,
                "Host-owned retire evidence cannot be published as guest-visible state.");
        }

        if (!evidencePolicy.CanExposeToGuest(evidenceClass))
        {
            return RetireEvidenceResult.Denied(
                RetireEvidenceDecision.GuestPublicationDenied,
                "Evidence policy does not permit guest-visible retire publication.");
        }

        if (!observability.CanPublishToGuest(evidenceClass))
        {
            return RetireEvidenceResult.Denied(
                RetireEvidenceDecision.GuestPublicationDenied,
                "Observability policy does not permit guest-visible retire publication.");
        }

        return RetireEvidenceResult.Allowed;
    }

    private static RetireEvidenceResult ValidateHostLocalCapture(
        EvidenceVisibilityClass evidenceClass,
        ObservabilityDescriptor observability)
    {
        if (!observability.CanCaptureHostLocal(evidenceClass))
        {
            return RetireEvidenceResult.Denied(
                RetireEvidenceDecision.HostCaptureDenied,
                "Observability policy does not permit host-local retire capture.");
        }

        return RetireEvidenceResult.Allowed;
    }
}
