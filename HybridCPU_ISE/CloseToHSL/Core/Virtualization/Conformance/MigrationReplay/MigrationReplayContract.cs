namespace YAKSys_Hybrid_CPU.Core;

public enum MigrationReplayContractDecision : byte
{
    Allowed = 0,
    MissingGoldenArtifact = 1,
    DescriptorAuthorityDenied = 2,
    MigrationValidationDenied = 3,
    HostOwnedEvidenceReplayDenied = 4,
    NondeterministicReplayDenied = 5,
}

public readonly record struct MigrationReplayContractRequest(
    MigrationPayloadClass PayloadClass,
    EvidenceVisibilityClass EvidenceClass,
    EvidenceRestorePolicy RestorePolicy,
    bool GoldenArtifactCaptured,
    bool DescriptorAuthorityValidated,
    bool MigrationValidationPassed,
    bool DeterministicReplay,
    bool AttemptsHostOwnedEvidenceReplay);

public readonly record struct MigrationReplayContractResult(
    MigrationReplayContractDecision Decision,
    string Reason)
{
    public bool IsAllowed => Decision == MigrationReplayContractDecision.Allowed;

    public static MigrationReplayContractResult Allowed { get; } =
        new(MigrationReplayContractDecision.Allowed, "Migration replay contract allowed.");

    public static MigrationReplayContractResult Denied(
        MigrationReplayContractDecision decision,
        string reason) =>
        new(decision, reason);
}

public sealed partial class MigrationReplayContract
{
    public MigrationReplayContractResult ValidateReplay(
        MigrationReplayContractRequest request)
    {
        if (!request.GoldenArtifactCaptured)
        {
            return MigrationReplayContractResult.Denied(
                MigrationReplayContractDecision.MissingGoldenArtifact,
                "Migration replay requires a golden artifact capture.");
        }

        if (!request.DescriptorAuthorityValidated)
        {
            return MigrationReplayContractResult.Denied(
                MigrationReplayContractDecision.DescriptorAuthorityDenied,
                "Migration replay requires descriptor authority validation.");
        }

        if (!request.MigrationValidationPassed)
        {
            return MigrationReplayContractResult.Denied(
                MigrationReplayContractDecision.MigrationValidationDenied,
                "Migration replay requires migration validation policy approval.");
        }

        if (request.AttemptsHostOwnedEvidenceReplay ||
            IsHostOwnedPayload(request.PayloadClass) ||
            IsHostOwnedEvidence(request.EvidenceClass))
        {
            return MigrationReplayContractResult.Denied(
                MigrationReplayContractDecision.HostOwnedEvidenceReplayDenied,
                "Migration replay cannot import host-owned runtime evidence.");
        }

        if (!request.DeterministicReplay)
        {
            return MigrationReplayContractResult.Denied(
                MigrationReplayContractDecision.NondeterministicReplayDenied,
                "Migration replay must be deterministic across restore.");
        }

        return MigrationReplayContractResult.Allowed;
    }

    public bool CanReplay(MigrationReplayContractRequest request) =>
        ValidateReplay(request).IsAllowed;

    private static bool IsHostOwnedPayload(MigrationPayloadClass payloadClass) =>
        payloadClass is MigrationPayloadClass.HostOwnedRuntimeEvidence
            or MigrationPayloadClass.SchedulerEvidence
            or MigrationPayloadClass.BackendBindingEvidence
            or MigrationPayloadClass.NativeTokenEvidence;

    private static bool IsHostOwnedEvidence(EvidenceVisibilityClass evidenceClass) =>
        evidenceClass is EvidenceVisibilityClass.HostOwnedRuntimeEvidence
            or EvidenceVisibilityClass.SchedulerEvidence
            or EvidenceVisibilityClass.BackendBindingEvidence
            or EvidenceVisibilityClass.NativeTokenEvidence;
}
