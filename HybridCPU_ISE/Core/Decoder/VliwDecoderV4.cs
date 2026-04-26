using HybridCPU_ISE.Arch;
using System.Globalization;
using System.Collections.Generic;
using YAKSys_Hybrid_CPU.Arch;
using YAKSys_Hybrid_CPU.Core.Pipeline.MicroOps;
using static YAKSys_Hybrid_CPU.Processor.CPU_Core;

namespace YAKSys_Hybrid_CPU.Core.Decoder
{
    /// <summary>
    /// HybridCPU ISA v4 native VLIW decoder.
    /// Single decode path — no ISA translation, no compatibility layers, no DBT.
    /// Accepts only canonical ISA v4 opcodes; rejects prohibited opcodes at decode time.
    /// Produces <see cref="InstructionIR"/> records inside the canonical
    /// <see cref="DecodedInstructionBundle"/> frontend contract.
    /// </summary>
    public sealed class VliwDecoderV4 : IDecoderFrontend
    {
        private const ushort XsqrtRawOpcode = 45;
        private const ushort NotRawOpcode = 52;
        private const ushort XfmacRawOpcode = 55;

        // Pre-built set: prohibited opcode names for O(1) lookup.
        // Populated once on class load from IsaV4Surface.ProhibitedOpcodes.
        // ─── Primary decode entry point ───────────────────────────────────────────

        /// <summary>
        /// Decode a single instruction from a VLIW bundle slot.
        /// </summary>
        /// <param name="instruction">Raw VLIW instruction from the bundle slot.</param>
        /// <param name="slotIndex">Physical slot index (0–7) within the bundle.</param>
        /// <returns>Canonical <see cref="InstructionIR"/> record.</returns>
        /// <exception cref="InvalidOpcodeException">
        /// Thrown when the opcode is in <see cref="IsaV4Surface.ProhibitedOpcodes"/>
        /// (pseudo-ops, hint opcodes, or compiler wrappers that must not appear in the
        /// hardware instruction stream).
        /// </exception>
        public InstructionIR Decode(
            in VLIW_Instruction instruction,
            int slotIndex)
        {
            ushort opcode = Processor.CPU_Core.IsaOpcode.FromRawValue(instruction.OpCode).Value;
            var opcodeName = OpcodeRegistry.GetMnemonicOrHex(opcode);

            // Gate 1: Reject prohibited opcodes immediately.
            if (IsProhibited(opcode, opcodeName))
            {
                throw new InvalidOpcodeException(
                    $"Opcode '{opcodeName}' (slot {slotIndex}) is not part of ISA v4 canonical " +
                    $"surface. Pseudo-ops and hint opcodes must not appear in the hardware " +
                    $"instruction stream. See IsaV4Surface.ProhibitedOpcodes.",
                    opcodeIdentifier: opcodeName,
                    slotIndex: slotIndex,
                    isProhibited: true);
            }

            RejectUnsupportedCustomAcceleratorContour(
                instruction.OpCode,
                slotIndex);
            RejectUnknownOpcode(
                opcode,
                slotIndex);
            RejectRetainedCompatContour(
                opcode,
                instruction.DataType,
                opcodeName,
                slotIndex);
            RejectUnsupportedResidualContour(
                opcode,
                instruction.DataType,
                opcodeName,
                slotIndex);
            RejectLegacyPolicyGap(in instruction, slotIndex);

            // Gate 2: Classify via the authoritative ISA v4 classifier.
            var (instrClass, serialClass) = InstructionClassifier.Classify(opcode);
            var (rd, rs1, rs2) = DecodeRegisterOperands(
                in instruction,
                instrClass,
                opcodeName,
                slotIndex);

            ulong? absoluteMemory = ResolveAbsoluteMemoryAddress(in instruction, opcode);
            bool hasAbsolute = absoluteMemory.HasValue;
            long imm = hasAbsolute
                ? (long)absoluteMemory!.Value
                : (long)(short)instruction.Immediate;

            // Build canonical IR record.
            return new InstructionIR
            {
                CanonicalOpcode = new Processor.CPU_Core.IsaOpcode(opcode),
                Class = instrClass,
                SerializationClass = serialClass,
                Rd = rd,
                Rs1 = rs1,
                Rs2 = rs2,
                Imm = imm,
                HasAbsoluteAddressing = hasAbsolute,
            };
        }

