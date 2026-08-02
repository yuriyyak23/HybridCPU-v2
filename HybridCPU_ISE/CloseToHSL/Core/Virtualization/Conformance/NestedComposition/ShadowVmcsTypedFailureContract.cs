using YAKSys_Hybrid_CPU.Core.Nested;

namespace YAKSys_Hybrid_CPU.Core;

public enum ShadowVmcsTypedFailureViolation : byte
{
    None = 0,
    MissingFailure = 1,
    NonGenericFailureCode = 2,
    MissingFailureMessage = 3,
}

public sealed partial class ShadowVmcsTypedFailureContract
{
    public ShadowVmcsTypedFailureViolation Evaluate(
        NestedValidationResult validation)
    {
        if (validation.Succeeded)
        {
            return ShadowVmcsTypedFailureViolation.MissingFailure;
        }

        if (validation.Code != NestedValidationCode.CompatibilityProjectionFailed)
        {
            return ShadowVmcsTypedFailureViolation.NonGenericFailureCode;
        }

        return string.IsNullOrWhiteSpace(validation.Message)
            ? ShadowVmcsTypedFailureViolation.MissingFailureMessage
            : ShadowVmcsTypedFailureViolation.None;
    }

    public bool IsSatisfied(NestedValidationResult validation) =>
        Evaluate(validation) == ShadowVmcsTypedFailureViolation.None;
}
