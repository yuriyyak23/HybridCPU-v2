using static YAKSys_Hybrid_CPU.Processor.CPU_Core;

namespace YAKSys_Hybrid_CPU.Core;

public enum VmxCompatDecodeDecision : byte
{
    Allowed = 0,
    UnknownOpcode = 1,
    DescriptorValidationDenied = 2,
    CapabilityValidationDenied = 3,
    SchedulingValidationDenied = 4,
    NoEmissionValidationDenied = 5,
}

public readonly record struct VmxCompatDecodeRequest(
    ushort Opcode,
    byte Rd,
    byte Rs1,
    byte Rs2,
    bool DescriptorValidated,
    bool CapabilityValidated,
    bool SchedulingValidated,
    bool NoEmissionValidated);

public readonly record struct VmxCompatDecodeResult(
    VmxCompatDecodeDecision Decision,
    VmxInstructionPayload Payload,
    string Reason)
{
    public bool IsAllowed => Decision == VmxCompatDecodeDecision.Allowed;

    public static VmxCompatDecodeResult Allowed(VmxInstructionPayload payload) =>
        new(VmxCompatDecodeDecision.Allowed, payload, string.Empty);

    public static VmxCompatDecodeResult Denied(
        VmxCompatDecodeDecision decision,
        string reason) =>
        new(decision, VmxInstructionPayload.Empty, reason);
}

public sealed partial class VmxCompatDecodeBoundary
{
    public VmxCompatDecodeResult Decode(VmxCompatDecodeRequest request)
    {
        if (!IsFrozenVmxOpcode(request.Opcode))
        {
            return VmxCompatDecodeResult.Denied(
                VmxCompatDecodeDecision.UnknownOpcode,
                "Opcode is not part of the frozen VMX compatibility frontend.");
        }

        if (!request.DescriptorValidated)
        {
            return VmxCompatDecodeResult.Denied(
                VmxCompatDecodeDecision.DescriptorValidationDenied,
                "VMX compatibility decode requires descriptor validation.");
        }

        if (!request.CapabilityValidated)
        {
            return VmxCompatDecodeResult.Denied(
                VmxCompatDecodeDecision.CapabilityValidationDenied,
                "VMX compatibility decode requires capability validation.");
        }

        if (!request.SchedulingValidated)
        {
            return VmxCompatDecodeResult.Denied(
                VmxCompatDecodeDecision.SchedulingValidationDenied,
                "VMX compatibility decode requires scheduling validation.");
        }

        if (!request.NoEmissionValidated)
        {
            return VmxCompatDecodeResult.Denied(
                VmxCompatDecodeDecision.NoEmissionValidationDenied,
                "VMX compatibility decode requires no-emission validation.");
        }

        return VmxCompatDecodeResult.Allowed(
            VmxInstructionPayload.FromDecodedRegisters(
                request.Opcode,
                request.Rd,
                request.Rs1,
                request.Rs2));
    }

    public static bool IsFrozenVmxOpcode(ushort opcode) =>
        opcode is IsaOpcodeValues.VMXON
            or IsaOpcodeValues.VMXOFF
            or IsaOpcodeValues.VMLAUNCH
            or IsaOpcodeValues.VMRESUME
            or IsaOpcodeValues.VMREAD
            or IsaOpcodeValues.VMWRITE
            or IsaOpcodeValues.VMCLEAR
            or IsaOpcodeValues.VMPTRLD
            or IsaOpcodeValues.VMPTRST
            or IsaOpcodeValues.VMCALL
            or IsaOpcodeValues.INVEPT
            or IsaOpcodeValues.INVVPID
            or IsaOpcodeValues.VMFUNC
            or IsaOpcodeValues.VMSAVEX
            or IsaOpcodeValues.VMRESTX;
}
