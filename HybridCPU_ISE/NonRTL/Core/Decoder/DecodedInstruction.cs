using HybridCPU_ISE.Arch;
using System;
using YAKSys_Hybrid_CPU.Arch;
using YAKSys_Hybrid_CPU.Core.Pipeline;
using YAKSys_Hybrid_CPU.Core.Pipeline.MicroOps;
using YAKSys_Hybrid_CPU.Core.Registers;

namespace YAKSys_Hybrid_CPU.Core.Decoder
{
    /// <summary>
    /// Canonical phase 03 decoder contract for one physical bundle slot.
    /// Carries slot identity plus semantic IR, neutral operand facts, and
    /// optional sideband slot metadata when the frontend provides it.
    /// </summary>
    public sealed record DecodedInstruction : IAbstractBundleSlot
    {
        public DecodedInstruction(
            int slotIndex,
            InstructionIR? instruction,
            InstructionSlotMetadata slotMetadata = default)
        {
            if ((uint)slotIndex >= BundleMetadata.BundleSlotCount)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(slotIndex),
                    slotIndex,
                    $"Slot index must be in [0, {BundleMetadata.BundleSlotCount - 1}].");
            }

            SlotIndex = slotIndex;
            Instruction = instruction;
            SlotMetadata = slotMetadata == default
                ? InstructionSlotMetadata.Default
                : slotMetadata;
        }

        public int SlotIndex { get; }

        public bool IsOccupied => Instruction is not null;

        public InstructionIR? Instruction { get; }

        public InstructionSlotMetadata SlotMetadata { get; }

        public Processor.CPU_Core.IsaOpcode CanonicalOpcode => RequireInstruction().CanonicalOpcode;

        public InstructionClass Class => RequireInstruction().Class;

        public SerializationClass SerializationClass => RequireInstruction().SerializationClass;

        public ArchRegId Rd => ArchRegId.Create(RequireInstruction().Rd);

        public ArchRegId Rs1 => ArchRegId.Create(RequireInstruction().Rs1);

        public ArchRegId Rs2 => ArchRegId.Create(RequireInstruction().Rs2);

        public long Imm => RequireInstruction().Imm;

        public static DecodedInstruction CreateEmpty(
            int slotIndex,
            InstructionSlotMetadata slotMetadata = default)
            => new(slotIndex, instruction: null, slotMetadata);

        public static DecodedInstruction CreateOccupied(
            int slotIndex,
            InstructionIR instruction,
            InstructionSlotMetadata slotMetadata = default)
        {
            ArgumentNullException.ThrowIfNull(instruction);
            return new DecodedInstruction(slotIndex, instruction, slotMetadata);
        }

        public InstructionIR RequireInstruction()
            => Instruction ?? throw new InvalidOperationException(
                $"Bundle slot {SlotIndex} does not contain a decoded instruction.");
    }
}
