namespace YAKSys_Hybrid_CPU.Core;

public enum SecureComputeEvidenceSidebandClass : byte
{
    GuestVisible = 0,
    CompatibilityAlias = 1,
    HostOwnedQuarantined = 2,
    RecomputeAfterRestore = 3,
}

public sealed partial class SecureComputeEvidenceSidebandEnvelope
{
    public SecureComputeEvidenceSidebandEnvelope()
        : this(SecureComputeEvidenceSidebandClass.HostOwnedQuarantined, domainTag: 0, evidenceHash: 0)
    {
    }

    public SecureComputeEvidenceSidebandEnvelope(
        SecureComputeEvidenceSidebandClass evidenceClass,
        ulong domainTag,
        ulong evidenceHash)
    {
        EvidenceClass = evidenceClass;
        DomainTag = domainTag;
        EvidenceHash = evidenceHash;
    }

    public SecureComputeEvidenceSidebandClass EvidenceClass { get; }

    public ulong DomainTag { get; }

    public ulong EvidenceHash { get; }

    public bool CanExposeToGuest =>
        EvidenceClass is SecureComputeEvidenceSidebandClass.GuestVisible
            or SecureComputeEvidenceSidebandClass.CompatibilityAlias;

    public bool MustRecomputeAfterRestore =>
        EvidenceClass == SecureComputeEvidenceSidebandClass.RecomputeAfterRestore;
}
