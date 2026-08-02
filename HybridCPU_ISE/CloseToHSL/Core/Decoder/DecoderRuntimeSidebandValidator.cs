using HybridCPU_ISE.Arch;
using YAKSys_Hybrid_CPU.Arch;
using YAKSys_Hybrid_CPU.Core.Execution.DmaStreamCompute;
using YAKSys_Hybrid_CPU.Core.Execution.ExternalAccelerators.Auth;
using YAKSys_Hybrid_CPU.Core.Execution.ExternalAccelerators.Descriptors;
using YAKSys_Hybrid_CPU.Core.Pipeline;
using YAKSys_Hybrid_CPU.Core.Pipeline.MicroOps;

namespace YAKSys_Hybrid_CPU.Core.Decoder;

/// <summary>
/// Runtime-only continuation of the decoder boundary. It deliberately runs
/// after static canonical decoding: descriptor guard evidence, reference
/// coherence and admission placement are state-dependent authority and are
/// therefore not <see cref="DecodeFailure"/> outcomes.
/// </summary>
internal static class DecoderRuntimeSidebandValidator
{
    internal static RuntimeSidebandProjection ValidateAndResolve(
        in VLIW_Instruction instruction,
        int slotIndex,
        InstructionSlotMetadata slotMetadata)
    {
        ushort opcode = checked((ushort)instruction.OpCode);
        string opcodeName = OpcodeRegistry.GetMnemonicOrHex(opcode);
        DmaStreamComputeDescriptor? dmaDescriptor = ValidateDmaDescriptor(
            in instruction,
            opcode,
            opcodeName,
            slotIndex,
            slotMetadata);
        AcceleratorCommandDescriptor? acceleratorDescriptor = ValidateAcceleratorDescriptor(
            in instruction,
            opcode,
            opcodeName,
            slotIndex,
            slotMetadata);
        return new RuntimeSidebandProjection(dmaDescriptor, acceleratorDescriptor);
    }

    private static DmaStreamComputeDescriptor? ValidateDmaDescriptor(
        in VLIW_Instruction instruction,
        ushort opcode,
        string opcodeName,
        int slotIndex,
        InstructionSlotMetadata slotMetadata)
    {
        if (opcode != Processor.CPU_Core.IsaOpcodeValues.DmaStreamCompute)
        {
            if (slotMetadata.DmaStreamComputeDescriptor is not null ||
                slotMetadata.DmaStreamComputeDescriptorReference.HasValue)
            {
                throw Failure(
                    $"Slot {slotIndex}: DmaStreamCompute descriptor sideband can only accompany the native DmaStreamCompute opcode.",
                    opcodeName,
                    slotIndex);
            }

            return null;
        }

        DmaStreamComputeDescriptor? descriptor = slotMetadata.DmaStreamComputeDescriptor;
        if (descriptor is null)
        {
            // Static carrier association is expected to have rejected this before
            // this continuation. Keep the guard fail-closed for direct callers.
            throw Failure(
                $"Slot {slotIndex}: DmaStreamCompute requires a typed descriptor sideband.",
                opcodeName,
                slotIndex);
        }

        if (!descriptor.OwnerGuardDecision.IsAllowed)
        {
            throw Failure(
                $"Slot {slotIndex}: DmaStreamCompute descriptor sideband lacks an accepted owner/domain guard decision.",
                opcodeName,
                slotIndex);
        }

        if (slotMetadata.DmaStreamComputeDescriptorReference is { } reference &&
            !reference.Equals(descriptor.DescriptorReference))
        {
            throw Failure(
                $"Slot {slotIndex}: DmaStreamCompute descriptor reference sideband does not match the accepted descriptor payload.",
                opcodeName,
                slotIndex);
        }

        return descriptor;
    }

    private static AcceleratorCommandDescriptor? ValidateAcceleratorDescriptor(
        in VLIW_Instruction instruction,
        ushort opcode,
        string opcodeName,
        int slotIndex,
        InstructionSlotMetadata slotMetadata)
    {
        AcceleratorCommandDescriptor? descriptor = slotMetadata.AcceleratorCommandDescriptor;
        if (!OpcodeRegistry.IsSystemDeviceCommandOpcode(opcode))
        {
            if (descriptor is not null)
            {
                throw Failure(
                    $"Slot {slotIndex}: AcceleratorCommandDescriptor sideband can only accompany the native ACCEL_SUBMIT L7-SDC opcode.",
                    opcodeName,
                    slotIndex);
            }

            return null;
        }

        if (opcode != Processor.CPU_Core.IsaOpcodeValues.ACCEL_SUBMIT)
        {
            return null;
        }

        if (descriptor is null)
        {
            // The static pipeline owns this rejection. Retain a fail-closed
            // continuation for callers that bypass it.
            throw Failure(
                "ACCEL_SUBMIT requires typed AcceleratorCommandDescriptor sideband.",
                opcodeName,
                slotIndex);
        }

        if (!AcceleratorOwnerDomainGuard.Default.IsDescriptorGuardBacked(descriptor, out string guardMessage))
        {
            throw Failure(
                $"Slot {slotIndex}: ACCEL_SUBMIT descriptor sideband lacks guard-backed owner/domain acceptance. {guardMessage}",
                opcodeName,
                slotIndex);
        }

        AcceleratorGuardDecision submitGuard = AcceleratorOwnerDomainGuard.Default.EnsureBeforeSubmit(
            descriptor,
            descriptor.OwnerGuardDecision.Evidence);
        if (!submitGuard.IsAllowed)
        {
            throw Failure(
                $"Slot {slotIndex}: ACCEL_SUBMIT admission guard rejected. {submitGuard.Message}",
                opcodeName,
                slotIndex);
        }

        MicroOpAdmissionMetadata admissionMetadata =
            (slotMetadata.SlotMetadata ?? SlotMetadata.Default).AdmissionMetadata;
        if (admissionMetadata.Equals(MicroOpAdmissionMetadata.Default))
        {
            throw Failure(
                $"Slot {slotIndex}: ACCEL_SUBMIT typed sideband requires explicit slot metadata; default admission metadata is not accepted.",
                opcodeName,
                slotIndex);
        }

        SlotPlacementMetadata placement = admissionMetadata.Placement;
        if (placement.RequiredSlotClass != SlotClass.SystemSingleton ||
            placement.PinningKind != SlotPinningKind.HardPinned ||
            placement.PinnedLaneId != 7)
        {
            throw Failure(
                $"Slot {slotIndex}: ACCEL_SUBMIT typed sideband requires SystemSingleton hard-pinned lane7 slot metadata.",
                opcodeName,
                slotIndex);
        }

        return descriptor;
    }

    private static InvalidOpcodeException Failure(string message, string opcodeName, int slotIndex) =>
        new(message, opcodeName, slotIndex, isProhibited: false);
}

internal sealed record RuntimeSidebandProjection(
    DmaStreamComputeDescriptor? DmaStreamComputeDescriptor,
    AcceleratorCommandDescriptor? AcceleratorCommandDescriptor);
