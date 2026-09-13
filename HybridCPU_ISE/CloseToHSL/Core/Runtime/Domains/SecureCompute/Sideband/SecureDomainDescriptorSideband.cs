namespace YAKSys_Hybrid_CPU.Core;

public enum SecureSidebandVisibility : byte
{
    InternalOnly = 0,
    GuestVisible = 1,
    CompatibilityAlias = 2,
}

public sealed partial class SecureDomainDescriptorSideband
{
    public SecureDomainDescriptorSideband()
        : this(domainTag: 0, visibility: SecureSidebandVisibility.InternalOnly, provenanceHash: 0)
    {
    }

    public SecureDomainDescriptorSideband(
        ulong domainTag,
        SecureSidebandVisibility visibility,
        ulong provenanceHash)
    {
        DomainTag = domainTag;
        Visibility = visibility;
        ProvenanceHash = provenanceHash;
    }

    public ulong DomainTag { get; }

    public SecureSidebandVisibility Visibility { get; }

    public ulong ProvenanceHash { get; }

    public bool IsAuthorityCarrier => false;
}
