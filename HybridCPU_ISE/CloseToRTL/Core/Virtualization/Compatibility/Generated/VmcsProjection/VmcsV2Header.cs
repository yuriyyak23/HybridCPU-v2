namespace YAKSys_Hybrid_CPU.Core.Vmcs.V2;

public sealed class VmcsV2Header
{
    public const uint CurrentRevisionId = 2;
    public const uint CurrentSizeBytes = 256;
    public const string CompatibilityBaseline = "VMX8";

    public VmcsV2Header(
        uint revisionId = CurrentRevisionId,
        uint sizeBytes = CurrentSizeBytes,
        string compatibilityBaseline = CompatibilityBaseline)
    {
        RevisionId = revisionId;
        SizeBytes = sizeBytes;
        Baseline = compatibilityBaseline;
    }

    public uint RevisionId { get; }

    public uint SizeBytes { get; }

    public string Baseline { get; }

    public bool IsReadOnlyCompatibilityProjection => true;

    public bool IsLaunched => false;

    public ulong InvalidationEpoch => 0;
}
