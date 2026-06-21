namespace YAKSys_Hybrid_CPU.Core;

public enum SecureEvidencePublicationDecision : byte
{
    Allowed = 0,
    DeniedMissingSecureEvidencePolicy = 1,
    DeniedMissingNeutralEvidencePolicy = 2,
    DeniedHostOwnedEvidence = 3,
    DeniedRecomputedEvidence = 4,
    DeniedNeutralEvidencePolicy = 5,
    DeniedSecureEvidencePolicy = 6,
    DeniedCompatibilityAliasPolicy = 7,
    DeniedCompletionFence = 8,
    DeniedRetireFence = 9,
    DeniedSidebandVisibility = 10,
    DeniedStaleEvidenceEpoch = 11,
}

public readonly record struct SecureEvidencePublicationResult(
    SecureEvidencePublicationDecision Decision,
    string Reason)
{
    public bool IsAllowed => Decision == SecureEvidencePublicationDecision.Allowed;

    public static SecureEvidencePublicationResult Allowed { get; } =
        new(SecureEvidencePublicationDecision.Allowed, string.Empty);

    public static SecureEvidencePublicationResult Denied(
        SecureEvidencePublicationDecision decision,
        string reason) =>
        new(decision, reason);
}

public sealed partial class SecureEvidencePublicationPolicy
{
    public static SecureEvidencePublicationPolicy Default { get; } = new();

    public SecureEvidencePublicationResult AdmitGuestVisibleEvidence(
        SecureEvidencePolicy? secureEvidencePolicy,
        EvidencePolicyDescriptor? neutralEvidencePolicy,
        SecureEvidenceVisibilityClass evidenceClass)
    {
        SecureEvidencePublicationResult prerequisite =
            ValidateEvidencePolicyPrerequisites(
                secureEvidencePolicy,
                neutralEvidencePolicy,
                evidenceClass);
        if (!prerequisite.IsAllowed)
        {
            return prerequisite;
        }

        EvidenceVisibilityClass neutralClass = evidenceClass == SecureEvidenceVisibilityClass.CompatibilityAlias
            ? EvidenceVisibilityClass.CompatibilityAlias
            : EvidenceVisibilityClass.GuestArchitecturalState;

        if (!neutralEvidencePolicy!.CanExposeToGuest(neutralClass))
        {
            return Deny(
                SecureEvidencePublicationDecision.DeniedNeutralEvidencePolicy,
                "Neutral evidence policy does not allow this evidence visibility class.");
        }

        if (!secureEvidencePolicy!.CanExposeToGuest(neutralEvidencePolicy, evidenceClass))
        {
            return Deny(
                SecureEvidencePublicationDecision.DeniedSecureEvidencePolicy,
                "Secure evidence policy does not allow this evidence visibility class.");
        }

        return SecureEvidencePublicationResult.Allowed;
    }

    public SecureEvidencePublicationResult AdmitCompatibilityAliasEvidence(
        SecureEvidencePolicy? secureEvidencePolicy,
        EvidencePolicyDescriptor? neutralEvidencePolicy,
        SecureCompatibilityProjectionPolicy? compatibilityPolicy,
        ulong aliasBit)
    {
        SecureEvidencePublicationResult visibility = AdmitGuestVisibleEvidence(
            secureEvidencePolicy,
            neutralEvidencePolicy,
            SecureEvidenceVisibilityClass.CompatibilityAlias);
        if (!visibility.IsAllowed)
        {
            return visibility;
        }

        if (compatibilityPolicy?.CanProjectAlias(aliasBit) != true)
        {
            return Deny(
                SecureEvidencePublicationDecision.DeniedCompatibilityAliasPolicy,
                "Compatibility alias evidence requires an explicit read-only compatibility projection policy.");
        }

        return SecureEvidencePublicationResult.Allowed;
    }

    public SecureEvidencePublicationResult AdmitCompletionPublication(
        SecureCompletionPublicationFence? publicationFence)
    {
        if (publicationFence?.CanPublishCompletion != true)
        {
            return Deny(
                SecureEvidencePublicationDecision.DeniedCompletionFence,
                "Secure completion publication requires an explicit completion fence.");
        }

        return SecureEvidencePublicationResult.Allowed;
    }

    public SecureEvidencePublicationResult AdmitRetirePublication(
        SecureCompletionPublicationFence? publicationFence)
    {
        if (publicationFence?.CanPublishRetire != true)
        {
            return Deny(
                SecureEvidencePublicationDecision.DeniedRetireFence,
                "Secure retire publication requires an explicit retire fence.");
        }

        return SecureEvidencePublicationResult.Allowed;
    }

