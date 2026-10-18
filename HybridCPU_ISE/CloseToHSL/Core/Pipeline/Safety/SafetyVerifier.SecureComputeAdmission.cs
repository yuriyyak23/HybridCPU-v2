using HybridCPU.ExternalRuntime.Contracts;
using HybridCPU_ISE.Arch;
using YAKSys_Hybrid_CPU.Core.Decoder;

namespace YAKSys_Hybrid_CPU.Core;

public enum SecureComputeCanonicalAdmissionDecision : byte
{
    DeniedUnknown = 0,
    DeniedLocalBinding = 1,
    DeniedCanonicalDecode = 2,
    DeniedMachineChainUnavailable = 3,
    DeniedOperationCompleted = 4,
    DeniedBackendCleanupRequired = 5,
}

/// <summary>Denial facts only. No certificate, permit, backend result or publication.</summary>
public sealed class SecureComputeCanonicalAdmissionResult
{
    internal SecureComputeCanonicalAdmissionResult(ExternalSecureRegisterLocalRequest request,
        SecureComputeCanonicalAdmissionDecision decision, CanonicalBundle? canonical)
    {
        Request = request;
        Decision = decision;
        Canonical = canonical;
    }

    public ExternalSecureRegisterLocalRequest Request { get; }
    public SecureComputeCanonicalAdmissionDecision Decision { get; }
    public CanonicalBundle? Canonical { get; }
    public bool IsAllowed => false;

    /// <summary>
    /// Denial with an unresolved provider reservation obligation. The original
    /// request is required for cleanup-only reconciliation, never admission retry.
    /// Canonical diagnostics, if present, confer no issue/publication authority.
    /// </summary>
    public static SecureComputeCanonicalAdmissionResult DeniedBackendCleanupRequired(
        ExternalSecureRegisterLocalRequest request, CanonicalBundle? canonical = null)
    {
        ArgumentNullException.ThrowIfNull(request);
        return new(request, SecureComputeCanonicalAdmissionDecision.DeniedBackendCleanupRequired, canonical);
    }

    public static SecureComputeCanonicalAdmissionResult DeniedLocalBinding(ExternalSecureRegisterLocalRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        return new(request, SecureComputeCanonicalAdmissionDecision.DeniedLocalBinding, null);
    }

    public static SecureComputeCanonicalAdmissionResult DeniedCompletedOperation(ExternalSecureRegisterLocalRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        return new(request, SecureComputeCanonicalAdmissionDecision.DeniedOperationCompleted, null);
    }
}

public partial class SafetyVerifier
{
    /// <summary>
    /// Canonicalizes one bounded neutral intent. ADD remains an ordinary primitive:
    /// it cannot become secure execution without final machine admission/permit.
    /// Input values remain in the original neutral request, never architectural state.
    /// </summary>
    public SecureComputeCanonicalAdmissionResult AdmitSecureRegisterLocal(ExternalSecureRegisterLocalRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        var raw = new VLIW_Instruction[8];
        raw[0] = new VLIW_Instruction
        {
            OpCode = Processor.CPU_Core.IsaOpcodeValues.ADD,
            Word1 = VLIW_Instruction.PackArchRegs(3, 1, 2),
        };
        if (!DeclarativeDecoderPipeline.TryDecodeBundle(raw, null, 0, 0, out var decoded, out _) ||
            decoded is null ||
            decoded.CanonicalBundle.GetSlot(0).SecurityOperationKind != CanonicalSecurityOperationKind.Ordinary)
            return new(request, SecureComputeCanonicalAdmissionDecision.DeniedCanonicalDecode, null);

        return new(request, SecureComputeCanonicalAdmissionDecision.DeniedMachineChainUnavailable,
            decoded.CanonicalBundle);
    }
}
