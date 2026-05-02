using HybridCPU_ISE.Arch;
using System;
using System.Collections.Generic;
using YAKSys_Hybrid_CPU;
using YAKSys_Hybrid_CPU.Arch;
using YAKSys_Hybrid_CPU.Core;
using YAKSys_Hybrid_CPU.Core.Registers;

namespace HybridCPU.Compiler.Core.IR
{
    /// <summary>
    /// Lowers materialized Stage 6 bundles into backend-facing `VLIW_Bundle` structures.
    /// </summary>
    public sealed class HybridCpuBundleLowerer
    {
        /// <summary>
        /// Lowers one bundled IR program into backend `VLIW_Bundle` instances in program order.
        /// </summary>
        public IReadOnlyList<VLIW_Bundle> LowerProgram(IrProgramBundlingResult programBundlingResult)
        {
            ArgumentNullException.ThrowIfNull(programBundlingResult);

            var loweredBundles = new List<VLIW_Bundle>();
            foreach (IrBasicBlockBundlingResult blockResult in programBundlingResult.BlockResults)
            {
                loweredBundles.AddRange(LowerBlock(blockResult));
            }

            return loweredBundles;
        }

        /// <summary>
        /// Emits per-physical-bundle sideband annotations aligned with lowered bundle slots.
        /// </summary>
        public IReadOnlyList<VliwBundleAnnotations> EmitAnnotationsForProgram(
            IrProgramBundlingResult programBundlingResult)
        {
            ArgumentNullException.ThrowIfNull(programBundlingResult);

            var annotations = new List<VliwBundleAnnotations>();
            foreach (IrBasicBlockBundlingResult blockResult in programBundlingResult.BlockResults)
            {
                foreach (IrMaterializedBundle bundle in blockResult.Bundles)
                {
                    annotations.Add(EmitAnnotationsForBundle(bundle));
                }
            }

            return annotations;
        }

        /// <summary>
        /// Lowers one bundled IR basic block into backend `VLIW_Bundle` instances in cycle order.
        /// </summary>
        public IReadOnlyList<VLIW_Bundle> LowerBlock(IrBasicBlockBundlingResult blockBundlingResult)
        {
            ArgumentNullException.ThrowIfNull(blockBundlingResult);

            var loweredBundles = new List<VLIW_Bundle>(blockBundlingResult.Bundles.Count);
            foreach (IrMaterializedBundle bundle in blockBundlingResult.Bundles)
            {
                loweredBundles.Add(LowerBundle(bundle));
            }

            return loweredBundles;
        }

        /// <summary>
        /// Lowers one materialized Stage 6 bundle into a backend `VLIW_Bundle`.
        /// </summary>
        public VLIW_Bundle LowerBundle(IrMaterializedBundle bundle)
        {
            ArgumentNullException.ThrowIfNull(bundle);

            var loweredBundle = new VLIW_Bundle();

            foreach (IrMaterializedBundleSlot slot in bundle.Slots)
            {
                VLIW_Instruction loweredInstruction = slot.Instruction is null
                    ? CreateNopInstruction()
                    : LowerInstruction(slot.Instruction);

                loweredBundle.SetInstruction(slot.SlotIndex, loweredInstruction);
            }

            return loweredBundle;
        }

        /// <summary>
        /// Emits typed-slot facts for a single materialized bundle as a side-channel.
        /// The caller pairs the facts with the lowered <see cref="VLIW_Bundle"/>.
        /// </summary>
        public static TypedSlotBundleFacts EmitFactsForBundle(IrMaterializedBundle bundle)
        {
            ArgumentNullException.ThrowIfNull(bundle);
            return HybridCpuTypedSlotFactsEmitter.EmitFacts(bundle);
        }

        public static VliwBundleAnnotations EmitAnnotationsForBundle(IrMaterializedBundle bundle)
        {
            ArgumentNullException.ThrowIfNull(bundle);

            var slotMetadata = new InstructionSlotMetadata[YAKSys_Hybrid_CPU.Core.BundleMetadata.BundleSlotCount];
            for (int slotIndex = 0; slotIndex < slotMetadata.Length; slotIndex++)
            {
                slotMetadata[slotIndex] = InstructionSlotMetadata.Default;
            }

            foreach (IrMaterializedBundleSlot slot in bundle.Slots)
            {
                if (slot.Instruction is not { } instruction)
                {
                    continue;
                }

                slotMetadata[slot.SlotIndex] = BuildInstructionSlotMetadata(instruction);
            }

            return new VliwBundleAnnotations(slotMetadata);
        }

        private static VLIW_Instruction LowerInstruction(IrInstruction instruction)
        {
            // Preserve only the surviving legacy-compatible VT hint in word3.
            // Stealability policy no longer round-trips through the encoded VLIW payload.
            var loweredInstruction = new VLIW_Instruction
            {
                OpCode = (uint)instruction.Opcode,
                DataTypeValue = instruction.DataType,
                PredicateMask = instruction.PredicateMask,
                Immediate = instruction.Immediate,
                DestSrc1Pointer = GetDestSrc1Pointer(instruction),
                Src2Pointer = GetSrc2Pointer(instruction),
                StreamLength = instruction.StreamLength,
                Stride = instruction.Stride,
                RowStride = instruction.RowStride,
                VirtualThreadId = instruction.Opcode == Processor.CPU_Core.InstructionsEnum.DmaStreamCompute ||
                                  OpcodeRegistry.IsSystemDeviceCommandOpcode((uint)instruction.Opcode)
                    ? (byte)0
                    : instruction.VirtualThreadId,
                Indexed = instruction.Indexed,
                Is2D = instruction.Is2D,
                Reduction = instruction.Reduction,
                TailAgnostic = instruction.TailAgnostic,
                MaskAgnostic = instruction.MaskAgnostic
            };

            return loweredInstruction;
        }

