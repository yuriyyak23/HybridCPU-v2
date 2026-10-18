namespace YAKSys_Hybrid_CPU.Core;

public sealed partial class CompletionSidebandEnvelope
{
    public CompletionSidebandEnvelope()
        : this(
            CompletionRecord.None,
            routeId: 0,
            sequence: 0,
            isHostOwnedEvidence: true,
            visibilityClass: EvidenceVisibilityClass.HostOwnedRuntimeEvidence)
    {
    }

    public CompletionSidebandEnvelope(
        CompletionRecord record,
        ulong routeId,
        ulong sequence,
        bool isHostOwnedEvidence,
        EvidenceVisibilityClass visibilityClass)
    {
        Record = record ?? CompletionRecord.None;
        RouteId = routeId;
        Sequence = sequence;
        IsHostOwnedEvidence = isHostOwnedEvidence;
        VisibilityClass = visibilityClass;
    }

    public static CompletionSidebandEnvelope Empty { get; } = new();

    public CompletionRecord Record { get; }

    public ulong RouteId { get; }

    public ulong Sequence { get; }

    public bool IsHostOwnedEvidence { get; }

    public EvidenceVisibilityClass VisibilityClass { get; }

    public bool HasPayload => !Record.IsEmpty;

    public bool RequiresHostHandling =>
        IsHostOwnedEvidence ||
        VisibilityClass is EvidenceVisibilityClass.HostOwnedRuntimeEvidence
            or EvidenceVisibilityClass.SchedulerEvidence
            or EvidenceVisibilityClass.BackendBindingEvidence
            or EvidenceVisibilityClass.NativeTokenEvidence;

    public bool CanExposeToGuest(EvidencePolicyDescriptor evidencePolicy) =>
        !IsHostOwnedEvidence &&
        evidencePolicy is not null &&
        evidencePolicy.CanExposeToGuest(VisibilityClass);
}
