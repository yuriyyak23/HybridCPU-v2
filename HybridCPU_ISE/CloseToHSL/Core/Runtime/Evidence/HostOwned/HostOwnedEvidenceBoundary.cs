namespace YAKSys_Hybrid_CPU.Core;

public enum HostOwnedEvidenceBoundaryDecision : byte
{
    Allowed = 0,
    GuestProjectionDenied = 1,
    HostCaptureDenied = 2,
    RestoreRequiresRecompute = 3,
    RestoreDenied = 4,
}

public readonly record struct HostOwnedEvidenceBoundaryResult(
    HostOwnedEvidenceBoundaryDecision Decision,
    string Reason)
{
    public bool IsAllowed => Decision == HostOwnedEvidenceBoundaryDecision.Allowed;

    public static HostOwnedEvidenceBoundaryResult Allowed { get; } =
        new(HostOwnedEvidenceBoundaryDecision.Allowed, string.Empty);

    public static HostOwnedEvidenceBoundaryResult Denied(
        HostOwnedEvidenceBoundaryDecision decision,
        string reason) =>
        new(decision, reason);
}

public sealed partial class HostOwnedEvidenceBoundary
{
    public HostOwnedEvidenceBoundaryResult ValidateGuestProjection(
        EvidencePolicyDescriptor evidencePolicy,
        ObservabilityDescriptor observability,
        EvidenceVisibilityClass evidenceClass)
    {
        if (!evidencePolicy.CanExposeToGuest(evidenceClass))
        {
            return HostOwnedEvidenceBoundaryResult.Denied(
                HostOwnedEvidenceBoundaryDecision.GuestProjectionDenied,
                "Evidence policy does not permit guest projection for this evidence class.");
        }

        if (!observability.CanPublishToGuest(evidenceClass))
        {
            return HostOwnedEvidenceBoundaryResult.Denied(
                HostOwnedEvidenceBoundaryDecision.GuestProjectionDenied,
                "Observability policy does not permit guest publication for this evidence class.");
        }

        return HostOwnedEvidenceBoundaryResult.Allowed;
    }

    public HostOwnedEvidenceBoundaryResult ValidateHostLocalCapture(
        ObservabilityDescriptor observability,
        EvidenceVisibilityClass evidenceClass)
    {
        if (!observability.CanCaptureHostLocal(evidenceClass))
        {
            return HostOwnedEvidenceBoundaryResult.Denied(
                HostOwnedEvidenceBoundaryDecision.HostCaptureDenied,
                "Observability policy does not permit host-local capture for this evidence class.");
        }

        return HostOwnedEvidenceBoundaryResult.Allowed;
    }

    public HostOwnedEvidenceBoundaryResult ValidateRestore(
        EvidencePolicyDescriptor evidencePolicy,
        EvidenceVisibilityClass evidenceClass,
        EvidenceRestorePolicy restorePolicy)
    {
        if (evidencePolicy.MustRecomputeAfterRestore(evidenceClass))
        {
            return HostOwnedEvidenceBoundaryResult.Denied(
                HostOwnedEvidenceBoundaryDecision.RestoreRequiresRecompute,
                "Host-owned evidence must be recomputed after restore.");
        }

        if (!evidencePolicy.CanSerializeAcrossMigration(evidenceClass, restorePolicy))
        {
            return HostOwnedEvidenceBoundaryResult.Denied(
                HostOwnedEvidenceBoundaryDecision.RestoreDenied,
                "Evidence policy does not permit migration restore for this evidence class.");
        }

        return HostOwnedEvidenceBoundaryResult.Allowed;
    }
}
