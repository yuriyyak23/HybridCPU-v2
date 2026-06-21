using System.Runtime.CompilerServices;

namespace YAKSys_Hybrid_CPU.Arch
{
    public static partial class OpcodeRegistry
    {
        /// <summary>
        /// Lookup opcode information by opcode value.
        /// Returns null if opcode not found.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static OpcodeInfo? GetInfo(uint opCode)
        {
            foreach (var info in Opcodes)
            {
                if (info.OpCode == opCode)
                    return info;
            }
            return null;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool TryGetMnemonic(uint opCode, out string mnemonic)
        {
            var info = GetInfo(opCode);
            if (info is null)
            {
                mnemonic = string.Empty;
                return false;
            }

            mnemonic = info.Value.Mnemonic;
            return true;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static string GetMnemonicOrHex(uint opCode) =>
            TryGetMnemonic(opCode, out string mnemonic)
                ? mnemonic
                : $"0x{opCode:X}";

        /// <summary>
        /// Returns <see langword="true"/> for canonical native L7-SDC lane7
        /// system-device command carriers.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool IsSystemDeviceCommandOpcode(uint opCode)
        {
            return opCode is
                Processor.CPU_Core.IsaOpcodeValues.ACCEL_QUERY_CAPS or
                Processor.CPU_Core.IsaOpcodeValues.ACCEL_SUBMIT or
                Processor.CPU_Core.IsaOpcodeValues.ACCEL_POLL or
                Processor.CPU_Core.IsaOpcodeValues.ACCEL_WAIT or
                Processor.CPU_Core.IsaOpcodeValues.ACCEL_CANCEL or
                Processor.CPU_Core.IsaOpcodeValues.ACCEL_FENCE or
                Processor.CPU_Core.IsaOpcodeValues.ACCEL_STATUS;
        }

        /// <summary>
        /// Resolve canonical runtime semantics for a published opcode contour.
        /// Returns false when the opcode is not published by <see cref="Opcodes"/>.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool TryGetPublishedSemantics(
            uint opCode,
            out InstructionClass instructionClass,
            out SerializationClass serializationClass)
        {
            var info = GetInfo(opCode);
            if (info is null)
            {
                instructionClass = default;
                serializationClass = default;
                return false;
            }

            instructionClass = info.Value.InstructionClass;
            serializationClass = info.Value.SerializationClass;
            return true;
        }

        /// <summary>
        /// Resolve canonical runtime semantics for a published opcode contour.
        /// Returns false when the opcode is not published by <see cref="Opcodes"/>.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool TryGetPublishedSemantics(
            Processor.CPU_Core.InstructionsEnum opcode,
            out InstructionClass instructionClass,
            out SerializationClass serializationClass)
            => TryGetPublishedSemantics((uint)opcode, out instructionClass, out serializationClass);

        /// <summary>
        /// Check if opcode is a vector instruction
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool IsVectorOp(uint opCode)
        {
            var info = GetInfo(opCode);
            return info?.IsVector ?? false;
        }

        /// <summary>
        /// Returns <see langword="true"/> when the opcode requires the canonical
        /// decoder-to-runtime vector payload projection.
        /// This includes published vector instructions plus the retained vector
        /// transfer carriers that are still materialized through vector transport.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool RequiresVectorPayloadProjection(uint opCode)
        {
            if (IsVectorConfigSystemSingletonOpcode(opCode))
            {
                return false;
            }

            if (IsVectorOp(opCode))
            {
                return true;
            }

            return (Processor.CPU_Core.InstructionsEnum)opCode is
                Processor.CPU_Core.InstructionsEnum.VLOAD or
                Processor.CPU_Core.InstructionsEnum.VSTORE;
        }

