namespace YAKSys_Hybrid_CPU.Core;

public readonly partial record struct SecureMeasurementHandle(
    ulong MeasurementId,
    ulong ProvenanceHash,
    ulong Epoch)
{
    public static SecureMeasurementHandle None { get; } = new(0, 0, 0);

    public bool IsMaterialized =>
        MeasurementId != 0 &&
        ProvenanceHash != 0 &&
        Epoch != 0;

    public bool MatchesEpoch(SecureRevocationEpoch epoch) =>
        IsMaterialized && epoch.IsCurrent(Epoch);
}
