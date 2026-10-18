namespace YAKSys_Hybrid_CPU.Core;

public enum SecureComputeDescriptorSidebandKind : byte
{
    DomainDescriptor = 0,
    MemoryDescriptor = 1,
    EvidencePolicy = 2,
    MigrationDescriptor = 3,
    IoDescriptor = 4,
    HypercallDescriptor = 5,
}

public sealed partial class SecureComputeDescriptorSidebandEnvelope
{
    public SecureComputeDescriptorSidebandEnvelope()
        : this(SecureComputeDescriptorSidebandKind.DomainDescriptor, domainTag: 0, provenanceHash: 0)
    {
    }

    public SecureComputeDescriptorSidebandEnvelope(
        SecureComputeDescriptorSidebandKind kind,
        ulong domainTag,
        ulong provenanceHash)
    {
        Kind = kind;
        DomainTag = domainTag;
        ProvenanceHash = provenanceHash;
    }

    public SecureComputeDescriptorSidebandKind Kind { get; }

    public ulong DomainTag { get; }

    public ulong ProvenanceHash { get; }

    public bool IsAuthorityCarrier => false;
}
