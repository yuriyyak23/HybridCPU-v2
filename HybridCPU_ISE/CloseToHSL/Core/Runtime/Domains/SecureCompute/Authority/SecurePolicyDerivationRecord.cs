namespace YAKSys_Hybrid_CPU.Core;

public enum SecurePolicyDerivationDecision : byte
{
    NotEvaluated = 0,
    AllowedSubset = 1,
    ParentMissing = 2,
    ChildExpandsAuthority = 3,
    EpochMismatch = 4,
    ProvenanceMissing = 5,
}

public readonly partial record struct SecurePolicyDerivationRecord(
    ulong ParentPolicyDigest,
    ulong ChildPolicyDigest,
    ulong ProvenanceHash,
    ulong ParentEpoch,
    ulong ChildEpoch)
{
    public static SecurePolicyDerivationRecord None { get; } = new(0, 0, 0, 0, 0);

    public bool HasProvenance =>
        ParentPolicyDigest != 0 &&
        ChildPolicyDigest != 0 &&
        ProvenanceHash != 0;

    public bool IsSameEpochOrDerived =>
        ParentEpoch != 0 &&
        ChildEpoch >= ParentEpoch;

    public bool MatchesEpoch(SecureRevocationEpoch epoch) =>
        epoch.IsCurrent(ParentEpoch) &&
        epoch.IsCurrent(ChildEpoch);
}
