namespace YAKSys_Hybrid_CPU.Core;

public sealed partial class EvidenceSidebandEnvelope
{
    public EvidenceSidebandEnvelope()
        : this(
            subjectId: 0,
            sequence: 0,
            payloadHash: 0,
            visibilityClass: EvidenceVisibilityClass.HostOwnedRuntimeEvidence,
            restorePolicy: EvidenceRestorePolicy.RecomputeAfterRestore,
            isHostOwned: true)
    {
    }

    public EvidenceSidebandEnvelope(
        ulong subjectId,
        ulong sequence,
        ulong payloadHash,
        EvidenceVisibilityClass visibilityClass,
        EvidenceRestorePolicy restorePolicy,
        bool isHostOwned)
    {
        SubjectId = subjectId;
        Sequence = sequence;
        PayloadHash = payloadHash;
        VisibilityClass = visibilityClass;
        RestorePolicy = restorePolicy;
        IsHostOwned = isHostOwned;
    }

    public static EvidenceSidebandEnvelope Empty { get; } = new();

    public ulong SubjectId { get; }

    public ulong Sequence { get; }

    public ulong PayloadHash { get; }

    public EvidenceVisibilityClass VisibilityClass { get; }

    public EvidenceRestorePolicy RestorePolicy { get; }

    public bool IsHostOwned { get; }

    public bool HasPayload => PayloadHash != 0;

    public bool RequiresHostHandling =>
        IsHostOwned ||
        VisibilityClass is EvidenceVisibilityClass.HostOwnedRuntimeEvidence
            or EvidenceVisibilityClass.SchedulerEvidence
            or EvidenceVisibilityClass.BackendBindingEvidence
            or EvidenceVisibilityClass.NativeTokenEvidence;

    public bool MustRecomputeAfterRestore(EvidencePolicyDescriptor evidencePolicy) =>
        evidencePolicy is null ||
        evidencePolicy.MustRecomputeAfterRestore(VisibilityClass) ||
        RestorePolicy == EvidenceRestorePolicy.RecomputeAfterRestore;

    public bool CanExposeToGuest(EvidencePolicyDescriptor evidencePolicy) =>
        !IsHostOwned &&
        evidencePolicy is not null &&
        evidencePolicy.CanExposeToGuest(VisibilityClass);
}
