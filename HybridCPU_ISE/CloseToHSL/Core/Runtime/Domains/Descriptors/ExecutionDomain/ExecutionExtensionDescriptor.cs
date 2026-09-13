namespace YAKSys_Hybrid_CPU.Core;

public enum ExecutionExtensionAuthority : byte
{
    RuntimeOwned = 0,
    CompatibilityProjection = 1,
}

public sealed partial class ExecutionExtensionDescriptor
{
    public ExecutionExtensionDescriptor()
        : this(
            authority: ExecutionExtensionAuthority.RuntimeOwned,
            vectorStream: null,
            extensionEpoch: 0,
            compatibilityProjectionEnabled: false)
    {
    }

    public ExecutionExtensionDescriptor(
        ExecutionExtensionAuthority authority,
        VectorStreamExecutionExtensionDescriptor? vectorStream,
        ulong extensionEpoch,
        bool compatibilityProjectionEnabled)
    {
        Authority = authority;
        VectorStream = vectorStream;
        ExtensionEpoch = extensionEpoch;
        CompatibilityProjectionEnabled = compatibilityProjectionEnabled;
    }

    public ExecutionExtensionAuthority Authority { get; }

    public VectorStreamExecutionExtensionDescriptor? VectorStream { get; }

    public ulong ExtensionEpoch { get; }

    public bool CompatibilityProjectionEnabled { get; }

    public bool IsRuntimeAuthoritative => Authority == ExecutionExtensionAuthority.RuntimeOwned;

    public bool HasVectorStream => VectorStream is not null;

    public bool AllowsVectorStreamProjection =>
        IsRuntimeAuthoritative &&
        CompatibilityProjectionEnabled &&
        VectorStream is { AllowsCompatibilityProjection: true };

    public bool CanSaveRestoreVectorStream(VectorStreamSaveMask mask) =>
        IsRuntimeAuthoritative &&
        VectorStream is not null &&
        VectorStream.AllowsSaveRestore(mask);

    public ExecutionExtensionDescriptor WithVectorStream(
        VectorStreamExecutionExtensionDescriptor vectorStream) =>
        new(Authority, vectorStream, ExtensionEpoch, CompatibilityProjectionEnabled);
}
