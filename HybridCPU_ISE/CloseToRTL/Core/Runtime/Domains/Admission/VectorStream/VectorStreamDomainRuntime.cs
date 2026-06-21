namespace YAKSys_Hybrid_CPU.Core;

public enum VectorStreamDomainRuntimeDecision : byte
{
    Allowed = 0,
    MissingExecutionExtension = 1,
    ExtensionAuthorityDenied = 2,
    MissingVectorStreamDescriptor = 3,
    VectorStreamAuthorityDenied = 4,
    VectorStreamDisabled = 5,
    SaveRestoreMaskDenied = 6,
    VectorLengthDenied = 7,
    MissingStreamDescriptorTable = 8,
    CompatibilityProjectionDenied = 9,
}

public readonly record struct VectorStreamDomainRuntimeRequest(
    ExecutionExtensionDescriptor? Extension,
    VectorStreamSaveMask RequiredSaveRestoreMask,
    ulong RequiredVectorLength,
    bool RequiresStreamDescriptorTable,
    bool RequiresCompatibilityProjection);

public readonly record struct VectorStreamDomainRuntimeResult(
    VectorStreamDomainRuntimeDecision Decision,
    string Reason)
{
    public bool IsAllowed => Decision == VectorStreamDomainRuntimeDecision.Allowed;

    public static VectorStreamDomainRuntimeResult Allowed { get; } =
        new(VectorStreamDomainRuntimeDecision.Allowed, "Vector-stream domain runtime admission allowed.");

    public static VectorStreamDomainRuntimeResult Denied(
        VectorStreamDomainRuntimeDecision decision,
        string reason) =>
        new(decision, reason);
}

public sealed partial class VectorStreamDomainRuntime
{
    public VectorStreamDomainRuntimeResult Validate(VectorStreamDomainRuntimeRequest request)
    {
        if (request.Extension is null)
        {
            return VectorStreamDomainRuntimeResult.Denied(
                VectorStreamDomainRuntimeDecision.MissingExecutionExtension,
                "Vector-stream runtime requires an execution-extension descriptor.");
        }

        if (!request.Extension.IsRuntimeAuthoritative)
        {
            return VectorStreamDomainRuntimeResult.Denied(
                VectorStreamDomainRuntimeDecision.ExtensionAuthorityDenied,
                "Execution extension authority must be runtime-owned.");
        }

        VectorStreamExecutionExtensionDescriptor? vectorStream = request.Extension.VectorStream;
        if (vectorStream is null)
        {
            return VectorStreamDomainRuntimeResult.Denied(
                VectorStreamDomainRuntimeDecision.MissingVectorStreamDescriptor,
                "Vector-stream runtime requires a vector-stream descriptor.");
        }

        if (!vectorStream.IsRuntimeAuthoritative)
        {
            return VectorStreamDomainRuntimeResult.Denied(
                VectorStreamDomainRuntimeDecision.VectorStreamAuthorityDenied,
                "Vector-stream state authority must be runtime-owned.");
        }

        if (!vectorStream.Enabled)
        {
            return VectorStreamDomainRuntimeResult.Denied(
                VectorStreamDomainRuntimeDecision.VectorStreamDisabled,
                "Vector-stream runtime is disabled by descriptor.");
        }

        if (request.RequiredSaveRestoreMask != VectorStreamSaveMask.None &&
            !vectorStream.AllowsSaveRestore(request.RequiredSaveRestoreMask))
        {
            return VectorStreamDomainRuntimeResult.Denied(
                VectorStreamDomainRuntimeDecision.SaveRestoreMaskDenied,
                "Vector-stream save/restore mask is denied by descriptor.");
        }

        if (request.RequiredVectorLength != 0 &&
            !vectorStream.AllowsVectorLength(request.RequiredVectorLength))
        {
            return VectorStreamDomainRuntimeResult.Denied(
                VectorStreamDomainRuntimeDecision.VectorLengthDenied,
                "Vector length exceeds descriptor limits.");
        }

        if (request.RequiresStreamDescriptorTable &&
            !vectorStream.HasBoundStreamDescriptorTable)
        {
            return VectorStreamDomainRuntimeResult.Denied(
                VectorStreamDomainRuntimeDecision.MissingStreamDescriptorTable,
                "Vector-stream runtime requires a bound stream descriptor table.");
        }

        if (request.RequiresCompatibilityProjection &&
            !request.Extension.AllowsVectorStreamProjection)
        {
            return VectorStreamDomainRuntimeResult.Denied(
                VectorStreamDomainRuntimeDecision.CompatibilityProjectionDenied,
                "Execution extension denies vector-stream compatibility projection.");
        }

        return VectorStreamDomainRuntimeResult.Allowed;
    }

    public bool CanAdmit(VectorStreamDomainRuntimeRequest request) =>
        Validate(request).IsAllowed;
}
