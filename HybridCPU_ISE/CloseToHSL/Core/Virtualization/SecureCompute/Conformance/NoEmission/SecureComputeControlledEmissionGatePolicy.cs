namespace YAKSys_Hybrid_CPU.Core;

public enum SecureComputeCompilerEmissionPath : byte
{
    NoCompilerChange = 0,
    SecureBackendHelper = 1,
    SecureHypercallHelper = 2,
    SecureSidebandMetadata = 3,
    FutureControlledEmission = 4,
}

public enum SecureComputeControlledEmissionDecision : byte
{
    NoEmissionPreserved = 0,
    DeniedMissingPositiveRuntimeOwner = 1,
    DeniedMissingControlledEmissionRfc = 2,
    DeniedMissingReleaseApproval = 3,
    DeniedBackendExecutionClosed = 4,
    DeniedNewInstructionEncoding = 5,
    DeniedNewOperandFormat = 6,
    DeniedCapabilityAwareLoadStoreFetch = 7,
    DeniedVmxSecureModeEmission = 8,
    DeniedCompilerEmissionFutureGated = 9,
}

public readonly record struct SecureComputeControlledEmissionRequest(
    SecureComputeCompilerEmissionPath Path,
    bool RequestsCompilerEmission,
    bool HasPositiveNeutralRuntimeOwner = false,
    bool HasControlledEmissionRfc = false,
    bool HasReleaseApproval = false,
    bool BackendExecutionAuthorized = false,
    bool EmitsNewInstructionEncoding = false,
    bool EmitsNewOperandFormat = false,
    bool EmitsCapabilityAwareLoadStoreFetch = false,
    bool EmitsVmxSecureMode = false);

public readonly record struct SecureComputeControlledEmissionResult(
    SecureComputeControlledEmissionDecision Decision,
    SecureComputeCompilerEmissionPath Path,
    bool CompilerEmissionAuthorized,
    bool BackendExecutionAuthorized,
    bool NewInstructionEncodingAuthorized,
    bool NewOperandFormatAuthorized,
    bool CapabilityAwareLoadStoreFetchAuthorized,
    bool VmxSecureModeEmissionAuthorized,
    string Reason)
{
    public bool IsAllowed =>
        Decision == SecureComputeControlledEmissionDecision.NoEmissionPreserved;

    public bool CreatesAnyEmissionAuthority =>
        CompilerEmissionAuthorized ||
        BackendExecutionAuthorized ||
        NewInstructionEncodingAuthorized ||
        NewOperandFormatAuthorized ||
        CapabilityAwareLoadStoreFetchAuthorized ||
        VmxSecureModeEmissionAuthorized;

    public static SecureComputeControlledEmissionResult NoEmissionPreserved(
        SecureComputeCompilerEmissionPath path,
        string reason) =>
        new(
            SecureComputeControlledEmissionDecision.NoEmissionPreserved,
            path,
            CompilerEmissionAuthorized: false,
            BackendExecutionAuthorized: false,
            NewInstructionEncodingAuthorized: false,
            NewOperandFormatAuthorized: false,
            CapabilityAwareLoadStoreFetchAuthorized: false,
            VmxSecureModeEmissionAuthorized: false,
            reason);

    public static SecureComputeControlledEmissionResult Denied(
        SecureComputeControlledEmissionDecision decision,
        SecureComputeCompilerEmissionPath path,
        string reason) =>
        new(
            decision,
            path,
            CompilerEmissionAuthorized: false,
            BackendExecutionAuthorized: false,
            NewInstructionEncodingAuthorized: false,
            NewOperandFormatAuthorized: false,
            CapabilityAwareLoadStoreFetchAuthorized: false,
            VmxSecureModeEmissionAuthorized: false,
            reason);
}

public sealed class SecureComputeControlledEmissionGatePolicy
{
    public static SecureComputeControlledEmissionGatePolicy FailClosed { get; } = new();

    public SecureComputeControlledEmissionResult Classify(
        SecureComputeControlledEmissionRequest request)
    {
        SecureComputeControlledEmissionResult noEmissionViolation =
            DenyNoEmissionViolation(request);
        if (!noEmissionViolation.IsAllowed)
        {
            return noEmissionViolation;
        }

        if (!request.RequestsCompilerEmission)
        {
            return SecureComputeControlledEmissionResult.NoEmissionPreserved(
                request.Path,
                "No compiler change is required; SecureCompute compiler no-emission remains closed.");
        }

        if (!request.HasPositiveNeutralRuntimeOwner)
        {
            return Deny(
                SecureComputeControlledEmissionDecision.DeniedMissingPositiveRuntimeOwner,
                request,
                "Controlled emission requires a positive neutral runtime owner first.");
        }

        if (!request.HasControlledEmissionRfc)
        {
            return Deny(
                SecureComputeControlledEmissionDecision.DeniedMissingControlledEmissionRfc,
                request,
                "Controlled emission requires a separate compiler RFC.");
        }

        if (!request.HasReleaseApproval)
        {
            return Deny(
                SecureComputeControlledEmissionDecision.DeniedMissingReleaseApproval,
                request,
                "Controlled emission requires named-path release approval.");
        }

        if (!request.BackendExecutionAuthorized)
        {
            return Deny(
                SecureComputeControlledEmissionDecision.DeniedBackendExecutionClosed,
                request,
                "Controlled emission cannot bypass backend execution closure.");
        }

        return Deny(
            SecureComputeControlledEmissionDecision.DeniedCompilerEmissionFutureGated,
            request,
            "Phase 19 records the controlled-emission gate only; secure compiler emission remains future-gated.");
    }

    private static SecureComputeControlledEmissionResult DenyNoEmissionViolation(
        SecureComputeControlledEmissionRequest request)
    {
        var noEmission = new SecureComputeNoEmissionContract();
        SecureComputeNoEmissionViolation violation = noEmission.Validate(
            request.EmitsNewInstructionEncoding,
            request.EmitsNewOperandFormat,
            request.EmitsCapabilityAwareLoadStoreFetch,
            request.EmitsVmxSecureMode);

        return violation switch
        {
            SecureComputeNoEmissionViolation.NewInstructionEncoding =>
                Deny(
                    SecureComputeControlledEmissionDecision.DeniedNewInstructionEncoding,
                    request,
                    "SecureCompute compiler no-emission denies new instruction encodings."),
            SecureComputeNoEmissionViolation.NewOperandFormat =>
                Deny(
                    SecureComputeControlledEmissionDecision.DeniedNewOperandFormat,
                    request,
                    "SecureCompute compiler no-emission denies new operand formats."),
            SecureComputeNoEmissionViolation.CapabilityAwareLoadStoreFetch =>
                Deny(
                    SecureComputeControlledEmissionDecision.DeniedCapabilityAwareLoadStoreFetch,
                    request,
                    "SecureCompute compiler no-emission denies capability-aware memory instructions."),
            SecureComputeNoEmissionViolation.VmxSecureModeEmission =>
                Deny(
                    SecureComputeControlledEmissionDecision.DeniedVmxSecureModeEmission,
                    request,
                    "SecureCompute compiler no-emission denies VMX secure-mode emission."),
            _ => SecureComputeControlledEmissionResult.NoEmissionPreserved(
                request.Path,
                string.Empty),
        };
    }

    private static SecureComputeControlledEmissionResult Deny(
        SecureComputeControlledEmissionDecision decision,
        SecureComputeControlledEmissionRequest request,
        string reason) =>
        SecureComputeControlledEmissionResult.Denied(
            decision,
            request.Path,
            reason);
}
