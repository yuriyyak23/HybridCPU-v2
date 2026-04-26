using HybridCPU_ISE.Arch;
using System.Collections.Generic;
using YAKSys_Hybrid_CPU.Arch;
using YAKSys_Hybrid_CPU.Core.Pipeline.MicroOps;
using static YAKSys_Hybrid_CPU.Processor.CPU_Core;

namespace YAKSys_Hybrid_CPU.Core.Decoder
{
    /// <summary>
    /// Test-only retained-wrapper compat decoder for ISA v4.
    /// This decoder is quarantined in the test assembly to ensure
    /// production builds ship only canonical decode paths.
    /// </summary>
    internal sealed class VliwCompatDecoderV4 : IDecoderFrontend
    {
        private readonly VliwDecoderV4 _canonicalDecoder = new();

        public InstructionIR Decode(
            in VLIW_Instruction instruction,
            int slotIndex)
        {
            if (TryDecodeRetainedCompatInstruction(
                in instruction,
                slotIndex,
                out InstructionIR compatProjection))
            {
                return compatProjection;
            }

            return _canonicalDecoder.Decode(in instruction, slotIndex);
        }

        public DecodedInstructionBundle DecodeInstructionBundle(
            System.ReadOnlySpan<VLIW_Instruction> bundle,
            VliwBundleAnnotations? bundleAnnotations,
            ulong bundleAddress,
            ulong bundleSerial = 0)
        {
            List<DecodedInstruction> results = new(bundle.Length);
            for (int i = 0; i < bundle.Length; i++)
            {
                var slot = bundle[i];
                if (slot.OpCode == 0)
                {
                    results.Add(DecodedInstruction.CreateEmpty(
                        i,
                        ResolveSlotMetadata(bundleAnnotations, i)));
                    continue;
                }

                results.Add(DecodedInstruction.CreateOccupied(
                    i,
                    Decode(in slot, i),
                    ResolveSlotMetadata(bundleAnnotations, i)));
            }

            return new DecodedInstructionBundle(
                bundleAddress,
                bundleSerial,
                results,
                bundleAnnotations?.BundleMetadata ?? BundleMetadata.Default);
        }

        public DecodedInstructionBundle DecodeInstructionBundle(
            System.ReadOnlySpan<VLIW_Instruction> bundle,
            ulong bundleAddress,
            ulong bundleSerial = 0)
        {
            return DecodeInstructionBundle(
                bundle,
                null,
                bundleAddress,
                bundleSerial);
        }

        private static bool TryDecodeRetainedCompatInstruction(
            in VLIW_Instruction instruction,
            int slotIndex,
            out InstructionIR compatProjection)
        {
            var rawOpcode = (InstructionsEnum)instruction.OpCode;

            if (TryDecodeRetainedAbsoluteMoveWrapper(
                in instruction,
                rawOpcode,
                slotIndex,
                out compatProjection))
            {
                return true;
            }

            if (TryDecodeRetainedConditionalBranchWrapper(
                in instruction,
                rawOpcode,
                slotIndex,
                out compatProjection))
            {
                return true;
            }

            if (TryDecodeRetainedLegacyAbsoluteMemory(
                in instruction,
                rawOpcode,
                slotIndex,
                out compatProjection))
            {
                return true;
            }

            compatProjection = null!;
            return false;
        }

        private static bool TryDecodeRetainedAbsoluteMoveWrapper(
            in VLIW_Instruction instruction,
            InstructionsEnum rawOpcode,
            int slotIndex,
            out InstructionIR compatProjection)
        {
            if (rawOpcode != InstructionsEnum.Move ||
                (instruction.DataType != 2 && instruction.DataType != 3))
            {
                compatProjection = null!;
                return false;
            }

            RejectRetiredPolicyGap(in instruction, slotIndex);

            var (packedRd, _, _) = DecodeCompatPackedArchRegisters(
                in instruction,
                nameof(InstructionsEnum.Move),
                slotIndex);

            InstructionsEnum canonicalOpcode = instruction.DataType == 2
                ? InstructionsEnum.Store
                : InstructionsEnum.Load;
            var (instructionClass, serializationClass) = InstructionClassifier.Classify(canonicalOpcode);
            ulong absoluteAddress = instruction.Src2Pointer != 0
                ? instruction.Src2Pointer
                : (ulong)(short)instruction.Immediate;

            compatProjection = new InstructionIR
            {
                CanonicalOpcode = canonicalOpcode,
                Class = instructionClass,
                SerializationClass = serializationClass,
                Rd = instruction.DataType == 3
                    ? packedRd
                    : VLIW_Instruction.NoArchReg,
                Rs1 = VLIW_Instruction.NoArchReg,
                Rs2 = instruction.DataType == 2
                    ? packedRd
                    : VLIW_Instruction.NoArchReg,
                Imm = (long)absoluteAddress,
                HasAbsoluteAddressing = true,
            };
            return true;
        }

