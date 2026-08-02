namespace YAKSys_Hybrid_CPU.Core;

public enum VectorStreamStateAuthority : byte
{
    RuntimeOwned = 0,
    CompatibilityProjection = 1,
}

public sealed partial class VectorStreamExecutionExtensionDescriptor
{
    public VectorStreamExecutionExtensionDescriptor()
        : this(
            authority: VectorStreamStateAuthority.RuntimeOwned,
            enabled: false,
            allowedSaveMask: VectorStreamSaveMask.None,
            maxVectorLength: 0,
            streamDescriptorTableBase: 0,
            streamDescriptorTableLimit: 0,
            replayEpoch: 0,
            allowsCompatibilityProjection: false)
    {
    }

    public VectorStreamExecutionExtensionDescriptor(
        VectorStreamStateAuthority authority,
        bool enabled,
        VectorStreamSaveMask allowedSaveMask,
        ulong maxVectorLength,
        ulong streamDescriptorTableBase,
        ulong streamDescriptorTableLimit,
        ulong replayEpoch,
        bool allowsCompatibilityProjection)
    {
        Authority = authority;
        Enabled = enabled;
        AllowedSaveMask = allowedSaveMask;
        MaxVectorLength = maxVectorLength;
        StreamDescriptorTableBase = streamDescriptorTableBase;
        StreamDescriptorTableLimit = streamDescriptorTableLimit;
        ReplayEpoch = replayEpoch;
        AllowsCompatibilityProjection = allowsCompatibilityProjection;
    }

    public VectorStreamStateAuthority Authority { get; }

    public bool Enabled { get; }

    public VectorStreamSaveMask AllowedSaveMask { get; }

    public ulong MaxVectorLength { get; }

    public ulong StreamDescriptorTableBase { get; }

    public ulong StreamDescriptorTableLimit { get; }

    public ulong ReplayEpoch { get; }

    public bool AllowsCompatibilityProjection { get; }

    public bool IsRuntimeAuthoritative => Authority == VectorStreamStateAuthority.RuntimeOwned;

    public bool HasBoundStreamDescriptorTable =>
        StreamDescriptorTableLimit > StreamDescriptorTableBase;

    public bool AllowsSaveRestore(VectorStreamSaveMask requestedMask) =>
        IsRuntimeAuthoritative &&
        Enabled &&
        requestedMask != VectorStreamSaveMask.None &&
        (AllowedSaveMask & requestedMask) == requestedMask;

    public bool AllowsVectorLength(ulong vectorLength) =>
        Enabled &&
        MaxVectorLength != 0 &&
        vectorLength <= MaxVectorLength;

    public VectorStreamExecutionExtensionDescriptor WithReplayEpoch(ulong replayEpoch) =>
        new(
            Authority,
            Enabled,
            AllowedSaveMask,
            MaxVectorLength,
            StreamDescriptorTableBase,
            StreamDescriptorTableLimit,
            replayEpoch,
            AllowsCompatibilityProjection);
}
