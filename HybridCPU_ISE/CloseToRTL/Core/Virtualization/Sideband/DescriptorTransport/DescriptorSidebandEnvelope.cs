namespace YAKSys_Hybrid_CPU.Core;

public enum DescriptorSidebandKind : byte
{
    None = 0,
    DomainDescriptor = 1,
    CapabilityDescriptor = 2,
    EvidencePolicy = 3,
    ObservabilityDescriptor = 4,
    MigrationDescriptor = 5,
    LaneDescriptor = 6,
    CompatibilityProjection = 7,
}

public sealed partial class DescriptorSidebandEnvelope
{
    public DescriptorSidebandEnvelope()
        : this(
            DescriptorSidebandKind.None,
            descriptorId: 0,
            descriptorEpoch: 0,
            descriptorHash: 0,
            isValidated: false,
            visibilityClass: EvidenceVisibilityClass.HostOwnedRuntimeEvidence)
    {
    }

    public DescriptorSidebandEnvelope(
        DescriptorSidebandKind kind,
        ulong descriptorId,
        ulong descriptorEpoch,
        ulong descriptorHash,
        bool isValidated,
        EvidenceVisibilityClass visibilityClass)
    {
        Kind = kind;
        DescriptorId = descriptorId;
        DescriptorEpoch = descriptorEpoch;
        DescriptorHash = descriptorHash;
        IsValidated = isValidated;
        VisibilityClass = visibilityClass;
    }

    public static DescriptorSidebandEnvelope Empty { get; } = new();

    public DescriptorSidebandKind Kind { get; }

    public ulong DescriptorId { get; }

    public ulong DescriptorEpoch { get; }

    public ulong DescriptorHash { get; }

    public bool IsValidated { get; }

    public EvidenceVisibilityClass VisibilityClass { get; }

    public bool HasDescriptor => Kind != DescriptorSidebandKind.None && DescriptorHash != 0;

    public bool IsCompatibilityProjection =>
        Kind == DescriptorSidebandKind.CompatibilityProjection;

    public bool RequiresHostHandling =>
        !IsValidated ||
        VisibilityClass is EvidenceVisibilityClass.HostOwnedRuntimeEvidence
            or EvidenceVisibilityClass.SchedulerEvidence
            or EvidenceVisibilityClass.BackendBindingEvidence
            or EvidenceVisibilityClass.NativeTokenEvidence;

    public bool CanExposeToGuest(EvidencePolicyDescriptor evidencePolicy) =>
        IsValidated &&
        evidencePolicy is not null &&
        evidencePolicy.CanExposeToGuest(VisibilityClass);
}
