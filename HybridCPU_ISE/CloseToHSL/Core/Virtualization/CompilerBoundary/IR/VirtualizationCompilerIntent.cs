namespace YAKSys_Hybrid_CPU.Core;

public enum VirtualizationCompilerIntentKind : byte
{
    None = 0,
    CompatibilityFrontendOperation = 1,
    DescriptorProjection = 2,
    DirectSubstrateMutation = 3,
    HostEvidencePublication = 4,
    NativeTokenPublication = 5,
}

public enum VirtualizationCompilerIntentDecision : byte
{
    Allowed = 0,
    MissingIntent = 1,
    DeniedNonCompatibilityFrontend = 2,
    DeniedDirectSubstrateMutation = 3,
    DeniedHostEvidenceIntent = 4,
    DeniedNativeTokenIntent = 5,
    DeniedUnvalidatedDescriptor = 6,
}

public readonly record struct VirtualizationCompilerIntentRequest(
    VirtualizationCompilerIntentKind IntentKind,
    bool IsCompatibilityFrontend,
    bool DescriptorValidated,
    bool EmitsHostOwnedEvidence,
    bool EmitsNativeLaneToken,
    bool AttemptsDirectSubstrateMutation);

public readonly record struct VirtualizationCompilerIntentResult(
    VirtualizationCompilerIntentDecision Decision,
    string Reason)
{
    public bool IsAllowed => Decision == VirtualizationCompilerIntentDecision.Allowed;

    public static VirtualizationCompilerIntentResult Allowed { get; } =
        new(VirtualizationCompilerIntentDecision.Allowed, string.Empty);

    public static VirtualizationCompilerIntentResult Denied(
        VirtualizationCompilerIntentDecision decision,
        string reason) =>
        new(decision, reason);
}

public sealed partial class VirtualizationCompilerIntent
{
    public VirtualizationCompilerIntentResult Evaluate(VirtualizationCompilerIntentRequest request)
    {
        if (request.IntentKind == VirtualizationCompilerIntentKind.None)
        {
            return VirtualizationCompilerIntentResult.Denied(
                VirtualizationCompilerIntentDecision.MissingIntent,
                "Virtualization compiler intent must be explicit.");
        }

        if (!request.IsCompatibilityFrontend)
        {
            return VirtualizationCompilerIntentResult.Denied(
                VirtualizationCompilerIntentDecision.DeniedNonCompatibilityFrontend,
                "VMX compiler intent may only target the compatibility frontend boundary.");
        }

        if (!request.DescriptorValidated)
        {
            return VirtualizationCompilerIntentResult.Denied(
                VirtualizationCompilerIntentDecision.DeniedUnvalidatedDescriptor,
                "Virtualization compiler intent requires descriptor validation.");
        }

        if (request.AttemptsDirectSubstrateMutation ||
            request.IntentKind == VirtualizationCompilerIntentKind.DirectSubstrateMutation)
        {
            return VirtualizationCompilerIntentResult.Denied(
                VirtualizationCompilerIntentDecision.DeniedDirectSubstrateMutation,
                "Compiler intent cannot directly mutate authoritative substrate state.");
        }

        if (request.EmitsHostOwnedEvidence ||
            request.IntentKind == VirtualizationCompilerIntentKind.HostEvidencePublication)
        {
            return VirtualizationCompilerIntentResult.Denied(
                VirtualizationCompilerIntentDecision.DeniedHostEvidenceIntent,
                "Compiler intent cannot emit host-owned evidence.");
        }

        if (request.EmitsNativeLaneToken ||
            request.IntentKind == VirtualizationCompilerIntentKind.NativeTokenPublication)
        {
            return VirtualizationCompilerIntentResult.Denied(
                VirtualizationCompilerIntentDecision.DeniedNativeTokenIntent,
                "Compiler intent cannot emit native lane tokens.");
        }

        return VirtualizationCompilerIntentResult.Allowed;
    }
}
