using HybridCPU_ISE.Arch;
using YAKSys_Hybrid_CPU.Arch;

namespace YAKSys_Hybrid_CPU.Core.Decoder;

/// <summary>
/// Retained public exception contract for RF-05. This adapter is intentionally
/// narrow: it converts only expected typed decode rejections. Programming and
/// invariant exceptions pass through untouched.
/// </summary>
internal static class DecodeFailureCompatibilityAdapter
{
    internal static InvalidOpcodeException ToInvalidOpcodeException(
        DecodeFailure failure,
        ReadOnlySpan<VLIW_Instruction> rawSlots)
    {
        ArgumentNullException.ThrowIfNull(failure);
        string opcodeIdentifier = failure.Field;
        if ((uint)failure.SlotIndex < (uint)rawSlots.Length)
        {
            opcodeIdentifier = OpcodeRegistry.GetMnemonicOrHex(rawSlots[failure.SlotIndex].OpCode);
        }

        ushort opcode = (uint)failure.SlotIndex < (uint)rawSlots.Length
            ? checked((ushort)rawSlots[failure.SlotIndex].OpCode)
            : (ushort)0;
        return Create(failure, opcodeIdentifier, opcode);
    }

    internal static InvalidOpcodeException ToInvalidOpcodeException(
        DecodeFailure failure,
        in VLIW_Instruction rawSlot)
    {
        ArgumentNullException.ThrowIfNull(failure);
        ushort opcode = checked((ushort)rawSlot.OpCode);
        return Create(failure, OpcodeRegistry.GetMnemonicOrHex(opcode), opcode);
    }

    private static InvalidOpcodeException Create(
        DecodeFailure failure,
        string opcodeIdentifier,
        ushort opcode)
    {
        bool isProhibited = failure.Code == DecodeFailureCode.ProhibitedOpcode ||
                            string.Equals(failure.Field, "Word3[50]", StringComparison.Ordinal);
        string message = failure.Code switch
        {
            DecodeFailureCode.ProhibitedOpcode =>
                $"Opcode '{opcodeIdentifier}' (slot {failure.SlotIndex}) is not part of ISA v4 canonical surface. " +
                "Pseudo-ops and hint opcodes must not appear in the hardware instruction stream. See IsaV4Surface.ProhibitedOpcodes.",
            DecodeFailureCode.UnsupportedOpcode =>
                $"Opcode '{opcode}' (slot {failure.SlotIndex}) uses unsupported optional scalar contour. {failure.Message}",
            DecodeFailureCode.ReservedEncoding when string.Equals(failure.Field, "Reserved", StringComparison.Ordinal) =>
                VLIW_Instruction.GetReservedWord0ViolationMessage(failure.SlotIndex),
            DecodeFailureCode.Sideband when opcode == Processor.CPU_Core.IsaOpcodeValues.DmaStreamCompute &&
                                               string.Equals(failure.Field, "DmaStreamComputeDescriptor", StringComparison.Ordinal) =>
                $"Slot {failure.SlotIndex}: DmaStreamCompute native decode requires typed decoded sideband descriptor payload.",
            DecodeFailureCode.Sideband when opcode == Processor.CPU_Core.IsaOpcodeValues.DmaStreamCompute &&
                                               string.Equals(failure.Field, "slot", StringComparison.Ordinal) =>
                $"Slot {failure.SlotIndex}: DmaStreamCompute is a lane6 DMA/stream instruction and cannot decode on any other VLIW slot.",
            _ => failure.Message,
        };
        return new InvalidOpcodeException(message, opcodeIdentifier, failure.SlotIndex, isProhibited);
    }
}
