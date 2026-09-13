using YAKSys_Hybrid_CPU.Core.Nested;
using YAKSys_Hybrid_CPU.Core.Vmcs.V2;

namespace YAKSys_Hybrid_CPU.Core;

public enum VmxFrontendResultKind : byte
{
    Success = 0,
    VmFailValid = 1,
    VmFailInvalid = 2,
    VmExit = 3,
    VmAbort = 4,
}

public readonly record struct VmxFrontendResult(
    VmxFrontendResultKind Kind,
    VmxCompletionKind CompletionKind,
    VmFailCode FailCode,
    VmAbortCode AbortCode,
    string Reason)
{
    public bool Succeeded => Kind == VmxFrontendResultKind.Success;

    public static VmxFrontendResult Success { get; } =
        new(
            VmxFrontendResultKind.Success,
            VmxCompletionKind.Success,
            VmFailCode.None,
            VmAbortCode.None,
            string.Empty);

    public static VmxFrontendResult FailValid(
        VmFailCode failCode,
        string reason) =>
        new(
            VmxFrontendResultKind.VmFailValid,
            VmxCompletionKind.VmFailValid,
            failCode,
            VmAbortCode.None,
            reason);

    public static VmxFrontendResult FailInvalid(
        VmFailCode failCode,
        string reason) =>
        new(
            VmxFrontendResultKind.VmFailInvalid,
            VmxCompletionKind.VmFailInvalid,
            failCode,
            VmAbortCode.None,
            reason);
}

public sealed partial class VmxFrontendResultMapper
{
    public VmxFrontendResult FromNestedValidation(NestedValidationResult validation) =>
        validation.Succeeded
            ? VmxFrontendResult.Success
            : validation.Code switch
            {
                NestedValidationCode.ProjectionDenied =>
                    VmxFrontendResult.FailValid(
                        VmFailCode.VmcsFieldAccessDenied,
                        validation.Message),
                NestedValidationCode.CompatibilityProjectionFailed =>
                    VmxFrontendResult.FailInvalid(
                        VmFailCode.GuestGprPersistenceIncomplete,
                        validation.Message),
                _ =>
                    VmxFrontendResult.FailInvalid(
                        VmFailCode.UnknownVmcsField,
                        validation.Message),
            };
}