        private static InstructionSlotMetadata BuildInstructionSlotMetadata(
            IrInstruction instruction)
        {
            SlotMetadata slotMetadata = BuildSlotMetadata(instruction);
            var metadata = new InstructionSlotMetadata(
                VtId.Create(instruction.VirtualThreadId),
                slotMetadata)
            {
                DmaStreamComputeDescriptor = instruction.DmaStreamComputeDescriptor,
                AcceleratorCommandDescriptor = instruction.AcceleratorCommandDescriptor
            };

            return metadata;
        }

        private static SlotMetadata BuildSlotMetadata(IrInstruction instruction)
        {
            SlotPinningKind runtimePinning =
                IrSlotClassMapping.ToRuntimePinningKind(instruction.Annotation.BindingKind);
            var placement = new SlotPlacementMetadata
            {
                RequiredSlotClass = instruction.Annotation.RequiredSlotClass,
                PinningKind = runtimePinning,
                PinnedLaneId = ResolvePinnedLaneId(
                    instruction.Annotation.RequiredSlotClass,
                    runtimePinning),
                DomainTag = instruction.Annotation.DomainTag
            };

            StealabilityPolicy stealabilityPolicy = instruction.Annotation.StealabilityHint
                ? StealabilityPolicy.Stealable
                : StealabilityPolicy.NotStealable;

            return new SlotMetadata
            {
                StealabilityPolicy = stealabilityPolicy,
                AdmissionMetadata = MicroOpAdmissionMetadata.Default with
                {
                    IsStealable = instruction.Annotation.StealabilityHint,
                    DomainTag = instruction.Annotation.DomainTag,
                    Placement = placement
                }
            };
        }

        private static byte ResolvePinnedLaneId(
            SlotClass requiredSlotClass,
            SlotPinningKind pinningKind)
        {
            if (pinningKind != SlotPinningKind.HardPinned)
            {
                return 0;
            }

            return requiredSlotClass switch
            {
                SlotClass.BranchControl => 7,
                SlotClass.SystemSingleton => 7,
                _ => 0
            };
        }

        private static ulong GetDestSrc1Pointer(IrInstruction instruction)
        {
            return TryPackArchitecturalRegisterTuple(instruction, "rd", "rs1", "rs2", out ulong packedRegisters)
                ? packedRegisters
                : GetPointerOperand(instruction, "destsrc1");
        }

        private static ulong GetSrc2Pointer(IrInstruction instruction)
        {
            return TryPackArchitecturalRegisterTuple(instruction, "ctrl0", "ctrl1", "ctrl2", out ulong packedRegisters)
                ? packedRegisters
                : GetPointerOperand(instruction, "src2");
        }

        private static VLIW_Instruction CreateNopInstruction()
        {
            return new VLIW_Instruction
            {
                OpCode = (uint)Processor.CPU_Core.InstructionsEnum.Nope,
                DataTypeValue = DataTypeEnum.INT8,
                StreamLength = 0
            };
        }

        private static ulong GetPointerOperand(IrInstruction instruction, string operandName)
        {
            foreach (IrOperand operand in instruction.Operands)
            {
                if (operand.Kind == IrOperandKind.Pointer && string.Equals(operand.Name, operandName, StringComparison.Ordinal))
                {
                    return operand.Value;
                }
            }

            return 0;
        }

        private static bool TryPackArchitecturalRegisterTuple(
            IrInstruction instruction,
            string firstName,
            string secondName,
            string thirdName,
            out ulong packedRegisters)
        {
            byte firstRegister = VLIW_Instruction.NoArchReg;
            byte secondRegister = VLIW_Instruction.NoArchReg;
            byte thirdRegister = VLIW_Instruction.NoArchReg;
            bool hasAnyRegister = false;

            foreach (IrOperand operand in instruction.Operands)
            {
                if (operand.Kind != IrOperandKind.Pointer ||
                    operand.Value > byte.MaxValue)
                {
                    continue;
                }

                if (string.Equals(operand.Name, firstName, StringComparison.Ordinal))
                {
                    firstRegister = (byte)operand.Value;
                    hasAnyRegister = true;
                }
                else if (string.Equals(operand.Name, secondName, StringComparison.Ordinal))
                {
                    secondRegister = (byte)operand.Value;
                    hasAnyRegister = true;
                }
                else if (string.Equals(operand.Name, thirdName, StringComparison.Ordinal))
                {
                    thirdRegister = (byte)operand.Value;
                    hasAnyRegister = true;
                }
            }

            if (!hasAnyRegister)
            {
                packedRegisters = 0;
                return false;
            }

            packedRegisters = VLIW_Instruction.PackArchRegs(
                firstRegister,
                secondRegister,
                thirdRegister);
            return true;
        }
    }
}
