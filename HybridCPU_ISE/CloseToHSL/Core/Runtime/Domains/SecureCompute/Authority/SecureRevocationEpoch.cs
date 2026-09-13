namespace YAKSys_Hybrid_CPU.Core;

public readonly partial record struct SecureRevocationEpoch(ulong Current)
{
    public static SecureRevocationEpoch Unmaterialized { get; } = new(0);

    public bool IsMaterialized => Current != 0;

    public bool IsCurrent(ulong candidateEpoch) =>
        IsMaterialized && candidateEpoch == Current;

    public bool IsStale(ulong candidateEpoch) =>
        !IsCurrent(candidateEpoch);
}