        /// <summary>
        /// Returns <see langword="true"/> when the opcode uses bundle-PC-relative
        /// static control-flow targeting and therefore can project a canonical
        /// target address without consulting register state.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool HasStaticRelativeControlFlowTarget(uint opCode)
        {
            return (Processor.CPU_Core.InstructionsEnum)opCode is
                Processor.CPU_Core.InstructionsEnum.JAL or
                Processor.CPU_Core.InstructionsEnum.BEQ or
                Processor.CPU_Core.InstructionsEnum.BNE or
                Processor.CPU_Core.InstructionsEnum.BLT or
                Processor.CPU_Core.InstructionsEnum.BGE or
                Processor.CPU_Core.InstructionsEnum.BLTU or
                Processor.CPU_Core.InstructionsEnum.BGEU;
        }

        /// <summary>
        /// Check if opcode supports masking
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool SupportsMasking(uint opCode)
        {
            var info = GetInfo(opCode);
            return info?.SupportsMasking ?? false;
        }

        /// <summary>
        /// Returns true only for the Phase 05B policy-bit contour selected for
        /// executable saturating arithmetic. Other opcodes with the raw saturating
        /// bit set must fail closed until separately scoped.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool SupportsSaturatingAddPolicy(uint opCode) =>
            opCode == (uint)Processor.CPU_Core.InstructionsEnum.VADD;

