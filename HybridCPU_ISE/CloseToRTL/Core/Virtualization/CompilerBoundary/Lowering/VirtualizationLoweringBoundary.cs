namespace YAKSys_Hybrid_CPU.Core;

public enum VirtualizationLoweringDecision : byte
{
    Allowed = 0,
    IntentDenied = 1,
    DescriptorValidationDenied = 2,
    CapabilityValidationDenied = 3,
    RuntimeValidationDenied = 4,
    NoEmissionDenied = 5,
    DirectHandlerEmissionDenied = 6,
}

public readonly record struct VirtualizationLoweringRequest(
    VirtualizationCompilerIntentResult IntentResult,
    VirtualizationEmissionDecision EmissionDecision,
    bool DescriptorValidated,
    bool CapabilityValidated,
    bool RuntimeValidated,
    bool EmitsDirectVmxHandler);

public readonly record struct VirtualizationLoweringResult(
    VirtualizationLoweringDecision Decision,
    string Reason)
{
    public bool IsAllowed => Decision == VirtualizationLoweringDecision.Allowed;

    public static VirtualizationLoweringResult Allowed { get; } =
        new(VirtualizationLoweringDecision.Allowed, string.Empty);

    public static VirtualizationLoweringResult Denied(
        VirtualizationLoweringDecision decision,
        string reason) =>
        new(decision, reason);
}

public sealed partial class VirtualizationLoweringBoundary
{
    public VirtualizationLoweringResult ValidateLowering(VirtualizationLoweringRequest request)
    {
        if (!request.IntentResult.IsAllowed)
        {
            return VirtualizationLoweringResult.Denied(
                VirtualizationLoweringDecision.IntentDenied,
                "Virtualization lowering requires an allowed compiler intent.");
        }

        if (!request.DescriptorValidated)
        {
            return VirtualizationLoweringResult.Denied(
                VirtualizationLoweringDecision.DescriptorValidationDenied,
                "Virtualization lowering requires descriptor validation.");
        }

        if (!request.CapabilityValidated)
        {
            return VirtualizationLoweringResult.Denied(
                VirtualizationLoweringDecision.CapabilityValidationDenied,
                "Virtualization lowering requires capability validation.");
        }

        if (!request.RuntimeValidated)
        {
            return VirtualizationLoweringResult.Denied(
                VirtualizationLoweringDecision.RuntimeValidationDenied,
                "Virtualization lowering requires runtime validation.");
        }

        if (request.EmissionDecision != VirtualizationEmissionDecision.AllowedCompatibilityProjection)
        {
            return VirtualizationLoweringResult.Denied(
                VirtualizationLoweringDecision.NoEmissionDenied,
                "Virtualization lowering requires the no-emission regression gate.");
        }

        if (request.EmitsDirectVmxHandler)
        {
            return VirtualizationLoweringResult.Denied(
                VirtualizationLoweringDecision.DirectHandlerEmissionDenied,
                "Virtualization lowering cannot emit direct VMX handler calls.");
        }

        return VirtualizationLoweringResult.Allowed;
    }
}
