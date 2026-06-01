namespace YAKSys_Hybrid_CPU.Core;

public enum SecureGrantHandleKind : byte
{
    None = 0,
    DomainIdentity = 1,
    MemoryPolicy = 2,
    EvidencePolicy = 3,
    MigrationPolicy = 4,
    IoPolicy = 5,
    HypercallPolicy = 6,
    DebugPolicy = 7,
    CompatibilityProjectionPolicy = 8,
}

public readonly partial record struct SecureGrantHandle(
    SecureGrantHandleKind Kind,
    ulong LocalId,
    ulong ProvenanceHash,
    ulong Epoch)
{
    public static SecureGrantHandle None { get; } = new(SecureGrantHandleKind.None, 0, 0, 0);

    public bool HasScalarShape =>
        Kind != SecureGrantHandleKind.None &&
        LocalId != 0 &&
        Epoch != 0;

    public bool HasProvenance =>
        ProvenanceHash != 0;

    public bool IsMaterialized =>
        HasScalarShape &&
        HasProvenance;

    public bool MatchesEpoch(SecureRevocationEpoch epoch) =>
        IsMaterialized && epoch.IsCurrent(Epoch);

    public static bool TryMaterializeFromGuestScalar(
        ulong guestVisibleScalar,
        out SecureGrantHandle handle)
    {
        _ = guestVisibleScalar;
        handle = None;
        return false;
    }
}