        private static bool TryDecodeRetainedConditionalBranchWrapper(
            in VLIW_Instruction instruction,
            InstructionsEnum rawOpcode,
            int slotIndex,
            out InstructionIR compatProjection)
        {
            if (!TryProjectCompatBranchOpcode(rawOpcode, out InstructionsEnum canonicalOpcode))
            {
                compatProjection = null!;
                return false;
            }

            RejectRetiredPolicyGap(in instruction, slotIndex);

            var (_, rs1, rs2) = DecodeCompatPackedArchRegisters(
                in instruction,
                rawOpcode.ToString(),
                slotIndex);
            var (instructionClass, serializationClass) = InstructionClassifier.Classify(canonicalOpcode);

            byte projectedRs1 = rawOpcode is InstructionsEnum.JumpIfBelowOrEqual or InstructionsEnum.JumpIfAbove
                ? rs2
                : rs1;
            byte projectedRs2 = rawOpcode is InstructionsEnum.JumpIfBelowOrEqual or InstructionsEnum.JumpIfAbove
                ? rs1
                : rs2;

            bool hasAbsoluteTarget = instruction.Src2Pointer != 0;
            long immediate = hasAbsoluteTarget
                ? (long)instruction.Src2Pointer
                : (long)(short)instruction.Immediate;

            compatProjection = new InstructionIR
            {
                CanonicalOpcode = canonicalOpcode,
                Class = instructionClass,
                SerializationClass = serializationClass,
                Rd = VLIW_Instruction.NoArchReg,
                Rs1 = projectedRs1,
                Rs2 = projectedRs2,
                Imm = immediate,
                HasAbsoluteAddressing = hasAbsoluteTarget,
            };
            return true;
        }

        private static bool TryProjectCompatBranchOpcode(
            InstructionsEnum rawOpcode,
            out InstructionsEnum canonicalOpcode)
        {
            canonicalOpcode = rawOpcode switch
            {
                InstructionsEnum.JumpIfEqual => InstructionsEnum.BEQ,
                InstructionsEnum.JumpIfNotEqual => InstructionsEnum.BNE,
                InstructionsEnum.JumpIfBelow => InstructionsEnum.BLTU,
                InstructionsEnum.JumpIfBelowOrEqual => InstructionsEnum.BGEU,
                InstructionsEnum.JumpIfAbove => InstructionsEnum.BLTU,
                InstructionsEnum.JumpIfAboveOrEqual => InstructionsEnum.BGEU,
                _ => default,
            };

            return canonicalOpcode != default;
        }

        private static (byte Rd, byte Rs1, byte Rs2) DecodeCompatPackedArchRegisters(
            in VLIW_Instruction instruction,
            string opcodeName,
            int slotIndex)
        {
            if (!VLIW_Instruction.TryUnpackArchRegs(
                    instruction.Word1,
                    out byte rd,
                    out byte rs1,
                    out byte rs2))
            {
                throw new InvalidOperationException(
                    $"Opcode '{opcodeName}' (slot {slotIndex}) uses legacy/global register encoding. " +
                    "Compat decoder accepts only the retained packed-arch wrapper shape with flat architectural register ids.");
            }

            return (rd, rs1, rs2);
        }

        private static bool TryDecodeRetainedLegacyAbsoluteMemory(
            in VLIW_Instruction instruction,
            InstructionsEnum rawOpcode,
            int slotIndex,
            out InstructionIR compatProjection)
        {
            if (rawOpcode is not (InstructionsEnum.Load or InstructionsEnum.Store))
            {
                compatProjection = null!;
                return false;
            }

            RejectRetiredPolicyGap(in instruction, slotIndex);

            var (packedRd, packedRs1, packedRs2) = DecodeCompatPackedArchRegisters(
                in instruction,
                rawOpcode.ToString(),
                slotIndex);

            var (instructionClass, serializationClass) = InstructionClassifier.Classify(rawOpcode);
            ulong absoluteAddress = instruction.Src2Pointer != 0
                ? instruction.Src2Pointer
                : (ulong)(short)instruction.Immediate;

            if (rawOpcode == InstructionsEnum.Load)
            {
                compatProjection = new InstructionIR
                {
                    CanonicalOpcode = rawOpcode,
                    Class = instructionClass,
                    SerializationClass = serializationClass,
                    Rd = packedRd,
                    Rs1 = packedRs1,
                    Rs2 = packedRs2,
                    Imm = (long)absoluteAddress,
                    HasAbsoluteAddressing = true,
                };
            }
            else
            {
                compatProjection = new InstructionIR
                {
                    CanonicalOpcode = rawOpcode,
                    Class = instructionClass,
                    SerializationClass = serializationClass,
                    Rd = VLIW_Instruction.NoArchReg,
                    Rs1 = packedRs1,
                    Rs2 = packedRs2,
                    Imm = (long)absoluteAddress,
                    HasAbsoluteAddressing = true,
                };
            }

            return true;
        }

        private static void RejectRetiredPolicyGap(
            in VLIW_Instruction instruction,
            int slotIndex)
        {
            if ((instruction.Word3 & VLIW_Instruction.RetiredPolicyGapMask) != 0)
            {
                throw new InvalidOpcodeException(
                    VLIW_Instruction.GetRetiredPolicyGapViolationMessage(slotIndex),
                    opcodeIdentifier: instruction.OpCode.ToString(System.Globalization.CultureInfo.InvariantCulture),
                    slotIndex: slotIndex,
                    isProhibited: true);
            }
        }

        private static InstructionSlotMetadata ResolveSlotMetadata(
            VliwBundleAnnotations? bundleAnnotations,
            int slotIndex)
        {
            if (bundleAnnotations != null &&
                bundleAnnotations.TryGetInstructionSlotMetadata(slotIndex, out InstructionSlotMetadata metadata))
            {
                return metadata;
            }

            return InstructionSlotMetadata.Default;
        }
    }
}