        /// <summary>
        /// Returns <see langword="true"/> when the opcode uses the packed flat-architectural
        /// Word1 register ABI for canonical decode / IR operand projection.
        /// This semantic query is intentionally classifier-driven and must not be derived
        /// from StreamLength or legacy scalar heuristics.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool UsesPackedArchRegisterWord1(Processor.CPU_Core.InstructionsEnum opcode)
        {
            Processor.CPU_Core.InstructionsEnum semanticOpcode =
                opcode;

            if (IsVectorOp((uint)semanticOpcode) &&
                !IsVectorConfigSystemSingletonOpcode(semanticOpcode))
            {
                return false;
            }

            InstructionClass instructionClass;
            if (!TryGetPublishedSemantics(semanticOpcode, out instructionClass, out _))
            {
                // Only a narrow classifier-only allowlist is permitted here.
                // Vector/matrix contours also fall back to classifier metadata, but they keep
                // pointer-based Word1/Word2 transport and must never be reinterpreted through
                // the packed architectural register ABI.
                return UsesClassifierOnlyPackedArchRegisterWord1(semanticOpcode);
            }

            return instructionClass is
                InstructionClass.ScalarAlu or
                InstructionClass.Memory or
                InstructionClass.ControlFlow or
                InstructionClass.Atomic or
                InstructionClass.System or
                InstructionClass.Csr or
                InstructionClass.SmtVt or
                InstructionClass.Vmx;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool UsesPackedArchRegisterWord1(uint opcode)
        {
            uint semanticOpcode = opcode;

            if (IsVectorOp(semanticOpcode) &&
                !IsVectorConfigSystemSingletonOpcode(semanticOpcode))
            {
                return false;
            }

            if (!TryGetPublishedSemantics(semanticOpcode, out InstructionClass instructionClass, out _))
            {
                return UsesClassifierOnlyPackedArchRegisterWord1(semanticOpcode);
            }

            return instructionClass is
                InstructionClass.ScalarAlu or
                InstructionClass.Memory or
                InstructionClass.ControlFlow or
                InstructionClass.Atomic or
                InstructionClass.System or
                InstructionClass.Csr or
                InstructionClass.SmtVt or
                InstructionClass.Vmx;
        }

        /// <summary>
        /// Check if opcode is a math or vector instruction.
        /// Table-driven replacement for <c>VLIW_Instruction.IsMathOrVector</c> (V6 B8/B9).
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool IsMathOrVectorOp(uint opCode)
        {
            var info = GetInfo(opCode);
            return info?.IsMathOrVector ?? false;
        }

        /// <summary>
        /// Check if opcode is a control flow instruction.
        /// Table-driven replacement for <c>VLIW_Instruction.IsControlFlow</c> (V6 B7).
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool IsControlFlowOp(uint opCode)
        {
            var info = GetInfo(opCode);
            return info?.IsControlFlow ?? false;
        }

        /// <summary>
        /// Check if opcode is a vector comparison instruction that generates a predicate mask.
        /// Table-driven replacement for the VCMPEQ–VCMPGE opcode-range check (V6 B9).
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool IsComparisonOp(uint opCode)
        {
            var info = GetInfo(opCode);
            return info?.Category == OpcodeCategory.Comparison
                && (info.Value.Flags & InstructionFlags.MaskManipulation) == 0;
        }

        /// <summary>
        /// Check if opcode is a predicate-mask manipulation instruction (VMAND/VMOR/VMXOR/VMNOT/VPOPC/VMSBF).
        /// Table-driven replacement for the VMAND–VPOPC opcode-range check (V6 B9).
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool IsMaskManipOp(uint opCode)
        {
            var info = GetInfo(opCode);
            return (info?.Flags & InstructionFlags.MaskManipulation) != 0;
        }

        /// <summary>
        /// Check if opcode is a scoped widen/narrow/convert vector contour.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool IsWidenNarrowConvertOp(uint opCode)
        {
            var info = GetInfo(opCode);
            return (info?.Flags & InstructionFlags.WidenNarrowConvert) != 0;
        }

        /// <summary>
        /// Check if opcode is a scoped prefix-scan vector contour.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool IsScanOp(uint opCode)
        {
            var info = GetInfo(opCode);
            return (info?.Flags & InstructionFlags.Scan) != 0;
        }

        /// <summary>
        /// Check if opcode is a fused multiply-add instruction (VFMADD/VFMSUB/VFNMADD/VFNMSUB).
        /// Table-driven replacement for the VFMADD–VFNMSUB opcode-range check (V6 B9).
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool IsFmaOp(uint opCode)
        {
            var info = GetInfo(opCode);
            if (info is null) return false;
            return (info.Value.Flags & InstructionFlags.ThreeOperand) != 0
                && (info.Value.Flags & InstructionFlags.FloatingPoint) != 0;
        }

        /// <summary>
        /// Check if opcode is a vector reduction instruction (VREDSUM–VREDXOR).
        /// Table-driven replacement for the VREDSUM–VREDXOR opcode-range check (V6 B9).
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool IsReductionOp(uint opCode)
        {
            var info = GetInfo(opCode);
            // Exclude mask-pop (VPOPC) which also carries Reduction but is a mask-manipulation op.
            if ((info?.Flags & InstructionFlags.MaskManipulation) != 0) return false;
            return (info?.Flags & InstructionFlags.Reduction) != 0;
        }


        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static bool IsVectorConfigSystemSingletonOpcode(
            Processor.CPU_Core.InstructionsEnum opcode)
            => opcode is
                Processor.CPU_Core.InstructionsEnum.VSETVL or
                Processor.CPU_Core.InstructionsEnum.VSETVLI or
                Processor.CPU_Core.InstructionsEnum.VSETIVLI;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static bool IsVectorConfigSystemSingletonOpcode(uint opcode)
            => opcode is
                Processor.CPU_Core.IsaOpcodeValues.VSETVL or
                Processor.CPU_Core.IsaOpcodeValues.VSETVLI or
                Processor.CPU_Core.IsaOpcodeValues.VSETIVLI;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static bool UsesClassifierOnlyPackedArchRegisterWord1(
            Processor.CPU_Core.InstructionsEnum opcode)
        {
            return opcode is
             Processor.CPU_Core.InstructionsEnum.VSETVEXCPMASK or
                Processor.CPU_Core.InstructionsEnum.VSETVEXCPPRI;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static bool UsesClassifierOnlyPackedArchRegisterWord1(uint opcode)
        {
            return opcode is
           Processor.CPU_Core.IsaOpcodeValues.VSETVEXCPMASK or
                Processor.CPU_Core.IsaOpcodeValues.VSETVEXCPPRI;
        }
    }
}