        /// <summary>
        /// Decode one frontend bundle into the canonical Phase 03 bundle contract.
        /// </summary>
        /// <param name="bundle">Span of raw VLIW instructions (one per slot).</param>
        /// <param name="bundleAddress">PC address of the first byte of the decoded bundle.</param>
        /// <param name="bundleSerial">Bundle serial used by downstream pipeline tracking.</param>
        /// <returns>
        /// Canonical <see cref="DecodedInstructionBundle"/> with one logical slot per
        /// physical bundle slot and semantic content carried only as <see cref="InstructionIR"/>.
        /// </returns>
        public DecodedInstructionBundle DecodeInstructionBundle(
            System.ReadOnlySpan<VLIW_Instruction> bundle,
            VliwBundleAnnotations? bundleAnnotations,
            ulong bundleAddress,
            ulong bundleSerial = 0)
        {
            List<DecodedInstruction> results = new List<DecodedInstruction>(bundle.Length);
            for (int i = 0; i < bundle.Length; i++)
            {
                var slot = bundle[i];
                // Skip empty slots: opcode 0 is reserved as the hardware NOP sentinel.
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

        /// <summary>
        /// Decode one frontend bundle into the canonical Phase 03 bundle contract.
        /// Uses neutral/default slot metadata when no sideband bundle annotations are available.
        /// </summary>
        /// <param name="bundle">Span of raw VLIW instructions (one per slot).</param>
        /// <param name="bundleAddress">PC address of the first byte of the decoded bundle.</param>
        /// <param name="bundleSerial">Bundle serial used by downstream pipeline tracking.</param>
        /// <returns>
        /// Canonical <see cref="DecodedInstructionBundle"/> with one logical slot per
        /// physical bundle slot and semantic content carried only as <see cref="InstructionIR"/>.
        /// </returns>
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

        // ─── Helpers ──────────────────────────────────────────────────────────────

        /// <summary>
        /// Extract the low-order 5-bit register field from a memory pointer field
        /// when the field is being used to carry a register address rather than a
        /// memory pointer (e.g., in compressed register-encoded forms).
        /// </summary>
        [System.Runtime.CompilerServices.MethodImpl(
            System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        private static int ExtractRegField(ulong pointerField)
            => (int)(pointerField & 0x1F);

        [System.Runtime.CompilerServices.MethodImpl(
            System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        private static void RejectUnsupportedCustomAcceleratorContour(
            uint rawOpcode,
            int slotIndex)
        {
            if (global::YAKSys_Hybrid_CPU.Core.InstructionRegistry.IsCustomAcceleratorOpcode(rawOpcode))
            {
                string opcodeIdentifier = $"0x{rawOpcode:X}";
                throw global::YAKSys_Hybrid_CPU.Core.InstructionRegistry.CreateUnsupportedCustomAcceleratorException(
                    rawOpcode,
                    opcodeIdentifier,
                    slotIndex);
            }
        }

        [System.Runtime.CompilerServices.MethodImpl(
            System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        private static void RejectUnknownOpcode(
            ushort rawOpcode,
            int slotIndex)
        {
            if (!IsKnownDecodeInputOpcode(rawOpcode))
            {
                string opcodeIdentifier = $"0x{rawOpcode:X}";
                throw new InvalidOpcodeException(
                    $"Opcode '{opcodeIdentifier}' (slot {slotIndex}) lies outside the canonical ISA v4 opcode space. " +
                    "Canonical decode must fail closed instead of projecting default ScalarAlu/Free metadata.",
                    opcodeIdentifier,
                    slotIndex,
                    isProhibited: false);
                }
        }

        [System.Runtime.CompilerServices.MethodImpl(
            System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        private static bool IsKnownDecodeInputOpcode(ushort rawOpcode)
        {
            if (OpcodeRegistry.GetInfo(rawOpcode).HasValue)
            {
                return true;
            }

            return rawOpcode is
                Processor.CPU_Core.IsaOpcodeValues.JumpIfNotEqual or
                Processor.CPU_Core.IsaOpcodeValues.JumpIfBelow or
                Processor.CPU_Core.IsaOpcodeValues.JumpIfBelowOrEqual or
                Processor.CPU_Core.IsaOpcodeValues.JumpIfAbove or
                Processor.CPU_Core.IsaOpcodeValues.JumpIfAboveOrEqual or
                Processor.CPU_Core.IsaOpcodeValues.Interrupt or
                Processor.CPU_Core.IsaOpcodeValues.InterruptReturn or
                Processor.CPU_Core.IsaOpcodeValues.Move or
                Processor.CPU_Core.IsaOpcodeValues.Load or
                Processor.CPU_Core.IsaOpcodeValues.Store or
                Processor.CPU_Core.IsaOpcodeValues.MTILE_LOAD or
                Processor.CPU_Core.IsaOpcodeValues.MTILE_STORE or
                Processor.CPU_Core.IsaOpcodeValues.MTILE_MACC or
                Processor.CPU_Core.IsaOpcodeValues.MTRANSPOSE or
                XsqrtRawOpcode or
                NotRawOpcode or
                XfmacRawOpcode;
        }

        [System.Runtime.CompilerServices.MethodImpl(
            System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        private static void RejectLegacyPolicyGap(
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

        [System.Runtime.CompilerServices.MethodImpl(
            System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        private static void RejectRetainedCompatContour(
            ushort rawOpcode,
            byte rawDataType,
            string opcodeName,
            int slotIndex)
        {
            if (rawOpcode == Processor.CPU_Core.IsaOpcodeValues.Move &&
                (rawDataType == 2 || rawDataType == 3))
            {
                string contourDescription = rawDataType == 2
                    ? "absolute-store"
                    : "absolute-load";

                throw new InvalidOpcodeException(
                    $"Opcode '{opcodeName}' (slot {slotIndex}) uses retained Move DT={rawDataType} {contourDescription} compat contour. " +
                    "Canonical ISA v4 decode must fail closed; route retained wrappers through the quarantined compat decoder/test helper seam instead of mutating canonical decoder truth.",
                    opcodeIdentifier: opcodeName,
                    slotIndex: slotIndex,
                    isProhibited: false);
            }

            if (rawOpcode == Processor.CPU_Core.IsaOpcodeValues.JumpIfEqual ||
                rawOpcode == Processor.CPU_Core.IsaOpcodeValues.JumpIfNotEqual ||
                rawOpcode == Processor.CPU_Core.IsaOpcodeValues.JumpIfBelow ||
                rawOpcode == Processor.CPU_Core.IsaOpcodeValues.JumpIfBelowOrEqual ||
                rawOpcode == Processor.CPU_Core.IsaOpcodeValues.JumpIfAbove ||
                rawOpcode == Processor.CPU_Core.IsaOpcodeValues.JumpIfAboveOrEqual)
            {
                throw new InvalidOpcodeException(
                    $"Opcode '{opcodeName}' (slot {slotIndex}) uses retained conditional-branch compat contour. " +
                    "Canonical ISA v4 decode must fail closed; route retained wrappers through the quarantined compat decoder/test helper seam instead of projecting canonical branch truth inside VliwDecoderV4.",
                    opcodeIdentifier: opcodeName,
                    slotIndex: slotIndex,
                    isProhibited: false);
            }
        }

        [System.Runtime.CompilerServices.MethodImpl(
            System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        private static void RejectUnsupportedResidualContour(
            ushort rawOpcode,
            byte rawDataType,
            string opcodeName,
            int slotIndex)
        {
            if (rawOpcode == Processor.CPU_Core.IsaOpcodeValues.Interrupt ||
                rawOpcode == Processor.CPU_Core.IsaOpcodeValues.InterruptReturn)
            {
                throw new InvalidOpcodeException(
                    $"Opcode '{opcodeName}' (slot {slotIndex}) uses unsupported retained {opcodeName} control-transfer contour. " +
                    "Canonical decode must fail closed instead of publishing system success without a typed mainline retire/boundary carrier.",
                    opcodeIdentifier: opcodeName,
                    slotIndex: slotIndex,
                    isProhibited: false);
            }

            if (rawOpcode == Processor.CPU_Core.IsaOpcodeValues.Move && rawDataType == 4)
            {
                throw new InvalidOpcodeException(
                    $"Opcode '{opcodeName}' (slot {slotIndex}) uses unsupported retained Move DT=4 dual-write contour. " +
                    "Canonical decode must fail closed instead of publishing partial scalar register truth.",
                    opcodeIdentifier: opcodeName,
                    slotIndex: slotIndex,
                    isProhibited: false);
            }

            if (rawOpcode == Processor.CPU_Core.IsaOpcodeValues.Move && rawDataType == 5)
            {
                throw new InvalidOpcodeException(
                    $"Opcode '{opcodeName}' (slot {slotIndex}) uses unsupported retained Move DT=5 triple-destination contour. " +
                    "Canonical decode must fail closed instead of publishing scalar success.",
                    opcodeIdentifier: opcodeName,
                    slotIndex: slotIndex,
                    isProhibited: false);
            }

            if (rawOpcode == Processor.CPU_Core.IsaOpcodeValues.MTILE_MACC ||
                rawOpcode == Processor.CPU_Core.IsaOpcodeValues.MTRANSPOSE)
            {
                throw new InvalidOpcodeException(
                    $"Opcode '{opcodeName}' (slot {slotIndex}) uses unsupported optional matrix contour. " +
                    "Canonical decode must fail closed instead of publishing scalar ALU register truth without an authoritative matrix carrier/materializer follow-through.",
                    opcodeIdentifier: opcodeName,
                    slotIndex: slotIndex,
                    isProhibited: false);
            }

            if (rawOpcode == Processor.CPU_Core.IsaOpcodeValues.MTILE_LOAD ||
                rawOpcode == Processor.CPU_Core.IsaOpcodeValues.MTILE_STORE)
            {
                throw new InvalidOpcodeException(
                    $"Opcode '{opcodeName}' (slot {slotIndex}) uses unsupported optional matrix memory contour. " +
                    "Canonical decode must fail closed instead of publishing memory placement/register truth without an authoritative matrix load/store carrier/materializer follow-through.",
                    opcodeIdentifier: opcodeName,
                    slotIndex: slotIndex,
                    isProhibited: false);
            }

            if (rawOpcode == XsqrtRawOpcode)
            {
                throw new InvalidOpcodeException(
                    $"Opcode '{XsqrtRawOpcode}' (slot {slotIndex}) uses unsupported optional scalar-math contour (XSQRT). " +
                    "Canonical decode must fail closed instead of publishing scalar ALU register truth without an authoritative scalar carrier/materializer follow-through.",
                    opcodeIdentifier: XsqrtRawOpcode.ToString(System.Globalization.CultureInfo.InvariantCulture),
                    slotIndex: slotIndex,
                    isProhibited: false);
            }

            if (rawOpcode == NotRawOpcode)
            {
                throw new InvalidOpcodeException(
                    $"Opcode '{NotRawOpcode}' (slot {slotIndex}) uses unsupported optional scalar bit-manip contour (NOT). " +
                    "Canonical decode must fail closed instead of publishing scalar ALU register truth without an authoritative scalar carrier/materializer follow-through.",
                    opcodeIdentifier: NotRawOpcode.ToString(System.Globalization.CultureInfo.InvariantCulture),
                    slotIndex: slotIndex,
                    isProhibited: false);
            }

            if (rawOpcode == XfmacRawOpcode)
            {
                throw new InvalidOpcodeException(
                    $"Opcode '{XfmacRawOpcode}' (slot {slotIndex}) uses unsupported optional scalar-math contour (XFMAC). " +
                    "Canonical decode must fail closed instead of publishing scalar ALU register truth without an authoritative scalar carrier/materializer follow-through.",
                    opcodeIdentifier: XfmacRawOpcode.ToString(System.Globalization.CultureInfo.InvariantCulture),
                    slotIndex: slotIndex,
                    isProhibited: false);
            }
        }

        [System.Runtime.CompilerServices.MethodImpl(
            System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        private static ulong? ResolveAbsoluteMemoryAddress(
            in VLIW_Instruction instruction,
            ushort opcode)
        {
            return opcode switch
            {
                Processor.CPU_Core.IsaOpcodeValues.Load or Processor.CPU_Core.IsaOpcodeValues.Store
                    => instruction.Src2Pointer != 0
                        ? instruction.Src2Pointer
                        : (ulong)(short)instruction.Immediate,
                _ => null,
            };
        }

        private static (byte Rd, byte Rs1, byte Rs2) DecodeRegisterOperands(
            in VLIW_Instruction instruction,
            InstructionClass instructionClass,
            string opcodeName,
            int slotIndex)
        {
            ushort opcode = unchecked((ushort)instruction.OpCode);

            if (opcode == Processor.CPU_Core.IsaOpcodeValues.VPOPC)
            {
                // Scalar-result predicate popcount encodes its destination GPR in the upper
                // immediate nibble rather than in the vector address/register fields.
                return (
                    (byte)((instruction.Immediate >> 8) & 0x0F),
                    VLIW_Instruction.NoArchReg,
                    VLIW_Instruction.NoArchReg);
            }

            bool isPublishedVectorOpcode = OpcodeRegistry.IsVectorOp(instruction.OpCode);

            if (isPublishedVectorOpcode &&
                instructionClass is InstructionClass.ScalarAlu or InstructionClass.Memory)
            {
                // Dedicated vector compute and vector-memory carriers do not publish scalar
                // register facts through canonical decode. Their authoritative follow-through is
                // vector/predicate-local rather than the packed Word1 arch-register ABI.
                return (
                    VLIW_Instruction.NoArchReg,
                    VLIW_Instruction.NoArchReg,
                    VLIW_Instruction.NoArchReg);
            }

            if (OpcodeRegistry.UsesPackedArchRegisterWord1(opcode))
            {
                if (!VLIW_Instruction.TryUnpackArchRegs(
                    instruction.Word1,
                    out byte rd,
                    out byte rs1,
                    out byte rs2))
                {
                    throw new InvalidOperationException(
                        $"Opcode '{opcodeName}' (slot {slotIndex}) uses legacy/global register encoding. " +
                        "Scalar/control/system decode accepts only flat architectural register ids.");
                }

                return (rd, rs1, rs2);
            }

            return (
                (byte)(instruction.DestSrc1Pointer != 0
                    ? ExtractRegField(instruction.DestSrc1Pointer)
                    : instruction.Reg1ID),
                (byte)(instruction.Src2Pointer != 0
                    ? ExtractRegField(instruction.Src2Pointer)
                    : instruction.Reg2ID),
                (byte)instruction.Reg3ID);
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

        // ─── Static prohibition query API ─────────────────────────────────────────

        /// <summary>
        /// Returns <see langword="true"/> if <paramref name="opcode"/> is listed as
        /// prohibited in <see cref="IsaV4Surface.ProhibitedOpcodes"/>.
        /// </summary>
        public static bool IsProhibited(Processor.CPU_Core.IsaOpcode opcode)
        {
            var name = opcode.ToString();
            return IsProhibited(opcode.Value, name);
        }

        /// <summary>
        /// Returns <see langword="true"/> if the string <paramref name="opcodeName"/> is
        /// listed in <see cref="IsaV4Surface.ProhibitedOpcodes"/>.
        /// </summary>
        public static bool IsProhibited(string opcodeName)
        {
            if (TryParseOpcodeIdentifier(opcodeName, out ushort opcode))
            {
                return IsProhibited(opcode, opcodeName);
            }

            return IsaV4Surface.ProhibitedOpcodes.Contains(opcodeName) ||
                   IsaV4Surface.ProhibitedOpcodes.Contains(opcodeName.ToUpperInvariant());
        }

        private static bool IsProhibited(ushort opcode, string opcodeName)
        {
            if (IsaV4Surface.ProhibitedOpcodes.Contains(opcodeName) ||
                IsaV4Surface.ProhibitedOpcodes.Contains(opcodeName.ToUpperInvariant()))
            {
                return true;
            }

            string decimalIdentifier = opcode.ToString(CultureInfo.InvariantCulture);
            if (IsaV4Surface.ProhibitedOpcodes.Contains(decimalIdentifier))
            {
                return true;
            }

            string hexIdentifier = $"0x{opcode:X}";
            return IsaV4Surface.ProhibitedOpcodes.Contains(hexIdentifier) ||
                   IsaV4Surface.ProhibitedOpcodes.Contains(hexIdentifier.ToUpperInvariant());
        }

        private static bool TryParseOpcodeIdentifier(string opcodeIdentifier, out ushort opcode)
        {
            if (ushort.TryParse(
                opcodeIdentifier,
                NumberStyles.Integer,
                CultureInfo.InvariantCulture,
                out opcode))
            {
                return true;
            }

            if (opcodeIdentifier.StartsWith("0x", StringComparison.OrdinalIgnoreCase) &&
                ushort.TryParse(
                    opcodeIdentifier[2..],
                    NumberStyles.AllowHexSpecifier,
                    CultureInfo.InvariantCulture,
                    out opcode))
            {
                return true;
            }

            opcode = default;
            return false;
        }
    }
}
