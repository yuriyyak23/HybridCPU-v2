using YAKSys_Hybrid_CPU.Core.Vmcs.V2;

namespace YAKSys_Hybrid_CPU.Core;

public enum VmxCompatRetireDecision : byte
{
    Allowed = 0,
    InvalidRetireEffect = 1,
    DescriptorValidationDenied = 2,
    EvidenceValidationDenied = 3,
    CompletionRouteDenied = 4,
    UntypedFaultDenied = 5,
}

public readonly record struct VmxCompatRetireResult(
    VmxCompatRetireDecision Decision,
    string Reason)
{
    public bool IsAllowed => Decision == VmxCompatRetireDecision.Allowed;

    public static VmxCompatRetireResult Allowed { get; } =
        new(VmxCompatRetireDecision.Allowed, string.Empty);

    public static VmxCompatRetireResult Denied(
        VmxCompatRetireDecision decision,
        string reason) =>
        new(decision, reason);
}

public readonly record struct VmxCompatRetireRequest(
    VmxRetireEffect RetireEffect,
    bool DescriptorValidated,
    bool EvidenceValidated,
    bool CompletionRouteValidated);

public sealed partial class VmxCompatRetireBoundary
{
    public VmxCompatRetireResult ValidatePublication(VmxCompatRetireRequest request)
    {
        if (!request.RetireEffect.IsValid)
        {
            return VmxCompatRetireResult.Denied(
                VmxCompatRetireDecision.InvalidRetireEffect,
                "Compatibility retire publication requires a valid retire effect.");
        }

        if (!request.DescriptorValidated)
        {
            return VmxCompatRetireResult.Denied(
                VmxCompatRetireDecision.DescriptorValidationDenied,
                "Compatibility retire publication requires descriptor validation.");
        }

        if (!request.EvidenceValidated)
        {
            return VmxCompatRetireResult.Denied(
                VmxCompatRetireDecision.EvidenceValidationDenied,
                "Compatibility retire publication requires evidence validation.");
        }

        if (RequiresCompletionRoute(request.RetireEffect) && !request.CompletionRouteValidated)
        {
            return VmxCompatRetireResult.Denied(
                VmxCompatRetireDecision.CompletionRouteDenied,
                "Compatibility retire publication requires validated completion routing.");
        }

        if (request.RetireEffect.IsFaulted && !IsTypedFault(request.RetireEffect.CompletionKind))
        {
            return VmxCompatRetireResult.Denied(
                VmxCompatRetireDecision.UntypedFaultDenied,
                "Faulted VMX retire effects must publish a typed VMFail or VMAbort outcome.");
        }

        return VmxCompatRetireResult.Allowed;
    }

    private static bool RequiresCompletionRoute(VmxRetireEffect effect) =>
        effect.ExitGuestContextOnRetire ||
        effect.CompletionKind == VmxCompletionKind.VmExit;

    private static bool IsTypedFault(VmxCompletionKind completionKind) =>
        completionKind is VmxCompletionKind.VmFailValid
            or VmxCompletionKind.VmFailInvalid
            or VmxCompletionKind.VmAbort;
}
