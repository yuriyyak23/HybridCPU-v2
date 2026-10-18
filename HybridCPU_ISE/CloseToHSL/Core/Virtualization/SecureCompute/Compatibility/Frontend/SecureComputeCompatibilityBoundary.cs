namespace YAKSys_Hybrid_CPU.Core;

public enum SecureComputeCompatibilityOperation : byte
{
    ReadProjection = 0,
    WriteProjection = 1,
    ActivateSecureCompute = 2,
    GrantSecureCompute = 3,
    StoreSecureState = 4,
}

public enum SecureComputeCompatibilityBoundaryDecision : byte
{
    DeniedByDefault = 0,
    AllowedProjectionOnly = 1,
    DeniedVmxActivation = 2,
    DeniedVmxCapsAuthority = 3,
    DeniedVmcsStateStore = 4,
    DeniedWriteMutation = 5,
}

public readonly record struct SecureComputeCompatibilityBoundaryResult(
    SecureComputeCompatibilityBoundaryDecision Decision,
    string Reason)
{
    public bool IsAllowed =>
        Decision == SecureComputeCompatibilityBoundaryDecision.AllowedProjectionOnly;

    public static SecureComputeCompatibilityBoundaryResult AllowedProjectionOnly { get; } =
        new(SecureComputeCompatibilityBoundaryDecision.AllowedProjectionOnly, string.Empty);
}

public sealed partial class SecureComputeCompatibilityBoundary
{
    public SecureComputeCompatibilityBoundaryResult Admit(
        SecureComputeCompatibilityOperation operation,
        bool projectionOnly)
    {
        return operation switch
        {
            SecureComputeCompatibilityOperation.ReadProjection when projectionOnly =>
                SecureComputeCompatibilityBoundaryResult.AllowedProjectionOnly,

            SecureComputeCompatibilityOperation.ActivateSecureCompute =>
                new(
                    SecureComputeCompatibilityBoundaryDecision.DeniedVmxActivation,
                    "VMX cannot activate SecureCompute."),

            SecureComputeCompatibilityOperation.GrantSecureCompute =>
                new(
                    SecureComputeCompatibilityBoundaryDecision.DeniedVmxCapsAuthority,
                    "VmxCaps cannot grant SecureCompute authority."),

            SecureComputeCompatibilityOperation.StoreSecureState =>
                new(
                    SecureComputeCompatibilityBoundaryDecision.DeniedVmcsStateStore,
                    "VMCS cannot store SecureCompute state."),

            SecureComputeCompatibilityOperation.WriteProjection =>
                new(
                    SecureComputeCompatibilityBoundaryDecision.DeniedWriteMutation,
                    "Compatibility writes cannot mutate SecureCompute state."),

            _ => new(
                SecureComputeCompatibilityBoundaryDecision.DeniedByDefault,
                "SecureCompute compatibility operation is denied by default."),
        };
    }
}
