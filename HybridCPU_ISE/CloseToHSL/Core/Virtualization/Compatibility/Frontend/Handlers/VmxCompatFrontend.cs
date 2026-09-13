namespace YAKSys_Hybrid_CPU.Core;

public enum VmxCompatFrontendAdmissionDecision : byte
{
    Allowed = 0,
    DecodeBoundaryDenied = 1,
    ProjectionBoundaryDenied = 2,
    RetireBoundaryDenied = 3,
    DescriptorValidationDenied = 4,
    CapabilityValidationDenied = 5,
    RuntimeAuthorityDenied = 6,
    DirectVmcsAuthorityDenied = 7,
}

public readonly record struct VmxCompatFrontendAdmissionRequest(
    bool DecodeBoundaryValidated,
    bool ProjectionBoundaryValidated,
    bool RetireBoundaryValidated,
    bool DescriptorValidated,
    bool CapabilityValidated,
    bool RuntimeAuthorityValidated,
    bool AttemptsDirectVmcsAuthority);

public readonly record struct VmxCompatFrontendAdmissionResult(
    VmxCompatFrontendAdmissionDecision Decision,
    string Reason)
{
    public bool IsAllowed => Decision == VmxCompatFrontendAdmissionDecision.Allowed;

    public static VmxCompatFrontendAdmissionResult Allowed { get; } =
        new(VmxCompatFrontendAdmissionDecision.Allowed, "VMX compatibility frontend admission allowed.");

    public static VmxCompatFrontendAdmissionResult Denied(
        VmxCompatFrontendAdmissionDecision decision,
        string reason) =>
        new(decision, reason);
}

public sealed partial class VmxCompatFrontend
{
    public VmxCompatFrontendAdmissionResult ValidateAdmission(
        VmxCompatFrontendAdmissionRequest request)
    {
        if (!request.DecodeBoundaryValidated)
        {
            return VmxCompatFrontendAdmissionResult.Denied(
                VmxCompatFrontendAdmissionDecision.DecodeBoundaryDenied,
                "VMX compatibility frontend requires decode-boundary validation.");
        }

        if (!request.ProjectionBoundaryValidated)
        {
            return VmxCompatFrontendAdmissionResult.Denied(
                VmxCompatFrontendAdmissionDecision.ProjectionBoundaryDenied,
                "VMX compatibility frontend requires projection-boundary validation.");
        }

        if (!request.RetireBoundaryValidated)
        {
            return VmxCompatFrontendAdmissionResult.Denied(
                VmxCompatFrontendAdmissionDecision.RetireBoundaryDenied,
                "VMX compatibility frontend requires retire-boundary validation.");
        }

        if (!request.DescriptorValidated)
        {
            return VmxCompatFrontendAdmissionResult.Denied(
                VmxCompatFrontendAdmissionDecision.DescriptorValidationDenied,
                "VMX compatibility frontend requires descriptor validation.");
        }

        if (!request.CapabilityValidated)
        {
            return VmxCompatFrontendAdmissionResult.Denied(
                VmxCompatFrontendAdmissionDecision.CapabilityValidationDenied,
                "VMX compatibility frontend requires capability validation.");
        }

        if (!request.RuntimeAuthorityValidated)
        {
            return VmxCompatFrontendAdmissionResult.Denied(
                VmxCompatFrontendAdmissionDecision.RuntimeAuthorityDenied,
                "VMX compatibility frontend requires runtime-owned authority validation.");
        }

        if (request.AttemptsDirectVmcsAuthority)
        {
            return VmxCompatFrontendAdmissionResult.Denied(
                VmxCompatFrontendAdmissionDecision.DirectVmcsAuthorityDenied,
                "VMX compatibility frontend cannot make compatibility projection state authoritative.");
        }

        return VmxCompatFrontendAdmissionResult.Allowed;
    }

    public bool CanAdmit(VmxCompatFrontendAdmissionRequest request) =>
        ValidateAdmission(request).IsAllowed;
}