    public SecureEvidencePublicationResult AdmitSidebandEnvelope(
        SecureComputeEvidenceSidebandEnvelope envelope,
        SecureEvidencePolicy? secureEvidencePolicy,
        EvidencePolicyDescriptor? neutralEvidencePolicy,
        SecureCompatibilityProjectionPolicy? compatibilityPolicy = null,
        ulong aliasBit = 0)
    {
        if (envelope.EvidenceClass == SecureComputeEvidenceSidebandClass.CompatibilityAlias)
        {
            SecureEvidencePublicationResult aliasResult = AdmitCompatibilityAliasEvidence(
                secureEvidencePolicy,
                neutralEvidencePolicy,
                compatibilityPolicy,
                aliasBit);
            return aliasResult.IsAllowed
                ? aliasResult
                : Deny(SecureEvidencePublicationDecision.DeniedSidebandVisibility, aliasResult.Reason);
        }

        SecureEvidenceVisibilityClass evidenceClass = envelope.EvidenceClass switch
        {
            SecureComputeEvidenceSidebandClass.GuestVisible =>
                SecureEvidenceVisibilityClass.GuestVisible,
            SecureComputeEvidenceSidebandClass.RecomputeAfterRestore =>
                SecureEvidenceVisibilityClass.RecomputedAfterRestore,
            _ => SecureEvidenceVisibilityClass.HostOwnedQuarantined,
        };

        SecureEvidencePublicationResult result = AdmitGuestVisibleEvidence(
            secureEvidencePolicy,
            neutralEvidencePolicy,
            evidenceClass);
        if (!result.IsAllowed)
        {
            return Deny(
                result.Decision == SecureEvidencePublicationDecision.DeniedHostOwnedEvidence ||
                    result.Decision == SecureEvidencePublicationDecision.DeniedRecomputedEvidence
                        ? result.Decision
                        : SecureEvidencePublicationDecision.DeniedSidebandVisibility,
                result.Reason);
        }

        return SecureEvidencePublicationResult.Allowed;
    }

    public SecureEvidencePublicationResult AdmitReplayOrRestoreEvidence(
        DomainMeasurementDescriptor? measurement,
        SecureRevocationEpoch expectedEpoch)
    {
        if (measurement is null ||
            measurement.State is SecureMeasurementState.Missing
                or SecureMeasurementState.Pending
                or SecureMeasurementState.Stale
                or SecureMeasurementState.Revoked ||
            !measurement.IsMaterialized ||
            !measurement.IsCurrentFor(expectedEpoch))
        {
            return Deny(
                SecureEvidencePublicationDecision.DeniedStaleEvidenceEpoch,
                "Secure evidence replay or restore requires a materialized measurement at the current epoch.");
        }

        return SecureEvidencePublicationResult.Allowed;
    }

    private static SecureEvidencePublicationResult ValidateEvidencePolicyPrerequisites(
        SecureEvidencePolicy? secureEvidencePolicy,
        EvidencePolicyDescriptor? neutralEvidencePolicy,
        SecureEvidenceVisibilityClass evidenceClass)
    {
        if (secureEvidencePolicy is null)
        {
            return Deny(
                SecureEvidencePublicationDecision.DeniedMissingSecureEvidencePolicy,
                "Secure evidence publication requires a secure evidence policy.");
        }

        if (neutralEvidencePolicy is null)
        {
            return Deny(
                SecureEvidencePublicationDecision.DeniedMissingNeutralEvidencePolicy,
                "Secure evidence publication requires a neutral evidence policy.");
        }

        if (secureEvidencePolicy.MustQuarantine(evidenceClass))
        {
            return Deny(
                SecureEvidencePublicationDecision.DeniedHostOwnedEvidence,
                "Host-owned or denied evidence cannot be published to guest-visible state.");
        }

        if (evidenceClass == SecureEvidenceVisibilityClass.RecomputedAfterRestore)
        {
            return Deny(
                SecureEvidencePublicationDecision.DeniedRecomputedEvidence,
                "Recomputed-after-restore evidence cannot be reused as guest-visible publication state.");
        }

        if (evidenceClass is SecureEvidenceVisibilityClass.MigrationSerializable)
        {
            return Deny(
                SecureEvidencePublicationDecision.DeniedSecureEvidencePolicy,
                "Migration-serializable evidence is not a guest-visible publication class.");
        }

        return SecureEvidencePublicationResult.Allowed;
    }

    private static SecureEvidencePublicationResult Deny(
        SecureEvidencePublicationDecision decision,
        string reason) =>
        SecureEvidencePublicationResult.Denied(decision, reason);
}
