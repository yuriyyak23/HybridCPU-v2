namespace YAKSys_Hybrid_CPU.Core;

public enum DomainCheckpointAuthority : byte
{
    DomainDescriptor = 0,
    CompatibilityProjection = 1,
}

public sealed partial class DomainCheckpointImage
{
    public DomainCheckpointImage()
        : this(
            authority: DomainCheckpointAuthority.DomainDescriptor,
            checkpointEpoch: 0,
            payloadMask: 0,
            evidenceMask: 0,
            containsCompatibilityProjectionMetadata: false)
    {
    }

    public DomainCheckpointImage(
        DomainCheckpointAuthority authority,
        ulong checkpointEpoch,
        ulong payloadMask,
        ulong evidenceMask,
        bool containsCompatibilityProjectionMetadata)
    {
        Authority = authority;
        CheckpointEpoch = checkpointEpoch;
        PayloadMask = payloadMask;
        EvidenceMask = evidenceMask;
        ContainsCompatibilityProjectionMetadata = containsCompatibilityProjectionMetadata;
    }

    public static DomainCheckpointImage Empty { get; } = new();

    public DomainCheckpointAuthority Authority { get; }

    public ulong CheckpointEpoch { get; }

    public ulong PayloadMask { get; }

    public ulong EvidenceMask { get; }

    public bool ContainsCompatibilityProjectionMetadata { get; }

    public bool IsDomainAuthoritative => Authority == DomainCheckpointAuthority.DomainDescriptor;

    public bool IsEmpty => PayloadMask == 0 && EvidenceMask == 0;

    public bool ContainsHostOwnedEvidence =>
        IncludesEvidence(EvidenceVisibilityClass.HostOwnedRuntimeEvidence) ||
        IncludesEvidence(EvidenceVisibilityClass.SchedulerEvidence) ||
        IncludesEvidence(EvidenceVisibilityClass.BackendBindingEvidence) ||
        IncludesEvidence(EvidenceVisibilityClass.NativeTokenEvidence);

    public bool IncludesPayload(MigrationPayloadClass payloadClass) =>
        (PayloadMask & ToMask(payloadClass)) != 0;

    public bool IncludesEvidence(EvidenceVisibilityClass evidenceClass) =>
        (EvidenceMask & ToMask(evidenceClass)) != 0;

    public MigrationValidationResult ValidateRestore(
        MigrationValidationPolicy policy,
        EvidenceRestorePolicy restorePolicy)
    {
        if (!IsDomainAuthoritative)
        {
            return MigrationValidationResult.Denied(
                MigrationValidationDecision.PayloadClassDenied,
                "Compatibility projection checkpoint cannot restore authoritative domain state.");
        }

        if (ContainsHostOwnedEvidence)
        {
            return MigrationValidationResult.Denied(
                MigrationValidationDecision.HostOwnedEvidenceRejected,
                "Domain checkpoint image contains host-owned evidence.");
        }

        foreach (MigrationPayloadClass payloadClass in PayloadClasses)
        {
            if (!IncludesPayload(payloadClass))
            {
                continue;
            }

            EvidenceVisibilityClass evidenceClass = ToEvidenceClass(payloadClass);
            MigrationValidationResult result = policy.ValidateImport(
                payloadClass,
                evidenceClass,
                restorePolicy);
            if (!result.IsAllowed)
            {
                return result;
            }
        }

        return MigrationValidationResult.Allowed;
    }

    public DomainCheckpointImage WithPayload(MigrationPayloadClass payloadClass) =>
        new(
            Authority,
            CheckpointEpoch,
            PayloadMask | ToMask(payloadClass),
            EvidenceMask | ToMask(ToEvidenceClass(payloadClass)),
            ContainsCompatibilityProjectionMetadata ||
            payloadClass == MigrationPayloadClass.CompatibilityProjectionMetadata);

    private static ulong ToMask(MigrationPayloadClass payloadClass) =>
        1UL << (byte)payloadClass;

    private static ulong ToMask(EvidenceVisibilityClass evidenceClass) =>
        1UL << (byte)evidenceClass;

    private static EvidenceVisibilityClass ToEvidenceClass(
        MigrationPayloadClass payloadClass) =>
        payloadClass switch
        {
            MigrationPayloadClass.GuestArchitecturalState => EvidenceVisibilityClass.GuestArchitecturalState,
            MigrationPayloadClass.CompatibilityProjectionMetadata => EvidenceVisibilityClass.CompatibilityAlias,
            MigrationPayloadClass.HostOwnedRuntimeEvidence => EvidenceVisibilityClass.HostOwnedRuntimeEvidence,
            MigrationPayloadClass.SchedulerEvidence => EvidenceVisibilityClass.SchedulerEvidence,
            MigrationPayloadClass.BackendBindingEvidence => EvidenceVisibilityClass.BackendBindingEvidence,
            MigrationPayloadClass.NativeTokenEvidence => EvidenceVisibilityClass.NativeTokenEvidence,
            _ => EvidenceVisibilityClass.CompatibilityAlias,
        };

    private static MigrationPayloadClass[] PayloadClasses { get; } =
    {
        MigrationPayloadClass.GuestArchitecturalState,
        MigrationPayloadClass.DomainDescriptorState,
        MigrationPayloadClass.CompatibilityProjectionMetadata,
        MigrationPayloadClass.HostOwnedRuntimeEvidence,
        MigrationPayloadClass.SchedulerEvidence,
        MigrationPayloadClass.BackendBindingEvidence,
        MigrationPayloadClass.NativeTokenEvidence,
    };
}
