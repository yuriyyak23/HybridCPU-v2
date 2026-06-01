namespace YAKSys_Hybrid_CPU.Core;

public enum VectorStreamProjectionDecision : byte
{
    Allowed = 0,
    MissingExecutionExtension = 1,
    MissingVectorStreamDescriptor = 2,
    RuntimeAuthorityMissing = 3,
    CompatibilityProjectionDenied = 4,
    SaveMaskDenied = 5,
    SnapshotStale = 6,
    HostEvidenceDenied = 7,
}

public readonly record struct VectorStreamProjectionResult(
    VectorStreamProjectionDecision Decision,
    VectorStreamSaveMask EffectiveMask,
    string Message)
{
    public bool IsAllowed => Decision == VectorStreamProjectionDecision.Allowed;

    public static VectorStreamProjectionResult Allowed(VectorStreamSaveMask effectiveMask) =>
        new(VectorStreamProjectionDecision.Allowed, effectiveMask, string.Empty);

    public static VectorStreamProjectionResult Denied(
        VectorStreamProjectionDecision decision,
        string message) =>
        new(decision, VectorStreamSaveMask.None, message);
}

public sealed partial class VectorStreamSaveRestoreProjection
{
    public VectorStreamProjectionResult Validate(
        ExecutionExtensionDescriptor? extension,
        VectorStreamSnapshot snapshot,
        VectorStreamSaveMask requestedMask,
        bool compatibilityProjectionRequest)
    {
        if (extension is null)
        {
            return Deny(
                VectorStreamProjectionDecision.MissingExecutionExtension,
                "Vector-stream save/restore requires an execution extension descriptor.");
        }

        if (!extension.IsRuntimeAuthoritative)
        {
            return Deny(
                VectorStreamProjectionDecision.RuntimeAuthorityMissing,
                "Compatibility projection cannot own vector-stream save/restore authority.");
        }

        VectorStreamExecutionExtensionDescriptor? vectorStream = extension.VectorStream;
        if (vectorStream is null)
        {
            return Deny(
                VectorStreamProjectionDecision.MissingVectorStreamDescriptor,
                "Vector-stream save/restore requires a vector-stream descriptor.");
        }

        if (!vectorStream.IsRuntimeAuthoritative)
        {
            return Deny(
                VectorStreamProjectionDecision.RuntimeAuthorityMissing,
                "Vector-stream compatibility projection cannot own architectural state.");
        }

        if (compatibilityProjectionRequest &&
            !extension.AllowsVectorStreamProjection)
        {
            return Deny(
                VectorStreamProjectionDecision.CompatibilityProjectionDenied,
                "Vector-stream compatibility projection is not enabled by descriptor policy.");
        }

        VectorStreamSaveMask effectiveMask = requestedMask & snapshot.SaveMask;
        if (!vectorStream.AllowsSaveRestore(effectiveMask))
        {
            return Deny(
                VectorStreamProjectionDecision.SaveMaskDenied,
                "Vector-stream descriptor denies the requested save/restore mask.");
        }

        if (!vectorStream.AllowsVectorLength(snapshot.VL))
        {
            return Deny(
                VectorStreamProjectionDecision.SaveMaskDenied,
                "Vector-stream descriptor denies the snapshot vector length.");
        }

        if (vectorStream.ReplayEpoch != 0 &&
            snapshot.StreamReplayEpoch != vectorStream.ReplayEpoch)
        {
            return Deny(
                VectorStreamProjectionDecision.SnapshotStale,
                "Vector-stream snapshot replay epoch is stale.");
        }

        return VectorStreamProjectionResult.Allowed(effectiveMask);
    }

    public VectorStreamProjectionResult RestoreTo(
        ExecutionExtensionDescriptor? extension,
        VectorStreamSnapshot snapshot,
        ICanonicalCpuState state,
        VectorStreamSaveMask requestedMask,
        bool compatibilityProjectionRequest)
    {
        VectorStreamProjectionResult result = Validate(
            extension,
            snapshot,
            requestedMask,
            compatibilityProjectionRequest);
        if (!result.IsAllowed)
        {
            return result;
        }

        snapshot.RestoreTo(state, result.EffectiveMask);
        return result;
    }

    private static VectorStreamProjectionResult Deny(
        VectorStreamProjectionDecision decision,
        string message) =>
        VectorStreamProjectionResult.Denied(decision, message);
}
